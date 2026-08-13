using System;
using System.Collections.Generic;
using System.Linq;
using Sudoku.Models;
using Sudoku.UI.Config;
using Board = Sudoku.Models.Board;

namespace Sudoku.Solver.Rules
{
    /**
     * Forcing Chains (AIC family) derive conclusions by following alternating
     * strong/weak inferences from an assumption and checking both branches.
     *
     * Canonical outcomes covered here:
     * 1. Contradiction forcing: if one assumption branch is impossible, the
     *    opposite truth value is forced.
     * 2. Common conclusion forcing (discontinuous AIC): if both branches force
     *    the same candidate false, eliminate that candidate; if both force the
     *    same candidate true, place that value.
     *
     * Strong links are built from:
     * - Bi-value cells (exactly two candidates in one cell).
     * - Conjugate pairs (exactly two candidates for a digit in one row/column/box).
     *
     * This bounded implementation targets human-tractable chains and intentionally
     * avoids exhaustive SAT-level search.
     *
     * This rule should only be applied if colouring is enabled and has at least
     * two colours enabled.
     */
    public class ForcingChainRule : CachedRuleBase
    {
        private enum UnitType
        {
            Row = 0,
            Column = 1,
            Box = 2
        }
        private const string TargetATag = "TargetA";
        private const string TargetBTag = "TargetB";
        private const string DeductionTag = "Deduction";

        private const int MaxSeedCount = 729;
        private const int MaxAssignmentsPerBranch = 4000;
        private const int MaxEvidencePerBranch = 8;

        private sealed class ChainModel
        {
            public int Size;
            public List<int> Candidates = new List<int>();
            public Dictionary<int, int> CandidateIndexByKey = new Dictionary<int, int>();
            /** All links Strong and weak between candidates */
            public Dictionary<int, List<int>> WeakLinksByKey = new Dictionary<int, List<int>>();
            /** Only strong links between candidates */
            public Dictionary<int, List<int>> StrongLinksByKey = new Dictionary<int, List<int>>();
            public Dictionary<int, List<int>> CellGroupsByIndex = new Dictionary<int, List<int>>();
            public Dictionary<int, List<int>> UnitDigitGroupsByIndex = new Dictionary<int, List<int>>();
        }

        private sealed class PropagationState
        {
            public HashSet<int> TrueCandidates = new HashSet<int>();
            public HashSet<int> FalseCandidates = new HashSet<int>();
            public bool HasContradiction;
            public string ContradictionReason;
            public int? ContradictionCandidate;
            public int? ContradictionSourceCandidate;
            public DirectionalLinkKind? ContradictionSourceLinkKind;
            public Queue<(int key, bool valueTrue)> Pending = new Queue<(int key, bool valueTrue)>();
            public List<int> AssignmentOrder = new List<int>();
            public Dictionary<int, InferenceCause> InferenceCauseByCandidate = new Dictionary<int, InferenceCause>();
        }

        private sealed class InferenceCause
        {
            public int FromCandidate;
            public DirectionalLinkKind LinkKind;
            public bool IsPreviewLink;
        }

        private sealed class ForcingPlan
        {
            public int SeedCandidate;
            public PropagationState TrueBranch;
            public PropagationState FalseBranch;
            public List<int> CommonFalseCandidates = new List<int>();
            public List<int> CommonTrueCandidates = new List<int>();
            public bool ContradictionOnTrueBranch;
            public bool ContradictionOnFalseBranch;
        }

        public override string Name => "Forcing Chain";

        public override Difficulty Difficulty => Difficulty.Expert;

        /**
         * Determine whether forcing-chain deductions are available.
         *
         * @param board Current puzzle board.
         * @returns True when prerequisites are met and a forcing deduction exists.
         */
        public override bool CanApply(Board board)
        {
            if (board == null) return false;
            if (!board.IsValid()) return false;
            if (board.Size <= 0) return false;
            if (ColourSettings.GetEnabledColourCount() < 2) return false;

            return FindPlan(board) != null || FindOneSidedContradictionPlan(board) != null;
        }

        /**
         * Calculate candidate removals or placements implied by forcing chains.
         *
         * @param board Current puzzle board.
         * @returns RuleResult containing one bounded forcing-chain deduction.
         */
        protected override RuleResult CalculateChangesInternal(Board board)
        {
            var result = new RuleResult();

            if (board == null || !board.IsValid() || board.Size <= 0 || ColourSettings.GetEnabledColourCount() < 2)
            {
                result.Apply = false;
                return result;
            }

            var plan = FindPlan(board);
            if (plan == null)
            {
                var contradictionFallback = FindOneSidedContradictionPlan(board);
                if (contradictionFallback == null)
                {
                    result.Apply = false;
                    return result;
                }

                plan = contradictionFallback;
            }

            if (plan.ContradictionOnTrueBranch || plan.ContradictionOnFalseBranch)
            {
                ApplyContradictionPlan(board, result, plan);
            }
            else
            {
                ApplyCommonConclusionPlan(board, result, plan);
            }

            AppendEvidence(board, result, plan);

            if (result.Changes.Count == 0)
            {
                var contradictionFallback = FindOneSidedContradictionPlan(board);
                if (contradictionFallback != null)
                {
                    result.UsedCells.Clear();
                    result.UsedDirectionalLinks.Clear();
                    result.Description = null;
                    ApplyContradictionPlan(board, result, contradictionFallback);
                    AppendEvidence(board, result, contradictionFallback);
                }
            }

            result.Apply = result.Changes.Count > 0;

            if (result.Apply && string.IsNullOrWhiteSpace(result.Description))
            {
                result.Description = "Forcing Chain produced a deduction.";
            }

            if (!result.Apply)
            {
                result.Description = null;
            }

            return result;
        }

        /**
         * Find a deterministic one-sided contradiction forcing plan.
         *
         * @param board Current puzzle board.
         * @returns One-sided contradiction plan, or null if none exists.
         */
        private ForcingPlan FindOneSidedContradictionPlan(Board board)
        {
            var model = BuildChainModel(board);
            if (model == null || model.Candidates.Count == 0)
            {
                return null;
            }

            ForcingPlan doubleSidedFallback = null;

            var orderedSeeds = model.Candidates
                .OrderBy(k => GetDigit(board, k))
                .ThenBy(k => GetRow(board, k))
                .ThenBy(k => GetColumn(board, k))
                .ToList();

            foreach (var seed in orderedSeeds)
            {
                var trueBranch = PropagateFromAssumption(board, model, seed, assumeTrue: true, enableUnitCompletion: true);
                var falseBranch = PropagateFromAssumption(board, model, seed, assumeTrue: false, enableUnitCompletion: true);
                bool oneSidedContradiction = trueBranch.HasContradiction ^ falseBranch.HasContradiction;

                int row = GetRow(board, seed);
                int column = GetColumn(board, seed);
                int digit = GetDigit(board, seed);
                var cell = board.Cells[row, column];
                if (cell == null || cell.Value.HasValue || cell.Candidates == null || !cell.Candidates.Contains(digit))
                {
                    continue;
                }

                bool doubleSidedContradiction = trueBranch.HasContradiction && falseBranch.HasContradiction;
                if (!oneSidedContradiction && !doubleSidedContradiction)
                {
                    continue;
                }

                var candidatePlan = new ForcingPlan
                {
                    SeedCandidate = seed,
                    TrueBranch = trueBranch,
                    FalseBranch = falseBranch,
                    ContradictionOnTrueBranch = trueBranch.HasContradiction,
                    ContradictionOnFalseBranch = falseBranch.HasContradiction
                };

                if (oneSidedContradiction)
                {
                    return candidatePlan;
                }

                if (doubleSidedFallback == null)
                {
                    doubleSidedFallback = candidatePlan;
                }
            }

            return doubleSidedFallback;
        }

