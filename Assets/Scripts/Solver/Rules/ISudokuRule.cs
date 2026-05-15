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
         * Update candidate sets on the provided <paramref name="board"/> where
         * candidates can be eliminated without assigning values. Return true if
         * any candidate set was changed.
         *
         * This method only updates candidate sets and does not place any values.
         */
        RuleResult ApplyOnlyCandidates(Board board);

        /**
         * Return true if this rule is applicable to the given <paramref name="board"/>.
         * @param board The puzzle board to inspect.
         */
        bool CanApply(Board board);

        /**
         * Apply the rule to the <paramref name="board"/>. Returns a <see cref="RuleResult"/>
         * describing any changes made (or not made).
         */
        RuleResult Apply(Board board);

        /**
         * Difficulty classification for the rule (used for rating puzzles or choosing
         * which techniques to run).
         */
        Difficulty Difficulty { get; }
    }
}
