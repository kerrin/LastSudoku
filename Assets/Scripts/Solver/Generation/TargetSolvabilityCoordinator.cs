using System;
using System.Collections.Generic;
using Sudoku.Models;

namespace Sudoku.Solver.Unsolver
{
    /**
     * Coordinates cross-rule target hardening so a selected target is not left trivially
     * solvable by excluded/easier value-placement rules.
     */
    public sealed class TargetSolvabilityCoordinator
    {
        private readonly List<ITargetSolvabilityBlocker> _blockers;

        public TargetSolvabilityCoordinator(IEnumerable<ITargetSolvabilityBlocker> blockers)
        {
            _blockers = blockers == null
                ? new List<ITargetSolvabilityBlocker>()
                : new List<ITargetSolvabilityBlocker>(blockers);
        }

        public bool TryMakeTargetNotSolvableByOtherRules(
            Board board,
            Cell targetCell,
            int targetValue,
            string anchorRuleName,
            Random random,
            Func<Board, bool> anchorSolveStillPossible = null)
        {
            var context = new TargetSolvabilityGuardContext();
            context.AnchorSolveStillPossible = anchorSolveStillPossible;
            return TryMakeTargetNotSolvableByOtherRules(
                board,
                targetCell,
                targetValue,
                anchorRuleName,
                random,
                context);
        }

        public bool TryMakeTargetNotSolvableByOtherRules(
            Board board,
            Cell targetCell,
            int targetValue,
            string anchorRuleName,
            Random random,
            TargetSolvabilityGuardContext context)
        {
            if (board == null || targetCell == null)
            {
                return false;
            }

            // Re-run while progress is made so one rule can break another rule's newly
            // introduced easy solve path.
            const int maxPasses = 24;
            for (int pass = 0; pass < maxPasses; pass++)
            {
                bool progressed = false;

                foreach (var blocker in _blockers)
                {
                    if (blocker == null)
                    {
                        continue;
                    }

                    if (string.Equals(blocker.RuleName, anchorRuleName, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!blocker.CanSolveTarget(board, targetCell, targetValue))
                    {
                        continue;
                    }

                    if (!context.TryEnter(blocker.RuleName, targetCell.Row, targetCell.Column, targetValue))
                    {
                        return false;
                    }

                    bool blocked;
                    try
                    {
                        blocked = blocker.TryMakeTargetNotSolvable(
                            board,
                            targetCell,
                            targetValue,
                            random,
                            this,
                            context);
                    }
                    finally
                    {
                        context.Exit(blocker.RuleName, targetCell.Row, targetCell.Column, targetValue);
                    }

                    if (!blocked)
                    {
                        return false;
                    }

                    progressed = true;
                }

                if (!progressed)
                {
                    break;
                }
            }

            foreach (var blocker in _blockers)
            {
                if (blocker == null)
                {
                    continue;
                }

                if (string.Equals(blocker.RuleName, anchorRuleName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (blocker.CanSolveTarget(board, targetCell, targetValue))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
