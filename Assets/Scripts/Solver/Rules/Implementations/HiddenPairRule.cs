using System.Collections.Generic;
using Cell = Sudoku.Models.Cell;
using Board = Sudoku.Models.Board;

namespace Sudoku.Solver.Rules
{
    /**
     * Hidden Pair is when there are exactly two candidates in a unit (row, column, or box) that appear only in two cells.
     * These two candidates can be removed from all other cells in that unit.
     */
    public class HiddenPairRule : ISudokuRule
    {
        public string Name => "Hidden Pair";

        public Difficulty Difficulty => Difficulty.Hard;
        public bool CanApply(Board board)
        {
            // TODO: Implement Hidden Pair logic here. This is a placeholder to allow compilation and testing of unsolve handlers without needing the full implementation of this rule.
            return false;
        }

        public RuleResult CalculateChanges(Board board)
        {
            var result = new RuleResult();
            
            // TODO: Implement Hidden Pair logic here. This is a placeholder to allow compilation and testing of unsolve handlers without needing the full implementation of this rule.
            
            result.Apply = false;
            return result;
        }
}

}
