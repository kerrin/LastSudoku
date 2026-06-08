using System;
using System.Collections.Generic;
using Sudoku.Models;
using Sudoku.Solver.Rules;

namespace Sudoku.Solver.Unsolver
{
    /**
     * Unsolve handler for the Naked Single rule.
     *
     * A cell <c>C</c> with value <c>V</c> is a valid Naked Single unsolve candidate when
     * all eight other digits (1..Size except V) already appear as set values among C's peers.
     * After removing V from C, the NakedSingle rule can re-place V by observing that every
     * other digit is visible in the peer set.
     */
    public class NakedSingleUnsolveHandler : IUnsolveHandler, ITargetSolvabilityBlocker
    {
        public string RuleName => nameof(NakedSingleRule);

        public UnsolveResult TryUnsolve(Board board, Random random)
        {
            var candidates = BuildCandidateList(board);
            if (candidates.Count == 0) return UnsolveResult.NoApplicableMove;

            var chosen = candidates[random.Next(candidates.Count)];
            int removedValue = chosen.Value.Value;
            chosen.Value = null;
            chosen.IsGiven = false;
            chosen.Candidates.Clear();
            chosen.Candidates.Add(removedValue);
            return UnsolveResult.Success;
        }

        /**
         * Collect all non-given cells whose value can be safely removed for a Naked Single.
         * Exposed for testing convenience.
         */
        public List<Cell> BuildCandidateList(Board board)
        {
            var result = new List<Cell>();
            for (int r = 0; r < board.Size; r++)
            {
                for (int c = 0; c < board.Size; c++)
                {
                    var cell = board.Cells[r, c];
                    if (!cell.Value.HasValue || cell.IsGiven) continue;

                    if (AllOtherValuesVisibleInPeers(board, cell))
                        result.Add(cell);
                }
            }
            return result;
        }

        public bool CanSolveTarget(Board board, Cell targetCell, int targetValue)
        {
            if (board == null || targetCell == null)
            {
                return false;
            }

            if (targetCell.Value.HasValue)
            {
                return false;
            }

            if (!targetCell.Candidates.Contains(targetValue))
            {
                return false;
            }

            return AllOtherValuesVisibleInPeers(board, targetCell, targetValue);
        }

        public bool TryMakeTargetNotSolvable(
            Board board,
            Cell targetCell,
            int targetValue,
            Random random,
            TargetSolvabilityCoordinator coordinator,
            TargetSolvabilityGuardContext context)
        {
            if (!CanSolveTarget(board, targetCell, targetValue))
            {
                context?.Trace("NakedSingle: target already not solvable; no action needed.");
                return true;
            }

            var peerOptions = new List<Cell>();
            foreach (var peer in board.GetPeers(targetCell))
            {
                if (!peer.Value.HasValue || peer.IsGiven)
                {
                    continue;
                }

                if (peer.Value.Value == targetValue)
                {
                    continue;
                }

                peerOptions.Add(peer);
            }

            Shuffle(peerOptions, random);

            foreach (var option in peerOptions)
            {
                int previousValue = option.Value.Value;
                var usedCells = new List<UsedCell>
                {
                    new UsedCell { Row = targetCell.Row, Column = targetCell.Column, HighlightTag = "Target" },
                    new UsedCell { Row = option.Row, Column = option.Column, HighlightTag = "Deduction" }
                };
                var beforeAttempt = PuzzleGenerator.CloneBoard(board);
                option.Value = null;
                option.IsGiven = false;
                RecomputeCandidates(board);

                bool anchorStillPossible = context?.AnchorSolveStillPossible == null
                    || context.AnchorSolveStillPossible(board);
                if (!CanSolveTarget(board, targetCell, targetValue)
                    && anchorStillPossible)
                {
                    context?.TraceTransition(
                        "NakedSingle removal",
                        $"Removed r{option.Row + 1}c{option.Column + 1}={previousValue}. Target is no longer naked-single solvable and the Right Angle anchor still holds.",
                        beforeAttempt,
                        board,
                        failed: false,
                        usedCells: usedCells);
                    return true;
                }

                context?.TraceTransition(
                    "NakedSingle removal failed",
                    anchorStillPossible
                        ? $"Removed r{option.Row + 1}c{option.Column + 1}={previousValue}, but the target is still naked-single solvable."
                        : $"Removed r{option.Row + 1}c{option.Column + 1}={previousValue}, but that broke Right Angle anchor viability; reverting.",
                    beforeAttempt,
                    board,
                    failed: true,
                    usedCells: new List<UsedCell>
                    {
                        new UsedCell { Row = targetCell.Row, Column = targetCell.Column, HighlightTag = "Target" },
                        new UsedCell { Row = option.Row, Column = option.Column, HighlightTag = "Failure" }
                    });

                option.Value = previousValue;
                option.IsGiven = false;
                RecomputeCandidates(board);
            }

            context?.Trace(
                "NakedSingle: exhausted all peer removals without success.",
                failed: true,
                usedCells: new List<UsedCell>
                {
                    new UsedCell { Row = targetCell.Row, Column = targetCell.Column, HighlightTag = "Failure" }
                });

            return false;
        }

        /**
         * Returns true when every digit in 1..Size except cell.Value appears as a set value
         * in at least one of the cell's peers (row ∪ column ∪ box, excluding the cell itself).
         */
        private static bool AllOtherValuesVisibleInPeers(Board board, Cell cell)
        {
            int value = cell.Value.Value;
            return AllOtherValuesVisibleInPeers(board, cell, value);
        }

        private static bool AllOtherValuesVisibleInPeers(Board board, Cell cell, int value)
        {
            var peerValues = new HashSet<int>();
            foreach (var peer in board.GetPeers(cell))
                if (peer.Value.HasValue) peerValues.Add(peer.Value.Value);

            for (int d = 1; d <= board.Size; d++)
            {
                if (d == value) continue;
                if (!peerValues.Contains(d)) return false;
            }
            return true;
        }

        private static void Shuffle<T>(List<T> list, Random random)
        {
            if (random == null)
            {
                return;
            }

            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private static void RecomputeCandidates(Board board)
        {
            if (board == null || board.Cells == null)
            {
                return;
            }

            for (int row = 0; row < board.Size; row++)
            {
                for (int column = 0; column < board.Size; column++)
                {
                    var cell = board.Cells[row, column];
                    cell.Candidates.Clear();
                    if (!cell.Value.HasValue)
                    {
                        for (int value = 1; value <= board.Size; value++)
                        {
                            cell.Candidates.Add(value);
                        }
                    }
                }
            }

            for (int row = 0; row < board.Size; row++)
            {
                for (int column = 0; column < board.Size; column++)
                {
                    var cell = board.Cells[row, column];
                    if (cell.Value.HasValue)
                    {
                        continue;
                    }

                    foreach (var peer in board.GetPeers(cell))
                    {
                        if (peer.Value.HasValue)
                        {
                            cell.Candidates.Remove(peer.Value.Value);
                        }
                    }
                }
            }
        }
    }
}
