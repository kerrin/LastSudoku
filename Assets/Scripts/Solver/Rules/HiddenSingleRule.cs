using System.Collections.Generic;
using Sudoku.Models;

namespace Sudoku.Solver.Rules
{
    /**
     * Hidden Single (also called Single Position) — for a given digit in a unit
     * (row/column/box), if only one cell can contain that digit according to candidates,
     * place it there.
     */
    public class HiddenSingleRule : ISudokuRule
    {
        public string Name => "Hidden Single";

        public Difficulty Difficulty => Difficulty.Easy;

        private enum UnitKind { Row, Column, Box }

        public bool CanApply(Board board)
        {
            return FindAny(board) != null;
        }

        private (Cell cell, int digit, UnitKind unit, int unitIndex)? FindAny(Board board)
        {
            var rowRes = FindInRows(board);
            if (rowRes != null) return rowRes;
            var colRes = FindInColumns(board);
            if (colRes != null) return colRes;
            var boxRes = FindInBoxes(board);
            if (boxRes != null) return boxRes;
            return null;
        }

        private (Cell cell, int digit, UnitKind unit, int unitIndex)? FindInRows(Board board)
        {
            int size = board.Size;
            for (int digit = 1; digit <= size; digit++)
            {
                for (int r = 0; r < size; r++)
                {
                    var candidates = new List<Cell>();
                    foreach (Cell cell in board.GetRow(r))
                    {
                        if (!cell.Value.HasValue && cell.Candidates.Contains(digit)) candidates.Add(cell);
                    }
                    if (candidates.Count == 1) return (candidates[0], digit, UnitKind.Row, r);
                }
            }
            return null;
        }

        private (Cell cell, int digit, UnitKind unit, int unitIndex)? FindInColumns(Board board)
        {
            int size = board.Size;
            for (int digit = 1; digit <= size; digit++)
            {
                for (int c = 0; c < size; c++)
                {
                    var candidates = new List<Cell>();
                    foreach (Cell cell in board.GetColumn(c))
                    {
                        if (!cell.Value.HasValue && cell.Candidates.Contains(digit)) candidates.Add(cell);
                    }
                    if (candidates.Count == 1) return (candidates[0], digit, UnitKind.Column, c);
                }
            }
            return null;
        }

        private (Cell cell, int digit, UnitKind unit, int unitIndex)? FindInBoxes(Board board)
        {
            int size = board.Size;
            for (int digit = 1; digit <= size; digit++)
            {
                for (int b = 0; b < size; b++)
                {
                    var candidates = new List<Cell>();
                    foreach (Cell cell in board.GetBox(b))
                    {
                        if (!cell.Value.HasValue && cell.Candidates.Contains(digit)) candidates.Add(cell);
                    }
                    if (candidates.Count == 1) return (candidates[0], digit, UnitKind.Box, b);
                }
            }
            return null;
        }

        public RuleResult Apply(Board board)
        {
            var result = new RuleResult();
            (Cell cell, int digit, UnitKind unit, int unitIndex)? found = FindAny(board);
            if (found == null)
            {
                result.Applied = false;
                return result;
            }
            (Cell cell, int digit, UnitKind unit, int unitIndex) = found.Value;
            var change = new CellChange { Row = cell.Row, Column = cell.Column, OldValue = cell.Value, NewValue = digit };
            // Highlight the whole unit (row/column/box) that contained the single candidate.
            List<Cell> unitCells = new List<Cell>();
            switch (unit)
            {
                case UnitKind.Row:
                    unitCells.AddRange(board.GetRow(unitIndex));
                    break;
                case UnitKind.Column:
                    unitCells.AddRange(board.GetColumn(unitIndex));
                    break;
                case UnitKind.Box:
                    unitCells.AddRange(board.GetBox(unitIndex));
                    break;
            }

            foreach (Cell u in unitCells)
            {
                if (!result.UsedCells.Exists(x => x.Row == u.Row && x.Column == u.Column))
                    result.UsedCells.Add(new UsedCell { Row = u.Row, Column = u.Column });
            }

            board.SetValue(cell, digit);
            result.Changes.Add(change);
            foreach (Cell peer in board.GetPeers(cell))
            {
                if (peer.Candidates.Remove(digit))
                {
                    var peerChange = new CellChange { Row = peer.Row, Column = peer.Column };
                    peerChange.RemovedCandidates.Add(digit);
                    result.Changes.Add(peerChange);
                    if (!result.UsedCells.Exists(u => u.Row == peer.Row && u.Column == peer.Column))
                        result.UsedCells.Add(new UsedCell { Row = peer.Row, Column = peer.Column });
                }
            }
            result.Applied = true;
            result.Description = $"Placed {digit} at ({cell.Row},{cell.Column}) via Hidden Single";
            return result;
        }

        public RuleResult ApplyOnlyCandidates(Board board)
        {
            var result = new RuleResult();
            bool changed = false;
            int size = board.Size;
            for (int r = 0; r < size; r++)
            {
                for (int c = 0; c < size; c++)
                {
                    Cell cell = board.Cells[r, c];
                    if (cell.Value.HasValue)
                    {
                        if (cell.Candidates.Count != 0)
                        {
                            var change = new CellChange { Row = r, Column = c };
                            foreach (int rem in cell.Candidates) change.RemovedCandidates.Add(rem);
                            cell.Candidates.Clear();
                            result.Changes.Add(change);
                            changed = true;
                        }
                        continue;
                    }
                    var present = new bool[size + 1];
                    foreach (Cell peer in board.GetPeers(cell)) if (peer.Value.HasValue) present[peer.Value.Value] = true;
                    var newCandidates = new HashSet<int>();
                    for (int d = 1; d <= size; d++) if (!present[d]) newCandidates.Add(d);
                    if (!newCandidates.SetEquals(cell.Candidates))
                    {
                        var change = new CellChange { Row = r, Column = c };
                        foreach (int old in new List<int>(cell.Candidates))
                        {
                            if (!newCandidates.Contains(old)) change.RemovedCandidates.Add(old);
                        }
                        cell.Candidates = newCandidates;
                        if (change.RemovedCandidates.Count > 0) result.Changes.Add(change);
                        changed = true;
                    }
                }
            }
            result.Applied = changed;
            if (changed) result.Description = "Updated candidate sets via Hidden Single candidate refresh";
            return result;
        }
    }
}
