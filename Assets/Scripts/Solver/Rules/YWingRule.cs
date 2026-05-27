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
     * (e.g. {a,b}, {a,c}, {b,c}), then the fourth corner not must be the
     * digit missing in the opposite corner pair (e.g. c in this example)
     * (ab),(bc)
     * (ac),(*c*)
     *
     * The 3 cells used for deduction cannot contain any other candidates beyond the three-digit union, 
     * but the cell we remove the candidate from can contain other candidates (which will be unaffected). 
     * This rule should only trigger if the target cell currently lists the digit as a candidate.
     */
    public class YWingRule : ISudokuRule
    {
        private class Placement
        {
            public List<Cell> Bivals;
            public Cell RowNeighbor;
            public Cell ColNeighbor;
            public Cell Target;
            public int Digit;
            public HashSet<int> Union;

            public Placement(List<Cell> bivals, Cell rowNeighbor, Cell colNeighbor, Cell target, int digit, HashSet<int> union)
            {
                Bivals = bivals;
                RowNeighbor = rowNeighbor;
                ColNeighbor = colNeighbor;
                Target = target;
                Digit = digit;
                Union = union;
            }
        }

        public string Name => "Y-Wing";

        public Difficulty Difficulty => Difficulty.Medium;

        public bool CanApply(Board board)
        {
            // Directly search for a valid placement. Avoid a global "pristine"
            // early-exit — test setups often clear candidates which can make the
            // naive detection unreliable. Rely on the actual pattern search instead.
            return FindPlacement(board) != null;
        }

        private Placement FindPlacement(Board board)
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
                                                    var intersections = trip.Select(cell => cell.Candidates.Intersect(candidateSet).ToList()).ToList();
                                                    if (intersections.Any(inter => inter.Count != 2)) continue;

                                                    // The three pivot cells must not contain candidates outside the
                                                    // chosen 3-digit union (they should be restricted to the union)
                                                    bool anyOutside = trip.Any(cell => cell.Candidates.Except(candidateSet).Any());
                                                    if (anyOutside) continue;

                                                    // the three pairs must be distinct (i.e. {a,b},{a,c},{b,c})
                                                    var distinctPairs = new HashSet<string>(intersections.Select(i => string.Join(",", i.OrderBy(x => x))));
                                                    if (distinctPairs.Count != 3) continue;

                                                    // union of those intersections must equal candidateSet
                                                    var unionInter = new HashSet<int>(intersections.SelectMany(i => i));
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

                            // Only accept the placement if the target currently lists the
                            // deduced digit as a candidate. The UI/preview path (which may
                            // only enact candidate removals) relies on this so we don't
                            // produce placements that would clear the cell's candidates
                            // without the digit actually being a candidate.
                            if (target.Candidates.Contains(digit))
                            {
                                return new Placement(bivals, rowNeighbor, colNeighbor, target, digit, union);
                            }
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

            var bivals = found.Bivals;
            var rowNeigh = found.RowNeighbor;
            var colNeigh = found.ColNeighbor;
            var target = found.Target;
            var digit = found.Digit;
            var union = found.Union;

            // remember whether the target previously listed the digit as a candidate
            bool hadCandidate = target.Candidates.Contains(digit);

            // Mark neighbor cells used for deduction (the intersection digit)
            if (rowNeigh != null && !r.UsedCells.Exists(u => u.Row == rowNeigh.Row && u.Column == rowNeigh.Column && u.Candidate == digit))
                r.UsedCells.Add(new UsedCell { Row = rowNeigh.Row, Column = rowNeigh.Column, Candidate = digit });
            if (colNeigh != null && !r.UsedCells.Exists(u => u.Row == colNeigh.Row && u.Column == colNeigh.Column && u.Candidate == digit))
                r.UsedCells.Add(new UsedCell { Row = colNeigh.Row, Column = colNeigh.Column, Candidate = digit });

            // Also mark the three bival corner cells and the specific candidates from the union
            foreach (var bv in bivals)
            {
                var relevant = bv.Candidates.Intersect(union).ToList();
                foreach (var c in relevant)
                {
                    if (!r.UsedCells.Exists(u => u.Row == bv.Row && u.Column == bv.Column && u.Candidate == c))
                        r.UsedCells.Add(new UsedCell { Row = bv.Row, Column = bv.Column, Candidate = c });
                }
                // If there were no specific intersecting candidates (defensive), still mark the cell
                if (relevant.Count == 0 && !r.UsedCells.Exists(u => u.Row == bv.Row && u.Column == bv.Column && u.Candidate == null))
                    r.UsedCells.Add(new UsedCell { Row = bv.Row, Column = bv.Column, Candidate = null });
            }

            // Remove the deduced digit from the target cell's candidates (don't set the value)
            if (hadCandidate)
            {
                var targetChange = new CellChange { Row = target.Row, Column = target.Column };
                targetChange.RemovedCandidates.Add(digit);
                r.Changes.Add(targetChange);
            }

            // also record the target as used (for UI highlighting)
            if (!r.UsedCells.Exists(u => u.Row == target.Row && u.Column == target.Column && u.Candidate == digit))
                r.UsedCells.Add(new UsedCell { Row = target.Row, Column = target.Column, Candidate = digit });

            r.Apply = r.Changes.Count > 0;
            if (r.Apply)
            {
                r.Description = $"YWing removed {digit} from ({target.Row},{target.Column})";
                if (!hadCandidate)
                {
                    UnityEngine.Debug.LogWarning($"YWing attempted to remove {digit} from ({target.Row},{target.Column}) even though it was not a candidate");
                    r.Description += " (digit was not present in target candidates)";
                }
            }

            return r;
        }
    }
}