        /**
         * Search for the first forcing-chain deduction plan.
         *
         * @param board Current puzzle board.
         * @returns A forcing plan or null when no deduction exists.
         */
        private ForcingPlan FindPlan(Board board)
        {
            var model = BuildChainModel(board);
            if (model == null || model.Candidates.Count == 0)
            {
                return null;
            }

            var digitCounts = BuildDigitCounts(board);
            var orderedCandidates = model.Candidates
                .OrderBy(k => digitCounts.TryGetValue(GetDigit(board, k), out var count) ? count : int.MaxValue)
                .ThenBy(k => GetRow(board, k))
                .ThenBy(k => GetColumn(board, k))
                .ToList();

            ForcingPlan bestPlan = null;
            int bestScore = int.MaxValue;

            foreach (var seed in orderedCandidates)
            {
                var trueBranch = PropagateFromAssumption(board, model, seed, assumeTrue: true, enableUnitCompletion: false);
                var falseBranch = PropagateFromAssumption(board, model, seed, assumeTrue: false, enableUnitCompletion: false);

                var plan = new ForcingPlan
                {
                    SeedCandidate = seed,
                    TrueBranch = trueBranch,
                    FalseBranch = falseBranch,
                    ContradictionOnTrueBranch = trueBranch.HasContradiction,
                    ContradictionOnFalseBranch = falseBranch.HasContradiction
                };

                bool hasOneSidedContradiction = plan.ContradictionOnTrueBranch ^ plan.ContradictionOnFalseBranch;
                if (plan.ContradictionOnTrueBranch && plan.ContradictionOnFalseBranch)
                {
                    continue;
                }

                if (hasOneSidedContradiction)
                {
                    continue;
                }

                plan.CommonFalseCandidates = trueBranch.FalseCandidates
                    .Intersect(falseBranch.FalseCandidates)
                    .Where(k => k != seed)
                    .Where(k => !IsSameCell(board, k, seed))
                    .Where(CandidateAvailable)
                    .OrderBy(k => GetRow(board, k))
                    .ThenBy(k => GetColumn(board, k))
                    .ThenBy(k => GetDigit(board, k))
                    .ToList();

                plan.CommonTrueCandidates = trueBranch.TrueCandidates
                    .Intersect(falseBranch.TrueCandidates)
                    .Where(k => !IsSameCell(board, k, seed))
                    .Where(CandidateAvailable)
                    .OrderBy(k => GetRow(board, k))
                    .ThenBy(k => GetColumn(board, k))
                    .ThenBy(k => GetDigit(board, k))
                    .ToList();

                bool hasCommonConclusion = plan.CommonFalseCandidates.Count > 0 || plan.CommonTrueCandidates.Count > 0;
                if (!hasCommonConclusion)
                {
                    continue;
                }

                int score = ScorePlan(board, digitCounts, plan);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestPlan = plan;
                }
            }

            return bestPlan;

            bool CandidateAvailable(int candidateKey)
            {
                return IsCandidateStillCandidate(board, candidateKey);
            }
        }

        /**
         * Build a global candidate count per digit to prefer rarer digits as chain seeds.
         *
         * @param board Current puzzle board.
         * @returns Map from digit to active candidate count.
         */
        private static Dictionary<int, int> BuildDigitCounts(Board board)
        {
            var counts = new Dictionary<int, int>();
            for (int digit = 1; digit <= board.Size; digit++)
            {
                int count = 0;
                for (int row = 0; row < board.Size; row++)
                {
                    for (int column = 0; column < board.Size; column++)
                    {
                        var cell = board.Cells[row, column];
                        if (cell != null && !cell.Value.HasValue && cell.Candidates != null && cell.Candidates.Contains(digit))
                        {
                            count++;
                        }
                    }
                }

                counts[digit] = count;
            }

            return counts;
        }

        /**
         * Score a candidate forcing plan so canonical common-conclusion deductions win.
         *
         * @param board Current puzzle board.
         * @param digitCounts Global candidate count per digit.
         * @param plan Candidate forcing plan.
         * @returns Lower scores are preferred.
         */
        private static int ScorePlan(Board board, IReadOnlyDictionary<int, int> digitCounts, ForcingPlan plan)
        {
            int seedDigit = GetDigit(board, plan.SeedCandidate);
            int seedCount = digitCounts.TryGetValue(seedDigit, out var c) ? c : int.MaxValue / 4;

            bool isCommonConclusion = plan.CommonFalseCandidates.Count > 0 || plan.CommonTrueCandidates.Count > 0;
            bool isPlacement = plan.CommonTrueCandidates.Count > 0;
            int deductionCount = plan.CommonTrueCandidates.Count > 0 ? plan.CommonTrueCandidates.Count : plan.CommonFalseCandidates.Count;
            int trueBranchSize = plan.TrueBranch != null ? plan.TrueBranch.AssignmentOrder.Count : int.MaxValue / 4;
            int falseBranchSize = plan.FalseBranch != null ? plan.FalseBranch.AssignmentOrder.Count : int.MaxValue / 4;
            int branchSize = Math.Min(trueBranchSize, falseBranchSize);

            int typeScore = 0;
            if (!isCommonConclusion)
            {
                typeScore = 2000;
            }
            else if (!isPlacement)
            {
                typeScore = 0;
            }

            return (typeScore * 1000000)
                + (seedCount * 10000)
                + (branchSize * 100)
                + deductionCount;
        }

