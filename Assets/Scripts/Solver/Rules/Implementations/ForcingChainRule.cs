using Board = Sudoku.Models.Board;

namespace Sudoku.Solver.Rules
{
    /**
     * Hidden Pair is when there are exactly two candidates in a unit (row, column, or box) that appear only in two cells.
     * These two candidates can be removed from all other cells in that unit.
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
