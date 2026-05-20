using System.Collections.Generic;
using System.Linq;
using Sudoku.Models;

namespace Sudoku.Solver.Rules
{
    /**
     * X-Wing technique (rows/columns variant).
     *
     * For a digit d, find two rows each containing exactly two candidate positions
     * for d and those candidate columns are identical for both rows. Then any other
     * cell in those two columns, rows or boxes cannot contain d and
     * the candidate can be removed.
     */
    public class XWingRule : ISudokuRule
    {
        public string Name => "X-Wing";

        public Difficulty Difficulty => Difficulty.Hard;

        public bool CanApply(Board board)
        {
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

        private (int digit, List<(int r,int c)> witnesses, List<(int r,int c)> removals)? FindElimination(Board board)
        {
            int size = board.Size;

            // Determine digits order by global candidate count to be deterministic
            var digitCounts = new Dictionary<int, int>();
            for (int d = 1; d <= size; d++)
            {
                int cnt = 0;
                for (int r = 0; r < size; r++)
                    for (int c = 0; c < size; c++)
                        if (!board.Cells[r, c].Value.HasValue && board.Cells[r, c].Candidates.Contains(d)) cnt++;
                digitCounts[d] = cnt;
            }
            var digitsOrder = digitCounts.OrderBy(kv => kv.Value).ThenBy(kv => kv.Key).Select(kv => kv.Key).ToList();

            // Row-based X-Wing: find two rows with exactly the same two candidate columns
            foreach (int digit in digitsOrder)
            {
                var rowsWithCols = new List<(int row, List<int> cols)>();
                for (int r = 0; r < size; r++)
                {
                    var cols = new List<int>();
                    for (int c = 0; c < size; c++)
                    {
                        var cell = board.Cells[r, c];
                        if (!cell.Value.HasValue && cell.Candidates.Contains(digit)) cols.Add(c);
                    }
                    if (cols.Count == 2) rowsWithCols.Add((r, cols));
                }

                for (int i = 0; i < rowsWithCols.Count; i++)
                    for (int j = i + 1; j < rowsWithCols.Count; j++)
                    {
                        var r1 = rowsWithCols[i];
                        var r2 = rowsWithCols[j];
                        // identical column sets
                        if (r1.cols[0] == r2.cols[0] && r1.cols[1] == r2.cols[1])
                        {
                            int c1 = r1.cols[0];
                            int c2 = r1.cols[1];
                            var removals = new List<(int r,int c)>();
                            for (int r = 0; r < size; r++)
                            {
                                if (r == r1.row || r == r2.row) continue;
                                var cell1 = board.Cells[r, c1];
                                var cell2 = board.Cells[r, c2];
                                if (!cell1.Value.HasValue && cell1.Candidates.Contains(digit)) removals.Add((r, c1));
                                if (!cell2.Value.HasValue && cell2.Candidates.Contains(digit)) removals.Add((r, c2));
                            }
                            // Also consider eliminations inside the boxes that contain the witnesses.
                            // For each of the four witness boxes, eliminate the digit from cells
                            // that are not in the two witness rows/columns and are not the witnesses themselves.
                            var boxWitnesses = new List<(int r,int c)>{(r1.row,c1),(r1.row,c2),(r2.row,c1),(r2.row,c2)};
                            var boxesSeen = new HashSet<int>();
                            foreach (var w in boxWitnesses)
                            {
                                int box = Sudoku.Models.Cell.ComputeBox(w.r, w.c);
                                if (boxesSeen.Contains(box)) continue;
                                boxesSeen.Add(box);
                                foreach (var bc in board.GetBox(box))
                                {
                                    if (bc.Row == w.r && (bc.Column == w.c)) continue;
                                    // skip the two witness rows and two witness columns
                                    if (bc.Row == r1.row || bc.Row == r2.row) continue;
                                    if (bc.Column == c1 || bc.Column == c2) continue;
                                    if (!bc.Value.HasValue && bc.Candidates.Contains(digit)) removals.Add((bc.Row, bc.Column));
                                }
                            }
                            if (removals.Count > 0)
                            {
                                var witnesses = new List<(int r,int c)>{(r1.row,c1),(r1.row,c2),(r2.row,c1),(r2.row,c2)};
                                return (digit, witnesses, removals);
                            }
                        }
                    }
            }

            // Column-based X-Wing (transpose): find two columns with identical pair of rows
            foreach (int digit in digitsOrder)
            {
                var colsWithRows = new List<(int col, List<int> rows)>();
                for (int c = 0; c < size; c++)
                {
                    var rows = new List<int>();
                    for (int r = 0; r < size; r++)
                    {
                        var cell = board.Cells[r, c];
                        if (!cell.Value.HasValue && cell.Candidates.Contains(digit)) rows.Add(r);
                    }
                    if (rows.Count == 2) colsWithRows.Add((c, rows));
                }

                for (int i = 0; i < colsWithRows.Count; i++)
                    for (int j = i + 1; j < colsWithRows.Count; j++)
                    {
                        var c1 = colsWithRows[i];
                        var c2 = colsWithRows[j];
                        if (c1.rows[0] == c2.rows[0] && c1.rows[1] == c2.rows[1])
                        {
                            int r1 = c1.rows[0];
                            int r2 = c1.rows[1];
                            var removals = new List<(int r,int c)>();
                            for (int c = 0; c < size; c++)
                            {
                                if (c == c1.col || c == c2.col) continue;
                                var cell1 = board.Cells[r1, c];
                                var cell2 = board.Cells[r2, c];
                                if (!cell1.Value.HasValue && cell1.Candidates.Contains(digit)) removals.Add((r1, c));
                                if (!cell2.Value.HasValue && cell2.Candidates.Contains(digit)) removals.Add((r2, c));
                            }
                            // Also consider eliminations inside the boxes that contain the witnesses.
                            var boxWitnesses = new List<(int r,int c)>{(r1,c1.col),(r1,c2.col),(r2,c1.col),(r2,c2.col)};
                            var boxesSeen = new HashSet<int>();
                            foreach (var w in boxWitnesses)
                            {
                                int box = Sudoku.Models.Cell.ComputeBox(w.r, w.c);
                                if (boxesSeen.Contains(box)) continue;
                                boxesSeen.Add(box);
                                foreach (var bc in board.GetBox(box))
                                {
                                    if (bc.Column == w.c && bc.Row == w.r) continue;
                                    // skip the two witness columns and two witness rows
                                    if (bc.Column == c1.col || bc.Column == c2.col) continue;
                                    if (bc.Row == r1 || bc.Row == r2) continue;
                                    if (!bc.Value.HasValue && bc.Candidates.Contains(digit)) removals.Add((bc.Row, bc.Column));
                                }
                            }
                            if (removals.Count > 0)
                            {
                                var witnesses = new List<(int r,int c)>{(r1,c1.col),(r1,c2.col),(r2,c1.col),(r2,c2.col)};
                                return (digit, witnesses, removals);
                            }
                        }
                    }
            }

            return null;
        }

        public RuleResult CalculateChanges(Board board)
        {
            var found = FindElimination(board);
            var r = new RuleResult();
            if (found == null)
            {
                r.Apply = false;
                return r;
            }
            var (digit, witnesses, removals) = found.Value;

            // mark the four witness cells (deduplicated) and only them as used
            var uniqueWitnesses = witnesses.Distinct().ToList();
            if (uniqueWitnesses.Count == 4)
            {
                foreach (var w in uniqueWitnesses)
                {
                    if (!r.UsedCells.Exists(u => u.Row == w.r && u.Column == w.c && u.Candidate == digit))
                        r.UsedCells.Add(new UsedCell { Row = w.r, Column = w.c, Candidate = digit });
                }
            }

            // Record removals (do NOT mark removed cells as used; only witnesses are used/highlighted)
            foreach (var rem in removals.OrderBy(x => x.r).ThenBy(x => x.c))
            {
                var cell = board.Cells[rem.r, rem.c];
                if (!cell.Value.HasValue && cell.Candidates.Contains(digit))
                {
                    var change = new CellChange { Row = rem.r, Column = rem.c };
                    change.RemovedCandidates.Add(digit);
                    r.Changes.Add(change);
                }
            }

            r.Apply = r.Changes.Count > 0;
            if (r.Apply) r.Description = $"X-Wing removed {digit} from {r.Changes.Count} cell(s)";
            return r;
        }
    }
}
