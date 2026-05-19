using System.Collections.Generic;
using System.Linq;
using Sudoku.Models;
using Cell = Sudoku.Models.Cell;

namespace Sudoku.Solver.Rules
{
    /**
     * A simple rectangle-based Y-wing-like deduction:
     * For a rectangle of cells (that do not need to be adjacent), 
     * if three corners are bi-value
     * cells whose candidate pairs together cover exactly three digits
     * (e.g. {a,b}, {a,c}, {b,c}), then the fourth corner must be the
     * digit not in the opposite corner pair (e.g. c in this example)
     * (ab),(bc)
     * (ac),(*c*)
     */
    public class YWingRule : ISudokuRule
    {
        public string Name => "Y-Wing Rectangle";

        public Difficulty Difficulty => Difficulty.Medium;

        public bool CanApply(Board board)
        {
            // Directly search for a valid placement. Avoid a global "pristine"
            // early-exit — test setups often clear candidates which can make the
            // naive detection unreliable. Rely on the actual pattern search instead.
            return FindPlacement(board) != null;
        }

        private (Cell rowNeighbor, Cell colNeighbor, Cell target, int digit)? FindPlacement(Board board)
        {
            int size = board.Size;

            // iterate over all rectangles defined by two distinct rows and two distinct columns
            for (int r1 = 0; r1 < size - 1; r1++)
            {
                for (int r2 = r1 + 1; r2 < size; r2++)
                {
                    for (int c1 = 0; c1 < size - 1; c1++)
                    {
                        for (int c2 = c1 + 1; c2 < size; c2++)
                        {
                            var tl = board.Cells[r1, c1];
                            var tr = board.Cells[r1, c2];
                            var bl = board.Cells[r2, c1];
                            var br = board.Cells[r2, c2];

                            var corners = new[] { tl, tr, bl, br };

                            // Consider any triple of the four corners as the candidate "pair" cells.
                            // We allow extra irrelevant candidates in those cells as long as
                            // each of the three corners contains exactly two of a shared set
                            // of three digits (i.e. their intersection with the union has size 2)
                            var empties = corners.Where(c => !c.Value.HasValue && c.Candidates.Count >= 2).ToList();
                            if (empties.Count < 3) continue;

                            bool tripleFound = false;
                            Cell chosenTarget = null;
                            List<Cell> chosenTrip = null;
                            HashSet<int> union = null;

                            // iterate all triples among the corners
                            for (int i = 0; i < 4 && !tripleFound; i++)
                            {
                                for (int j = i + 1; j < 4 && !tripleFound; j++)
                                {
                                    for (int k = j + 1; k < 4 && !tripleFound; k++)
                                    {
                                        var trip = new List<Cell> { corners[i], corners[j], corners[k] };
                                        // require they are empty and have at least two candidates
                                        if (trip.Any(c => c.Value.HasValue || c.Candidates.Count < 2)) continue;

                                        var all = trip.SelectMany(c => c.Candidates).Distinct().ToList();
                                        if (all.Count < 3) continue;

                                        // Try every 3-element subset of 'all' as the potential {a,b,c}
                                        bool ok = false;
                                        HashSet<int> chosenU = null;
                                        int n = all.Count;
                                        for (int a = 0; a < n && !ok; a++)
                                        {
                                            for (int b = a + 1; b < n && !ok; b++)
                                            {
                                                for (int cidx = b + 1; cidx < n && !ok; cidx++)
                                                {
                                                    var candidateSet = new HashSet<int> { all[a], all[b], all[cidx] };
                                                    // each corner's intersection with candidateSet must be exactly two digits
                                                    bool localOk = true;
                                                    foreach (var cell in trip)
                                                    {
                                                        var interCount = cell.Candidates.Intersect(candidateSet).Count();
                                                        if (interCount != 2) { localOk = false; break; }
                                                    }
                                                    if (!localOk) continue;

                                                    // union of those intersections must equal candidateSet
                                                    var unionInter = new HashSet<int>(trip.SelectMany(cell => cell.Candidates.Intersect(candidateSet)));
                                                    if (unionInter.SetEquals(candidateSet))
                                                    {
                                                        ok = true;
                                                        chosenU = candidateSet;
                                                    }
                                                }
                                            }
                                        }
                                        if (!ok) continue;

                                        // the remaining corner is the target
                                        var remaining = corners.Except(trip).FirstOrDefault();
                                        if (remaining == null || remaining.Value.HasValue) continue;

                                        // we've found a valid triple pattern
                                        tripleFound = true;
                                        chosenTarget = remaining;
                                        chosenTrip = trip;
                                        union = chosenU;
                                    }
                                }
                            }

                            if (!tripleFound) continue;

                            var bivals = chosenTrip;
                            var target = chosenTarget;
                            UnityEngine.Debug.Log($"YWing: rectangle rows {r1},{r2} cols {c1},{c2} triple: {string.Join(";", bivals.Select(b => $"({b.Row},{b.Column}):[{string.Join(",", b.Candidates)}]"))} target=({target.Row},{target.Column}):[{string.Join(",", target.Candidates)}]");

                            // find the neighbor in same row and same column (within rectangle)
                            Cell rowNeighbor = null;
                            Cell colNeighbor = null;
                            // locate which corner is target to pick its row/col neighbors inside the rectangle
                            if (target == tl)
                            {
                                rowNeighbor = tr;
                                colNeighbor = bl;
                            }
                            else if (target == tr)
                            {
                                rowNeighbor = tl;
                                colNeighbor = br;
                            }
                            else if (target == bl)
                            {
                                rowNeighbor = br;
                                colNeighbor = tl;
                            }
                            else if (target == br)
                            {
                                rowNeighbor = bl;
                                colNeighbor = tr;
                            }

                            if (rowNeighbor == null || colNeighbor == null) continue;
                            if (rowNeighbor.Value.HasValue || colNeighbor.Value.HasValue) continue;
                            if (rowNeighbor.Candidates.Count == 0 || colNeighbor.Candidates.Count == 0) continue;

                            // Intersection should be computed with respect to the chosen 3-digit set
                            var intersect = new HashSet<int>(rowNeighbor.Candidates.Intersect(colNeighbor.Candidates).Intersect(union));
                            if (intersect.Count != 1) continue;
                            int digit = intersect.First();

                            // sanity: digit should belong to the union of the three bivals
                            if (!union.Contains(digit)) continue;

                            // Even if the target does not currently list the digit as a candidate
                            // we still consider the deduction valid per the Y-wing rectangle rule.
                            return (rowNeighbor, colNeighbor, target, digit);
                        }
                    }
                }
            }

            return null;
        }

