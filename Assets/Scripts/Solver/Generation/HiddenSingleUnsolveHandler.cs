using System;
using System.Collections.Generic;
using Sudoku.Models;
using Sudoku.Solver.Rules;

namespace Sudoku.Solver.Unsolver
{
    /**
     * Unsolve handler for the Hidden Single rule.
     *
     * A cell <c>C</c> with value <c>V</c> is a valid Hidden Single unsolve candidate when:
     * <list type="number">
     *   <item>NakedSingle would <b>not</b> fire after removal — at least one digit other
     *         than V is absent from C's combined peer values.</item>
     *   <item>After removing V from C, V is the unique possible placement for some unit
     *         (row, column, or box) — every other empty cell in that unit would have V
     *         excluded by its own cross-unit peers.</item>
     * </list>
     *
     * The check is performed analytically against set values only; candidates are not
     * read from or written to the board during the search.
     */
    public class HiddenSingleUnsolveHandler : IUnsolveHandler, ITargetSolvabilityBlocker
    {
        private readonly NakedSingleUnsolveHandler _nakedSingleUnsolve = new NakedSingleUnsolveHandler();
        public string RuleName => nameof(HiddenSingleRule);

        public UnsolveResult TryUnsolve(Board board, Random random)
        {
            var candidates = BuildCandidateList(board);
            if (candidates.Count == 0) return UnsolveResult.NoApplicableMove;

            var chosen = candidates[random.Next(candidates.Count)];
            chosen.Value = null;
            chosen.IsGiven = false;
            return UnsolveResult.Success;
        }

        /**
         * Collect all non-given cells whose value can be removed to create a Hidden Single
         * (but not a Naked Single) opportunity. Exposed for testing.
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

                    int value = cell.Value.Value;

                    // Skip cells that NakedSingle can already handle.
                    if (NakedSingleWouldFire(board, cell)) continue;

                    if (WouldBeHiddenSingleInRow(board, cell, value)
                        || WouldBeHiddenSingleInColumn(board, cell, value)
                        || WouldBeHiddenSingleInBox(board, cell, value))
                    {
                        result.Add(cell);
                    }
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

            RecomputeCandidates(board);
            if (!targetCell.Candidates.Contains(targetValue))
            {
                return false;
            }

            return CountCandidateOccurrences(board.GetRow(targetCell.Row), targetValue) == 1
                || CountCandidateOccurrences(board.GetColumn(targetCell.Column), targetValue) == 1
                || CountCandidateOccurrences(board.GetBox(targetCell.Box), targetValue) == 1;
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
                return true;
            }

            var removable = _nakedSingleUnsolve.BuildCandidateList(board);
            var removableKeys = new HashSet<int>();
            foreach (var cell in removable)
            {
                removableKeys.Add(ToCellKey(board.Size, cell.Row, cell.Column));
            }

            var blockerOptions = CollectTargetValueBlockers(board, targetCell, targetValue, removableKeys);
            Shuffle(blockerOptions, random);

            foreach (var blocker in blockerOptions)
            {
                int previousValue = blocker.Value.Value;
                blocker.Value = null;
                blocker.IsGiven = false;

                RecomputeCandidates(board);
                if (!CanSolveTarget(board, targetCell, targetValue)
                    && coordinator.TryMakeTargetNotSolvableByOtherRules(
                        board,
                        targetCell,
                        targetValue,
                        RuleName,
                        random,
                        context))
                {
                    return true;
                }

                blocker.Value = previousValue;
                blocker.IsGiven = false;
            }

            RecomputeCandidates(board);
            return false;
        }

        // ── Naked Single guard ─────────────────────────────────────────────────────

        private static bool NakedSingleWouldFire(Board board, Cell cell)
        {
            int value = cell.Value.Value;
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

        // ── Hidden Single unit checks ──────────────────────────────────────────────

        /**
         * After temporarily removing <paramref name="value"/> from <paramref name="cell"/>,
         * returns true when <paramref name="cell"/> is the only empty cell in its row that
         * could hold <paramref name="value"/> (all other empty row-cells have it excluded
         * by their column or box peers).
         */
        private static bool WouldBeHiddenSingleInRow(Board board, Cell cell, int value)
        {
            foreach (var d in board.GetRow(cell.Row))
            {
                if (ReferenceEquals(d, cell) || d.Value.HasValue) continue;
                if (!WouldValueBeExcludedFromCell(board, d, cell, value)) return false;
            }
            return true;
        }

