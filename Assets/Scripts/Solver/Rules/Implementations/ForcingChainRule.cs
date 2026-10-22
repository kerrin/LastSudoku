using System;
using System.Collections.Generic;
using System.Linq;
using Sudoku.Models;
using Sudoku.UI.Config;
using Board = Sudoku.Models.Board;
using ChainModel = Sudoku.Solver.Rules.ForcingChainLinkModel;

namespace Sudoku.Solver.Rules
{
    /**
     * Forcing Chains (AIC family) derive conclusions by following
     * strong and weak inferences.
     *
     * Strong links are built from:
     * - Bi-value cells (exactly two candidates in one cell).
     * - Conjugate pairs (exactly two candidates for a single digit in one row/column/box).
     * 
     * Weak links are built from:
     * - Two or more digits in the same cell or unit (row/column/box), where only one can be true. 
     *      e.g. candidates in a cell, as only one can be the value.
     *      or all candidates for the same digit in a unit (row/column/box), as only one can be true.
     *
     * Chains can start from all candidates in a single cell, or from a candidate
     * that has same-digit peers in a row, column, or box.
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
        private const string SeedTag = "Seed";
        private const string DeductionTag = "Deduction";
        private const string FailureTag = "Failure";
        private const int MaxEvidencePerBranch = 100;

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
            return CalculateChangesInternal(board, null, true);
        }

        /**
         * Search for one requested conclusion without changing the public path's normal scoring.
         */
        internal RuleResult CalculateChangesForCandidate(Board board, int row, int column, int digit, bool valueTrue = true)
        {
            if (board == null)
            {
                return CalculateChangesInternal(null, null, valueTrue);
            }

            if (row < 0 || row >= board.Size)
            {
                throw new ArgumentOutOfRangeException(nameof(row));
            }

            if (column < 0 || column >= board.Size)
            {
                throw new ArgumentOutOfRangeException(nameof(column));
            }

            if (digit < 1 || digit > board.Size)
            {
                throw new ArgumentOutOfRangeException(nameof(digit));
            }

            return CalculateChangesInternal(
                board,
                MakeCandidateKey(board, row, column, digit),
                valueTrue);
        }

        private RuleResult CalculateChangesInternal(Board board, int? preferredCandidate, bool preferredValue)
        {
            var result = new RuleResult();

            if (board == null || !board.IsValid() || board.Size <= 0 || ColourSettings.GetEnabledColourCount() < 2)
            {
                result.Apply = false;
                return result;
            }

            var plan = FindPlan(board, preferredCandidate, preferredValue);
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
                ApplyCommonConclusionPlan(board, result, plan, preferredCandidate, preferredValue);
            }

            AppendEvidence(board, result, plan);

