using Sudoku.Models;

namespace Sudoku.Solver.Rules
{
    /**
     * Base class for rules that use the shared calculation cache and standard applicability check.
     */
    public abstract class CachedRuleBase : ISudokuRule
    {
        /** Human-friendly rule name. */
        public abstract string Name { get; }

        /** Difficulty classification for this rule. */
        public abstract Difficulty Difficulty { get; }

        /**
         * Return true when this rule can produce at least one change.
         */
        public virtual bool CanApply(Board board)
        {
            return CalculateChanges(board).Apply;
        }

        /**
         * Return cached rule changes for this board state.
         */
        public RuleResult CalculateChanges(Board board)
        {
            return RuleCalculationCache.GetOrCalculate(this, board, () => CalculateChangesInternal(board));
        }

        /**
         * Implemented by derived classes to calculate uncached changes.
         */
        protected abstract RuleResult CalculateChangesInternal(Board board);
    }
}
