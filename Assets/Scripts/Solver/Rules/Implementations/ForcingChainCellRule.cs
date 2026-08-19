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
     * strong/weak inferences.
     *
     * Strong links are built from:
     * - Bi-value cells (exactly two candidates in one cell).
     * - Conjugate pairs (exactly two candidates for a digit in one row/column/box).
     * 
     * Weak links are built from:
     * - True digits to all seen matching digits in the same cell or unit (row/column/box), where there are multiple candidates of the digit seen.
     *
     * Forcing chains can occur when:
     * 1. Chains start from all candidates in a single cell
     * 2. Chains start from a single digit's candidates in a single row/column/box
     * and all chains combine to produce a common conclusion (candidate placement or removal).
     * This class implements 1
     *
     * This bounded implementation targets human-tractable chains and intentionally
     * avoids exhaustive SAT-level search.
     *
     * This rule should only be applied if colouring is enabled and has at least
     * two colours enabled.
     */
    public class ForcingChainCellRule : CachedRuleBase
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
            public Dictionary<int, List<InferenceCause>> InferenceCausesByCandidate = new Dictionary<int, List<InferenceCause>>();
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

        public override string Name => "Forcing Chain (Cell)";

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

            return FindPlan(board) != null;
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
                result.Apply = false;
                return result;
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

            // Check all cells with candidates
            // For each cell check all candidates as true assumptions, and check if:
            // - Any result in contradictions, and force them to false
            // - There are any common conclusions between the true branches, and force those conclusions (which may be true or false)
            foreach (Cell cell in board.Cells)
            {
                if (cell == null || cell.Value.HasValue || cell.Candidates == null || cell.Candidates.Count == 0)
                {
                    continue;
                }

                var plansForCell = new List<ForcingPlan>();
                foreach (int digit in cell.Candidates.OrderBy(v => v))
                {
                    int seed = MakeCandidateKey(board, cell.Row, cell.Column, digit);
                    if (!model.CandidateIndexByKey.ContainsKey(seed))
                    {
                        continue;
                    }

                    var plan = EvaluateSeedCandidate(board, model, seed);
                    if (plan != null)
                    {
                        // Store the plan to check with other digit plans in the same cell for common conclusions.
                        plansForCell.Add(plan);
                    }
                }
                var combinedPlan = CombinePlansForCell(plansForCell);
                var score = ScorePlan(board, digitCounts, combinedPlan);

                // Check if the score is better than the best score so far, and if so, store it as the best plan.
                if (score < bestScore)
                {
                    bestScore = score;
                    bestPlan = combinedPlan;
                }
            }

            return bestPlan;
        }

        /**
         * Evaluate a seed candidate by propagating true and false assumptions.
         *
         * @param board Current puzzle board.
         * @param model Chain model containing candidate links.
         * @param seedCandidate Candidate key to evaluate.
         * @returns Forcing plan if deductions are found; otherwise, null.
         */
        private ForcingPlan EvaluateSeedCandidate(Board board, ChainModel model, int seedCandidate)
        {
            var trueBranch = PropagateFromAssumption(board, model, seedCandidate, assumeTrue: true, enableUnitCompletion: true);
            var falseBranch = PropagateFromAssumption(board, model, seedCandidate, assumeTrue: false, enableUnitCompletion: true);

            if (!trueBranch.HasContradiction && !falseBranch.HasContradiction)
            {
                // Find common conclusions between the two branches.
                var commonFalse = trueBranch.FalseCandidates.Intersect(falseBranch.FalseCandidates).ToList();
                var commonTrue = trueBranch.TrueCandidates.Intersect(falseBranch.TrueCandidates).ToList();

                if (commonFalse.Count == 0 && commonTrue.Count == 0)
                {
                    return null;
                }

                return new ForcingPlan
                {
                    SeedCandidate = seedCandidate,
                    TrueBranch = trueBranch,
                    FalseBranch = falseBranch,
                    CommonFalseCandidates = commonFalse,
                    CommonTrueCandidates = commonTrue,
                    ContradictionOnTrueBranch = false,
                    ContradictionOnFalseBranch = false
                };
            }
            else
            {
                return new ForcingPlan
                {
                    SeedCandidate = seedCandidate,
                    TrueBranch = trueBranch,
                    FalseBranch = falseBranch,
                    CommonFalseCandidates = new List<int>(),
                    CommonTrueCandidates = new List<int>(),
                    ContradictionOnTrueBranch = trueBranch.HasContradiction,
                    ContradictionOnFalseBranch = falseBranch.HasContradiction
                };
            }
        }

        /**
         * Combine the plans and check for common conclusions, and score them to find the best one.
         * 
         * @param plans List of forcing plans for candidates in the same cell.
         * @returns Combined forcing plan.
         */
        private ForcingPlan CombinePlansForCell(List<ForcingPlan> plans)
        {
            if (plans == null || plans.Count == 0)
            {
                return null;
            }

            var combinedPlan = new ForcingPlan
            {
                SeedCandidate = plans[0].SeedCandidate,
                TrueBranch = plans[0].TrueBranch,
                FalseBranch = plans[0].FalseBranch,
                CommonFalseCandidates = new List<int>(plans[0].CommonFalseCandidates),
                CommonTrueCandidates = new List<int>(plans[0].CommonTrueCandidates),
                ContradictionOnTrueBranch = plans[0].ContradictionOnTrueBranch,
                ContradictionOnFalseBranch = plans[0].ContradictionOnFalseBranch
            };

            for (int i = 1; i < plans.Count; i++)
            {
                var plan = plans[i];
                combinedPlan.CommonFalseCandidates = combinedPlan.CommonFalseCandidates.Intersect(plan.CommonFalseCandidates).ToList();
                combinedPlan.CommonTrueCandidates = combinedPlan.CommonTrueCandidates.Intersect(plan.CommonTrueCandidates).ToList();
                combinedPlan.ContradictionOnTrueBranch |= plan.ContradictionOnTrueBranch;
                combinedPlan.ContradictionOnFalseBranch |= plan.ContradictionOnFalseBranch;
            }

            return combinedPlan;
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
                    // Only true candidates propagate weak links; false candidates do not.
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
                            isPreviewLink: ShouldExposeWeakConflictAsLink(board, candidate, weakLinks[i]) && !IsStrongLink(model, candidate, weakLinks[i]));
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
         * Determine whether a candidate pair is already connected by a strong link.
         *
         * @param model Inference graph model.
         * @param fromCandidate Source candidate.
         * @param toCandidate Target candidate.
         * @returns True when the pair is a strong-link edge.
         */
        private static bool IsStrongLink(ChainModel model, int fromCandidate, int toCandidate)
        {
            return model != null
                && model.StrongLinksByKey.TryGetValue(fromCandidate, out var linked)
                && linked != null
                && linked.Contains(toCandidate);
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
                    RecordInferenceCause(state, candidate, sourceCandidate.Value, sourceLinkKind.Value, isPreviewLink);
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
                    RecordInferenceCause(state, candidate, sourceCandidate.Value, sourceLinkKind.Value, isPreviewLink);
                    return;
                }
            }

            if(!state.Pending.Contains((candidate, valueTrue)))
            {
                state.Pending.Enqueue((candidate, valueTrue));

                if (sourceCandidate.HasValue && sourceLinkKind.HasValue)
                {
                    RecordInferenceCause(state, candidate, sourceCandidate.Value, sourceLinkKind.Value, isPreviewLink);
                }
            }
        }

        /**
         * Record one causal edge for a candidate, optionally as preview-only.
         *
         * @param state Branch state.
         * @param candidate Target candidate.
         * @param fromCandidate Source candidate.
         * @param linkKind Strong or weak link kind.
         * @param isPreviewLink True when the edge should be rendered in the preview.
         */
        private static void RecordInferenceCause(PropagationState state, int candidate, int fromCandidate, DirectionalLinkKind linkKind, bool isPreviewLink)
        {
            if (state == null)
            {
                return;
            }

            if (!state.InferenceCausesByCandidate.TryGetValue(candidate, out var causes))
            {
                causes = new List<InferenceCause>();
                state.InferenceCausesByCandidate[candidate] = causes;
            }

            bool alreadyStored = causes.Exists(c => c.FromCandidate == fromCandidate && c.LinkKind == linkKind && c.IsPreviewLink == isPreviewLink);
            if (alreadyStored)
            {
                return;
            }

            causes.Add(new InferenceCause
            {
                FromCandidate = fromCandidate,
                LinkKind = linkKind,
                IsPreviewLink = isPreviewLink,
            });
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
                // Too many trues in the cell, so the branch is impossible.
                state.HasContradiction = true;
                state.ContradictionReason = $"Cell r{row + 1}c{column + 1} has multiple true forced digits.";
                return;
            }

            if (trueCount == 0 && unknownCount == 0)
            {
                // Resulted in no possible candidates for the cell, so the branch is impossible.
                state.HasContradiction = true;
                state.ContradictionReason = $"Cell r{row + 1}c{column + 1} has no valid candidates.";
                return;
            }

            if (trueCount == 0 && unknownCount == 1)
            {
                // Only one candidate left in the cell, so it must be true.
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

                    if (expectTrue)
                    {
                        if (plan.TrueBranch.TrueCandidates.Contains(candidate))
                        {
                            addedAny |= AppendPathEvidenceForBranch(board, result, plan.TrueBranch, plan.SeedCandidate, candidate);
                        }

                        if (plan.FalseBranch.TrueCandidates.Contains(candidate))
                        {
                            addedAny |= AppendPathEvidenceForBranch(board, result, plan.FalseBranch, plan.SeedCandidate, candidate);
                        }
                    }

                    if (expectFalse)
                    {
                        if(plan.TrueBranch.FalseCandidates.Contains(candidate))
                        {
                            addedAny |= AppendPathEvidenceForBranch(board, result, plan.TrueBranch, plan.SeedCandidate, candidate);
                        }

                        if (plan.FalseBranch.FalseCandidates.Contains(candidate))
                        {
                            addedAny |= AppendPathEvidenceForBranch(board, result, plan.FalseBranch, plan.SeedCandidate, candidate);
                        }
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
                var causes = TryGetCauses(branch, toCandidate);
                if (causes == null)
                {
                    continue;
                }

                foreach (var cause in causes)
                {
                    if (!cause.IsPreviewLink)
                    {
                        continue;
                    }

                    AddEvidenceDirectionalLink(result, board, fromCandidate, toCandidate, cause.LinkKind);
                }
            }

            return true;
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
                var causes = TryGetCauses(branch, targetCandidate);
                if (causes == null)
                {
                    continue;
                }

                foreach(var cause in causes)
                {
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

            return InnerTryBuildPath(visited, branch, seedCandidate, current, path, out path);
        }

        /**
         * Recursive function for TryBuildPath, that attempts to build a path from the target candidate to the seed candidate.
         * If the path cannot be fully reconstructed, finalPath will be cleared..
         *
         * @param visited Set of already-visited candidates to avoid cycles.
         * @param branch Branch propagation state.
         * @param seedCandidate Seed candidate key.
         * @param current Current candidate key being processed.
         * @param path Current path from target to seed.
         * @param finalPath Output path from seed to target.
         * @returns True when a full path was reconstructed.
         */
        private static bool InnerTryBuildPath(HashSet<int> visited, PropagationState branch, int seedCandidate, int current, List<int> path, out List<int> finalPath)
        {
            finalPath = path;
            if (!visited.Add(current))
            {
                finalPath.RemoveAt(finalPath.Count - 1);
                return false;
            }

            finalPath.Add(current);
            if (current == seedCandidate)
            {
                finalPath.Reverse();
                return true;
            }

            var causes = TryGetCauses(branch, current);
            if (causes == null)
            {
                finalPath.RemoveAt(finalPath.Count - 1);
                return false;
            }

            foreach(var cause in causes)
            {
                if(InnerTryBuildPath(visited, branch, seedCandidate, cause.FromCandidate, finalPath, out finalPath))
                {
                    return true;
                }
            }
            finalPath.RemoveAt(finalPath.Count - 1);
            return false;
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
         * Prefer a previewable cause when multiple causes exist for the same candidate.
         *
         * @param branch Branch propagation state.
         * @param candidate Target candidate.
         * @param cause Chosen cause, if any.
         * @returns The causes, or null if none exist.
         */
        private static List<InferenceCause> TryGetCauses(PropagationState branch, int candidate)
        {
            if (branch == null || !branch.InferenceCausesByCandidate.TryGetValue(candidate, out var causes) || causes == null || causes.Count == 0)
            {
                return null;
            }

            return causes;
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
                if (existing == null || existing.Start == null || existing.End == null)
                {
                    continue;
                }

                bool sameDirection = existing.Start.Row == candidate.Start.Row
                    && existing.Start.Column == candidate.Start.Column
                    && existing.Start.Digit == candidate.Start.Digit
                    && existing.End.Row == candidate.End.Row
                    && existing.End.Column == candidate.End.Column
                    && existing.End.Digit == candidate.End.Digit;

                bool oppositeDirection = existing.Start.Row == candidate.End.Row
                    && existing.Start.Column == candidate.End.Column
                    && existing.Start.Digit == candidate.End.Digit
                    && existing.End.Row == candidate.Start.Row
                    && existing.End.Column == candidate.Start.Column
                    && existing.End.Digit == candidate.Start.Digit;

                if (sameDirection || oppositeDirection)
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

            // if (fromRow == toRow)
            // {
            //     // Only show links when the digit is a conjugate pair (only 2 candidates) in the row
            //     return CountDigitCandidatesInRow(board, fromRow, GetDigit(board, fromCandidate)) > 2;
            // }

            // if (fromColumn == toColumn)
            // {
            //     // Only show links when the digit is a conjugate pair (only 2 candidates) in the column
            //     return CountDigitCandidatesInColumn(board, fromColumn, GetDigit(board, fromCandidate)) > 2;
            // }

            // if (board.Cells[fromRow, fromColumn] != null && board.Cells[fromRow, fromColumn].Box == board.Cells[toRow, toColumn].Box)
            // {
            //     // Only show links when the digit is a conjugate pair (only 2 candidates) in the box
            //     return CountDigitCandidatesInBox(board, board.Cells[fromRow, fromColumn].Box, GetDigit(board, fromCandidate)) > 2;
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