            // A valid forcing-chain deduction must still render its evidence even when
            // the final result is presented as a preview-only state (for example, when
            // the branch resolves to a contradiction chain without creating a concrete
            // board mutation yet). The board preview depends on Apply + UsedCells,
            // not only on whether a CellChange list was created.
            result.Apply = result.Changes.Count > 0 || result.UsedCells.Count > 0;

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
         * Search for the best scoring plan, or the best-scoring plan supporting a requested conclusion.
         *
         * @param board Current puzzle board.
         * @param preferredCandidate Optional candidate that the returned plan must conclude.
         * @param preferredValue Whether the requested candidate is forced true or false.
         * @returns A forcing plan or null when no matching deduction exists.
         */
        private ForcingPlan FindPlan(Board board, int? preferredCandidate = null, bool preferredValue = true)
        {
            var model = ForcingChainLinkModel.Build(board);
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
            ForcingPlan preferredPlan = null;
            int preferredScore = int.MaxValue;

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

                var commonConclusionPlans = new List<ForcingPlan>();
                foreach (int digit in cell.Candidates.OrderBy(v => v))
                {
                    int seed = MakeCandidateKey(board, cell.Row, cell.Column, digit);
                    if (!model.CandidateIndexByKey.ContainsKey(seed))
                    {
                        continue;
                    }

                    var plan = EvaluateSeedCandidate(board, model, seed);
                    if (plan != null && IsPlanWithinPreviewEvidenceLimit(plan))
                    {
                        if (plan.ContradictionOnTrueBranch || plan.ContradictionOnFalseBranch)
                        {
                            // A contradiction is evidence about this seed only. Never merge
                            // contradiction flags from different candidate assumptions.
                            int contradictionScore = ScorePlan(board, digitCounts, plan);
                            ConsiderPreferredPlan(
                                board,
                                plan,
                                preferredCandidate,
                                preferredValue,
                                contradictionScore,
                                ref preferredPlan,
                                ref preferredScore);
                            if (contradictionScore < bestScore)
                            {
                                bestScore = contradictionScore;
                                bestPlan = plan;
                            }
                        }
                        else
                        {
                            commonConclusionPlans.Add(plan);
                        }
                    }
                }
                var combinedPlan = ForcingChainPlanCombiner.Combine(commonConclusionPlans);
                var score = combinedPlan == null ? int.MaxValue : ScorePlan(board, digitCounts, combinedPlan);
                ConsiderPreferredPlan(
                    board,
                    combinedPlan,
                    preferredCandidate,
                    preferredValue,
                    score,
                    ref preferredPlan,
                    ref preferredScore);

                // Check if the score is better than the best score so far, and if so, store it as the best plan.
                if (score < bestScore)
                {
                    bestScore = score;
                    bestPlan = combinedPlan;
                }
            }

            var unitPlan = FindUnitPlan(
                board,
                model,
                digitCounts,
                orderedCandidates,
                preferredCandidate,
                preferredValue);
            var unitPlanScore = unitPlan == null ? int.MaxValue : ScorePlan(board, digitCounts, unitPlan);
            ConsiderPreferredPlan(
                board,
                unitPlan,
                preferredCandidate,
                preferredValue,
                unitPlanScore,
                ref preferredPlan,
                ref preferredScore);
            if (unitPlanScore < bestScore)
            {
                bestScore = unitPlanScore;
                bestPlan = unitPlan;
            }

            var verityPlan = FindUnitVerityPlan(board, model, digitCounts, preferredCandidate, preferredValue);
            var verityPlanScore = verityPlan == null ? int.MaxValue : ScorePlan(board, digitCounts, verityPlan);
            ConsiderPreferredPlan(
                board,
                verityPlan,
                preferredCandidate,
                preferredValue,
                verityPlanScore,
                ref preferredPlan,
                ref preferredScore);
            if (verityPlanScore < bestScore)
            {
                bestPlan = verityPlan;
            }

