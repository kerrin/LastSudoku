using Board = Sudoku.Models.Board;

namespace Sudoku.Solver.Rules
{
    /**
     * A simple Pointing Pair implementation.
     *
     * A Pointing Pair (or pointing triple) occurs when all candidates of a digit inside a 3×3 box fall on the same row or column. 
     * The candidate must be placed somewhere in that box, so it must be on that row or column - 
     * which means the digit can be eliminated from the rest of that row or column outside the box.
     */
    public class PointingPairRule : ISudokuRule
    {
        public string Name => "Pointing Pair";

        public Difficulty Difficulty => Difficulty.Medium;

        public bool CanApply(Board board)
        {
            RuleResult result = CalculateChanges(board);
            return  result.Apply;
        }

        public RuleResult CalculateChanges(Board board)
        {
            var result = new RuleResult();
            int size = board.Size;
           
            // TODO: Implement Pointing Pair logic here. This is a placeholder to allow compilation and testing of unsolve handlers without needing the full implementation of this rule.
            
            result.Apply = false;
            return result;
        }
    }
}
