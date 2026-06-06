using Sudoku.Models;

namespace Sudoku.Solver.Rules
{
    /**
     * Interface describing a single solving rule/technique that can be
     * applied to a <see cref="Board"/> instance.
     */
    public interface ISudokuRule
    {
        /** Human-friendly rule name. */
        string Name { get; }

        /**
         * Return true if this rule is applicable to the given <paramref name="board"/>.
         * @param board The puzzle board to inspect.
         */
        bool CanApply(Board board);

        /**
         * Calculate the changes this rule would make to the <paramref name="board"/>.
         * Returns a <see cref="RuleResult"/> describing any changes (or not).
         * The returned result is non-mutating: callers must call `EnactAll` or
         * `EnactCandidates` on the `RuleResult` to apply the recorded changes to the board.
         */
        RuleResult CalculateChanges(Board board);

        /**
         * Difficulty classification for the rule (used for rating puzzles or choosing
         * which techniques to run).
         */
        Difficulty Difficulty { get; }
    }
}
