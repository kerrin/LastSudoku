using System.Collections.Generic;
using System.Linq;
using Sudoku.Models;

namespace Sudoku.Solver.Rules
{
    /**
     * Right-angle rule.
     *
     * For each box and digit, look for a 2x2 corner within the box where three
     * of the four corner cells contain the digit as a candidate. If the fourth
     * corner cell is empty and the corresponding row (outside the box) has no
     * candidates for that digit and the corresponding column (outside the box)
     * has no candidates for that digit, then the digit must be placed in the
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
                for (int box = 0; box < boxCount; box++)
                {
                    int startBoxRow = (box / boxesPerRow) * board.BoxHeight;
                    int startBoxCol = (box % boxesPerRow) * board.BoxWidth;

                    // collect candidate cells in rows and columns inside this box (allow more than two, we'll consider subsets)
                    var rowLinks = new List<(int row, List<Cell> cells)>();
                    for (int r = startBoxRow; r < startBoxRow + board.BoxHeight; r++)
                    {
                        var cells = new List<Cell>();
                        for (int c = startBoxCol; c < startBoxCol + board.BoxWidth; c++)
                        {
                            var cell = board.Cells[r, c];
                            if (!cell.Value.HasValue && cell.Candidates.Contains(digit)) cells.Add(cell);
                        }
                        if (cells.Count >= 2) rowLinks.Add((r, cells));
                    }

                    var colLinks = new List<(int col, List<Cell> cells)>();
                    for (int c = startBoxCol; c < startBoxCol + board.BoxWidth; c++)
                    {
                        var cells = new List<Cell>();
                        for (int r = startBoxRow; r < startBoxRow + board.BoxHeight; r++)
                        {
                            var cell = board.Cells[r, c];
                            if (!cell.Value.HasValue && cell.Candidates.Contains(digit)) cells.Add(cell);
                        }
                        if (cells.Count >= 2) colLinks.Add((c, cells));
                    }

                    // consider any 2-element subsets of the candidate cells in a box row and a box column
                    foreach (var rl in rowLinks)
                        foreach (var cl in colLinks)
                        {
                            var rowPairs = GetTwoElementCellSubsets(rl.cells);
                            var colPairs = GetTwoElementCellSubsets(cl.cells);
                            foreach (var pairR in rowPairs)
                                foreach (var pairC in colPairs)
                                {
                                    foreach (var rCell in pairR)
                                        foreach (var cCell in pairC)
                                        {
                                            if (rCell.Row == cCell.Row || rCell.Column == cCell.Column) continue;
                                            var e1 = rCell;
                                            var e2 = cCell;
                                            var inter = board.Cells[e1.Row, e2.Column];
                                            if (inter == e1 || inter == e2) continue;
                                            if (!inter.Value.HasValue && inter.Candidates.Contains(digit))
                                            {
                                                   // do not use link pairs that include the intersection cell itself
                                                   if (pairR.Contains(inter) || pairC.Contains(inter)) continue;

                                                // require both selected link pairs to be explicit strong-links (both cells single-candidate)
                                                bool bothExplicit = pairR.All(x => x.Candidates.Count == 1) && pairC.All(x => x.Candidates.Count == 1);
                                                if (!bothExplicit) continue;
                                                    UnityEngine.Debug.Log($"RightAngle: endpoints ({e1.Row},{e1.Column}) & ({e2.Row},{e2.Column}) -> remove ({inter.Row},{inter.Column}) for digit {digit}");
                                                    var removals = new List<Cell> { inter };
                                                    return (e1, e2, digit, removals);
                                            }
                                        }
                                }
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
            if (!r.UsedCells.Exists(u => u.Row == e1.Row && u.Column == e1.Column)) r.UsedCells.Add(new UsedCell { Row = e1.Row, Column = e1.Column });
            if (!r.UsedCells.Exists(u => u.Row == e2.Row && u.Column == e2.Column)) r.UsedCells.Add(new UsedCell { Row = e2.Row, Column = e2.Column });

            foreach (var p in removals)
            {
                if (p.Candidates.Contains(digit))
                {
                    UnityEngine.Debug.Log($"RightAngle removing digit {digit} from ({p.Row},{p.Column})");
                    p.Candidates.Clear();
                    var change = new CellChange { Row = p.Row, Column = p.Column };
                    change.RemovedCandidates.Add(digit);
                    r.Changes.Add(change);
                    if (!r.UsedCells.Exists(u => u.Row == p.Row && u.Column == p.Column)) r.UsedCells.Add(new UsedCell { Row = p.Row, Column = p.Column });
                }
            }
            r.Applied = r.Changes.Count > 0;
            if (r.Applied) r.Description = $"Right-angle removed {digit} from {r.Changes.Count} cell(s)";
            return r;
        }

        public bool UpdateCandidates(Board board)
        {
            return false;
        }

        // Helper: return all 2-element subsets of a list of cells
        private static List<List<Cell>> GetTwoElementCellSubsets(List<Cell> items)
        {
            var res = new List<List<Cell>>();
            if (items == null) return res;
            if (items.Count == 2)
            {
                res.Add(new List<Cell> { items[0], items[1] });
                return res;
            }
            for (int i = 0; i < items.Count; i++)
                for (int j = i + 1; j < items.Count; j++)
                    res.Add(new List<Cell> { items[i], items[j] });
            return res;
        }
    }
}