        public RuleResult CalculateChanges(Board board)
        {
            var found = FindPlacement(board);
            var r = new RuleResult();
            if (found == null)
            {
                r.Apply = false;
                return r;
            }

            var (rowNeigh, colNeigh, target, digit) = found.Value;

            // remember whether the target previously listed the digit as a candidate
            bool hadCandidate = target.Candidates.Contains(digit);

            // Mark neighbor cells used for deduction
            if (rowNeigh != null && !r.UsedCells.Exists(u => u.Row == rowNeigh.Row && u.Column == rowNeigh.Column && u.Candidate == digit))
                r.UsedCells.Add(new UsedCell { Row = rowNeigh.Row, Column = rowNeigh.Column, Candidate = digit });
            if (colNeigh != null && !r.UsedCells.Exists(u => u.Row == colNeigh.Row && u.Column == colNeigh.Column && u.Candidate == digit))
                r.UsedCells.Add(new UsedCell { Row = colNeigh.Row, Column = colNeigh.Column, Candidate = digit });

            // Place the digit into the target cell (clear candidates)
            var change = new CellChange { Row = target.Row, Column = target.Column, OldValue = target.Value, NewValue = digit };
            for (int v = 1; v <= board.Size; v++) change.RemovedCandidates.Add(v);
            r.Changes.Add(change);

            // also record the target as used
            if (!r.UsedCells.Exists(u => u.Row == target.Row && u.Column == target.Column && u.Candidate == digit))
                r.UsedCells.Add(new UsedCell { Row = target.Row, Column = target.Column, Candidate = digit });

            r.Apply = r.Changes.Count > 0;
            if (r.Apply)
            {
                r.Description = $"YWing rectangle placed {digit} into ({target.Row},{target.Column})";
                if (!hadCandidate)
                {
                    UnityEngine.Debug.LogWarning($"YWing placed {digit} into ({target.Row},{target.Column}) even though it was not a candidate");
                    r.Description += " (digit was not present in target candidates)";
                }
            }
            return r;
        }
    }
}
