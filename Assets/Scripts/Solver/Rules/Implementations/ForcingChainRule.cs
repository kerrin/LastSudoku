using Board = Sudoku.Models.Board;

namespace Sudoku.Solver.Rules
{
    /**
     * Forcing Chain (sometimes called Nice Loops, Alternating Inference Chains, or AIC) 
     * is a technique that involves creating a chain of candidate placements that leads to a contradiction.
     * This is a generalisation of Colouring, X-Wing, and Skyscraper; 
     * It can involve multiple branches and more complex logic.
     * We need to constrain the steps to a sensible number that a real person can actually be able to do.
     *
     * It can also be defined as:
     * Where a chain of candidates alternating strong links (where if one is false, the other must be true) and weak links (where if one is true, the other must be false) is formed.
     * There are a few types of forcing chains:
     * 1.1 Discontinuous AIC: The ends of the chain don't see each other, any cell that sees both ends of the chain can have candidates eliminated.
     * 1.2 Nice Loops: Every strong link endpoint must be true (one of the pair), and every weak link connection means both can’t be true:
     *    * Eliminations at weak link junctions (remove the candidate from other cells seeing both endpoints of a weak link)
     *    * Placements at strong link junctions (if the chain forces a value)
     * 2. Discontinuous AIC (Placement Chain). When both ends of the chain converge to the same conclusion: a specific cell must contain a specific digit. This directly places that digit.
     * 
     * This rule should only be applied if colouring is enabled and has at least two colours enabled.
     */
    public class ForcingChainRule : CachedRuleBase
    {
        public override string Name => "Forcing Chain";

        public override Difficulty Difficulty => Difficulty.Expert;
        public override bool CanApply(Board board)
        {
            // TODO: Not implemented.
            return false;
        }

        protected override RuleResult CalculateChangesInternal(Board board)
        {
            var result = new RuleResult();
            
            // TODO: Not implemented.
            
            result.Apply = false;
            return result;
        }
}

}

