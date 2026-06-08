using System;
using System.Collections.Generic;
using Sudoku.Models;
using Sudoku.Solver.Rules;

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
                        context?.Trace($"{blocker.RuleName}: cannot currently solve target, skipping.");
                        continue;
                    }

                    context?.Trace(
                        $"{blocker.RuleName}: can solve target; attempting to force unsolvable.",
                        failed: false,
                        usedCells: new List<UsedCell>
                        {
                            new UsedCell { Row = targetCell.Row, Column = targetCell.Column, HighlightTag = "Target" }
                        });

                    if (!context.TryEnter(blocker.RuleName, targetCell.Row, targetCell.Column, targetValue))
                    {
                        context?.Trace(
                            $"{blocker.RuleName}: blocked by recursion guard.",
                            failed: true,
                            usedCells: new List<UsedCell>
                            {
                                new UsedCell { Row = targetCell.Row, Column = targetCell.Column, HighlightTag = "Failure" }
                            });
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
                        context?.Trace(
                            $"{blocker.RuleName}: failed to make target unsolvable.",
                            failed: true,
                            usedCells: new List<UsedCell>
                            {
                                new UsedCell { Row = targetCell.Row, Column = targetCell.Column, HighlightTag = "Failure" }
                            });
                        return false;
                    }

                    context?.Trace($"{blocker.RuleName}: target no longer solvable by this blocker after adjustment.");

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
                    context?.Trace(
                        $"{blocker.RuleName}: still solves target after hardening pass.",
                        failed: true,
                        usedCells: new List<UsedCell>
                        {
                            new UsedCell { Row = targetCell.Row, Column = targetCell.Column, HighlightTag = "Failure" }
                        });
                    return false;
                }
            }

            return true;
        }
    }
}