        /**
         * Build the strong/weak inference graph used by propagation.
         *
         * @param board Current puzzle board.
         * @returns Chain model containing candidates, links, and deduction groups.
         */
        private ChainModel BuildChainModel(Board board)
        {
            var model = new ChainModel { Size = board.Size };
            int size = board.Size;

            // Collect active candidate candidates from unsolved cells.
            for (int row = 0; row < size; row++)
            {
                for (int column = 0; column < size; column++)
                {
                    var cell = board.Cells[row, column];
                    if (cell == null || cell.Value.HasValue || cell.Candidates == null || cell.Candidates.Count == 0)
                    {
                        continue;
                    }

                    foreach (int digit in cell.Candidates.OrderBy(v => v))
                    {
                        int key = MakeCandidateKey(board, row, column, digit);
                        model.CandidateIndexByKey[key] = model.Candidates.Count;
                        model.Candidates.Add(key);
                    }
                }
            }

            if (model.Candidates.Count == 0)
            {
                return model;
            }

            // Prepare per-cell and per-unit groups used for hidden/naked single propagation.
            for (int row = 0; row < size; row++)
            {
                for (int column = 0; column < size; column++)
                {
                    var cell = board.Cells[row, column];
                    if (cell == null || cell.Value.HasValue || cell.Candidates == null)
                    {
                        continue;
                    }

                    if(cell.Candidates.Count == 2) {
                        // Strong Link
                        var pair = cell.Candidates.OrderBy(v => v).ToList();
                        int a = MakeCandidateKey(board, row, column, pair[0]);
                        int b = MakeCandidateKey(board, row, column, pair[1]);
                        AddLink(model.StrongLinksByKey, a, b);
                    }
                    if (cell.Candidates.Count != 0) {
                        // Group all candidates in the same cell for local completion propagation.
                        int cellIndex = row * size + column;
                        var group = new List<int>();
                        foreach (int digit in cell.Candidates)
                        {
                            group.Add(MakeCandidateKey(board, row, column, digit));
                        }

                        model.CellGroupsByIndex[cellIndex] = group;
                    }
                }
            }

            for (int digit = 1; digit <= size; digit++)
            {
                for (int row = 0; row < size; row++)
                {
                    // Build unit-digit groups for hidden/naked single propagation.
                    var group = new List<int>();
                    for (int column = 0; column < size; column++)
                    {
                        var cell = board.Cells[row, column];
                        if (cell == null || cell.Value.HasValue) continue;
                        if (cell.Candidates != null && cell.Candidates.Contains(digit))
                        {
                            group.Add(MakeCandidateKey(board, row, column, digit));
                        }
                    }

                    if (group.Count > 0)
                    {
                        int groupIndex = MakeUnitDigitGroupIndex(board, unitType: UnitType.Row, unitIndex: row, digit);
                        model.UnitDigitGroupsByIndex[groupIndex] = group;
                        // If there are exactly two candidates for this digit in the row (conjugate pair), add a Strong Link between them.
                        if (group.Count == 2)
                        {
                            AddLink(model.StrongLinksByKey, group[0], group[1]);
                        }
                    }
                }

                for (int column = 0; column < size; column++)
                {// Build unit-digit groups for hidden/naked single propagation in column.
                    var group = new List<int>();
                    for (int row = 0; row < size; row++)
                    {
                        var cell = board.Cells[row, column];
                        if (cell == null || cell.Value.HasValue) continue;
                        if (cell.Candidates != null && cell.Candidates.Contains(digit))
                        {
                            group.Add(MakeCandidateKey(board, row, column, digit));
                        }
                    }

                    if (group.Count > 0)
                    {
                        int groupIndex = MakeUnitDigitGroupIndex(board, unitType: UnitType.Column, unitIndex: column, digit);
                        model.UnitDigitGroupsByIndex[groupIndex] = group;
                        // If there are exactly two candidates for this digit in the column (conjugate pair), add a Strong Link between them.
                        if (group.Count == 2)
                        {
                            AddLink(model.StrongLinksByKey, group[0], group[1]);
                        }
                    }
                }

                for (int box = 0; box < size; box++)
                {
                    // Build unit-digit groups for hidden/naked single propagation in box.
                    var group = new List<int>();
                    foreach (var cell in board.GetBox(box))
                    {
                        if (cell == null || cell.Value.HasValue) continue;
                        if (cell.Candidates != null && cell.Candidates.Contains(digit))
                        {
                            group.Add(MakeCandidateKey(board, cell.Row, cell.Column, digit));
                        }
                    }

                    if (group.Count > 0)
                    {
                        int groupIndex = MakeUnitDigitGroupIndex(board, unitType: UnitType.Box, unitIndex: box, digit);
                        model.UnitDigitGroupsByIndex[groupIndex] = group;
                        // If there are exactly two candidates for this digit in the box (conjugate pair), add a Strong Link between them.
                        if (group.Count == 2)
                        {
                            AddLink(model.StrongLinksByKey, group[0], group[1]);
                        }
                    }
                }
            }

            // Weak links: keep the full propagation graph so the rule can find
            // forcing chains, but only real conjugate-pair edges are exposed as
            // visible weak links in the preview. The rest remain internal conflict
            // steps used by the solver.
            foreach (var candidate in model.Candidates)
            {
                var weakLinks = new HashSet<int>();
                int row = GetRow(board, candidate);
                int column = GetColumn(board, candidate);
                int digit = GetDigit(board, candidate);
                var cell = board.Cells[row, column];

                if (cell != null && cell.Candidates != null)
                {
                    // All other candidates in the same cell conflict with this candidate.
                    foreach (int other in cell.Candidates)
                    {
                        if (other == digit) continue;
                        weakLinks.Add(MakeCandidateKey(board, row, column, other));
                    }
                }

                // All peers in the same row, column, or box that have this digit as a candidate conflict with this candidate.
                foreach (var peer in board.GetPeers(board.Cells[row, column]))
                {
                    if (peer == null || peer.Value.HasValue || peer.Candidates == null) continue;
                    if (!peer.Candidates.Contains(digit)) continue;
                    weakLinks.Add(MakeCandidateKey(board, peer.Row, peer.Column, digit));
                }

                model.WeakLinksByKey[candidate] = weakLinks.OrderBy(k => k).ToList();
            }

            return model;
        }

