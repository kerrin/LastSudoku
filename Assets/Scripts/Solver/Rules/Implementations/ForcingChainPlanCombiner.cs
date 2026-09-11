using System.Collections.Generic;
using System.Linq;
using Sudoku.Models;

namespace Sudoku.Solver.Rules
{
    /**
     * Shared branch-state used by all forcing-chain rules during propagation.
     */
    internal sealed class PropagationState
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

    /**
     * Shared provenance metadata that explains how a candidate was inferred.
     */
    internal sealed class InferenceCause
    {
        public int FromCandidate;
        public DirectionalLinkKind LinkKind;
        public bool IsPreviewLink;
    }

    /**
     * Shared forcing-chain state used by both the cell and unit chain variants.
     */
    internal sealed class ForcingPlan
    {
        public int SeedCandidate;
        public PropagationState TrueBranch;
        public PropagationState FalseBranch;
        public List<int> CommonFalseCandidates = new List<int>();
        public List<int> CommonTrueCandidates = new List<int>();
        public bool ContradictionOnTrueBranch;
        public bool ContradictionOnFalseBranch;
    }

    /**
     * Shared helpers for combining candidate forcing plans produced by the cell and
     * unit forcing-chain variants.
     */
    internal static class ForcingChainPlanCombiner
    {
        /**
         * Combine multiple forcing plans into a single plan by intersecting all shared
         * common conclusions and merging contradiction flags.
         *
         * @param plans Candidate plans to merge.
         * @returns Combined plan or null when no valid plans were supplied.
         */
        public static ForcingPlan Combine(IReadOnlyCollection<ForcingPlan> plans)
        {
            if (plans == null || plans.Count == 0)
            {
                return null;
            }

            var validPlans = plans
                .Where(plan => plan != null)
                .ToList();

            if (validPlans.Count == 0)
            {
                return null;
            }

            var combined = new ForcingPlan
            {
                SeedCandidate = validPlans[0].SeedCandidate,
                TrueBranch = validPlans[0].TrueBranch,
                FalseBranch = validPlans[0].FalseBranch,
                CommonFalseCandidates = new List<int>(validPlans[0].CommonFalseCandidates ?? Enumerable.Empty<int>()),
                CommonTrueCandidates = new List<int>(validPlans[0].CommonTrueCandidates ?? Enumerable.Empty<int>()),
                ContradictionOnTrueBranch = validPlans[0].ContradictionOnTrueBranch,
                ContradictionOnFalseBranch = validPlans[0].ContradictionOnFalseBranch
            };

            for (int i = 1; i < validPlans.Count; i++)
            {
                var plan = validPlans[i];
                combined.CommonFalseCandidates = combined.CommonFalseCandidates
                    .Intersect(plan.CommonFalseCandidates ?? Enumerable.Empty<int>())
                    .ToList();
                combined.CommonTrueCandidates = combined.CommonTrueCandidates
                    .Intersect(plan.CommonTrueCandidates ?? Enumerable.Empty<int>())
                    .ToList();
                combined.ContradictionOnTrueBranch |= plan.ContradictionOnTrueBranch;
                combined.ContradictionOnFalseBranch |= plan.ContradictionOnFalseBranch;
            }

            return combined;
        }
    }
}
