using System.Collections.Generic;
using System.Linq;
using Sudoku.Models;

namespace Sudoku.Solver.Rules
{
    /**
     * Right-angle rule.
     *
     * For each box and digit, look for a 2x2 corner within the box where three
     * of the four corner cells contain the digit. If the fourth
     * corner cell has no digit and the row and column in the box not part of the corner
     * have a single digit that is not in the courner box, 
     * then that digit must be placed in the
     * fourth corner of the 2x2 and can be assigned.
     *
     * Another way of describing it:
     *    The 3 digits in a right angle in the same box
     *    And the row and column not in the right angle cells for a digit that is:
     *       Not in 3 the right angle digits
     *       Appears in both the row and column
     */
    public class RightAngleRule : ISudokuRule
    {
        public string Name => "Right Angle";

        public Difficulty Difficulty => Difficulty.Medium;
        public bool CanApply(Board board)
        {
            // Avoid firing on a pristine board where every empty cell still contains
            // the full set of candidates (1..Size). Only run if some candidate
            // pruning has already occurred.
            int size = board.Size;
            bool anyPruned = false;
            for (int r = 0; r < size && !anyPruned; r++)
                for (int c = 0; c < size; c++)
                {
                    var cell = board.Cells[r, c];
                    if (!cell.Value.HasValue && cell.Candidates.Count != size)
                    {
                        anyPruned = true;
                        break;
                    }
                }
            if (!anyPruned) return false;

            return FindElimination(board) != null;
        }

        public RuleResult CalculateChanges(Board board)
        {
            var info = FindElimination(board);
            var result = new RuleResult();
            if (info == null)
            {
                result.Apply = false;
                return result;
            }

            foreach (Cell used in info.UsedCells)
            {
                if (!result.UsedCells.Exists(u => u.Row == used.Row && u.Column == used.Column && u.Candidate == info.digit))
                    result.UsedCells.Add(new UsedCell { Row = used.Row, Column = used.Column, Candidate = info.digit });
            }
            var change = new CellChange { Row = info.DeducedCell.Row, Column = info.DeducedCell.Column, NewValue = info.digit, RemovedCandidates = RuleExtensions.AllCandidatesExcept(info.DeducedCell, info.digit) };
            
            result.Changes.Add(change);
            result.Apply = true;
            result.Description = $"Right-angle placed {info.digit} into {change.Row},{change.Column} cell";
            return result;
        }

        private class ElimInfo
        {
            public int digit;
            public List<Cell> UsedCells = new List<Cell>();
            public Cell DeducedCell = new Cell();
            public string Description;
        }
        private ElimInfo FindElimination(Board board)
        {
            ElimInfo result = new ElimInfo();
            int size = board.Size;
            int boxesPerRow = size / board.BoxWidth;
            int boxCount = (board.Size / board.BoxWidth) * (board.Size / board.BoxHeight);

            // For each box, scan every 2x2 corner inside the box. If exactly three
            // of the four corner cells are set (have values) and one is empty,
            // then for any digit not among those three values, if that digit
            // appears as a candidate somewhere in the other row (outside the box)
            // and somewhere in the other column (outside the box), we can place
            // that digit into the empty corner.
            // Where other row/column means the row/column of the cells not in the 2x2 corner.
            for (int box = 0; box < boxCount; box++)
            {
                int startBoxRow = (box / boxesPerRow) * board.BoxHeight;
                int startBoxCol = (box % boxesPerRow) * board.BoxWidth;

                // iterate top-left cell of each 2x2 sub-block inside the box
                for (int r0 = startBoxRow; r0 <= startBoxRow + board.BoxHeight - 2; r0++)
                {
                    for (int c0 = startBoxCol; c0 <= startBoxCol + board.BoxWidth - 2; c0++)
                    {
                        var a = board.Cells[r0, c0];
                        var b = board.Cells[r0, c0 + 1];
                        var c = board.Cells[r0 + 1, c0];
                        var d = board.Cells[r0 + 1, c0 + 1];
                        var quad = new[] { a, b, c, d };

                        // count placed values and identify the empty cell
                        int placedCount = 0;
                        var placedValues = new HashSet<int>();
                        Cell empty = null;
                        foreach (var cell in quad)
                        {
                            if (cell.Value.HasValue)
                            {
                                placedCount++;
                                placedValues.Add(cell.Value.Value);
                            }
                            else
                            {
                                empty = cell;
                            }
                        }

                        if (placedCount != 3 || empty == null) continue;
                        // Calculate the row and the column of the box that doesn't contain the right angle (the "other" row and column)
                        var boxObj = board.GetBox(box);
                        var quadRows = quad.Select(cell => cell.Row).ToHashSet();
                        var rowInBox = boxObj.Where(cell => !quadRows.Contains(cell.Row)).Select(cell => cell.Row).First();
                        var quadCols = quad.Select(cell => cell.Column).ToHashSet();
                        var colInBox = boxObj.Where(cell => !quadCols.Contains(cell.Column)).Select(cell => cell.Column).First();

                        // Consider digits not among the three placed values
                        for (int digit = 1; digit <= size; digit++)
                        {
                            if (placedValues.Contains(digit)) continue;

                            // Skip digits already present anywhere in the box
                            if (boxObj.Any(cell => cell.Value.HasValue && cell.Value.Value == digit)) continue;

                            // check row for a set value of the digit outside this box
                            // Require a placed digit in the same row but not inside the box.
                            bool rowHas = false;
                            for (int cc = 0; cc < size; cc++)
                            {
                                if (cc >= startBoxCol && cc < startBoxCol + board.BoxWidth) continue;
                                var cell = board.Cells[rowInBox, cc];
                                if (cell.Value.HasValue && cell.Value == digit) { rowHas = true; break; }
                            }

                            if (!rowHas) continue;

                            // check column for a set value of the digit outside this box
                            bool colHas = false;
                            for (int rr = 0; rr < size; rr++)
                            {
                                if (rr >= startBoxRow && rr < startBoxRow + board.BoxHeight) continue;
                                var cell = board.Cells[rr, colInBox];
                                if (cell.Value.HasValue && cell.Value == digit) { colHas = true; break; }
                            }

                            if (!colHas) continue;

                            result.digit = digit;
                            result.DeducedCell = empty;
                            // Endpoint cells: the placed cell in the empty's row within
                            // the box. Find them and return as endpoints.
                            foreach (var cell in quad)
                            {
                                if (cell.Row == empty.Row && cell.Value.HasValue) result.UsedCells.Add(cell);
                                if (cell.Column == empty.Column && cell.Value.HasValue) result.UsedCells.Add(cell);
                                if (cell.Row != empty.Row && cell.Column != empty.Column && cell.Value.HasValue) result.UsedCells.Add(cell);
                            }
                            // Now mark the used cells in the row and column outside the box as well (for UI highlighting)
                            for (int cc = 0; cc < size; cc++)                            {
                                var cell = board.Cells[rowInBox, cc];
                                if (cell.Value.HasValue && cell.Value == digit && !result.UsedCells.Exists(u => u.Row == cell.Row && u.Column == cell.Column)) result.UsedCells.Add(cell);
                            }

                            for (int rr = 0; rr < size; rr++)
                            {
                                var cell = board.Cells[rr, colInBox];
                                if (cell.Value.HasValue && cell.Value == digit && !result.UsedCells.Exists(u => u.Row == cell.Row && u.Column == cell.Column)) result.UsedCells.Add(cell);
                            }

                            return result;
                        }
                    }
                }
            }
            return null;
        }
    }
}
