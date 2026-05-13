using Sudoku.Models;

namespace Sudoku.Solver.Rules
{
    /// <summary>
    /// Interface describing a single solving rule/technique that can be
    /// applied to a <see cref="Board"/> instance.
    /// </summary>
    public interface ISudokuRule
    {
        /// <summary>
        /// Human-friendly rule name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Return true if this rule is applicable to the given <paramref name="board"/>.
        /// </summary>
        /// <param name="board">The puzzle board to inspect.</param>
        bool CanApply(Board board);

        /// <summary>
        /// Apply the rule to the <paramref name="board"/>. Returns a <see cref="RuleResult"/>
        /// describing any changes made (or not made).
        /// </summary>
        RuleResult Apply(Board board);

        /// <summary>
        /// Difficulty classification for the rule (used for rating puzzles or choosing
        /// which techniques to run).
        /// </summary>
        Difficulty Difficulty { get; }
    }
}
