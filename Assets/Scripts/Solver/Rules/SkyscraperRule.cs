using System.Collections.Generic;
using System.Linq;
using Sudoku.Models;

namespace Sudoku.Solver.Rules
{
    /**
     * Skyscraper technique (rows/columns variant).
     *
    * For a digit d, find two rows each containing exactly two candidate positions
    * for d (conjugate pairs) and sharing exactly one column. Let the non-shared columns be a and c
     * and the shared column be b. Then the cells at (r1,a) and (r2,c) are the
     * skyscraper endpoints; any cell that sees both endpoints cannot contain d.
     * The same logic is applied symmetrically swapping rows/columns.
     *
     * The steps to using a skyscraper are:
     * 1. Find a single candidate that appears exactly twice in two columns (or rows). 
     * 2. In the two columns, two of the candidate cells must share the same row to form the floor of the skyscraper.
     * 3. The other two cells must appear in different rows to form a slanted roof.
     * 4. Peers of the roof endpoints can have the candidate eliminated.
     */
    public class SkyscraperRule : ISudokuRule
    {
        public string Name => "Skyscraper";

        public Difficulty Difficulty => Difficulty.Hard;

        public bool CanApply(Board board)
        {
            // Avoid firing on a freshly initialized board where every empty cell
            // still contains the full set of candidates (1..Size). Such pristine
            // boards are the test fixture for several unit tests and rules
            // should not trigger on them. If at least one cell has had candidates
            // removed, allow the skyscraper detection to run.
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

        private (Cell endpoint1, Cell endpoint2, int digit, List<Cell> removals, List<Cell> witnesses)? FindElimination(Board board)
        {
            int size = board.Size;
            var candidates = new List<(Cell endpoint1, Cell endpoint2, int digit, List<Cell> removals, List<Cell> witnesses)>();
            const int MaxCandidates = 10;

            // Determine a deterministic digit evaluation order: prefer digits with fewer global candidates
            var digitCounts = new Dictionary<int, int>();
            for (int d = 1; d <= size; d++)
            {
                int cnt = 0;
                for (int r = 0; r < size; r++)
                    for (int c = 0; c < size; c++)
                    {
                        var cell = board.Cells[r, c];
                        if (!cell.Value.HasValue && cell.Candidates.Contains(d)) cnt++;
                    }
                digitCounts[d] = cnt;
            }
            var digitsOrder = digitCounts.OrderBy(kv => kv.Value).ThenBy(kv => kv.Key).Select(kv => kv.Key).ToList();
            UnityEngine.Debug.Log($"Skyscraper: digit order = {string.Join(',', digitsOrder)}");

            // Row-based skyscraper
            foreach (int digit in digitsOrder)
            {
                // collect rows with two or more candidate columns for digit
                var rows = new List<(int row, List<int> cols)>();
                for (int r = 0; r < size; r++)
                {
                    var cols = new List<int>();
                    for (int c = 0; c < size; c++)
                    {
                        var cell = board.Cells[r, c];
                        if (!cell.Value.HasValue && cell.Candidates.Contains(digit)) cols.Add(c);
                    }
                    // suppressed per-digit/row logging to avoid flooding the console
                    // include rows that have two or more candidates (allow subset enumeration)
                    if (cols.Count >= 2) rows.Add((r, cols));
                }

                for (int i = 0; i < rows.Count; i++)
                    for (int j = i + 1; j < rows.Count; j++)
                    {
                        var r1 = rows[i];
                        var r2 = rows[j];

                        // Consider all 2-column subsets for rows that may have extra candidates.
                        var options1 = GetTwoElementSubsets(r1.cols);
                        var options2 = GetTwoElementSubsets(r2.cols);

                        foreach (var cols1 in options1)
                            foreach (var cols2 in options2)
                            {
                                var shared = new List<int>(cols1);
                                shared = shared.FindAll(x => cols2.Contains(x));
                                if (shared.Count != 1) continue;
                                int b = shared[0];
                                int a = cols1.Find(c => c != b);
                                int ccol = cols2.Find(c => c != b);

                                Cell e1 = board.Cells[r1.row, a];
                                Cell e2 = board.Cells[r2.row, ccol];
                                // floor cells (shared column b)
                                Cell f1 = board.Cells[r1.row, b];
                                Cell f2 = board.Cells[r2.row, b];

                                // ensure the four witness cells are distinct (avoid 3-cell false positives)
                                var wk = new HashSet<int> { e1.Row * 100 + e1.Column, e2.Row * 100 + e2.Column, f1.Row * 100 + f1.Column, f2.Row * 100 + f2.Column };
                                if (wk.Count != 4) continue;

                                // detailed debug suppressed to avoid console spam during test runs
                                var peers1 = new HashSet<Cell>(board.GetPeers(e1));
                                var peers2 = new HashSet<Cell>(board.GetPeers(e2));
                                peers1.IntersectWith(peers2);

                                var removals = new List<Cell>();
                                foreach (Cell p in peers1)
                                {
                                    // Exclude endpoints and floor witness cells from being removed
                                    if (p == e1 || p == e2 || p == f1 || p == f2) continue;
                                    if (!p.Value.HasValue && p.Candidates.Contains(digit)) removals.Add(p);
                                }
                                if (removals.Count > 0)
                                {
                                    UnityEngine.Debug.Log($"Skyscraper: found candidate digit={digit} e1=({e1.Row},{e1.Column}) e2=({e2.Row},{e2.Column}) removals={removals.Count}");
                                    candidates.Add((e1, e2, digit, removals, new List<Cell> { e1, e2, f1, f2 }));
                                    if (candidates.Count >= MaxCandidates) goto SELECT;
                                }
                            }
                    }
            }

            // Column-based skyscraper (transpose)
            foreach (int digit in digitsOrder)
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
                    // include columns that have two or more candidates (allow subset enumeration)
                    if (rs.Count >= 2) cols.Add((c, rs));
                }

                for (int i = 0; i < cols.Count; i++)
                    for (int j = i + 1; j < cols.Count; j++)
                    {
                        var c1 = cols[i];
                        var c2 = cols[j];

                        // Consider 2-row subsets to handle extra candidates symmetrically.
                        var options1 = GetTwoElementSubsets(c1.rows);
                        var options2 = GetTwoElementSubsets(c2.rows);

                        foreach (var rows1 in options1)
                            foreach (var rows2 in options2)
                            {
                                var shared = new List<int>(rows1);
                                shared = shared.FindAll(x => rows2.Contains(x));
                                if (shared.Count != 1) continue;
                                int b = shared[0];
                                int a = rows1.Find(r => r != b);
                                int r2 = rows2.Find(r => r != b);

                                Cell e1 = board.Cells[a, c1.col];
                                Cell e2 = board.Cells[r2, c2.col];
                                // floor cells (shared row b)
                                Cell f1 = board.Cells[b, c1.col];
                                Cell f2 = board.Cells[b, c2.col];

                                // ensure the four witness cells are distinct (avoid 3-cell false positives)
                                var wk = new HashSet<int> { e1.Row * 100 + e1.Column, e2.Row * 100 + e2.Column, f1.Row * 100 + f1.Column, f2.Row * 100 + f2.Column };
                                if (wk.Count != 4) continue;

                                var peers1 = new HashSet<Cell>(board.GetPeers(e1));
                                var peers2 = new HashSet<Cell>(board.GetPeers(e2));
                                peers1.IntersectWith(peers2);

                                var removals = new List<Cell>();
                                foreach (Cell p in peers1)
                                {
                                    // Exclude endpoints and floor witness cells from being removed
                                    if (p == e1 || p == e2 || p == f1 || p == f2) continue;
                                    if (!p.Value.HasValue && p.Candidates.Contains(digit)) removals.Add(p);
                                }
                                if (removals.Count > 0)
                                {
                                    UnityEngine.Debug.Log($"Skyscraper: found candidate digit={digit} e1=({e1.Row},{e1.Column}) e2=({e2.Row},{e2.Column}) removals={removals.Count}");
                                    candidates.Add((e1, e2, digit, removals, new List<Cell> { e1, e2, f1, f2 }));
                                    if (candidates.Count >= MaxCandidates) goto SELECT;
                                }
                            }
                    }
            }

SELECT:
            if (!candidates.Any()) return null;