        /**
         * Propagate implications for one assumption branch.
         *
         * @param board Current puzzle board.
         * @param model Inference graph model.
         * @param seedCandidate Assumed candidate key.
         * @param assumeTrue Assumed truth value for the seed candidate.
         * @returns Propagation state for the branch.
         */
        private PropagationState PropagateFromAssumption(
            Board board,
            ChainModel model,
            int seedCandidate,
            bool assumeTrue,
            bool enableUnitCompletion)
        {
            var state = new PropagationState();
            TryEnqueueAssignment(board, model, state, seedCandidate, assumeTrue, "Seed assumption");

            while (state.Pending.Count > 0 && !state.HasContradiction)
            {
                if (state.AssignmentOrder.Count > MaxAssignmentsPerBranch)
                {
                    state.HasContradiction = true;
                    state.ContradictionReason = "Chain bound exceeded.";
                    break;
                }

                var (candidate, valueTrue) = state.Pending.Dequeue();
                if (!ApplyAssignment(board, model, state, candidate, valueTrue))
                {
                    continue;
                }

                // Strong-link propagation:
                // false on one endpoint forces true on the other, true forces false.
                if (model.StrongLinksByKey.TryGetValue(candidate, out var links))
                {
                    for (int i = 0; i < links.Count; i++)
                    {
                        TryEnqueueAssignment(
                            board,
                            model,
                            state,
                            links[i],
                            !valueTrue,
                            "Strong link",
                            sourceCandidate: candidate,
                            sourceLinkKind: DirectionalLinkKind.Strong);
                    }
                }

                // Weak links: true candidate excludes all conflicting candidates.
                if (valueTrue && model.WeakLinksByKey.TryGetValue(candidate, out var weakLinks))
                {
                    for (int i = 0; i < weakLinks.Count; i++)
                    {
                        TryEnqueueAssignment(
                            board,
                            model,
                            state,
                            weakLinks[i],
                            false,
                            "Weak link",
                            sourceCandidate: candidate,
                            sourceLinkKind: DirectionalLinkKind.Weak,
                            isPreviewLink: ShouldExposeWeakConflictAsLink(board, candidate, weakLinks[i]));
                    }
                }

                // Local cell completion is safe and useful for forcing chains:
                // if all but one candidates in a cell are false, the last one is true.
                ResolveCellGroup(board, model, state, GetRow(board, candidate), GetColumn(board, candidate));

                if (enableUnitCompletion)
                {
                    // Unit completion is useful for contradiction discovery but can
                    // over-constrain sparse synthetic boards used for common-conclusion examples.
                    ResolveUnitGroups(
                        board,
                        model,
                        state,
                        GetRow(board, candidate),
                        GetColumn(board, candidate),
                        GetDigit(board, candidate));
                }
            }

            return state;
        }

        /**
         * Enqueue a candidate assignment while preserving contradiction checks.
         *
         * @param board Current puzzle board.
         * @param model Inference graph model.
         * @param state Branch state.
         * @param candidate Candidate key.
         * @param valueTrue Assigned truth value.
         * @param reason Human-readable enqueue reason.
         */
        private void TryEnqueueAssignment(
            Board board,
            ChainModel model,
            PropagationState state,
            int candidate,
            bool valueTrue,
            string reason,
            int? sourceCandidate = null,
            DirectionalLinkKind? sourceLinkKind = null,
            bool isPreviewLink = true)
        {
            if (state.HasContradiction)
            {
                return;
            }

            if (!model.CandidateIndexByKey.ContainsKey(candidate))
            {
                return;
            }

            if (valueTrue)
            {
                if (state.FalseCandidates.Contains(candidate))
                {
                    state.HasContradiction = true;
                    state.ContradictionCandidate = candidate;
                    state.ContradictionSourceCandidate = sourceCandidate;
                    state.ContradictionSourceLinkKind = sourceLinkKind;
                    state.ContradictionReason = $"Candidate {FormatCandidate(board, candidate)} became both true and false ({reason}).";
                    return;
                }

                if (state.TrueCandidates.Contains(candidate))
                {
                    return;
                }
            }
            else
            {
                if (state.TrueCandidates.Contains(candidate))
                {
                    state.HasContradiction = true;
                    state.ContradictionCandidate = candidate;
                    state.ContradictionSourceCandidate = sourceCandidate;
                    state.ContradictionSourceLinkKind = sourceLinkKind;
                    state.ContradictionReason = $"Candidate {FormatCandidate(board, candidate)} became both true and false ({reason}).";
                    return;
                }

                if (state.FalseCandidates.Contains(candidate))
                {
                    return;
                }
            }

            state.Pending.Enqueue((candidate, valueTrue));

            if (sourceCandidate.HasValue && sourceLinkKind.HasValue && !state.InferenceCauseByCandidate.ContainsKey(candidate))
            {
                state.InferenceCauseByCandidate[candidate] = new InferenceCause
                {
                    FromCandidate = sourceCandidate.Value,
                    LinkKind = sourceLinkKind.Value,
                    IsPreviewLink = isPreviewLink,
                };
            }
        }

        /**
         * Apply one pending candidate assignment into branch state.
         *
         * @param board Current puzzle board.
         * @param model Inference graph model.
         * @param state Branch state.
         * @param candidate Candidate key.
         * @param valueTrue Assigned truth value.
         * @returns True when the assignment changed state and should propagate.
         */
        private bool ApplyAssignment(Board board, ChainModel model, PropagationState state, int candidate, bool valueTrue)
        {
            if (valueTrue)
            {
                if (state.TrueCandidates.Contains(candidate))
                {
                    return false;
                }

                if (state.FalseCandidates.Contains(candidate))
                {
                    state.HasContradiction = true;
                    state.ContradictionCandidate = candidate;
                    state.ContradictionReason = $"Candidate {FormatCandidate(board, candidate)} became both true and false.";
                    return false;
                }

                state.TrueCandidates.Add(candidate);
            }
            else
            {
                if (state.FalseCandidates.Contains(candidate))
                {
                    return false;
                }

                if (state.TrueCandidates.Contains(candidate))
                {
                    state.HasContradiction = true;
                    state.ContradictionCandidate = candidate;
                    state.ContradictionReason = $"Candidate {FormatCandidate(board, candidate)} became both true and false.";
                    return false;
                }

                state.FalseCandidates.Add(candidate);
            }

            state.AssignmentOrder.Add(candidate);
            return true;
        }

        /**
         * Resolve a single cell's candidate group after an assignment.
         *
         * @param board Current puzzle board.
         * @param model Inference graph model.
         * @param state Branch state.
         * @param row Cell row.
         * @param column Cell column.
         */
        private void ResolveCellGroup(Board board, ChainModel model, PropagationState state, int row, int column)
        {
            int cellIndex = row * board.Size + column;
            if (!model.CellGroupsByIndex.TryGetValue(cellIndex, out var group) || group.Count == 0)
            {
                return;
            }

            int trueCount = 0;
            int unknownCount = 0;
            int unknownCandidate = 0;

            for (int i = 0; i < group.Count; i++)
            {
                int key = group[i];
                if (state.TrueCandidates.Contains(key))
                {
                    trueCount++;
                    continue;
                }

                if (!state.FalseCandidates.Contains(key))
                {
                    unknownCount++;
                    unknownCandidate = key;
                }
            }

            if (trueCount > 1)
            {
                state.HasContradiction = true;
                state.ContradictionReason = $"Cell r{row + 1}c{column + 1} has multiple forced digits.";
                return;
            }

            if (trueCount == 0 && unknownCount == 0)
            {
                state.HasContradiction = true;
                state.ContradictionReason = $"Cell r{row + 1}c{column + 1} has no valid candidates.";
                return;
            }

            if (trueCount == 0 && unknownCount == 1)
            {
                TryEnqueueAssignment(board, model, state, unknownCandidate, true, "Cell completion");
            }
        }

