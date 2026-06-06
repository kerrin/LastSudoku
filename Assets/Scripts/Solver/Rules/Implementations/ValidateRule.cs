using Sudoku.Models;

namespace Sudoku.Solver.Rules
{
    /**
     * Rule that performs a non-destructive board validation. When the board
     * contains duplicate solved digits in any unit, this rule reports the
     * conflicting cells (as `UsedCell` entries) so UI code can highlight them.
     *
     * The rule does not modify the board; it simply reports Apply=true when
     * conflicts are present and leaves `Changes` empty.
     */
    public class ValidateRule : ISudokuRule
    {
        public string Name => "Validate Board";

        public Difficulty Difficulty => Difficulty.Easy;

        public bool CanApply(Board board)
        {
            // This rule should not be selected automatically by the engine.
            // It exists only as a UI action for explicit validation, so
            // prevent ApplyNext from auto-applying it by returning false.
            return false;
        }

        public RuleResult CalculateChanges(Board board)
        {
            var res = new RuleResult();
            if (board == null) { res.Apply = false; res.Description = "No board"; return res; }

            var conflicts = board.FindConflicts();
            if (conflicts != null && conflicts.Count > 0)
            {
                res.Apply = true;
                res.Description = $"Validation: {conflicts.Count} conflict(s) found.";
                // expose conflicts via UsedCells so visualizers can highlight them
                res.UsedCells.AddRange(conflicts);
            }
            else
            {
                res.Apply = false;
                res.Description = "Validation: board OK.";
            }
            return res;
        }
    }
}
