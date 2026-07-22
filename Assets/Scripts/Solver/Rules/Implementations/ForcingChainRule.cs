using Board = Sudoku.Models.Board;

namespace Sudoku.Solver.Rules
{
    /**
     * Forcing Chain is a technique that involves creating a chain of candidate placements that leads to a contradiction.
     * This is like colouring, but instead of just two colours, it can involve multiple branches and more complex logic.
     * We need to constrain the steps to a sensible number for a real person to be able to do.
     *
    * This rule should only be applied if colouring is enabled and has at least three colours enabled.
     */
    public class ForcingChainRule : ISudokuRule
    {
        public string Name => "Forcing Chain";

        public Difficulty Difficulty => Difficulty.Expert;
        public bool CanApply(Board board)
        {
            // TODO: Not implemented.
            return false;
        }

        public RuleResult CalculateChanges(Board board)
        {
            var result = new RuleResult();
            
            // TODO: Not implemented.
            
            result.Apply = false;
            return result;
        }
}

}