        /**
         * Resolve row/column/box digit groups impacted by a candidate assignment.
         *
         * @param board Current puzzle board.
         * @param model Inference graph model.
         * @param state Branch state.
         * @param row Candidate row.
         * @param column Candidate column.
         * @param digit Candidate digit.
         */
        private void ResolveUnitGroups(Board board, ChainModel model, PropagationState state, int row, int column, int digit)
        {
            int box = board.Cells[row, column].Box;

            ResolveUnitGroup(board, model, state, MakeUnitDigitGroupIndex(board, UnitType.Row, row, digit), $"Row {row + 1}");
            ResolveUnitGroup(board, model, state, MakeUnitDigitGroupIndex(board, UnitType.Column, column, digit), $"Column {column + 1}");
            ResolveUnitGroup(board, model, state, MakeUnitDigitGroupIndex(board, UnitType.Box, box, digit), $"Box {box + 1}");
        }

        /**
         * Resolve a single unit-digit group.
         *
         * @param board Current puzzle board.
         * @param model Inference graph model.
         * @param state Branch state.
         * @param groupIndex Group index.
         * @param groupLabel Human-readable group label for diagnostics.
         */
        private void ResolveUnitGroup(Board board, ChainModel model, PropagationState state, int groupIndex, string groupLabel)
        {
            if (!model.UnitDigitGroupsByIndex.TryGetValue(groupIndex, out var group) || group.Count == 0)
            {
                return;
            }

            int trueCount = 0;
            int unknownCount = 0;
            int falseCount = 0;
            int unknownCandidate = 0;

            for (int i = 0; i < group.Count; i++)
            {
                int key = group[i];
                if (state.TrueCandidates.Contains(key))
                {
                    trueCount++;
                    continue;
                }

                if (!state.FalseCandidates.Contains(key))
                {
                    unknownCount++;
                    unknownCandidate = key;
                }
                else
                {
                    falseCount++;
                }
            }

            if (trueCount > 1)
            {
                state.HasContradiction = true;
                state.ContradictionReason = $"{groupLabel} has multiple forced placements for one digit.";
                return;
            }

            if (trueCount == 0 && unknownCount == 0)
            {
                state.HasContradiction = true;
                state.ContradictionReason = $"{groupLabel} has no location left for a digit.";
                return;
            }

            // Restrict hidden-single forcing to groups reduced by branch propagation.
            // This avoids globally-singleton unit groups on sparse synthetic candidate
            // boards from dominating forcing-chain test scenarios.
            if (trueCount == 0 && unknownCount == 1 && falseCount > 0)
            {
                TryEnqueueAssignment(board, model, state, unknownCandidate, true, "Unit completion");
            }
        }

        /**
         * Apply contradiction-based forcing deduction to the result.
         *
         * @param board Current puzzle board.
         * @param result Rule result accumulator.
         * @param plan Selected forcing plan.
         */
        private void ApplyContradictionPlan(Board board, RuleResult result, ForcingPlan plan)
        {
            int row = GetRow(board, plan.SeedCandidate);
            int column = GetColumn(board, plan.SeedCandidate);
            int digit = GetDigit(board, plan.SeedCandidate);
            var cell = board.Cells[row, column];

            // Prefer elimination when the "assume true" branch contradicts.
            // This also serves as a defensive fallback for sparse synthetic candidate boards
            // where both branches may contradict due intentionally incomplete candidate models.
            if (plan.ContradictionOnTrueBranch)
            {
                if (!cell.Value.HasValue && cell.Candidates.Contains(digit))
                {
                    var change = new CellChange { Row = row, Column = column };
                    change.RemovedCandidates.Add(digit);
                    result.Changes.Add(change);
                    result.Description =
                        $"Forcing Chain: assuming {FormatCandidate(board, plan.SeedCandidate)} is true contradicts, so remove {digit} from r{row + 1}c{column + 1}.";
                }
            }
            else if (plan.ContradictionOnFalseBranch)
            {
                if (!cell.Value.HasValue && cell.Candidates.Contains(digit))
                {
                    result.Changes.Add(new CellChange
                    {
                        Row = row,
                        Column = column,
                        NewValue = digit,
                        ValueOnlySet = true,
                        ForceSetValue = false
                    });
                    result.Description =
                        $"Forcing Chain: assuming {FormatCandidate(board, plan.SeedCandidate)} is false contradicts, so set r{row + 1}c{column + 1}={digit}.";
                }
            }
        }

        /**
         * Apply common-conclusion forcing deduction to the result.
         *
         * @param board Current puzzle board.
         * @param result Rule result accumulator.
         * @param plan Selected forcing plan.
         */
        private void ApplyCommonConclusionPlan(Board board, RuleResult result, ForcingPlan plan)
        {
            // Prefer a direct placement when both branches force a candidate true.
            foreach (int candidate in plan.CommonTrueCandidates)
            {
                int row = GetRow(board, candidate);
                int column = GetColumn(board, candidate);
                int digit = GetDigit(board, candidate);
                var cell = board.Cells[row, column];
                if (cell.Value.HasValue || !cell.Candidates.Contains(digit))
                {
                    continue;
                }

                result.Changes.Add(new CellChange
                {
                    Row = row,
                    Column = column,
                    NewValue = digit,
                    ValueOnlySet = true,
                    ForceSetValue = false
                });
                result.Description =
                    $"Forcing Chain: both branches force r{row + 1}c{column + 1}={digit}.";
                return;
            }

            // Otherwise remove candidates forced false in both branches.
            int added = 0;
            for (int i = 0; i < plan.CommonFalseCandidates.Count; i++)
            {
                int candidate = plan.CommonFalseCandidates[i];
                int row = GetRow(board, candidate);
                int column = GetColumn(board, candidate);
                int digit = GetDigit(board, candidate);
                var cell = board.Cells[row, column];

                if (cell.Value.HasValue || !cell.Candidates.Contains(digit))
                {
                    continue;
                }

                var existing = result.Changes.FirstOrDefault(ch => ch.Row == row && ch.Column == column && ch.NewValue == null);
                if (existing == null)
                {
                    existing = new CellChange { Row = row, Column = column };
                    result.Changes.Add(existing);
                }

                if (!existing.RemovedCandidates.Contains(digit))
                {
                    existing.RemovedCandidates.Add(digit);
                    added++;
                }
            }

            if (added > 0)
            {
                result.Description = $"Forcing Chain removed candidates from {added} forced-false candidate(s).";
            }
        }