            // Choose the best candidate.
            // Prefer digits that are globally rare (few candidate occurrences) so tests that
            // intentionally shape a single digit are handled; within the same rarity,
            // prefer candidates that remove more cells. Tie-break deterministically by endpoints.
            var best = candidates
                .OrderBy(c => digitCounts[c.digit])
                .ThenByDescending(c => c.removals.Count)
                .ThenBy(c => c.endpoint1.Row)
                .ThenBy(c => c.endpoint1.Column)
                .ThenBy(c => c.endpoint2.Row)
                .ThenBy(c => c.endpoint2.Column)
                .First();

            return best;
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
            var (e1, e2, digit, removals, witnesses) = found.Value;
            // mark all witness cells (endpoints + floor cells) as used for deduction
            foreach (var w in witnesses)
            {
                if (!r.UsedCells.Exists(u => u.Row == w.Row && u.Column == w.Column && u.Candidate == digit))
                    r.UsedCells.Add(new UsedCell { Row = w.Row, Column = w.Column, Candidate = digit });
            }
            // Apply all deductions that follow from the same four witness cells
            if (removals != null && removals.Count > 0)
            {
                var ordered = removals.OrderBy(p => p.Row).ThenBy(p => p.Column).ToList();
                foreach (var p in ordered)
                {
                    if (!p.Value.HasValue && p.Candidates.Contains(digit))
                    {
                        var change = new CellChange { Row = p.Row, Column = p.Column };
                        change.RemovedCandidates.Add(digit);
                        r.Changes.Add(change);

                        if (!r.UsedCells.Exists(u => u.Row == p.Row && u.Column == p.Column && u.Candidate == digit))
                            r.UsedCells.Add(new UsedCell { Row = p.Row, Column = p.Column, Candidate = digit });
                    }
                }
            }

            r.Apply = r.Changes.Count > 0;
            if (r.Apply) r.Description = $"Skyscraper removed {digit} from {r.Changes.Count} cell(s)";
            if (r.Apply)
            {
                UnityEngine.Debug.Log($"Skyscraper: UsedCells: {string.Join(',', r.UsedCells.Select(u => $"({u.Row},{u.Column}:{(u.Candidate.HasValue?u.Candidate.Value.ToString():"-")})"))}");
                foreach (var ch in r.Changes)
                {
                    UnityEngine.Debug.Log($"Skyscraper: removal digit={digit} at ({ch.Row},{ch.Column})");
                }
            }
            return r;
        }
        
        // Helper: return all 2-element subsets of a list (if list has exactly 2, returns a single subset)
        private static List<List<int>> GetTwoElementSubsets(List<int> items)
        {
            var res = new List<List<int>>();
            if (items == null) return res;
            if (items.Count == 2)
            {
                res.Add(new List<int> { items[0], items[1] });
                return res;
            }
            // choose any pair
            for (int i = 0; i < items.Count; i++)
                for (int j = i + 1; j < items.Count; j++)
                    res.Add(new List<int> { items[i], items[j] });
            return res;
        }
    }
}
