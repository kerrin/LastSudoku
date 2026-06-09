using Board = Sudoku.Models.Board;

namespace Sudoku.Solver.Rules
{
    /**
     * Forcing Chain is a technique that involves creating a chain of candidate placements that leads to a contradiction.
     * This is like colouring, but instead of just two colours, it can involve multiple branches and more complex logic.
     */
    public class ForcingChainRule : ISudokuRule
    {
        public string Name => "Forcing Chain";

        public Difficulty Difficulty => Difficulty.NotImplemented;
        public bool CanApply(Board board)
        {
            // Not to be implemented, only used in the tutorials as an addition method for solving extremely difficult puzzles.
            return false;
        }

        public RuleResult CalculateChanges(Board board)
        {
            var result = new RuleResult();
            
            // Not implemented
            
            result.Apply = false;
            return result;
        }
}

}
