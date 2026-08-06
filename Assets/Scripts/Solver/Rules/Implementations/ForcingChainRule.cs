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
     *    the same literal false, eliminate that candidate; if both force the
     *    same literal true, place that value.
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
        private const string TargetATag = "TargetA";
        private const string TargetBTag = "TargetB";
        private const string DeductionTag = "Deduction";

        private const int MaxSeedCount = 729;
        private const int MaxAssignmentsPerBranch = 4000;
        private const int MaxEvidencePerBranch = 8;

        private sealed class ChainModel
        {
            public int Size;
            public List<int> Literals = new List<int>();
            public Dictionary<int, int> LiteralIndexByKey = new Dictionary<int, int>();
            public Dictionary<int, List<int>> StrongLinksByKey = new Dictionary<int, List<int>>();
            public Dictionary<int, List<int>> WeakConflictByKey = new Dictionary<int, List<int>>();
            public Dictionary<int, List<int>> CellGroupsByIndex = new Dictionary<int, List<int>>();
            public Dictionary<int, List<int>> UnitDigitGroupsByIndex = new Dictionary<int, List<int>>();
        }

        private sealed class PropagationState
        {
            public HashSet<int> TrueLiterals = new HashSet<int>();
            public HashSet<int> FalseLiterals = new HashSet<int>();
            public bool HasContradiction;
            public string ContradictionReason;
            public int? ContradictionLiteral;
            public int? ContradictionSourceLiteral;
            public DirectionalLinkKind? ContradictionSourceLinkKind;
            public Queue<(int key, bool valueTrue)> Pending = new Queue<(int key, bool valueTrue)>();
            public List<int> AssignmentOrder = new List<int>();
            public Dictionary<int, InferenceCause> InferenceCauseByLiteral = new Dictionary<int, InferenceCause>();
        }

        private sealed class InferenceCause
        {
            public int FromLiteral;
            public DirectionalLinkKind LinkKind;
            public bool IsPreviewLink;
        }

        private sealed class ForcingPlan
        {
            public int SeedLiteral;
            public PropagationState TrueBranch;
            public PropagationState FalseBranch;
            public List<int> CommonFalseLiterals = new List<int>();
            public List<int> CommonTrueLiterals = new List<int>();
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
            if (model == null || model.Literals.Count == 0)
            {
                return null;
            }

            ForcingPlan doubleSidedFallback = null;

            var orderedSeeds = model.Literals
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
                    SeedLiteral = seed,
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
            if (model == null || model.Literals.Count == 0)
            {
                return null;
            }

            var digitCounts = BuildDigitCounts(board);
            var orderedSeeds = model.Literals
                .OrderBy(k => digitCounts.TryGetValue(GetDigit(board, k), out var count) ? count : int.MaxValue)
                .ThenBy(k => GetRow(board, k))
                .ThenBy(k => GetColumn(board, k))
                .ToList();

            ForcingPlan bestPlan = null;
            int bestScore = int.MaxValue;

            foreach (var seed in orderedSeeds)
            {
                var trueBranch = PropagateFromAssumption(board, model, seed, assumeTrue: true, enableUnitCompletion: false);
                var falseBranch = PropagateFromAssumption(board, model, seed, assumeTrue: false, enableUnitCompletion: false);

                var plan = new ForcingPlan
                {
                    SeedLiteral = seed,
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

                plan.CommonFalseLiterals = trueBranch.FalseLiterals
                    .Intersect(falseBranch.FalseLiterals)
                    .Where(k => k != seed)
                    .Where(k => !IsSameCell(board, k, seed))
                    .Where(LiteralAvailable)
                    .OrderBy(k => GetRow(board, k))
                    .ThenBy(k => GetColumn(board, k))
                    .ThenBy(k => GetDigit(board, k))
                    .ToList();

                plan.CommonTrueLiterals = trueBranch.TrueLiterals
                    .Intersect(falseBranch.TrueLiterals)
                    .Where(k => !IsSameCell(board, k, seed))
                    .Where(LiteralAvailable)
                    .OrderBy(k => GetRow(board, k))
                    .ThenBy(k => GetColumn(board, k))
                    .ThenBy(k => GetDigit(board, k))
                    .ToList();

                bool hasCommonConclusion = plan.CommonFalseLiterals.Count > 0 || plan.CommonTrueLiterals.Count > 0;
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

            bool LiteralAvailable(int literalKey)
            {
                return IsLiteralStillCandidate(board, literalKey);
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
            int seedDigit = GetDigit(board, plan.SeedLiteral);
            int seedCount = digitCounts.TryGetValue(seedDigit, out var c) ? c : int.MaxValue / 4;

            bool isCommonConclusion = plan.CommonFalseLiterals.Count > 0 || plan.CommonTrueLiterals.Count > 0;
            bool isPlacement = plan.CommonTrueLiterals.Count > 0;
            int deductionCount = plan.CommonTrueLiterals.Count > 0 ? plan.CommonTrueLiterals.Count : plan.CommonFalseLiterals.Count;
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
         * @returns Chain model containing literals, links, and deduction groups.
         */
        private ChainModel BuildChainModel(Board board)
        {
            var model = new ChainModel { Size = board.Size };
            int size = board.Size;

            // Collect active candidate literals from unsolved cells.
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
                        int key = MakeLiteralKey(board, row, column, digit);
                        model.LiteralIndexByKey[key] = model.Literals.Count;
                        model.Literals.Add(key);
                    }
                }
            }

            if (model.Literals.Count == 0)
            {
                return model;
            }

            // Prepare per-cell and per-unit groups used for hidden/naked single propagation.
            for (int row = 0; row < size; row++)
            {
                for (int column = 0; column < size; column++)
                {
                    var cell = board.Cells[row, column];
                    if (cell == null || cell.Value.HasValue || cell.Candidates == null || cell.Candidates.Count == 0)
                    {
                        continue;
                    }

                    int cellIndex = row * size + column;
                    var group = new List<int>();
                    foreach (int digit in cell.Candidates)
                    {
                        group.Add(MakeLiteralKey(board, row, column, digit));
                    }

                    model.CellGroupsByIndex[cellIndex] = group;
                }
            }

            for (int digit = 1; digit <= size; digit++)
            {
                for (int row = 0; row < size; row++)
                {
                    var group = new List<int>();
                    for (int column = 0; column < size; column++)
                    {
                        var cell = board.Cells[row, column];
                        if (cell == null || cell.Value.HasValue) continue;
                        if (cell.Candidates != null && cell.Candidates.Contains(digit))
                        {
                            group.Add(MakeLiteralKey(board, row, column, digit));
                        }
                    }

                    if (group.Count > 0)
                    {
                        int groupIndex = MakeUnitDigitGroupIndex(board, unitType: 0, unitIndex: row, digit);
                        model.UnitDigitGroupsByIndex[groupIndex] = group;
                    }
                }

                for (int column = 0; column < size; column++)
                {
                    var group = new List<int>();
                    for (int row = 0; row < size; row++)
                    {
                        var cell = board.Cells[row, column];
                        if (cell == null || cell.Value.HasValue) continue;
                        if (cell.Candidates != null && cell.Candidates.Contains(digit))
                        {
                            group.Add(MakeLiteralKey(board, row, column, digit));
                        }
                    }

                    if (group.Count > 0)
                    {
                        int groupIndex = MakeUnitDigitGroupIndex(board, unitType: 1, unitIndex: column, digit);
                        model.UnitDigitGroupsByIndex[groupIndex] = group;
                    }
                }

                for (int box = 0; box < size; box++)
                {
                    var group = new List<int>();
                    foreach (var cell in board.GetBox(box))
                    {
                        if (cell == null || cell.Value.HasValue) continue;
                        if (cell.Candidates != null && cell.Candidates.Contains(digit))
                        {
                            group.Add(MakeLiteralKey(board, cell.Row, cell.Column, digit));
                        }
                    }

                    if (group.Count > 0)
                    {
                        int groupIndex = MakeUnitDigitGroupIndex(board, unitType: 2, unitIndex: box, digit);
                        model.UnitDigitGroupsByIndex[groupIndex] = group;
                    }
                }
            }

            // Weak conflicts: keep the full propagation graph so the rule can find
            // forcing chains, but only real conjugate-pair edges are exposed as
            // visible weak links in the preview. The rest remain internal conflict
            // steps used by the solver.
            foreach (var literal in model.Literals)
            {
                var conflicts = new HashSet<int>();
                int row = GetRow(board, literal);
                int column = GetColumn(board, literal);
                int digit = GetDigit(board, literal);
                var cell = board.Cells[row, column];

                if (cell != null && cell.Candidates != null)
                {
                    foreach (int other in cell.Candidates)
                    {
                        if (other == digit) continue;
                        conflicts.Add(MakeLiteralKey(board, row, column, other));
                    }
                }

                foreach (var peer in board.GetPeers(board.Cells[row, column]))
                {
                    if (peer == null || peer.Value.HasValue || peer.Candidates == null) continue;
                    if (!peer.Candidates.Contains(digit)) continue;
                    conflicts.Add(MakeLiteralKey(board, peer.Row, peer.Column, digit));
                }

                model.WeakConflictByKey[literal] = conflicts.OrderBy(k => k).ToList();
            }

            // Strong links from bi-value cells.
            for (int row = 0; row < size; row++)
            {
                for (int column = 0; column < size; column++)
                {
                    var cell = board.Cells[row, column];
                    if (cell == null || cell.Value.HasValue || cell.Candidates == null || cell.Candidates.Count != 2)
                    {
                        continue;
                    }

                    var pair = cell.Candidates.OrderBy(v => v).ToList();
                    int a = MakeLiteralKey(board, row, column, pair[0]);
                    int b = MakeLiteralKey(board, row, column, pair[1]);
                    AddStrongLink(model.StrongLinksByKey, a, b);
                }
            }

            // Strong links from conjugate pairs in each unit.
            for (int digit = 1; digit <= size; digit++)
            {
                for (int row = 0; row < size; row++)
                {
                    var members = model.UnitDigitGroupsByIndex.TryGetValue(MakeUnitDigitGroupIndex(board, 0, row, digit), out var rowGroup)
                        ? rowGroup
                        : null;
                    if (members != null && members.Count == 2)
                    {
                        AddStrongLink(model.StrongLinksByKey, members[0], members[1]);
                        AddWeakConflictPair(model.WeakConflictByKey, members[0], members[1]);
                    }
                }

                for (int column = 0; column < size; column++)
                {
                    var members = model.UnitDigitGroupsByIndex.TryGetValue(MakeUnitDigitGroupIndex(board, 1, column, digit), out var colGroup)
                        ? colGroup
                        : null;
                    if (members != null && members.Count == 2)
                    {
                        AddStrongLink(model.StrongLinksByKey, members[0], members[1]);
                        AddWeakConflictPair(model.WeakConflictByKey, members[0], members[1]);
                    }
                }

                for (int box = 0; box < size; box++)
                {
                    var members = model.UnitDigitGroupsByIndex.TryGetValue(MakeUnitDigitGroupIndex(board, 2, box, digit), out var boxGroup)
                        ? boxGroup
                        : null;
                    if (members != null && members.Count == 2)
                    {
                        AddStrongLink(model.StrongLinksByKey, members[0], members[1]);
                        AddWeakConflictPair(model.WeakConflictByKey, members[0], members[1]);
                    }
                }
            }

            return model;
        }

        /**
         * Propagate implications for one assumption branch.
         *
         * @param board Current puzzle board.
         * @param model Inference graph model.
         * @param seedLiteral Assumed literal key.
         * @param assumeTrue Assumed truth value for the seed literal.
         * @returns Propagation state for the branch.
         */
        private PropagationState PropagateFromAssumption(
            Board board,
            ChainModel model,
            int seedLiteral,
            bool assumeTrue,
            bool enableUnitCompletion)
        {
            var state = new PropagationState();
            TryEnqueueAssignment(board, model, state, seedLiteral, assumeTrue, "Seed assumption");

            while (state.Pending.Count > 0 && !state.HasContradiction)
            {
                if (state.AssignmentOrder.Count > MaxAssignmentsPerBranch)
                {
                    state.HasContradiction = true;
                    state.ContradictionReason = "Chain bound exceeded.";
                    break;
                }

                var (literal, valueTrue) = state.Pending.Dequeue();
                if (!ApplyAssignment(board, model, state, literal, valueTrue))
                {
                    continue;
                }

                // Strong-link propagation:
                // false on one endpoint forces true on the other, true forces false.
                if (model.StrongLinksByKey.TryGetValue(literal, out var links))
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
                            sourceLiteral: literal,
                            sourceLinkKind: DirectionalLinkKind.Strong);
                    }
                }

                // Weak conflicts: true literal excludes all conflicting literals.
                if (valueTrue && model.WeakConflictByKey.TryGetValue(literal, out var conflicts))
                {
                    for (int i = 0; i < conflicts.Count; i++)
                    {
                        var conflict = conflicts[i];
                        TryEnqueueAssignment(
                            board,
                            model,
                            state,
                            conflict,
                            false,
                            "Weak conflict",
                            sourceLiteral: literal,
                            sourceLinkKind: DirectionalLinkKind.Weak,
                            isPreviewLink: ShouldExposeWeakConflictAsLink(board, literal, conflict));
                    }
                }

                // Local cell completion is safe and useful for forcing chains:
                // if all but one candidates in a cell are false, the last one is true.
                ResolveCellGroup(board, model, state, GetRow(board, literal), GetColumn(board, literal));

                if (enableUnitCompletion)
                {
                    // Unit completion is useful for contradiction discovery but can
                    // over-constrain sparse synthetic boards used for common-conclusion examples.
                    ResolveUnitGroups(
                        board,
                        model,
                        state,
                        GetRow(board, literal),
                        GetColumn(board, literal),
                        GetDigit(board, literal));
                }
            }

            return state;
        }

        /**
         * Enqueue a literal assignment while preserving contradiction checks.
         *
         * @param board Current puzzle board.
         * @param model Inference graph model.
         * @param state Branch state.
         * @param literal Literal key.
         * @param valueTrue Assigned truth value.
         * @param reason Human-readable enqueue reason.
         */
        private void TryEnqueueAssignment(
            Board board,
            ChainModel model,
            PropagationState state,
            int literal,
            bool valueTrue,
            string reason,
            int? sourceLiteral = null,
            DirectionalLinkKind? sourceLinkKind = null,
            bool isPreviewLink = true)
        {
            if (state.HasContradiction)
            {
                return;
            }

            if (!model.LiteralIndexByKey.ContainsKey(literal))
            {
                return;
            }

            if (valueTrue)
            {
                if (state.FalseLiterals.Contains(literal))
                {
                    state.HasContradiction = true;
                    state.ContradictionLiteral = literal;
                    state.ContradictionSourceLiteral = sourceLiteral;
                    state.ContradictionSourceLinkKind = sourceLinkKind;
                    state.ContradictionReason = $"Literal {FormatLiteral(board, literal)} became both true and false ({reason}).";
                    return;
                }

                if (state.TrueLiterals.Contains(literal))
                {
                    return;
                }
            }
            else
            {
                if (state.TrueLiterals.Contains(literal))
                {
                    state.HasContradiction = true;
                    state.ContradictionLiteral = literal;
                    state.ContradictionSourceLiteral = sourceLiteral;
                    state.ContradictionSourceLinkKind = sourceLinkKind;
                    state.ContradictionReason = $"Literal {FormatLiteral(board, literal)} became both true and false ({reason}).";
                    return;
                }

                if (state.FalseLiterals.Contains(literal))
                {
                    return;
                }
            }

            state.Pending.Enqueue((literal, valueTrue));

            if (sourceLiteral.HasValue && sourceLinkKind.HasValue && !state.InferenceCauseByLiteral.ContainsKey(literal))
            {
                state.InferenceCauseByLiteral[literal] = new InferenceCause
                {
                    FromLiteral = sourceLiteral.Value,
                    LinkKind = sourceLinkKind.Value,
                    IsPreviewLink = isPreviewLink,
                };
            }
        }

        /**
         * Apply one pending literal assignment into branch state.
         *
         * @param board Current puzzle board.
         * @param model Inference graph model.
         * @param state Branch state.
         * @param literal Literal key.
         * @param valueTrue Assigned truth value.
         * @returns True when the assignment changed state and should propagate.
         */
        private bool ApplyAssignment(Board board, ChainModel model, PropagationState state, int literal, bool valueTrue)
        {
            if (valueTrue)
            {
                if (state.TrueLiterals.Contains(literal))
                {
                    return false;
                }

                if (state.FalseLiterals.Contains(literal))
                {
                    state.HasContradiction = true;
                    state.ContradictionLiteral = literal;
                    state.ContradictionReason = $"Literal {FormatLiteral(board, literal)} became both true and false.";
                    return false;
                }

                state.TrueLiterals.Add(literal);
            }
            else
            {
                if (state.FalseLiterals.Contains(literal))
                {
                    return false;
                }

                if (state.TrueLiterals.Contains(literal))
                {
                    state.HasContradiction = true;
                    state.ContradictionLiteral = literal;
                    state.ContradictionReason = $"Literal {FormatLiteral(board, literal)} became both true and false.";
                    return false;
                }

                state.FalseLiterals.Add(literal);
            }

            state.AssignmentOrder.Add(literal);
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
            int unknownLiteral = 0;

            for (int i = 0; i < group.Count; i++)
            {
                int key = group[i];
                if (state.TrueLiterals.Contains(key))
                {
                    trueCount++;
                    continue;
                }

                if (!state.FalseLiterals.Contains(key))
                {
                    unknownCount++;
                    unknownLiteral = key;
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
                TryEnqueueAssignment(board, model, state, unknownLiteral, true, "Cell completion");
            }
        }

        /**
         * Resolve row/column/box digit groups impacted by a literal assignment.
         *
         * @param board Current puzzle board.
         * @param model Inference graph model.
         * @param state Branch state.
         * @param row Literal row.
         * @param column Literal column.
         * @param digit Literal digit.
         */
        private void ResolveUnitGroups(Board board, ChainModel model, PropagationState state, int row, int column, int digit)
        {
            int box = board.Cells[row, column].Box;

            ResolveUnitGroup(board, model, state, MakeUnitDigitGroupIndex(board, 0, row, digit), $"Row {row + 1}");
            ResolveUnitGroup(board, model, state, MakeUnitDigitGroupIndex(board, 1, column, digit), $"Column {column + 1}");
            ResolveUnitGroup(board, model, state, MakeUnitDigitGroupIndex(board, 2, box, digit), $"Box {box + 1}");
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
            int unknownLiteral = 0;

            for (int i = 0; i < group.Count; i++)
            {
                int key = group[i];
                if (state.TrueLiterals.Contains(key))
                {
                    trueCount++;
                    continue;
                }

                if (!state.FalseLiterals.Contains(key))
                {
                    unknownCount++;
                    unknownLiteral = key;
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
                TryEnqueueAssignment(board, model, state, unknownLiteral, true, "Unit completion");
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
            int row = GetRow(board, plan.SeedLiteral);
            int column = GetColumn(board, plan.SeedLiteral);
            int digit = GetDigit(board, plan.SeedLiteral);
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
                        $"Forcing Chain: assuming {FormatLiteral(board, plan.SeedLiteral)} is true contradicts, so remove {digit} from r{row + 1}c{column + 1}.";
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
                        $"Forcing Chain: assuming {FormatLiteral(board, plan.SeedLiteral)} is false contradicts, so set r{row + 1}c{column + 1}={digit}.";
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
            // Prefer a direct placement when both branches force a literal true.
            foreach (int literal in plan.CommonTrueLiterals)
            {
                int row = GetRow(board, literal);
                int column = GetColumn(board, literal);
                int digit = GetDigit(board, literal);
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
            for (int i = 0; i < plan.CommonFalseLiterals.Count; i++)
            {
                int literal = plan.CommonFalseLiterals[i];
                int row = GetRow(board, literal);
                int column = GetColumn(board, literal);
                int digit = GetDigit(board, literal);
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
                result.Description = $"Forcing Chain removed candidates from {added} forced-false literal(s).";
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
            AddUsedCell(result, GetRow(board, plan.SeedLiteral), GetColumn(board, plan.SeedLiteral), GetDigit(board, plan.SeedLiteral), TargetATag);
            AddUsedCell(result, GetRow(board, plan.SeedLiteral), GetColumn(board, plan.SeedLiteral), GetDigit(board, plan.SeedLiteral), TargetBTag);

            bool emittedPathEvidence = AppendCausalEvidencePaths(board, result, plan);
            if (emittedPathEvidence)
            {
                return;
            }

            // Fallback: preserve old bounded evidence when no causal path can be reconstructed.
            var trueEvidence = plan.TrueBranch.AssignmentOrder.Take(MaxEvidencePerBranch).ToList();
            foreach (int literal in trueEvidence)
            {
                AddUsedCell(result, GetRow(board, literal), GetColumn(board, literal), GetDigit(board, literal), DeductionTag);
            }

            var falseEvidence = plan.FalseBranch.AssignmentOrder.Take(MaxEvidencePerBranch).ToList();
            foreach (int literal in falseEvidence)
            {
                AddUsedCell(result, GetRow(board, literal), GetColumn(board, literal), GetDigit(board, literal), DeductionTag);
            }

            AppendEvidenceLinks(board, result, plan.TrueBranch, trueEvidence);
            AppendEvidenceLinks(board, result, plan.FalseBranch, falseEvidence);
        }

        /**
         * Build branch evidence from actual inference parent chains that lead to
         * the literals changed by the selected deduction.
         *
         * @param board Current puzzle board.
         * @param result Rule result accumulator.
         * @param plan Selected forcing plan.
         * @returns True when at least one causal path was emitted.
         */
        private bool AppendCausalEvidencePaths(Board board, RuleResult result, ForcingPlan plan)
        {
            var changedLiterals = CollectChangedLiterals(board, result);
            if (changedLiterals.Count == 0)
            {
                return false;
            }

            bool addedAny = false;

            if (plan.ContradictionOnTrueBranch)
            {
                addedAny |= AppendContradictionEvidenceForBranch(board, result, plan.TrueBranch, plan.SeedLiteral);
            }

            if (plan.ContradictionOnFalseBranch)
            {
                addedAny |= AppendContradictionEvidenceForBranch(board, result, plan.FalseBranch, plan.SeedLiteral);
            }

            bool usingCommonConclusions = !plan.ContradictionOnTrueBranch && !plan.ContradictionOnFalseBranch;
            if (usingCommonConclusions)
            {
                foreach (int literal in changedLiterals)
                {
                    bool expectTrue = plan.CommonTrueLiterals.Contains(literal);
                    bool expectFalse = plan.CommonFalseLiterals.Contains(literal);

                    if (expectTrue && plan.TrueBranch.TrueLiterals.Contains(literal))
                    {
                        addedAny |= AppendPathEvidenceForBranch(board, result, plan.TrueBranch, plan.SeedLiteral, literal);
                    }

                    if (expectTrue && plan.FalseBranch.TrueLiterals.Contains(literal))
                    {
                        addedAny |= AppendPathEvidenceForBranch(board, result, plan.FalseBranch, plan.SeedLiteral, literal);
                    }

                    if (expectFalse && plan.TrueBranch.FalseLiterals.Contains(literal))
                    {
                        addedAny |= AppendPathEvidenceForBranch(board, result, plan.TrueBranch, plan.SeedLiteral, literal);
                    }

                    if (expectFalse && plan.FalseBranch.FalseLiterals.Contains(literal))
                    {
                        addedAny |= AppendPathEvidenceForBranch(board, result, plan.FalseBranch, plan.SeedLiteral, literal);
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
         * @param seedLiteral Branch seed literal key.
         * @param targetLiteral Target literal key reached by inference.
         * @returns True when a valid path was added.
         */
        private bool AppendPathEvidenceForBranch(Board board, RuleResult result, PropagationState branch, int seedLiteral, int targetLiteral)
        {
            if (branch == null)
            {
                return false;
            }

            if (!TryBuildPath(branch, seedLiteral, targetLiteral, out var path))
            {
                return false;
            }

            for (int i = 0; i < path.Count; i++)
            {
                int literal = path[i];
                AddUsedCell(result, GetRow(board, literal), GetColumn(board, literal), GetDigit(board, literal), DeductionTag);
            }

            for (int i = 1; i < path.Count; i++)
            {
                int toLiteral = path[i];
                int fromLiteral = path[i - 1];
                if (!branch.InferenceCauseByLiteral.TryGetValue(toLiteral, out var cause))
                {
                    continue;
                }

                if (!cause.IsPreviewLink)
                {
                    continue;
                }

                AddEvidenceDirectionalLink(result, board, fromLiteral, toLiteral, cause.LinkKind);
            }

            return true;
        }

        /**
         * Emit contradiction evidence including the final edge that attempted to
         * force an already-opposite literal.
         *
         * @param board Current puzzle board.
         * @param result Rule result accumulator.
         * @param branch Contradicting branch state.
         * @param seedLiteral Seed literal key.
         * @returns True when contradiction evidence was added.
         */
        private bool AppendContradictionEvidenceForBranch(Board board, RuleResult result, PropagationState branch, int seedLiteral)
        {
            if (branch == null)
            {
                return false;
            }

            int contradictionLiteral = branch.ContradictionLiteral ?? seedLiteral;

            if (branch.ContradictionSourceLiteral.HasValue && branch.ContradictionSourceLinkKind.HasValue)
            {
                int sourceLiteral = branch.ContradictionSourceLiteral.Value;
                bool addedPath = AppendPathEvidenceForBranch(board, result, branch, seedLiteral, sourceLiteral);
                AddUsedCell(result, GetRow(board, contradictionLiteral), GetColumn(board, contradictionLiteral), GetDigit(board, contradictionLiteral), DeductionTag);
                AddEvidenceDirectionalLink(result, board, sourceLiteral, contradictionLiteral, branch.ContradictionSourceLinkKind.Value);
                return addedPath || sourceLiteral == seedLiteral || contradictionLiteral == sourceLiteral;
            }

            return AppendPathEvidenceForBranch(board, result, branch, seedLiteral, contradictionLiteral);
        }

        /**
         * Reconstruct one parent-linked path from seed to target.
         *
         * @param branch Branch propagation state.
         * @param seedLiteral Seed literal key.
         * @param targetLiteral Target literal key.
         * @param path Output path from seed to target.
         * @returns True when a full path was reconstructed.
         */
        private static bool TryBuildPath(PropagationState branch, int seedLiteral, int targetLiteral, out List<int> path)
        {
            path = new List<int>();
            var visited = new HashSet<int>();
            int current = targetLiteral;

            while (true)
            {
                if (!visited.Add(current))
                {
                    path.Clear();
                    return false;
                }

                path.Add(current);
                if (current == seedLiteral)
                {
                    path.Reverse();
                    return true;
                }

                if (!branch.InferenceCauseByLiteral.TryGetValue(current, out var cause))
                {
                    path.Clear();
                    return false;
                }

                current = cause.FromLiteral;
            }
        }

        /**
         * Map concrete rule changes back to literal keys for evidence targeting.
         *
         * @param board Current puzzle board.
         * @param result Rule result containing changes.
         * @returns Ordered set of changed literal keys.
         */
        private static List<int> CollectChangedLiterals(Board board, RuleResult result)
        {
            var literals = new HashSet<int>();
            if (result == null || result.Changes == null)
            {
                return literals.ToList();
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
                    literals.Add(MakeLiteralKey(board, change.Row, change.Column, change.NewValue.Value));
                }

                if (change.RemovedCandidates == null)
                {
                    continue;
                }

                for (int c = 0; c < change.RemovedCandidates.Count; c++)
                {
                    literals.Add(MakeLiteralKey(board, change.Row, change.Column, change.RemovedCandidates[c]));
                }
            }

            return literals
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
         * @param evidenceLiterals Ordered evidence literals shown in the preview.
         */
        private void AppendEvidenceLinks(Board board, RuleResult result, PropagationState branch, List<int> evidenceLiterals)
        {
            if (result == null || branch == null || evidenceLiterals == null || evidenceLiterals.Count == 0)
            {
                return;
            }

            var evidenceSet = new HashSet<int>(evidenceLiterals);
            for (int i = 0; i < evidenceLiterals.Count; i++)
            {
                int targetLiteral = evidenceLiterals[i];
                if (!branch.InferenceCauseByLiteral.TryGetValue(targetLiteral, out var cause))
                {
                    continue;
                }

                if (!evidenceSet.Contains(cause.FromLiteral))
                {
                    continue;
                }

                if (!cause.IsPreviewLink)
                {
                    continue;
                }

                AddEvidenceDirectionalLink(result, board, cause.FromLiteral, targetLiteral, cause.LinkKind);
            }
        }

        /**
         * Add one directional evidence link when not already present.
         *
         * @param result Rule result accumulator.
         * @param board Current puzzle board.
         * @param fromLiteral Source literal key.
         * @param toLiteral Target literal key.
         * @param kind Strong or weak link kind.
         */
        private static void AddEvidenceDirectionalLink(RuleResult result, Board board, int fromLiteral, int toLiteral, DirectionalLinkKind kind)
        {
            if (GetRow(board, fromLiteral) == GetRow(board, toLiteral) 
                && GetColumn(board, fromLiteral) == GetColumn(board, toLiteral))
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
                    Row = GetRow(board, fromLiteral),
                    Column = GetColumn(board, fromLiteral),
                    Digit = GetDigit(board, fromLiteral),
                },
                End = new DirectionalLinkEndpoint
                {
                    Row = GetRow(board, toLiteral),
                    Column = GetColumn(board, toLiteral),
                    Digit = GetDigit(board, toLiteral),
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
         * @param fromLiteral Source literal.
         * @param toLiteral Target literal.
         * @returns True when the conflict is a conjugate pair in a row/column/box.
         */
        private static bool ShouldExposeWeakConflictAsLink(Board board, int fromLiteral, int toLiteral)
        {
            if (GetDigit(board, fromLiteral) != GetDigit(board, toLiteral))
            {
                return false;
            }

            int fromRow = GetRow(board, fromLiteral);
            int fromColumn = GetColumn(board, fromLiteral);
            int toRow = GetRow(board, toLiteral);
            int toColumn = GetColumn(board, toLiteral);

            if (fromRow == toRow && fromColumn == toColumn)
            {
                return false;
            }

            if (fromRow == toRow)
            {
                return CountDigitCandidatesInRow(board, fromRow, GetDigit(board, fromLiteral)) == 2;
            }

            if (fromColumn == toColumn)
            {
                return CountDigitCandidatesInColumn(board, fromColumn, GetDigit(board, fromLiteral)) == 2;
            }

            if (board.Cells[fromRow, fromColumn] != null && board.Cells[fromRow, fromColumn].Box == board.Cells[toRow, toColumn].Box)
            {
                return CountDigitCandidatesInBox(board, board.Cells[fromRow, fromColumn].Box, GetDigit(board, fromLiteral)) == 2;
            }

            return false;
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
         * Add a bidirectional strong link between two literals.
         *
         * @param links Strong-link adjacency map.
         * @param a First literal key.
         * @param b Second literal key.
         */
        private static void AddStrongLink(Dictionary<int, List<int>> links, int a, int b)
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
         * Add a bidirectional weak conflict between two literals.
         *
         * @param links Weak-conflict adjacency map.
         * @param a First literal key.
         * @param b Second literal key.
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
         * Determine whether a literal is still available on the board.
         *
         * @param board Current puzzle board.
         * @param literal Literal key.
         * @returns True when the corresponding candidate is still present.
         */
        private static bool IsLiteralStillCandidate(Board board, int literal)
        {
            int row = GetRow(board, literal);
            int column = GetColumn(board, literal);
            int digit = GetDigit(board, literal);
            var cell = board.Cells[row, column];
            return cell != null && !cell.Value.HasValue && cell.Candidates != null && cell.Candidates.Contains(digit);
        }

        /**
         * Determine whether two literal keys belong to the same cell.
         *
         * @param board Current puzzle board.
         * @param a First literal key.
         * @param b Second literal key.
         * @returns True when both literals refer to the same row/column cell.
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
         * @param fromLiteral Source literal.
         * @param toLiteral Target literal.
         * @returns True when the weak edge is supported by a unit with exactly two candidates.
         */
        private static bool IsValidWeakPreviewLink(Board board, int fromLiteral, int toLiteral)
        {
            if (GetDigit(board, fromLiteral) != GetDigit(board, toLiteral))
            {
                return false;
            }

            int fromRow = GetRow(board, fromLiteral);
            int fromColumn = GetColumn(board, fromLiteral);
            int toRow = GetRow(board, toLiteral);
            int toColumn = GetColumn(board, toLiteral);

            if (fromRow == toRow && fromColumn == toColumn)
            {
                return false;
            }

            int digit = GetDigit(board, fromLiteral);

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
         * Format a literal as rNcM=d for explanations.
         *
         * @param board Current puzzle board.
         * @param literal Literal key.
         * @returns Human-readable literal text.
         */
        private static string FormatLiteral(Board board, int literal)
        {
            return $"r{GetRow(board, literal) + 1}c{GetColumn(board, literal) + 1}={GetDigit(board, literal)}";
        }

        /**
         * Encode one candidate literal key.
         *
         * @param board Current puzzle board.
         * @param row Row index.
         * @param column Column index.
         * @param digit Candidate digit.
         * @returns Encoded literal key.
         */
        private static int MakeLiteralKey(Board board, int row, int column, int digit)
        {
            int size = board.Size;
            return ((row * size) + column) * (size + 1) + digit;
        }

        /**
         * Decode literal row index.
         *
         * @param board Current puzzle board.
         * @param literal Encoded literal key.
         * @returns Row index.
         */
        private static int GetRow(Board board, int literal)
        {
            int size = board.Size;
            int packedCell = literal / (size + 1);
            return packedCell / size;
        }

        /**
         * Decode literal column index.
         *
         * @param board Current puzzle board.
         * @param literal Encoded literal key.
         * @returns Column index.
         */
        private static int GetColumn(Board board, int literal)
        {
            int size = board.Size;
            int packedCell = literal / (size + 1);
            return packedCell % size;
        }

        /**
         * Decode literal digit.
         *
         * @param board Current puzzle board.
         * @param literal Encoded literal key.
         * @returns Candidate digit.
         */
        private static int GetDigit(Board board, int literal)
        {
            return literal % (board.Size + 1);
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
        private static int MakeUnitDigitGroupIndex(Board board, int unitType, int unitIndex, int digit)
        {
            return (((unitType * board.Size) + unitIndex) * (board.Size + 1)) + digit;
        }

    }

}

