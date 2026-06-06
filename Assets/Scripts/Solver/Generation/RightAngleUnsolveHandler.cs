using System;
using System.Collections.Generic;
using Sudoku.Models;
using Sudoku.Solver.Rules;

namespace Sudoku.Solver.Unsolver
{
    /**
     * Unsolve handler for the Right Angle rule.
     *
     * The handler prefers removals that are specifically attributable to
     * <see cref="RightAngleRule"/>, but it can fall back to removals that are also
     * recoverable by easier value-placement rules when no RightAngle-exclusive option
     * exists.
     */
    public class RightAngleUnsolveHandler : IUnsolveHandler
    {
        private readonly RightAngleRule _rightAngleRule = new RightAngleRule();

        public string RuleName => nameof(RightAngleRule);

        /**
         * Attempt to remove one value whose restoration is specifically attributable to
         * the Right Angle rule.
         *
         * @param board The working board to modify when a valid removal is found.
         * @param random Random source used to choose among valid removals.
         * @returns <see cref="UnsolveResult.Success"/> when a value is removed; otherwise
         *          <see cref="UnsolveResult.NoApplicableMove"/>.
         */
        public UnsolveResult TryUnsolve(Board board, Random random)
        {
            var candidates = BuildCandidateList(board);
            if (candidates.Count == 0)
            {
                return UnsolveResult.NoApplicableMove;
            }

            var chosen = candidates[random.Next(candidates.Count)];
            chosen.Value = null;
            chosen.IsGiven = false;
            return UnsolveResult.Success;
        }

        /**
         * Collect all non-given cells whose removal creates a Right Angle placement.
         * RightAngle-exclusive candidates are preferred; candidates that also satisfy
         * easier rules are returned only when no preferred option exists.
         *
         * @param board The board to inspect.
         * @returns A list of original board cells that can be removed safely.
         */
        public List<Cell> BuildCandidateList(Board board)
        {
            var preferred = new List<Cell>();
            var fallback = new List<Cell>();
            for (int r = 0; r < board.Size; r++)
            {
                for (int c = 0; c < board.Size; c++)
                {
                    var cell = board.Cells[r, c];
                    if (!cell.Value.HasValue || cell.IsGiven)
                    {
                        continue;
                    }

                    if (TryClassifyRightAngleOpportunity(board, cell, out bool alsoSolvedByEasierRule))
                    {
                        if (alsoSolvedByEasierRule)
                        {
                            fallback.Add(cell);
                        }
                        else
                        {
                            preferred.Add(cell);
                        }
                    }
                }
            }

            return preferred.Count > 0 ? preferred : fallback;
        }

        /**
         * Test a single cell by removing it on a clone, rebuilding candidates, and then
         * checking whether Right Angle restores exactly that value in that cell.
         *
         * @param board The source board.
         * @param sourceCell The valued cell to test.
         * @param alsoSolvedByEasierRule Set when the removed value is also recoverable by
         *        Naked Single or Hidden Single.
         * @returns True when the removed value is restored by Right Angle.
         */
        private bool TryClassifyRightAngleOpportunity(Board board, Cell sourceCell, out bool alsoSolvedByEasierRule)
        {
            var trialBoard = PuzzleGenerator.CloneBoard(board);
            var trialCell = trialBoard.Cells[sourceCell.Row, sourceCell.Column];
            int value = trialCell.Value.Value;

            trialCell.Value = null;
            trialCell.IsGiven = false;
            RecomputeCandidates(trialBoard);

            bool isNakedSingle = IsNakedSingle(trialBoard, trialCell, value);
            bool isHiddenSingle = IsHiddenSingle(trialBoard, trialCell, value);
            alsoSolvedByEasierRule = isNakedSingle || isHiddenSingle;

            var result = _rightAngleRule.CalculateChanges(trialBoard);
            if (result == null || !result.Apply)
            {
                alsoSolvedByEasierRule = false;
                return false;
            }

            foreach (var change in result.Changes)
            {
                if (change.NewValue == value
                    && change.Row == trialCell.Row
                    && change.Column == trialCell.Column)
                {
                    return true;
                }
            }

            alsoSolvedByEasierRule = false;
            return false;
        }

        /**
         * Recompute candidates from the current set values so rule checks evaluate against
         * the same candidate semantics used by the solver on finalized puzzles.
         *
         * @param board The board whose candidates should be rebuilt.
         */
        private static void RecomputeCandidates(Board board)
        {
            for (int r = 0; r < board.Size; r++)
            {
                for (int c = 0; c < board.Size; c++)
                {
                    var cell = board.Cells[r, c];
                    cell.Candidates.Clear();
                    if (!cell.Value.HasValue)
                    {
                        for (int digit = 1; digit <= board.Size; digit++)
                        {
                            cell.Candidates.Add(digit);
                        }
                    }
                }
            }

            for (int r = 0; r < board.Size; r++)
            {
                for (int c = 0; c < board.Size; c++)
                {
                    var cell = board.Cells[r, c];
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

        /**
         * Determine whether the tested cell would be immediately placed by Naked Single.
         *
         * @param board The candidate-rebuilt trial board.
         * @param cell The empty cell under test.
         * @param value The original value removed from the cell.
         * @returns True when all other digits are already visible among the cell's peers.
         */
        private static bool IsNakedSingle(Board board, Cell cell, int value)
        {
            var peerValues = new HashSet<int>();
            foreach (var peer in board.GetPeers(cell))
            {
                if (peer.Value.HasValue)
                {
                    peerValues.Add(peer.Value.Value);
                }
            }

            for (int digit = 1; digit <= board.Size; digit++)
            {
                if (digit == value)
                {
                    continue;
                }

                if (!peerValues.Contains(digit))
                {
                    return false;
                }
            }

            return true;
        }

        /**
         * Determine whether the tested cell would already qualify as a Hidden Single.
         *
         * @param board The candidate-rebuilt trial board.
         * @param cell The empty cell under test.
         * @param value The original value removed from the cell.
         * @returns True when the value appears in exactly one candidate position in any
         *          of the cell's row, column, or box.
         */
        private static bool IsHiddenSingle(Board board, Cell cell, int value)
        {
            if (!cell.Candidates.Contains(value))
            {
                return false;
            }

            return CountCandidateOccurrences(board.GetRow(cell.Row), value) == 1
                || CountCandidateOccurrences(board.GetColumn(cell.Column), value) == 1
                || CountCandidateOccurrences(board.GetBox(cell.Box), value) == 1;
        }

        /**
         * Count how many empty cells in a unit still allow the supplied digit.
         *
         * @param unitCells The row, column, or box to inspect.
         * @param value The digit to count.
         * @returns The number of empty cells whose candidates include the digit.
         */
        private static int CountCandidateOccurrences(IEnumerable<Cell> unitCells, int value)
        {
            int count = 0;
            foreach (var unitCell in unitCells)
            {
                if (!unitCell.Value.HasValue && unitCell.Candidates.Contains(value))
                {
                    count++;
                }
            }

            return count;
        }
    }
}