        /**
         * Add branch evidence to RuleResult.UsedCells for visualization.
         *
         * @param board Current puzzle board.
         * @param result Rule result accumulator.
         * @param plan Selected forcing plan.
         */
        private void AppendEvidence(Board board, RuleResult result, ForcingPlan plan)
        {
            AddUsedCell(result, GetRow(board, plan.SeedCandidate), GetColumn(board, plan.SeedCandidate), GetDigit(board, plan.SeedCandidate), TargetATag);
            AddUsedCell(result, GetRow(board, plan.SeedCandidate), GetColumn(board, plan.SeedCandidate), GetDigit(board, plan.SeedCandidate), TargetBTag);

            bool emittedPathEvidence = AppendCausalEvidencePaths(board, result, plan);
            if (emittedPathEvidence)
            {
                return;
            }

            // Fallback: preserve old bounded evidence when no causal path can be reconstructed.
            var trueEvidence = plan.TrueBranch.AssignmentOrder.Take(MaxEvidencePerBranch).ToList();
            foreach (int candidate in trueEvidence)
            {
                AddUsedCell(result, GetRow(board, candidate), GetColumn(board, candidate), GetDigit(board, candidate), DeductionTag);
            }

            var falseEvidence = plan.FalseBranch.AssignmentOrder.Take(MaxEvidencePerBranch).ToList();
            foreach (int candidate in falseEvidence)
            {
                AddUsedCell(result, GetRow(board, candidate), GetColumn(board, candidate), GetDigit(board, candidate), DeductionTag);
            }

            AppendEvidenceLinks(board, result, plan.TrueBranch, trueEvidence);
            AppendEvidenceLinks(board, result, plan.FalseBranch, falseEvidence);
        }

        /**
         * Build branch evidence from actual inference parent chains that lead to
         * the candidates changed by the selected deduction.
         *
         * @param board Current puzzle board.
         * @param result Rule result accumulator.
         * @param plan Selected forcing plan.
         * @returns True when at least one causal path was emitted.
         */
        private bool AppendCausalEvidencePaths(Board board, RuleResult result, ForcingPlan plan)
        {
            var changedCandidates = CollectChangedCandidates(board, result);
            if (changedCandidates.Count == 0)
            {
                return false;
            }

            bool addedAny = false;

            if (plan.ContradictionOnTrueBranch)
            {
                addedAny |= AppendContradictionEvidenceForBranch(board, result, plan.TrueBranch, plan.SeedCandidate);
            }

            if (plan.ContradictionOnFalseBranch)
            {
                addedAny |= AppendContradictionEvidenceForBranch(board, result, plan.FalseBranch, plan.SeedCandidate);
            }

            bool usingCommonConclusions = !plan.ContradictionOnTrueBranch && !plan.ContradictionOnFalseBranch;
            if (usingCommonConclusions)
            {
                foreach (int candidate in changedCandidates)
                {
                    bool expectTrue = plan.CommonTrueCandidates.Contains(candidate);
                    bool expectFalse = plan.CommonFalseCandidates.Contains(candidate);

                    if (expectTrue && plan.TrueBranch.TrueCandidates.Contains(candidate))
                    {
                        addedAny |= AppendPathEvidenceForBranch(board, result, plan.TrueBranch, plan.SeedCandidate, candidate);
                    }

                    if (expectTrue && plan.FalseBranch.TrueCandidates.Contains(candidate))
                    {
                        addedAny |= AppendPathEvidenceForBranch(board, result, plan.FalseBranch, plan.SeedCandidate, candidate);
                    }

                    if (expectFalse && plan.TrueBranch.FalseCandidates.Contains(candidate))
                    {
                        addedAny |= AppendPathEvidenceForBranch(board, result, plan.TrueBranch, plan.SeedCandidate, candidate);
                    }

                    if (expectFalse && plan.FalseBranch.FalseCandidates.Contains(candidate))
                    {
                        addedAny |= AppendPathEvidenceForBranch(board, result, plan.FalseBranch, plan.SeedCandidate, candidate);
                    }
                }
            }

            return addedAny;
        }

        /**
         * Emit UsedCells and directional links for one reconstructed inference path.
         *
         * @param board Current puzzle board.
         * @param result Rule result accumulator.
         * @param branch Branch state containing parent causes.
         * @param seedCandidate Branch seed candidate key.
         * @param targetCandidate Target candidate key reached by inference.
         * @returns True when a valid path was added.
         */
        private bool AppendPathEvidenceForBranch(Board board, RuleResult result, PropagationState branch, int seedCandidate, int targetCandidate)
        {
            if (branch == null)
            {
                return false;
            }

            if (!TryBuildPath(branch, seedCandidate, targetCandidate, out var path))
            {
                return false;
            }

            for (int i = 0; i < path.Count; i++)
            {
                int candidate = path[i];
                AddUsedCell(result, GetRow(board, candidate), GetColumn(board, candidate), GetDigit(board, candidate), DeductionTag);
            }

            for (int i = 1; i < path.Count; i++)
            {
                int toCandidate = path[i];
                int fromCandidate = path[i - 1];
                if (!branch.InferenceCauseByCandidate.TryGetValue(toCandidate, out var cause))
                {
                    continue;
                }

                if (!cause.IsPreviewLink)
                {
                    continue;
                }

                AddEvidenceDirectionalLink(result, board, fromCandidate, toCandidate, cause.LinkKind);
            }

            return true;
        }

        /**
         * Emit contradiction evidence including the final edge that attempted to
         * force an already-opposite candidate.
         *
         * @param board Current puzzle board.
         * @param result Rule result accumulator.
         * @param branch Contradicting branch state.
         * @param seedCandidate Seed candidate key.
         * @returns True when contradiction evidence was added.
         */
        private bool AppendContradictionEvidenceForBranch(Board board, RuleResult result, PropagationState branch, int seedCandidate)
        {
            if (branch == null)
            {
                return false;
            }

            int contradictionCandidate = branch.ContradictionCandidate ?? seedCandidate;

            if (branch.ContradictionSourceCandidate.HasValue && branch.ContradictionSourceLinkKind.HasValue)
            {
                int sourceCandidate = branch.ContradictionSourceCandidate.Value;
                bool addedPath = AppendPathEvidenceForBranch(board, result, branch, seedCandidate, sourceCandidate);
                AddUsedCell(result, GetRow(board, contradictionCandidate), GetColumn(board, contradictionCandidate), GetDigit(board, contradictionCandidate), DeductionTag);
                AddEvidenceDirectionalLink(result, board, sourceCandidate, contradictionCandidate, branch.ContradictionSourceLinkKind.Value);
                return addedPath || sourceCandidate == seedCandidate || contradictionCandidate == sourceCandidate;
            }

            return AppendPathEvidenceForBranch(board, result, branch, seedCandidate, contradictionCandidate);
        }

        /**
         * Reconstruct one parent-linked path from seed to target.
         *
         * @param branch Branch propagation state.
         * @param seedCandidate Seed candidate key.
         * @param targetCandidate Target candidate key.
         * @param path Output path from seed to target.
         * @returns True when a full path was reconstructed.
         */
        private static bool TryBuildPath(PropagationState branch, int seedCandidate, int targetCandidate, out List<int> path)
        {
            path = new List<int>();
            var visited = new HashSet<int>();
            int current = targetCandidate;

            while (true)
            {
                if (!visited.Add(current))
                {
                    path.Clear();
                    return false;
                }

                path.Add(current);
                if (current == seedCandidate)
                {
                    path.Reverse();
                    return true;
                }

                if (!branch.InferenceCauseByCandidate.TryGetValue(current, out var cause))
                {
                    path.Clear();
                    return false;
                }

                current = cause.FromCandidate;
            }
        }

