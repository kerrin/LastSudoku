using System.Collections.Generic;
using Cell = Sudoku.Models.Cell;
using Board = Sudoku.Models.Board;
using UnityEngine;

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

        /**
         * Search for a hidden single in rows. 
         * Returns the first one found, or null if none found.
         */
        private HiddenSingleResult FindInRows(Board board)
        {
            int size = board.Size;
            for (int r = 0; r < size; r++)
            {
                for (int digit = 1; digit <= size; digit++)
                {
                    var candidates = new List<Cell>();
                    foreach (Cell cell in board.GetRow(r))
                    {
                        if (cell.Value.HasValue && cell.Value == digit) { candidates.Clear(); break; } // Digit already placed in this row, skip to next digit.
                        if (!cell.Value.HasValue && cell.Candidates.Contains(digit)) candidates.Add(cell);
                        if (candidates.Count > 1) break; // More than one candidate for this digit in the row, skip to next digit.
                    }
                    if (candidates.Count == 1) return new HiddenSingleResult { Cell = candidates[0], Digit = digit, Unit = UnitKind.Row, UnitIndex = r };
                }
            }
            return null;
        }

        /**
         * Search for a hidden single in columns. 
         * Returns the first one found, or null if none found.
         */
        private HiddenSingleResult FindInColumns(Board board)
        {
            int size = board.Size;
            for (int c = 0; c < size; c++)
            {
                for (int digit = 1; digit <= size; digit++)
                {
                    var candidates = new List<Cell>();
                    foreach (Cell cell in board.GetColumn(c))
                    {
                        if (cell.Value.HasValue && cell.Value == digit) { candidates.Clear(); break; } // Digit already placed in this column, skip to next digit.
                        if (!cell.Value.HasValue && cell.Candidates.Contains(digit)) candidates.Add(cell);
                        if (candidates.Count > 1) break; // More than one candidate for this digit in the column, skip to next digit.
                    }
                    if (candidates.Count == 1) return new HiddenSingleResult { Cell = candidates[0], Digit = digit, Unit = UnitKind.Column, UnitIndex = c };
                }
            }
            return null;
        }

        /**
         * Search for a hidden single in boxes. 
         * Returns the first one found, or null if none found.
         */
        private HiddenSingleResult FindInBoxes(Board board)
        {
            int size = board.Size;
            for (int b = 0; b < size; b++)
            {
                for (int digit = 1; digit <= size; digit++)
                {
                    var candidates = new List<Cell>();
                    foreach (Cell cell in board.GetBox(b))
                    {
                        if (cell.Value.HasValue && cell.Value == digit) { candidates.Clear(); break; } // If the digit is already placed in the box, skip to next digit.
                        if (!cell.Value.HasValue && cell.Candidates.Contains(digit)) candidates.Add(cell);
                        if (candidates.Count > 1) break; // More than one candidate for this digit in the box, skip to next digit. 
                    }
                    if (candidates.Count == 1) return new HiddenSingleResult { Cell = candidates[0], Digit = digit, Unit = UnitKind.Box, UnitIndex = b };
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
            Cell cell = found.Cell;
            int digit = found.Digit;
            UnitKind unit = found.Unit;
            int unitIndex = found.UnitIndex;
            CellChange change = new CellChange { Row = cell.Row, Column = cell.Column, NewValue = digit, RemovedCandidates = RuleExtensions.AllCandidatesExcept(cell, digit) };
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

            // Record the placement and peer candidate removals as consequences of the single deduction.
            result.Changes.Add(change);
            if (!result.UsedCells.Exists(u => u.Row == cell.Row && u.Column == cell.Column && u.Candidate == digit))
                result.UsedCells.Add(new UsedCell { Row = cell.Row, Column = cell.Column, Candidate = digit });

            foreach (Cell peer in board.GetPeers(cell))
            {
                if (peer.Candidates.Contains(digit))
                {
                    var peerChange = new CellChange { Row = peer.Row, Column = peer.Column };
                    peerChange.RemovedCandidates.Add(digit);
                    result.Changes.Add(peerChange);
                }
            }

            result.Apply = true;
            result.Description = $"Placed {digit} at ({cell.Row},{cell.Column}) via Hidden Single";
            return result;
        }
    }
}
