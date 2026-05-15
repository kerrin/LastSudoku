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
     */
    public class RightAngleRule : ISudokuRule
    {
        public string Name => "Right Angle";

        public Difficulty Difficulty => Difficulty.Medium;
        public bool CanApply(Board board)
        {
            return FindElimination(board) != null;
        }

        private (Cell e1, Cell e2, int digit, List<Cell> removals)? FindElimination(Board board)
        {
            int size = board.Size;
            int boxesPerRow = size / board.BoxWidth;
            int boxCount = (board.Size / board.BoxWidth) * (board.Size / board.BoxHeight);

            // Candidate-based detection removed.

            // If no candidate-based elimination was found, fall back to values-only
            // deduction: find boxes where three of the four corner cells are filled
            // and the fourth can be deduced by missing digits in box/row/column.
            for (int box = 0; box < boxCount; box++)
            {
                int startBoxRow = (box / boxesPerRow) * board.BoxHeight;
                int startBoxCol = (box % boxesPerRow) * board.BoxWidth;
                int endBoxRow = startBoxRow + board.BoxHeight - 1;
                int endBoxCol = startBoxCol + board.BoxWidth - 1;

                var cornerCells = new List<Cell>
                {
                    board.Cells[startBoxRow, startBoxCol],
                    board.Cells[startBoxRow, endBoxCol],
                    board.Cells[endBoxRow, startBoxCol],
                    board.Cells[endBoxRow, endBoxCol]
                };

                // Collect digits already placed in this box (values-only)
                var placedInBox = new HashSet<int>();
                for (int r = startBoxRow; r <= endBoxRow; r++)
                    for (int c = startBoxCol; c <= endBoxCol; c++)
                    {
                        var cell = board.Cells[r, c];
                        if (cell.Value.HasValue) placedInBox.Add(cell.Value.Value);
                    }

                foreach (var emptyCornerCandidate in cornerCells)
                {
                    if (emptyCornerCandidate.Value.HasValue) continue;

                    // count how many of the other three corners are filled
                    var otherCorners = cornerCells.Where(c => c != emptyCornerCandidate).ToList();
                    if (otherCorners.Count(c => c.Value.HasValue) != 3) continue;

                    // digits missing from box
                    var missingInBox = new HashSet<int>();
                    for (int d = 1; d <= size; d++) if (!placedInBox.Contains(d)) missingInBox.Add(d);

                    // digits missing from the row
                    var missingInRow = new HashSet<int>();
                    var row = emptyCornerCandidate.Row;
                    var presentInRow = new HashSet<int>();
                    for (int cc = 0; cc < size; cc++) if (board.Cells[row, cc].Value.HasValue) presentInRow.Add(board.Cells[row, cc].Value.Value);
                    for (int d = 1; d <= size; d++) if (!presentInRow.Contains(d)) missingInRow.Add(d);

                    // digits missing from the column
                    var missingInCol = new HashSet<int>();
                    var col = emptyCornerCandidate.Column;
                    var presentInCol = new HashSet<int>();
                    for (int rr = 0; rr < size; rr++) if (board.Cells[rr, col].Value.HasValue) presentInCol.Add(board.Cells[rr, col].Value.Value);
                    for (int d = 1; d <= size; d++) if (!presentInCol.Contains(d)) missingInCol.Add(d);

                    // intersection: digits that fit in box, row and column
                    var intersection = missingInBox.Intersect(missingInRow).Intersect(missingInCol).ToList();
                    UnityEngine.Debug.Log($"RightAngle: box {box} corners=({cornerCells[0].Value},{cornerCells[1].Value},{cornerCells[2].Value},{cornerCells[3].Value}) missingInBox={missingInBox.Count} missingInRow={missingInRow.Count} missingInCol={missingInCol.Count} intersection=[{string.Join(',', intersection)}]");
                    if (intersection.Count == 1)
                    {
                        var digit = intersection[0];
                        UnityEngine.Debug.Log($"RightAngle: box {box} emptyCorner ({emptyCornerCandidate.Row},{emptyCornerCandidate.Column}) deduced {digit}");
                        // endpoints: pick two of the filled corners for explanation
                        var filled = otherCorners.Where(c => c.Value.HasValue).ToList();
                        var e1 = filled[0];
                        var e2 = filled.Count > 1 ? filled[1] : null;
                        return (e1, e2, digit, new List<Cell> { emptyCornerCandidate });
                    }
                }
            }
            return null;
        }

        public RuleResult Apply(Board board)
        {
            var found = FindElimination(board);
            var r = new RuleResult();
            if (found == null)
            {
                r.Applied = false;
                return r;
            }
            var (e1, e2, digit, removals) = found.Value;

            // mark endpoints as used for deduction
            if (e1 != null && !r.UsedCells.Exists(u => u.Row == e1.Row && u.Column == e1.Column)) r.UsedCells.Add(new UsedCell { Row = e1.Row, Column = e1.Column });
            if (e2 != null && !r.UsedCells.Exists(u => u.Row == e2.Row && u.Column == e2.Column)) r.UsedCells.Add(new UsedCell { Row = e2.Row, Column = e2.Column });

            foreach (var p in removals)
            {
                // place the digit into the intersection cell
                    if (!p.Value.HasValue)
                    {
                        UnityEngine.Debug.Log($"RightAngle placing digit {digit} into ({p.Row},{p.Column})");
                        var change = new CellChange { Row = p.Row, Column = p.Column, OldValue = p.Value, NewValue = digit };
                        p.Value = digit;
                        r.Changes.Add(change);
                        if (!r.UsedCells.Exists(u => u.Row == p.Row && u.Column == p.Column)) r.UsedCells.Add(new UsedCell { Row = p.Row, Column = p.Column });
                    }
            }
            r.Applied = r.Changes.Count > 0;
            if (r.Applied) r.Description = $"Right-angle placed {digit} into {r.Changes.Count} cell(s)";
            return r;
        }

        public bool UpdateCandidates(Board board)
        {
            return false;
        }

        // (no helper subsets required)
    }
}