        /**
         * Same as <see cref="WouldBeHiddenSingleInRow"/> but for the cell's column.
         */
        private static bool WouldBeHiddenSingleInColumn(Board board, Cell cell, int value)
        {
            foreach (var d in board.GetColumn(cell.Column))
            {
                if (ReferenceEquals(d, cell) || d.Value.HasValue) continue;
                if (!WouldValueBeExcludedFromCell(board, d, cell, value)) return false;
            }
            return true;
        }

        /**
         * Same as <see cref="WouldBeHiddenSingleInRow"/> but for the cell's box.
         */
        private static bool WouldBeHiddenSingleInBox(Board board, Cell cell, int value)
        {
            foreach (var d in board.GetBox(cell.Box))
            {
                if (ReferenceEquals(d, cell) || d.Value.HasValue) continue;
                if (!WouldValueBeExcludedFromCell(board, d, cell, value)) return false;
            }
            return true;
        }

        /**
         * Returns true when <paramref name="value"/> would be excluded from
         * <paramref name="target"/>'s candidates after <paramref name="sourceCell"/> has its
         * value removed (i.e. <paramref name="value"/> appears as a set value somewhere in
         * <paramref name="target"/>'s peers, not counting <paramref name="sourceCell"/>).
         */
        private static bool WouldValueBeExcludedFromCell(Board board, Cell target, Cell sourceCell, int value)
        {
            // Check target's row (exclude target itself and the cell being removed)
            foreach (var rp in board.GetRow(target.Row))
            {
                if (ReferenceEquals(rp, target) || ReferenceEquals(rp, sourceCell)) continue;
                if (rp.Value == value) return true;
            }
            // Check target's column
            foreach (var cp in board.GetColumn(target.Column))
            {
                if (ReferenceEquals(cp, target) || ReferenceEquals(cp, sourceCell)) continue;
                if (cp.Value == value) return true;
            }
            // Check target's box
            foreach (var bp in board.GetBox(target.Box))
            {
                if (ReferenceEquals(bp, target) || ReferenceEquals(bp, sourceCell)) continue;
                if (bp.Value == value) return true;
            }
            return false;
        }

        private static List<Cell> CollectTargetValueBlockers(
            Board board,
            Cell targetCell,
            int targetValue,
            HashSet<int> removableKeys)
        {
            var result = new List<Cell>();
            var seen = new HashSet<int>();

            AddBlockersFromUnit(board, board.GetRow(targetCell.Row), targetCell, targetValue, removableKeys, seen, result);
            AddBlockersFromUnit(board, board.GetColumn(targetCell.Column), targetCell, targetValue, removableKeys, seen, result);
            AddBlockersFromUnit(board, board.GetBox(targetCell.Box), targetCell, targetValue, removableKeys, seen, result);

            return result;
        }

        private static void AddBlockersFromUnit(
            Board board,
            IEnumerable<Cell> unit,
            Cell targetCell,
            int targetValue,
            HashSet<int> removableKeys,
            HashSet<int> seen,
            List<Cell> output)
        {
            foreach (var cell in unit)
            {
                if (ReferenceEquals(cell, targetCell) || cell.Value.HasValue)
                {
                    continue;
                }

                foreach (var peer in board.GetPeers(cell))
                {
                    if (!peer.Value.HasValue || peer.Value.Value != targetValue || peer.IsGiven)
                    {
                        continue;
                    }

                    int key = ToCellKey(board.Size, peer.Row, peer.Column);
                    if (!removableKeys.Contains(key) || !seen.Add(key))
                    {
                        continue;
                    }

                    output.Add(peer);
                }
            }
        }

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
                        for (int value = 1; value <= board.Size; value++)
                        {
                            cell.Candidates.Add(value);
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

        private static int CountCandidateOccurrences(IEnumerable<Cell> unitCells, int value)
        {
            int count = 0;
            foreach (var cell in unitCells)
            {
                if (!cell.Value.HasValue && cell.Candidates.Contains(value))
                {
                    count++;
                }
            }

            return count;
        }

        private static int ToCellKey(int boardSize, int row, int column)
        {
            return row * boardSize + column;
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
    }
}
