
using Board = Sudoku.Models.Board;

namespace Sudoku.Solver.Rules
{
    /**
     * Implements the Naked Pairs technique:
     * If two cells in a unit (row, column, or block) have exactly the same two candidates,
     * those candidates can be removed from all other cells in that unit.
     */
    public class NakedPairRule : ISudokuRule
    {
        /** Rule display name. */
        public string Name => "Naked Pair";

        /** Difficulty classification for this rule. */
        public Difficulty Difficulty => Difficulty.Medium;

        /**
         * Quick check to see if this rule can be applied to the given board.
         */
        public bool CanApply(Board board)
        {
           return CalculateChanges(board).Apply;
        }

        /**
         * Apply the first naked-pair found: remove the pair's candidates from all other cells in the unit.
         */
        public RuleResult CalculateChanges(Board board)
        {
            var result = new RuleResult();
            
            // TODO: Implement Naked Pair logic here. This is a placeholder to allow compilation and testing of unsolve handlers without needing the full implementation of this rule.
            
            result.Apply = false;
            return result;
        }
    }
}
