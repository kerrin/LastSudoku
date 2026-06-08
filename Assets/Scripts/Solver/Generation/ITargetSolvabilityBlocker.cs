using System;
using Sudoku.Models;

namespace Sudoku.Solver.Unsolver
{
    /**
     * Optional capability for unsolve handlers that can reason about whether a specific
     * target cell/value is currently solvable by their paired rule and can apply local
     * mutations to break that solvability.
     */
    public interface ITargetSolvabilityBlocker
    {
        /** Rule-name identifier (typically the paired solver rule type name). */
        string RuleName { get; }

        /**
         * Returns true when this rule can currently solve <paramref name="targetCell"/>
         * as <paramref name="targetValue"/>.
         */
        bool CanSolveTarget(Board board, Cell targetCell, int targetValue);

        /**
         * Attempt to mutate the board so this rule no longer solves
         * <paramref name="targetCell"/> as <paramref name="targetValue"/>.
         *
         * Implementations should be local and conservative; cross-rule orchestration is
         * handled by <see cref="TargetSolvabilityCoordinator"/>.
         */
        bool TryMakeTargetNotSolvable(
            Board board,
            Cell targetCell,
            int targetValue,
            Random random,
            TargetSolvabilityCoordinator coordinator,
            TargetSolvabilityGuardContext context);
    }
}
