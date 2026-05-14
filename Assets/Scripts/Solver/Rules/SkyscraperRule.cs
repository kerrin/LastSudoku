using System.Collections.Generic;
using System.Linq;
using Sudoku.Models;

namespace Sudoku.Solver.Rules
{
    /**
     * Skyscraper technique (rows/columns variant).
     *
     * For a digit d, find two rows each containing exactly two candidate positions
     * for d and sharing exactly one column. Let the non-shared columns be a and c
     * and the shared column be b. Then the cells at (r1,a) and (r2,c) are the
     * skyscraper endpoints; any cell that sees both endpoints cannot contain d.
     * The same logic is applied symmetrically swapping rows/columns.
     */
    public class SkyscraperRule : ISudokuRule
    {
        public string Name => "Skyscraper";

        public Difficulty Difficulty => Difficulty.Hard;

        public bool CanApply(Board board)
        {
            return FindElimination(board) != null;
        }

        private (Cell endpoint1, Cell endpoint2, int digit, List<Cell> removals)? FindElimination(Board board)
        {
            int size = board.Size;
            // Row-based skyscraper
            for (int digit = 1; digit <= size; digit++)
            {
                // collect rows with at least two candidate columns for digit
                var rows = new List<(int row, List<int> cols)>();
                for (int r = 0; r < size; r++)
                {
                    var cols = new List<int>();
                    for (int c = 0; c < size; c++)
                    {
                        var cell = board.Cells[r, c];
                        if (!cell.Value.HasValue && cell.Candidates.Contains(digit)) cols.Add(c);
                    }
                    if (cols.Count >= 2) rows.Add((r, cols));
                }
                

                for (int i = 0; i < rows.Count; i++)
                {
                    for (int j = i + 1; j < rows.Count; j++)
                    {
                        var r1 = rows[i];
                        var r2 = rows[j];
                        // Consider all pairs of candidate columns in each row (allowing extra candidates)
                        for (int i1 = 0; i1 < r1.cols.Count; i1++)
                        {
                            for (int i2 = i1 + 1; i2 < r1.cols.Count; i2++)
                            {
                                for (int j1 = 0; j1 < r2.cols.Count; j1++)
                                {
                                    for (int j2 = j1 + 1; j2 < r2.cols.Count; j2++)
                                    {
                                        var pair1 = new List<int> { r1.cols[i1], r1.cols[i2] };
                                        var pair2 = new List<int> { r2.cols[j1], r2.cols[j2] };
                                        var shared = pair1.Intersect(pair2).ToList();
                                        if (shared.Count != 1) continue;
                                        int b = shared[0];
                                        int a = pair1.First(c => c != b);
                                        int ccol = pair2.First(c => c != b);

                                        Cell e1 = board.Cells[r1.row, a];
                                        Cell e2 = board.Cells[r2.row, ccol];

                                        var peers1 = new HashSet<Cell>(board.GetPeers(e1));
                                        var peers2 = new HashSet<Cell>(board.GetPeers(e2));
                                        peers1.IntersectWith(peers2);

                                        var removals = new List<Cell>();
                                        foreach (Cell p in peers1)
                                        {
                                            if (p == e1 || p == e2) continue;
                                            if (!p.Value.HasValue && p.Candidates.Contains(digit)) removals.Add(p);
                                        }
                                        if (removals.Count > 0) return (e1, e2, digit, removals);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Column-based skyscraper (transpose)
            for (int digit = 1; digit <= size; digit++)
            {
                var cols = new List<(int col, List<int> rows)>();
                for (int c = 0; c < size; c++)
                {
                    var rs = new List<int>();
                    for (int r = 0; r < size; r++)
                    {
                        var cell = board.Cells[r, c];
                        if (!cell.Value.HasValue && cell.Candidates.Contains(digit)) rs.Add(r);
                    }
                    if (rs.Count >= 2) cols.Add((c, rs));
                }

                for (int i = 0; i < cols.Count; i++)
                    for (int j = i + 1; j < cols.Count; j++)
                    {
                        var c1 = cols[i];
                        var c2 = cols[j];
                        // consider pairs of rows in each column
                        for (int i1 = 0; i1 < c1.rows.Count; i1++)
                            for (int i2 = i1 + 1; i2 < c1.rows.Count; i2++)
                                for (int j1 = 0; j1 < c2.rows.Count; j1++)
                                    for (int j2 = j1 + 1; j2 < c2.rows.Count; j2++)
                                    {
                                        var pair1 = new List<int> { c1.rows[i1], c1.rows[i2] };
                                        var pair2 = new List<int> { c2.rows[j1], c2.rows[j2] };
                                        var shared = pair1.Intersect(pair2).ToList();
                                        if (shared.Count != 1) continue;
                                        int b = shared[0];
                                        int a = pair1.First(r => r != b);
                                        int r2 = pair2.First(r => r != b);

                                        Cell e1 = board.Cells[a, c1.col];
                                        Cell e2 = board.Cells[r2, c2.col];

                                        var peers1 = new HashSet<Cell>(board.GetPeers(e1));
                                        var peers2 = new HashSet<Cell>(board.GetPeers(e2));
                                        peers1.IntersectWith(peers2);

                                        var removals = new List<Cell>();
                                        foreach (Cell p in peers1)
                                        {
                                            if (p == e1 || p == e2) continue;
                                            if (!p.Value.HasValue && p.Candidates.Contains(digit)) removals.Add(p);
                                        }
                                        if (removals.Count > 0) return (e1, e2, digit, removals);
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
            foreach (Cell p in removals)
            {
                if (p.Candidates.Remove(digit))
                {
                    var change = new CellChange { Row = p.Row, Column = p.Column };
                    change.RemovedCandidates.Add(digit);
                    r.Changes.Add(change);
                    if (!r.UsedCells.Exists(u => u.Row == p.Row && u.Column == p.Column)) r.UsedCells.Add(new UsedCell { Row = p.Row, Column = p.Column });
                }
            }
            r.Applied = r.Changes.Count > 0;
            if (r.Applied) r.Description = $"Skyscraper removed {digit} from {r.Changes.Count} cell(s)";
            return r;
        }

        public bool UpdateCandidates(Board board)
        {
            return false;
        }
    }
}
