using System.Collections.Generic;
using System.Linq;
using Sudoku.Models;
using Cell = Sudoku.Models.Cell;

namespace Sudoku.Solver.Rules
{
    /**
     * Canonical XYZ-Wing (pivot + two wings):
     * - Pivot cell has tri-value {a,b,c} candidates.
     * - Wing 1 cell has bi-value {a,c} candidates and sees the pivot.
     * - Wing 2 cell has bi-value {b,c} candidates and sees the pivot.
     * Any cell that sees both wings and the pivot cannot contain c.
     */
    public class XYZWingRule : ISudokuRule
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

        public string Name => "XYZ-Wing";

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
            var trivalue = GetTriValueCells(board);
            var bivals = GetBiValueCells(board);

            foreach (var pivot in trivalue)
            {
                var pivotCandidates = pivot.Candidates.OrderBy(x => x).ToList();
                int a = pivotCandidates[0];
                int b = pivotCandidates[1];
                int c = pivotCandidates[2];

                var pivotPeers = new HashSet<Cell>(board.GetPeers(pivot));

                // Wing A candidates: {a,c}
                var pincerAOptions = bivals
                    .Where(cell => cell != pivot)
                    .Where(cell => pivotPeers.Contains(cell))
                    .Where(cell => cell.Candidates.Contains(a) && !cell.Candidates.Contains(b))
                    .Where(cell => cell.Candidates.Contains(c))
                    .OrderBy(cell => cell.Row)
                    .ThenBy(cell => cell.Column)
                    .ToList();

                // Wing B candidates: {b,c}
                var pincerBOptions = bivals
                    .Where(cell => cell != pivot)
                    .Where(cell => pivotPeers.Contains(cell))
                    .Where(cell => cell.Candidates.Contains(b) && !cell.Candidates.Contains(a))
                    .Where(cell => cell.Candidates.Contains(c))
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
                        if (eliminationDigit != c)
                        {
                            continue;
                        }

                        // Wings must be subsets of pivot and together reproduce pivot candidates.
                        if (!pincerA.Candidates.All(pivot.Candidates.Contains) || !pincerB.Candidates.All(pivot.Candidates.Contains))
                        {
                            continue;
                        }

                        var wingsUnion = new HashSet<int>(pincerA.Candidates);
                        wingsUnion.UnionWith(pincerB.Candidates);
                        if (!wingsUnion.SetEquals(pivot.Candidates))
                        {
                            continue;
                        }

                        var commonPeers = new HashSet<Cell>(board.GetPeers(pincerA));
                        commonPeers.IntersectWith(board.GetPeers(pincerB));
                        commonPeers.IntersectWith(board.GetPeers(pivot));

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

        private static List<Cell> GetTriValueCells(Board board)
        {
            var trivalue = new List<Cell>();
            int size = board.Size;
            for (int r = 0; r < size; r++)
            {
                for (int c = 0; c < size; c++)
                {
                    var cell = board.Cells[r, c];
                    if (!cell.Value.HasValue && cell.Candidates.Count == 3)
                    {
                        trivalue.Add(cell);
                    }
                }
            }

            return trivalue
                .OrderBy(cell => cell.Row)
                .ThenBy(cell => cell.Column)
                .ToList();
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
            return RuleCalculationCache.GetOrCalculate(this, board, () => CalculateChangesInternal(board));
        }

        private RuleResult CalculateChangesInternal(Board board)
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
                r.Description = $"XYZ-Wing removed {eliminationDigit} from {r.Changes.Count} cell(s)";
            }

            return r;
        }
    }
}