        /**
         * Map concrete rule changes back to candidate keys for evidence targeting.
         *
         * @param board Current puzzle board.
         * @param result Rule result containing changes.
         * @returns Ordered set of changed candidate keys.
         */
        private static List<int> CollectChangedCandidates(Board board, RuleResult result)
        {
            var candidates = new HashSet<int>();
            if (result == null || result.Changes == null)
            {
                return candidates.ToList();
            }

            for (int i = 0; i < result.Changes.Count; i++)
            {
                var change = result.Changes[i];
                if (change == null)
                {
                    continue;
                }

                if (change.NewValue.HasValue)
                {
                    candidates.Add(MakeCandidateKey(board, change.Row, change.Column, change.NewValue.Value));
                }

                if (change.RemovedCandidates == null)
                {
                    continue;
                }

                for (int c = 0; c < change.RemovedCandidates.Count; c++)
                {
                    candidates.Add(MakeCandidateKey(board, change.Row, change.Column, change.RemovedCandidates[c]));
                }
            }

            return candidates
                .OrderBy(k => GetRow(board, k))
                .ThenBy(k => GetColumn(board, k))
                .ThenBy(k => GetDigit(board, k))
                .ToList();
        }

        /**
         * Add directional strong/weak links used by the displayed forcing evidence.
         *
         * @param board Current puzzle board.
         * @param result Rule result accumulator.
         * @param branch Branch propagation state.
         * @param evidenceCandidates Ordered evidence candidates shown in the preview.
         */
        private void AppendEvidenceLinks(Board board, RuleResult result, PropagationState branch, List<int> evidenceCandidates)
        {
            if (result == null || branch == null || evidenceCandidates == null || evidenceCandidates.Count == 0)
            {
                return;
            }

            var evidenceSet = new HashSet<int>(evidenceCandidates);
            for (int i = 0; i < evidenceCandidates.Count; i++)
            {
                int targetCandidate = evidenceCandidates[i];
                if (!branch.InferenceCauseByCandidate.TryGetValue(targetCandidate, out var cause))
                {
                    continue;
                }

                if (!evidenceSet.Contains(cause.FromCandidate))
                {
                    continue;
                }

                if (!cause.IsPreviewLink)
                {
                    continue;
                }

                AddEvidenceDirectionalLink(result, board, cause.FromCandidate, targetCandidate, cause.LinkKind);
            }
        }

        /**
         * Add one directional evidence link when not already present.
         *
         * @param result Rule result accumulator.
         * @param board Current puzzle board.
         * @param fromCandidate Source candidate key.
         * @param toCandidate Target candidate key.
         * @param kind Strong or weak link kind.
         */
        private static void AddEvidenceDirectionalLink(RuleResult result, Board board, int fromCandidate, int toCandidate, DirectionalLinkKind kind)
        {
            if (GetRow(board, fromCandidate) == GetRow(board, toCandidate) 
                && GetColumn(board, fromCandidate) == GetColumn(board, toCandidate))
            {
                return;
            }

            if (result.UsedDirectionalLinks == null)
            {
                result.UsedDirectionalLinks = new List<DirectionalCellLink>();
            }

            var candidate = new DirectionalCellLink
            {
                Kind = kind,
                Start = new DirectionalLinkEndpoint
                {
                    Row = GetRow(board, fromCandidate),
                    Column = GetColumn(board, fromCandidate),
                    Digit = GetDigit(board, fromCandidate),
                },
                End = new DirectionalLinkEndpoint
                {
                    Row = GetRow(board, toCandidate),
                    Column = GetColumn(board, toCandidate),
                    Digit = GetDigit(board, toCandidate),
                }
            };

            for (int i = 0; i < result.UsedDirectionalLinks.Count; i++)
            {
                var existing = result.UsedDirectionalLinks[i];
                if (existing != null && existing.Equals(candidate))
                {
                    return;
                }
            }

            result.UsedDirectionalLinks.Add(candidate);
        }

        /**
         * Determine whether a weak conflict should be rendered as a visible weak link.
         *
         * @param board Current puzzle board.
         * @param fromCandidate Source candidate.
         * @param toCandidate Target candidate.
         * @returns True when the conflict is a conjugate pair in a row/column/box.
         */
        private static bool ShouldExposeWeakConflictAsLink(Board board, int fromCandidate, int toCandidate)
        {
            if (GetDigit(board, fromCandidate) != GetDigit(board, toCandidate))
            {
                // Only show links between the same digits
                return false;
            }

            int fromRow = GetRow(board, fromCandidate);
            int fromColumn = GetColumn(board, fromCandidate);
            int toRow = GetRow(board, toCandidate);
            int toColumn = GetColumn(board, toCandidate);

            if (fromRow == toRow && fromColumn == toColumn)
            {
                // Same cells never have a link, it is infered
                return false;
            }

            if (fromRow == toRow)
            {
                // Only show links when the digit is a conjugate pair (only 2 candidates) in the row
                return CountDigitCandidatesInRow(board, fromRow, GetDigit(board, fromCandidate)) == 2;
            }

            if (fromColumn == toColumn)
            {
                // Only show links when the digit is a conjugate pair (only 2 candidates) in the column
                return CountDigitCandidatesInColumn(board, fromColumn, GetDigit(board, fromCandidate)) == 2;
            }

            // if (board.Cells[fromRow, fromColumn] != null && board.Cells[fromRow, fromColumn].Box == board.Cells[toRow, toColumn].Box)
            // {
            //     // Only show links when the digit is a conjugate pair (only 2 candidates) in the box
            //     return CountDigitCandidatesInBox(board, board.Cells[fromRow, fromColumn].Box, GetDigit(board, fromCandidate)) == 2;
            // }

            return true;
        }

        /**
         * Add one UsedCell entry when not already present.
         *
         * @param result Rule result accumulator.
         * @param row Row index.
         * @param column Column index.
         * @param digit Candidate digit.
         * @param tag Visualization tag.
         */
        private static void AddUsedCell(RuleResult result, int row, int column, int digit, string tag)
        {
            if (result.UsedCells.Exists(u => u.Row == row && u.Column == column && u.Candidate == digit && string.Equals(u.HighlightTag, tag, StringComparison.Ordinal)))
            {
                return;
            }

            result.UsedCells.Add(new UsedCell
            {
                Row = row,
                Column = column,
                Candidate = digit,
                HighlightTag = tag
            });
        }

