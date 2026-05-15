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

            for (int digit = 1; digit <= size; digit++)
            {
                var allRowPairs = new List<(Cell a, Cell b)>();
                var allColPairs = new List<(Cell a, Cell b)>();

                // gather all row-pairs and col-pairs across every box
                for (int box = 0; box < boxCount; box++)
                {
                    int startBoxRow = (box / boxesPerRow) * board.BoxHeight;
                    int startBoxCol = (box % boxesPerRow) * board.BoxWidth;

                    var boxCells = new List<Cell>();
                    // Get the cells in the current box that contain the digit as a candidate
                    for (int r = startBoxRow; r < startBoxRow + board.BoxHeight; r++)
                        for (int c = startBoxCol; c < startBoxCol + board.BoxWidth; c++)
                        {
                            var cell = board.Cells[r, c];
                            if (!cell.Value.HasValue && cell.Candidates.Contains(digit)) boxCells.Add(cell);
                        }

                    if (boxCells.Count < 2) continue;
                    for (int i = 0; i < boxCells.Count; i++)
                        for (int j = i + 1; j < boxCells.Count; j++)
                        {
                            var a = boxCells[i];
                            var b = boxCells[j];
                            if (a.Row == b.Row) allRowPairs.Add((a, b));
                            if (a.Column == b.Column) allColPairs.Add((a, b));
                        }
                }

                if (allRowPairs.Count == 0 || allColPairs.Count == 0) continue;

                // helpers
                List<Cell> CandidatesInRow(int row)
                {
                    var list = new List<Cell>();
                    for (int cc = 0; cc < size; cc++)
                    {
                        var c = board.Cells[row, cc];
                        if (!c.Value.HasValue && c.Candidates.Contains(digit)) list.Add(c);
                    }
                    return list;
                }

                List<Cell> CandidatesInColumn(int col)
                {
                    var list = new List<Cell>();
                    for (int rr = 0; rr < size; rr++)
                    {
                        var c = board.Cells[rr, col];
                        if (!c.Value.HasValue && c.Candidates.Contains(digit)) list.Add(c);
                    }
                    return list;
                }

                var removals = new List<Cell>();
                (Cell e1, Cell e2) endpoints = (null, null);

                foreach (var rp in allRowPairs)
                {
                    var fullRow = CandidatesInRow(rp.a.Row);
                    if (fullRow.Count != 2) continue;
                    if (!(fullRow.Contains(rp.a) && fullRow.Contains(rp.b))) continue;

                    foreach (var cp in allColPairs)
                    {
                        var fullCol = CandidatesInColumn(cp.a.Column);
                        if (fullCol.Count != 2) continue;
                        if (!(fullCol.Contains(cp.a) && fullCol.Contains(cp.b))) continue;

                        int interRow = rp.a.Row;
                        int interCol = cp.a.Column;
                        var inter = board.Cells[interRow, interCol];
                        if (inter == rp.a || inter == rp.b || inter == cp.a || inter == cp.b) continue;
                        if (!inter.Value.HasValue && inter.Candidates.Contains(digit))
                        {
                            if (!removals.Contains(inter)) removals.Add(inter);
                            if (endpoints.e1 == null) endpoints = (rp.a, cp.a);
                        }
                    }
                }

                if (removals.Count > 0) return (endpoints.e1, endpoints.e2, digit, removals);
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
                    p.Candidates.Clear();
                    r.Changes.Add(change);
                    if (!r.UsedCells.Exists(u => u.Row == p.Row && u.Column == p.Column)) r.UsedCells.Add(new UsedCell { Row = p.Row, Column = p.Column });
                }
            }
            r.Applied = r.Changes.Count > 0;
            if (r.Applied) r.Description = $"Right-angle placed {digit} into {r.Changes.Count} cell(s)";
            return r;
        }

        public RuleResult ApplyOnlyCandidates(Board board)
        {
            return new RuleResult { Applied = false };
        }

        // (no helper subsets required)
    }
}
