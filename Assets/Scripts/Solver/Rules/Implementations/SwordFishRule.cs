using Board = Sudoku.Models.Board;

namespace Sudoku.Solver.Rules
{
    /**
     * "Swordfish" is similar to X-wing but uses three sets of cells instead of two.
     *
     * If a candidate appears 6 times across three different rows, and those digits are in columns that form 3 pairs,
     * then that candidate can be removed from all other cells in those three columns. The same logic applies with rows and columns swapped.
     * https://sudoku.com/sudoku-rules/swordfish/
     */
    public class SwordFishRule : ISudokuRule
    {
        public string Name => "Swordfish";

        public Difficulty Difficulty => Difficulty.Hard;
        public bool CanApply(Board board)
        {
            // TODO: Implement Swordfish logic here. This is a placeholder to allow compilation and testing of unsolve handlers without needing the full implementation of this rule.
            return false;
        }

        public RuleResult CalculateChanges(Board board)
        {
            var result = new RuleResult();
            
            // TODO: Implement Swordfish logic here. This is a placeholder to allow compilation and testing of unsolve handlers without needing the full implementation of this rule.
            
            result.Apply = false;
            return result;
        }
}
