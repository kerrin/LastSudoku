using Board = Sudoku.Models.Board;

namespace Sudoku.Solver.Rules
{
    /**
     * Hidden Triple is when there are exactly three candidates in a unit (row, column, or box) that appear only in three cells.
     * These three candidates can be removed from all other cells in that unit.
     */
    public class HiddenTripleRule : ISudokuRule
    {
        public string Name => "Hidden Triple";

        public Difficulty Difficulty => Difficulty.Hard;
        public bool CanApply(Board board)
        {
            // TODO: Implement Hidden Triple logic here. This is a placeholder to allow compilation and testing of unsolve handlers without needing the full implementation of this rule.
            return false;
        }

        public RuleResult CalculateChanges(Board board)
        {
            var result = new RuleResult();
            
            // TODO: Implement Hidden Triple logic here. This is a placeholder to allow compilation and testing of unsolve handlers without needing the full implementation of this rule.
            
            result.Apply = false;
            return result;
        }
}