        /**
         * Add a bidirectional strong link between two candidates.
         *
         * @param links Strong-link adjacency map.
         * @param a First candidate key.
         * @param b Second candidate key.
         */
        private static void AddLink(Dictionary<int, List<int>> links, int a, int b)
        {
            if (a == b) return;

            if (!links.TryGetValue(a, out var listA))
            {
                listA = new List<int>();
                links[a] = listA;
            }

            if (!listA.Contains(b))
            {
                listA.Add(b);
            }

            if (!links.TryGetValue(b, out var listB))
            {
                listB = new List<int>();
                links[b] = listB;
            }

            if (!listB.Contains(a))
            {
                listB.Add(a);
            }
        }

        /**
         * Add a bidirectional weak conflict between two candidates.
         *
         * @param links Weak-conflict adjacency map.
         * @param a First candidate key.
         * @param b Second candidate key.
         */
        private static void AddWeakConflictPair(Dictionary<int, List<int>> links, int a, int b)
        {
            if (a == b) return;

            if (!links.TryGetValue(a, out var listA))
            {
                listA = new List<int>();
                links[a] = listA;
            }

            if (!listA.Contains(b))
            {
                listA.Add(b);
            }

            if (!links.TryGetValue(b, out var listB))
            {
                listB = new List<int>();
                links[b] = listB;
            }

            if (!listB.Contains(a))
            {
                listB.Add(a);
            }
        }

        /**
         * Determine whether a candidate is still available on the board.
         *
         * @param board Current puzzle board.
         * @param candidate Candidate key.
         * @returns True when the corresponding candidate is still present.
         */
        private static bool IsCandidateStillCandidate(Board board, int candidate)
        {
            int row = GetRow(board, candidate);
            int column = GetColumn(board, candidate);
            int digit = GetDigit(board, candidate);
            var cell = board.Cells[row, column];
            return cell != null && !cell.Value.HasValue && cell.Candidates != null && cell.Candidates.Contains(digit);
        }

        /**
         * Determine whether two candidate keys belong to the same cell.
         *
         * @param board Current puzzle board.
         * @param a First candidate key.
         * @param b Second candidate key.
         * @returns True when both candidates refer to the same row/column cell.
         */
        private static bool IsSameCell(Board board, int a, int b)
        {
            return GetRow(board, a) == GetRow(board, b)
                && GetColumn(board, a) == GetColumn(board, b);
        }

        /**
         * Determine whether a weak preview edge is a valid conjugate-pair conflict.
         *
         * @param board Current puzzle board.
         * @param fromCandidate Source candidate.
         * @param toCandidate Target candidate.
         * @returns True when the weak edge is supported by a unit with exactly two candidates.
         */
        private static bool IsValidWeakPreviewLink(Board board, int fromCandidate, int toCandidate)
        {
            if (GetDigit(board, fromCandidate) != GetDigit(board, toCandidate))
            {
                return false;
            }

            int fromRow = GetRow(board, fromCandidate);
            int fromColumn = GetColumn(board, fromCandidate);
            int toRow = GetRow(board, toCandidate);
            int toColumn = GetColumn(board, toCandidate);

            if (fromRow == toRow && fromColumn == toColumn)
            {
                return false;
            }

            int digit = GetDigit(board, fromCandidate);

            if (fromRow == toRow)
            {
                return CountDigitCandidatesInRow(board, fromRow, digit) == 2;
            }

            if (fromColumn == toColumn)
            {
                return CountDigitCandidatesInColumn(board, fromColumn, digit) == 2;
            }

            if (board.Cells[fromRow, fromColumn] != null && board.Cells[fromRow, fromColumn].Box == board.Cells[toRow, toColumn].Box)
            {
                return CountDigitCandidatesInBox(board, board.Cells[fromRow, fromColumn].Box, digit) == 2;
            }

            return false;
        }

        private static int CountDigitCandidatesInRow(Board board, int row, int digit)
        {
            int count = 0;
            for (int column = 0; column < board.Size; column++)
            {
                var cell = board.Cells[row, column];
                if (cell != null && !cell.Value.HasValue && cell.Candidates != null && cell.Candidates.Contains(digit))
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountDigitCandidatesInColumn(Board board, int column, int digit)
        {
            int count = 0;
            for (int row = 0; row < board.Size; row++)
            {
                var cell = board.Cells[row, column];
                if (cell != null && !cell.Value.HasValue && cell.Candidates != null && cell.Candidates.Contains(digit))
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountDigitCandidatesInBox(Board board, int box, int digit)
        {
            int count = 0;
            foreach (var cell in board.GetBox(box))
            {
                if (cell != null && !cell.Value.HasValue && cell.Candidates != null && cell.Candidates.Contains(digit))
                {
                    count++;
                }
            }

            return count;
        }

        /**
         * Format a candidate as rNcM=d for explanations.
         *
         * @param board Current puzzle board.
         * @param candidate Candidate key.
         * @returns Human-readable candidate text.
         */
        private static string FormatCandidate(Board board, int candidate)
        {
            return $"r{GetRow(board, candidate) + 1}c{GetColumn(board, candidate) + 1}={GetDigit(board, candidate)}";
        }

        /**
         * Encode one candidate candidate key.
         *
         * @param board Current puzzle board.
         * @param row Row index.
         * @param column Column index.
         * @param digit Candidate digit.
         * @returns Encoded candidate key.
         */
        private static int MakeCandidateKey(Board board, int row, int column, int digit)
        {
            int size = board.Size;
            return ((row * size) + column) * (size + 1) + digit;
        }

        /**
         * Decode candidate row index.
         *
         * @param board Current puzzle board.
         * @param candidate Encoded candidate key.
         * @returns Row index.
         */
        private static int GetRow(Board board, int candidate)
        {
            int size = board.Size;
            int packedCell = candidate / (size + 1);
            return packedCell / size;
        }

        /**
         * Decode candidate column index.
         *
         * @param board Current puzzle board.
         * @param candidate Encoded candidate key.
         * @returns Column index.
         */
        private static int GetColumn(Board board, int candidate)
        {
            int size = board.Size;
            int packedCell = candidate / (size + 1);
            return packedCell % size;
        }

        /**
         * Decode candidate digit.
         *
         * @param board Current puzzle board.
         * @param candidate Encoded candidate key.
         * @returns Candidate digit.
         */
        private static int GetDigit(Board board, int candidate)
        {
            return candidate % (board.Size + 1);
        }

        /**
         * Build a compact unit-digit group index.
         *
         * @param board Current puzzle board.
         * @param unitType 0=row, 1=column, 2=box.
         * @param unitIndex Unit index.
         * @param digit Candidate digit.
         * @returns Group index key.
         */
        private static int MakeUnitDigitGroupIndex(Board board, UnitType unitType, int unitIndex, int digit)
        {
            return ((((int)unitType) * board.Size) + unitIndex) * (board.Size + 1) + digit;
        }

    }

}