            return preferredCandidate.HasValue ? preferredPlan : bestPlan;
        }

        private static void ConsiderPreferredPlan(
            Board board,
            ForcingPlan plan,
            int? preferredCandidate,
            bool preferredValue,
            int score,
            ref ForcingPlan preferredPlan,
            ref int preferredScore)
        {
            if (!preferredCandidate.HasValue
                || !PlanSupportsCandidate(board, plan, preferredCandidate.Value, preferredValue)
                || score >= preferredScore)
            {
                return;
            }

            preferredPlan = plan;
            preferredScore = score;
        }

        private static bool PlanSupportsCandidate(Board board, ForcingPlan plan, int candidate, bool valueTrue)
        {
            if (plan == null)
            {
                return false;
            }

            if (plan.ContradictionOnTrueBranch)
            {
                return !valueTrue && plan.SeedCandidate == candidate;
            }

            if (plan.ContradictionOnFalseBranch)
            {
                return valueTrue && plan.SeedCandidate == candidate;
            }

            var conclusions = valueTrue ? plan.CommonTrueCandidates : plan.CommonFalseCandidates;
            return conclusions.Contains(candidate)
                && IsCandidateStillCandidate(board, candidate);
        }

        /**
         * Search unit candidate groups where every possible placement forces the same candidate true.
         *
         * @param board Current puzzle board.
         * @param model Candidate inference graph.
         * @returns A verity plan, or null when no shared conclusion exists.
         */
        private ForcingPlan FindUnitVerityPlan(
            Board board,
            ChainModel model,
            IReadOnlyDictionary<int, int> digitCounts,
            int? preferredCandidate = null,
            bool preferredValue = true)
        {
            ForcingPlan bestPlan = null;
            int bestScore = int.MaxValue;
            ForcingPlan preferredPlan = null;
            int preferredScore = int.MaxValue;

            foreach (var group in model.UnitDigitGroupsByIndex.Values)
            {
                if (group == null || group.Count < 2)
                {
                    continue;
                }

                var branches = new List<ForcingChainBranch>(group.Count);
                var viableBranches = new List<ForcingChainBranch>(group.Count);
                HashSet<int> commonTrueCandidates = null;

                var generatedBranches = ForcingChainBranchGenerator.Generate(
                    board,
                    model,
                    group,
                    assumeTrue: true,
                    enableUnitCompletion: true);
                foreach (var branch in generatedBranches)
                {
                    if (CountPreviewEvidence(branch.State) > MaxEvidencePerBranch)
                    {
                        branches.Clear();
                        viableBranches.Clear();
                        commonTrueCandidates = null;
                        break;
                    }

                    branches.Add(branch);
                    if (branch.State.HasContradiction)
                    {
                        continue;
                    }

                    viableBranches.Add(branch);

                    if (commonTrueCandidates == null)
                    {
                        commonTrueCandidates = new HashSet<int>(branch.State.TrueCandidates);
                    }
                    else
                    {
                        commonTrueCandidates.IntersectWith(branch.State.TrueCandidates);
                    }
                }

                if (branches.Count != group.Count || viableBranches.Count == 0 || commonTrueCandidates == null)
                {
                    continue;
                }

                commonTrueCandidates.RemoveWhere(candidate => !IsCandidateStillCandidate(board, candidate));

                if (commonTrueCandidates.Count == 0)
                {
                    continue;
                }

                var firstBranch = branches[0];
                var secondBranch = branches.Count > 1 ? branches[1] : firstBranch;
                var plan = new ForcingPlan
                {
                    SeedCandidate = firstBranch.SeedCandidate,
                    TrueBranch = firstBranch.State,
                    FalseBranch = secondBranch.State,
                    CommonTrueCandidates = commonTrueCandidates
                        // Prefer the earliest common inference so downstream placements
                        // do not hide the actual verity conclusion.
                        .OrderBy(candidate => viableBranches.Max(branch =>
                        {
                            int index = branch.State.AssignmentOrder.IndexOf(candidate);
                            return index < 0 ? int.MaxValue : index;
                        }))
                        .ThenBy(candidate => GetRow(board, candidate))
                        .ThenBy(candidate => GetColumn(board, candidate))
                        .ThenBy(candidate => GetDigit(board, candidate))
                        .ToList(),
                    VerityBranches = branches
                };

                int score = ScorePlan(board, digitCounts, plan);
                ConsiderPreferredPlan(
                    board,
                    plan,
                    preferredCandidate,
                    preferredValue,
                    score,
                    ref preferredPlan,
                    ref preferredScore);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestPlan = plan;
                }
            }

            return preferredCandidate.HasValue ? preferredPlan : bestPlan;
        }

        /**
         * Search same-digit unit peers using propagation without unit completion.
         *
         * @param board Current puzzle board.
         * @param model Chain model containing candidate links.
         * @param digitCounts Global candidate count per digit.
         * @param orderedCandidates Candidate seeds ordered by digit rarity and position.
         * @returns The best unit-seeded plan, or null when none is available.
         */
        private ForcingPlan FindUnitPlan(
            Board board,
            ChainModel model,
            IReadOnlyDictionary<int, int> digitCounts,
            IReadOnlyList<int> orderedCandidates,
            int? preferredCandidate = null,
            bool preferredValue = true)
        {
            ForcingPlan bestPlan = null;
            int bestScore = int.MaxValue;
            ForcingPlan preferredPlan = null;
            int preferredScore = int.MaxValue;

            foreach (int seed in orderedCandidates)
            {
                if (!HasSameDigitUnitPeer(board, seed))
                {
                    continue;
                }

                var plan = EvaluateUnitSeedCandidate(board, model, seed);
                if (!IsPlanWithinPreviewEvidenceLimit(plan))
                {
                    continue;
                }

                int score = ScorePlan(board, digitCounts, plan);
                ConsiderPreferredPlan(
                    board,
                    plan,
                    preferredCandidate,
                    preferredValue,
                    score,
                    ref preferredPlan,
                    ref preferredScore);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestPlan = plan;
                }
            }

            return preferredCandidate.HasValue ? preferredPlan : bestPlan;
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
            var trueBranch = ForcingChainBranchGenerator.Generate(
                board, model, new[] { seedCandidate }, assumeTrue: true, enableUnitCompletion: true)[0].State;
            var falseBranch = ForcingChainBranchGenerator.Generate(
                board, model, new[] { seedCandidate }, assumeTrue: false, enableUnitCompletion: true)[0].State;

            if (trueBranch.HasContradiction && falseBranch.HasContradiction)
            {
                return null;
            }

            if (trueBranch.HasContradiction || falseBranch.HasContradiction)
            {
                var contradictionBranch = trueBranch.HasContradiction ? trueBranch : falseBranch;
                if (!IsMeaningfulContradictionBranch(contradictionBranch, seedCandidate))
                {
                    return null;
                }

                return new ForcingPlan
                {
                    SeedCandidate = seedCandidate,
                    TrueBranch = trueBranch,
                    FalseBranch = falseBranch,
                    ContradictionOnTrueBranch = trueBranch.HasContradiction,
                    ContradictionOnFalseBranch = falseBranch.HasContradiction
                };
            }

            // Find common conclusions between the two non-contradictory branches.
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
                CommonTrueCandidates = commonTrue
            };
        }

        /**
         * Evaluate a candidate with the unit-seeded chain rules.
         *
         * @param board Current puzzle board.
         * @param model Chain model containing candidate links.
         * @param seedCandidate Candidate key to evaluate.
         * @returns A valid unit-seeded plan, or null when it yields no useful deduction.
         */
        private ForcingPlan EvaluateUnitSeedCandidate(Board board, ChainModel model, int seedCandidate)
        {
            var trueBranch = ForcingChainBranchGenerator.Generate(
                board, model, new[] { seedCandidate }, assumeTrue: true, enableUnitCompletion: false)[0].State;
            var falseBranch = ForcingChainBranchGenerator.Generate(
                board, model, new[] { seedCandidate }, assumeTrue: false, enableUnitCompletion: false)[0].State;

            if (trueBranch.HasContradiction && falseBranch.HasContradiction)
            {
                return null;
            }

            var plan = new ForcingPlan
            {
                SeedCandidate = seedCandidate,
                TrueBranch = trueBranch,
                FalseBranch = falseBranch,
                ContradictionOnTrueBranch = trueBranch.HasContradiction,
                ContradictionOnFalseBranch = falseBranch.HasContradiction
            };

            plan.CommonFalseCandidates = trueBranch.FalseCandidates
                .Intersect(falseBranch.FalseCandidates)
                .Where(candidate => candidate != seedCandidate)
                .Where(candidate => !IsSameCell(board, candidate, seedCandidate))
                .Where(candidate => IsCandidateStillCandidate(board, candidate))
                .OrderBy(candidate => GetRow(board, candidate))
                .ThenBy(candidate => GetColumn(board, candidate))
                .ThenBy(candidate => GetDigit(board, candidate))
                .ToList();

            plan.CommonTrueCandidates = trueBranch.TrueCandidates
                .Intersect(falseBranch.TrueCandidates)
                .Where(candidate => !IsSameCell(board, candidate, seedCandidate))
                .Where(candidate => IsCandidateStillCandidate(board, candidate))
                .OrderBy(candidate => GetRow(board, candidate))
                .ThenBy(candidate => GetColumn(board, candidate))
                .ThenBy(candidate => GetDigit(board, candidate))
                .ToList();

            bool hasCommonConclusion =
                plan.CommonFalseCandidates.Count > 0 || plan.CommonTrueCandidates.Count > 0;
            bool hasMeaningfulContradiction =
                (plan.ContradictionOnTrueBranch &&
                 IsMeaningfulContradictionBranch(trueBranch, seedCandidate)) ||
                (plan.ContradictionOnFalseBranch &&
                 IsMeaningfulContradictionBranch(falseBranch, seedCandidate));

            return hasCommonConclusion || hasMeaningfulContradiction ? plan : null;
        }

        private static bool HasSameDigitUnitPeer(Board board, int seedCandidate)
        {
            var cell = board.Cells[GetRow(board, seedCandidate), GetColumn(board, seedCandidate)];
            int digit = GetDigit(board, seedCandidate);

            return HasSameDigitPeer(board.GetRow(cell.Row), cell, digit)
                || HasSameDigitPeer(board.GetColumn(cell.Column), cell, digit)
                || HasSameDigitPeer(board.GetBox(cell.Box), cell, digit);
        }

        private static bool HasSameDigitPeer(IEnumerable<Cell> unit, Cell seedCell, int digit)
        {
            return unit.Any(cell =>
                cell != null
                && !ReferenceEquals(cell, seedCell)
                && !cell.Value.HasValue
                && cell.Candidates != null
                && cell.Candidates.Contains(digit));
        }

        /**
         * Combine the plans and check for common conclusions, and score them to find the best one.
         * 
         * @param plans List of forcing plans for candidates in the same cell.
         * @returns Combined forcing plan.
         */
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
         * Score plans so unit verity conclusions precede seed-specific deductions.
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
            int deductionCount = plan.CommonTrueCandidates.Count > 0 ? plan.CommonTrueCandidates.Count : plan.CommonFalseCandidates.Count;
            int trueBranchSize = CountPreviewEvidence(plan.TrueBranch);
            int falseBranchSize = CountPreviewEvidence(plan.FalseBranch);
            int branchSize = plan.VerityBranches != null && plan.VerityBranches.Count > 0
                ? plan.VerityBranches.Sum(branch => CountPreviewEvidence(branch.State))
                : Math.Min(trueBranchSize, falseBranchSize);

            int typeScore = 0;
            if (plan.ContradictionOnTrueBranch || plan.ContradictionOnFalseBranch || !isCommonConclusion)
            {
                typeScore = 2000;
            }
            else if (plan.VerityBranches == null || plan.VerityBranches.Count == 0)
            {
                typeScore = 2;
            }

            return (typeScore * 1000000)
                + (seedCount * 10000)
                + (branchSize * 100)
                + deductionCount;
        }

        /**
         * Determine whether the plan's preview evidence fits within the allowed
         * per-branch display cap. This is used during plan selection so the solver
         * can discard oversized previews and continue testing other viable plans.
         *
         * @param plan Candidate forcing plan.
         * @returns True when both branches stay within the preview cap.
         */
        private static bool IsPlanWithinPreviewEvidenceLimit(ForcingPlan plan)
        {
            if (plan == null)
            {
                return false;
            }

            if (plan.VerityBranches != null && plan.VerityBranches.Any(
                    branch => branch == null || CountPreviewEvidence(branch.State) > MaxEvidencePerBranch))
            {
                return false;
            }

            return CountPreviewEvidence(plan.TrueBranch) <= MaxEvidencePerBranch
                && CountPreviewEvidence(plan.FalseBranch) <= MaxEvidencePerBranch;
        }

        /**
         * Count the unique preview deductions exposed by one branch.
         *
         * @param branch Branch propagation state.
         * @returns Number of unique candidate assignments in the branch preview.
         */
        private static int CountPreviewEvidence(PropagationState branch)
        {
            if (branch == null || branch.AssignmentOrder == null)
            {
                return 0;
            }

            return branch.AssignmentOrder.Distinct().Count();
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
        private void ApplyCommonConclusionPlan(
            Board board,
            RuleResult result,
            ForcingPlan plan,
            int? preferredCandidate = null,
            bool preferredValue = true)
        {
            // Prefer a direct placement when both branches force a candidate true.
            var trueCandidates = preferredCandidate.HasValue
                ? (preferredValue
                    ? plan.CommonTrueCandidates.Where(candidate => candidate == preferredCandidate.Value)
                    : Enumerable.Empty<int>())
                : plan.CommonTrueCandidates;
            foreach (int candidate in trueCandidates)
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
                    BuildCommonConclusionDescription(
                        board,
                        plan,
                        row,
                        column,
                        digit);
                return;
            }

            // Otherwise remove candidates forced false in both branches.
            int added = 0;
            var falseCandidates = preferredCandidate.HasValue
                ? (!preferredValue
                    ? plan.CommonFalseCandidates.Where(candidate => candidate == preferredCandidate.Value)
                    : Enumerable.Empty<int>())
                : plan.CommonFalseCandidates;
            foreach (int candidate in falseCandidates)
            {
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

        private static string BuildCommonConclusionDescription(
            Board board,
            ForcingPlan plan,
            int row,
            int column,
            int digit)
        {
            var contradictionBranch = plan.VerityBranches?.FirstOrDefault(branch =>
                branch?.State?.HasContradiction == true);
            if (contradictionBranch == null)
            {
                return $"Forcing Chain: both branches force r{row + 1}c{column + 1}={digit}.";
            }

            return
                $"Forcing Chain: assuming {FormatCandidate(board, contradictionBranch.SeedCandidate)} leads to a contradiction " +
                $"({contradictionBranch.State.ContradictionReason}); the remaining branches force r{row + 1}c{column + 1}={digit}.";
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
            // The seed assumption is the start of the chain, not both ends of it.
            // Reconstructed branch paths already assign the alternating TargetA / TargetB
            // tags along the causal sequence; adding both tags to the same seed candidate
            // creates a conflicting preview and prevents the branch from displaying correctly.
            bool emittedPathEvidence = AppendCausalEvidencePaths(board, result, plan);
            if (!emittedPathEvidence)
            {
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

            foreach (int candidate in CollectChangedCandidates(board, result))
            {
                AddUsedCell(
                    result,
                    GetRow(board, candidate),
                    GetColumn(board, candidate),
                    GetDigit(board, candidate),
                    DeductionTag);
            }
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
            bool addedAny = false;

            if (plan.VerityBranches != null && plan.VerityBranches.Count > 0)
            {
                foreach (var branch in plan.VerityBranches)
                {
                    foreach (int candidate in changedCandidates)
                    {
                        if (plan.CommonTrueCandidates.Contains(candidate)
                            && branch.State.TrueCandidates.Contains(candidate))
                        {
                            addedAny |= AppendPathEvidenceForBranch(
                                board,
                                result,
                                branch.State,
                                branch.SeedCandidate,
                                candidate);
                        }
                    }

                    if (branch.State.HasContradiction)
                    {
                        addedAny |= AppendContradictionEvidenceForBranch(
                            board,
                            result,
                            branch.State,
                            branch.SeedCandidate);
                    }
                }

                return addedAny;
            }

            if (changedCandidates.Count == 0)
            {
                var fallbackEvidence = new List<int>();
                if (plan.TrueBranch != null)
                {
                    fallbackEvidence.AddRange(plan.TrueBranch.AssignmentOrder);
                }
                if (plan.FalseBranch != null)
                {
                    fallbackEvidence.AddRange(plan.FalseBranch.AssignmentOrder);
                }

                fallbackEvidence = fallbackEvidence
                    .Distinct()
                    .Take(MaxEvidencePerBranch)
                    .ToList();

                if (fallbackEvidence.Count > 0)
                {
                    for (int i = 0; i < fallbackEvidence.Count; i++)
                    {
                        int candidate = fallbackEvidence[i];
                        string tag = DeterminePathHighlightTag(fallbackEvidence, i);
                        AddUsedCell(result, GetRow(board, candidate), GetColumn(board, candidate), GetDigit(board, candidate), tag);
                    }

                    for (int i = 1; i < fallbackEvidence.Count; i++)
                    {
                        int toCandidate = fallbackEvidence[i];
                        int fromCandidate = fallbackEvidence[i - 1];
                        var causes = TryGetCauses(plan.TrueBranch, toCandidate) ?? TryGetCauses(plan.FalseBranch, toCandidate);
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

                            if (cause.FromCandidate == fromCandidate)
                            {
                                AddEvidenceDirectionalLink(result, board, fromCandidate, toCandidate, cause.LinkKind);
                            }
                        }
                    }

                    return true;
                }

                return false;
            }

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
                string tag = DeterminePathHighlightTag(path, i);
                AddUsedCell(result, GetRow(board, candidate), GetColumn(board, candidate), GetDigit(board, candidate), tag);
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
         * Determine the alternating chain colour tag for a candidate on the reconstructed path.
         *
         * @param path Ordered candidate path from seed to target.
         * @param index Candidate index in the path.
         * @returns TargetA / TargetB for path nodes after the seed, or Deduction for fallback-only cases.
         */
        private static string DeterminePathHighlightTag(List<int> path, int index)
        {
            if (path == null || path.Count == 0)
            {
                return DeductionTag;
            }

            if (index == 0)
            {
                return SeedTag;
            }

            return (index % 2 == 1) ? TargetBTag : TargetATag;
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

            bool addedGroupEvidence = false;
            if (branch.ContradictionGroupCandidates != null && branch.ContradictionGroupCandidates.Count > 1)
            {
                foreach (int candidate in branch.ContradictionGroupCandidates)
                {
                    addedGroupEvidence |= AppendPathEvidenceForBranch(
                        board,
                        result,
                        branch,
                        seedCandidate,
                        candidate);
                    AddUsedCell(
                        result,
                        GetRow(board, candidate),
                        GetColumn(board, candidate),
                        GetDigit(board, candidate),
                        FailureTag);
                }
            }

            if (addedGroupEvidence)
            {
                return true;
            }

            int contradictionCandidate = branch.ContradictionCandidate ?? seedCandidate;

            if (branch.ContradictionSourceCandidate.HasValue && branch.ContradictionSourceLinkKind.HasValue)
            {
                int sourceCandidate = branch.ContradictionSourceCandidate.Value;
                bool addedPath = AppendPathEvidenceForBranch(board, result, branch, seedCandidate, sourceCandidate);
                if (!addedPath)
                {
                    AddUsedCell(result, GetRow(board, seedCandidate), GetColumn(board, seedCandidate), GetDigit(board, seedCandidate), SeedTag);
                    AddUsedCell(result, GetRow(board, sourceCandidate), GetColumn(board, sourceCandidate), GetDigit(board, sourceCandidate), TargetBTag);
                }
                AddUsedCell(result, GetRow(board, contradictionCandidate), GetColumn(board, contradictionCandidate), GetDigit(board, contradictionCandidate), DeductionTag);
                AddEvidenceDirectionalLink(result, board, sourceCandidate, contradictionCandidate, branch.ContradictionSourceLinkKind.Value);
                return addedPath || sourceCandidate == seedCandidate || contradictionCandidate == sourceCandidate;
            }

            bool pathAdded = AppendPathEvidenceForBranch(board, result, branch, seedCandidate, contradictionCandidate);
            if (!pathAdded)
            {
                AddUsedCell(result, GetRow(board, seedCandidate), GetColumn(board, seedCandidate), GetDigit(board, seedCandidate), SeedTag);
                AddUsedCell(result, GetRow(board, contradictionCandidate), GetColumn(board, contradictionCandidate), GetDigit(board, contradictionCandidate), TargetBTag);
            }
            return pathAdded;
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
            if (current == seedCandidate)
            {
                finalPath.Add(current);
                finalPath.Reverse();
                return true;
            }

            if (!visited.Add(current))
            {
                return false;
            }

            finalPath.Add(current);
            var causes = TryGetCauses(branch, current);
            if (causes == null)
            {
                if (finalPath.Count > 0 && finalPath[finalPath.Count - 1] == current)
                {
                    finalPath.RemoveAt(finalPath.Count - 1);
                }
                visited.Remove(current);
                return false;
            }

            foreach (var cause in causes)
            {
                if (InnerTryBuildPath(visited, branch, seedCandidate, cause.FromCandidate, finalPath, out finalPath))
                {
                    return true;
                }
            }

            if (finalPath.Count > 0 && finalPath[finalPath.Count - 1] == current)
            {
                finalPath.RemoveAt(finalPath.Count - 1);
            }
            visited.Remove(current);
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
         * @returns True for a same-digit conflict between distinct peer cells.
         */


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
            if (string.IsNullOrWhiteSpace(tag))
            {
                return;
            }

            var existing = result.UsedCells.FirstOrDefault(u => u.Row == row && u.Column == column && u.Candidate == digit);
            if (existing == null)
            {
                result.UsedCells.Add(new UsedCell
                {
                    Row = row,
                    Column = column,
                    Candidate = digit,
                    HighlightTag = tag
                });
                return;
            }

            if (string.Equals(existing.HighlightTag, tag, StringComparison.Ordinal))
            {
                return;
            }

            int existingPriority = GetHighlightTagPriority(existing.HighlightTag);
            int newPriority = GetHighlightTagPriority(tag);
            if (newPriority > existingPriority)
            {
                existing.HighlightTag = tag;
            }
        }

        /**
         * Higher values indicate a more important visualization tag when the same
         * cell/candidate would otherwise be registered with conflicting chain colours.
         */
        private static int GetHighlightTagPriority(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                return 0;
            }

            if (string.Equals(tag, SeedTag, StringComparison.OrdinalIgnoreCase))
            {
                return 4;
            }

            if (string.Equals(tag, DeductionTag, StringComparison.OrdinalIgnoreCase))
            {
                return 5;
            }

            if (string.Equals(tag, FailureTag, StringComparison.OrdinalIgnoreCase))
            {
                return 6;
            }

            if (string.Equals(tag, TargetATag, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tag, TargetBTag, StringComparison.OrdinalIgnoreCase))
            {
                return 3;
            }

            return 1;
        }

        /**
         * Add a bidirectional link between two candidates.
         *
         * @param links Link adjacency map.
         * @param a First candidate key.
         * @param b Second candidate key.
         */


        /**
         * Add weak conflicts between every pair of candidates in one cell or unit-digit group.
         */


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
         * A contradiction-only branch counts as a real forcing chain only when it
         * actually propagates beyond the seed candidate. A branch that contradicts
         * on the initial seed without any additional inference is a false positive.
         */
        private static bool IsMeaningfulContradictionBranch(PropagationState branch, int seedCandidate)
        {
            if (branch == null || branch.AssignmentOrder == null || branch.AssignmentOrder.Count <= 1)
            {
                return false;
            }

            return branch.ContradictionCandidate == null || branch.ContradictionCandidate != seedCandidate || branch.AssignmentOrder.Count > 1;
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
            return ForcingChainLinkModel.MakeCandidateKey(board, row, column, digit);
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
            return ForcingChainLinkModel.GetRow(board, candidate);
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
            return ForcingChainLinkModel.GetColumn(board, candidate);
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
            return ForcingChainLinkModel.GetDigit(board, candidate);
        }

    }

}
