using System.Collections.Generic;
using System.Linq;
using Sudoku.Models;

namespace Sudoku.Solver.Rules
{
    /**
     * Shared branch-state used by both forcing-chain seed strategies during propagation.
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
        public List<int> ContradictionGroupCandidates = new List<int>();
        public Queue<(int key, bool valueTrue, int? sourceCandidate, DirectionalLinkKind? sourceLinkKind)> Pending
            = new Queue<(int key, bool valueTrue, int? sourceCandidate, DirectionalLinkKind? sourceLinkKind)>();
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

    internal sealed class ForcingChainBranch
    {
        public int SeedCandidate;
        public PropagationState State;
    }

    /**
     * Shared forcing-chain state used by the cell- and unit-seeded searches.
     */
    internal sealed class ForcingPlan
    {
        public int SeedCandidate;
        public PropagationState TrueBranch;
        public PropagationState FalseBranch;
        public List<int> CommonFalseCandidates = new List<int>();
        public List<int> CommonTrueCandidates = new List<int>();
        public List<ForcingChainBranch> VerityBranches = new List<ForcingChainBranch>();
        public bool ContradictionOnTrueBranch;
        public bool ContradictionOnFalseBranch;
    }

    /**
     * Shared helpers for combining candidate forcing plans from both seed strategies.
     */
    internal static class ForcingChainPlanCombiner
    {
        /**
         * Combine multiple forcing plans into a single plan by intersecting all shared
         * common conclusions. Contradiction plans are not combinable because each
         * contradiction applies to a specific seed assumption.
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
                .Where(plan => plan != null
                    && !plan.ContradictionOnTrueBranch
                    && !plan.ContradictionOnFalseBranch)
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
                CommonTrueCandidates = new List<int>(validPlans[0].CommonTrueCandidates ?? Enumerable.Empty<int>())
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
            }

            return combined;
        }
    }
}
