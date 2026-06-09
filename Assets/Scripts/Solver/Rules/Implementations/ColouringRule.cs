using Board = Sudoku.Models.Board;

namespace Sudoku.Solver.Rules
{
    /**
     * Colouring is a technique used to eliminate candidates by coloring cells in a way that reveals contradictions.
     * If a candidate appears in two different colors in the same unit, it can be removed from all other cells in that unit.
     * You start by picking a candidate and coloring it in one color. Then, you look for cells that see that candidate and color them in the opposite color.
     * Once the chain resolves, you then start again with other candidates and continue the process of coloring until you can find a contradiction or eliminate candidates based on the coloring.
     */
    public class ColouringRule : ISudokuRule
    {
        public string Name => "Colouring";

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
