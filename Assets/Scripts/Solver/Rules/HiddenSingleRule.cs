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

        private class HiddenSingleResult
        {
            public Cell Cell { get; set; }
            public int Digit { get; set; }
            public UnitKind Unit { get; set; }
            public int UnitIndex { get; set; }
        }
        public bool CanApply(Board board)
        {
            return FindAny(board) != null;
        }

        private HiddenSingleResult FindAny(Board board)
        {
            var rowRes = FindInRows(board);
            if (rowRes != null) return rowRes;
            var colRes = FindInColumns(board);
            if (colRes != null) return colRes;
            var boxRes = FindInBoxes(board);
            if (boxRes != null) return boxRes;
            return null;
        }

        private HiddenSingleResult FindInRows(Board board)
        {
            int size = board.Size;
            for (int digit = 1; digit <= size; digit++)
            {
                for (int r = 0; r < size; r++)
                {
                    var candidates = new List<Cell>();
                    foreach (Cell cell in board.GetRow(r))
                    {
                        if (!cell.Value.HasValue && cell.Candidates.Contains(digit) && cell.Candidates.Count == 1) {
                            return new HiddenSingleResult { Cell = cell, Digit = digit, Unit = UnitKind.Row, UnitIndex = r };
                        }
                    }
                    
                }
            }
            return null;
        }

        private HiddenSingleResult FindInColumns(Board board)
        {
            int size = board.Size;
            for (int digit = 1; digit <= size; digit++)
            {
                for (int c = 0; c < size; c++)
                {
                    var candidates = new List<Cell>();
                    foreach (Cell cell in board.GetColumn(c))
                    {
                        if (!cell.Value.HasValue && cell.Candidates.Contains(digit) && cell.Candidates.Count == 1)
                        {
                            return new HiddenSingleResult { Cell = cell, Digit = digit, Unit = UnitKind.Column, UnitIndex = c };
                        }
                    }
                    
                }
            }
            return null;
        }

        private HiddenSingleResult FindInBoxes(Board board)
        {
            int size = board.Size;
            for (int digit = 1; digit <= size; digit++)
            {
                for (int b = 0; b < size; b++)
                {
                    var candidates = new List<Cell>();
                    foreach (Cell cell in board.GetBox(b))
                    {
                        if (!cell.Value.HasValue && cell.Candidates.Contains(digit) && cell.Candidates.Count == 1) {
                            return new HiddenSingleResult { Cell = cell, Digit = digit, Unit = UnitKind.Box, UnitIndex = b };
                        }
                    }
                }
            }
            return null;
        }

        public RuleResult CalculateChanges(Board board)
        {
            var result = new RuleResult();
            HiddenSingleResult found = FindAny(board);
            if (found == null)
            {
                result.Apply = false;
                return result;
            }
            var cell = found.Cell;
            var digit = found.Digit;
            var unit = found.Unit;
            var unitIndex = found.UnitIndex;
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
                if (!result.UsedCells.Exists(x => x.Row == u.Row && x.Column == u.Column && x.Candidate == digit))
                    result.UsedCells.Add(new UsedCell { Row = u.Row, Column = u.Column, Candidate = digit });
            }

            // Record the placement and peer candidate removals (do not modify board here)
            result.Changes.Add(change);
            foreach (Cell peer in board.GetPeers(cell))
            {
                if (peer.Candidates.Contains(digit))
                {
                    var peerChange = new CellChange { Row = peer.Row, Column = peer.Column };
                    peerChange.RemovedCandidates.Add(digit);
                    result.Changes.Add(peerChange);
                    if (!result.UsedCells.Exists(u => u.Row == peer.Row && u.Column == peer.Column && u.Candidate == digit))
                        result.UsedCells.Add(new UsedCell { Row = peer.Row, Column = peer.Column, Candidate = digit });
                }
            }
            result.Apply = true;
            result.Description = $"Placed {digit} at ({cell.Row},{cell.Column}) via Hidden Single";
            return result;
        }
    }
}
