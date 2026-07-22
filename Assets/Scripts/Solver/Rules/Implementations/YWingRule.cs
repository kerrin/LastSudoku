using System.Collections.Generic;
using System.Linq;
using Sudoku.Models;
using Cell = Sudoku.Models.Cell;

namespace Sudoku.Solver.Rules
{
    /**
     * Canonical Y-Wing (pivot + two pincers):
     * - Pivot cell has bi-value {a,b} candidates.
     * - Pincer 1 cell has bi-value {a,c} candidates and sees the pivot.
     * - Pincer 2 cell has bi-value {b,c} candidates and sees the pivot.
     * Any cell that sees (row, column, or cell) both pincer cells cannot contain c.
     */
    public class YWingRule : ISudokuRule
    {
        private class Placement
        {
            public Cell Pivot;
            public Cell PincerA;
            public Cell PincerB;
            public int EliminationDigit;
            public List<Cell> Removals;

            public Placement(Cell pivot, Cell pincerA, Cell pincerB, int eliminationDigit, List<Cell> removals)
            {
                Pivot = pivot;
                PincerA = pincerA;
                PincerB = pincerB;
                EliminationDigit = eliminationDigit;
                Removals = removals;
            }
        }

        public string Name => "Y-Wing";

        public Difficulty Difficulty => Difficulty.Hard;

        public bool CanApply(Board board)
        {
            // Directly search for a valid placement. Avoid a global "pristine"
            // early-exit — test setups often clear candidates which can make the
            // naive detection unreliable. Rely on the actual pattern search instead.
            return FindPlacement(board) != null;
        }

        private Placement FindPlacement(Board board)
        {
            var bivals = GetBiValueCells(board);
            foreach (var pivot in bivals)
            {
                var pivotCandidates = pivot.Candidates.OrderBy(x => x).ToList();
                int a = pivotCandidates[0];
                int b = pivotCandidates[1];

                var pivotPeers = new HashSet<Cell>(board.GetPeers(pivot));

                // Pincer A candidates: {a,c} where c != b
                var pincerAOptions = bivals
                    .Where(cell => cell != pivot)
                    .Where(cell => pivotPeers.Contains(cell))
                    .Where(cell => cell.Candidates.Contains(a) && !cell.Candidates.Contains(b))
                    .OrderBy(cell => cell.Row)
                    .ThenBy(cell => cell.Column)
                    .ToList();

                // Pincer B candidates: {b,c} where c != a
                var pincerBOptions = bivals
                    .Where(cell => cell != pivot)
                    .Where(cell => pivotPeers.Contains(cell))
                    .Where(cell => cell.Candidates.Contains(b) && !cell.Candidates.Contains(a))
                    .OrderBy(cell => cell.Row)
                    .ThenBy(cell => cell.Column)
                    .ToList();

                foreach (var pincerA in pincerAOptions)
                {
                    foreach (var pincerB in pincerBOptions)
                    {
                        if (pincerA == pincerB)
                        {
                            continue;
                        }

                        var shared = pincerA.Candidates.Intersect(pincerB.Candidates).ToList();
                        if (shared.Count != 1)
                        {
                            continue;
                        }

                        int eliminationDigit = shared[0];
                        if (eliminationDigit == a || eliminationDigit == b)
                        {
                            continue;
                        }

                        var commonPeers = new HashSet<Cell>(board.GetPeers(pincerA));
                        commonPeers.IntersectWith(board.GetPeers(pincerB));

                        var removals = commonPeers
                            .Where(cell => cell != pivot && cell != pincerA && cell != pincerB)
                            .Where(cell => !cell.Value.HasValue)
                            .Where(cell => cell.Candidates.Contains(eliminationDigit))
                            .OrderBy(cell => cell.Row)
                            .ThenBy(cell => cell.Column)
                            .ToList();

                        if (removals.Count > 0)
                        {
                            return new Placement(pivot, pincerA, pincerB, eliminationDigit, removals);
                        }
                    }
                }
            }

            return null;
        }

        private static List<Cell> GetBiValueCells(Board board)
        {
            var bivals = new List<Cell>();
            int size = board.Size;
            for (int r = 0; r < size; r++)
            {
                for (int c = 0; c < size; c++)
                {
                    var cell = board.Cells[r, c];
                    if (!cell.Value.HasValue && cell.Candidates.Count == 2)
                    {
                        bivals.Add(cell);
                    }
                }
            }

            return bivals
                .OrderBy(cell => cell.Row)
                .ThenBy(cell => cell.Column)
                .ToList();
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

            var pivot = found.Pivot;
            var pincerA = found.PincerA;
            var pincerB = found.PincerB;
            int eliminationDigit = found.EliminationDigit;

            // Highlight pivot and both pincers with their bi-value candidates.
            foreach (var witness in new[] { pivot, pincerA, pincerB })
            {
                foreach (var candidate in witness.Candidates.OrderBy(x => x))
                {
                    if (!r.UsedCells.Exists(u => u.Row == witness.Row && u.Column == witness.Column && u.Candidate == candidate))
                    {
                        r.UsedCells.Add(new UsedCell { Row = witness.Row, Column = witness.Column, Candidate = candidate });
                    }
                }
            }

            foreach (var target in found.Removals)
            {
                if (!target.Value.HasValue && target.Candidates.Contains(eliminationDigit))
                {
                    var targetChange = new CellChange { Row = target.Row, Column = target.Column };
                    targetChange.RemovedCandidates.Add(eliminationDigit);
                    r.Changes.Add(targetChange);
                }
            }

            r.Apply = r.Changes.Count > 0;
            if (r.Apply)
            {
                r.Description = $"YWing removed {eliminationDigit} from {r.Changes.Count} cell(s)";
            }

            return r;
        }
    }
}
