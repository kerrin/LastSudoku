using System.Collections.Generic;
using System.Linq;
using Sudoku.Models;
using Cell = Sudoku.Models.Cell;

namespace Sudoku.Solver.Rules
{
    /**
     * Canonical W-Wing:
     * - Two bi-value cells contain the same pair {a,b} and do not see each other.
     * - One candidate (say a) forms a strong link between two other cells in a row/column/box.
     * - Each bi-value cell sees one end of that strong link.
     * Then any cell that sees both bi-value cells cannot contain the other candidate (b).
     */
    public class WWingRule : ISudokuRule
    {
        private class Placement
        {
            public Cell PincerA;
            public Cell PincerB;
            public Cell LinkA;
            public Cell LinkB;
            public int StrongDigit;
            public int EliminationDigit;
            public List<Cell> Removals;

            public Placement(
                Cell pincerA,
                Cell pincerB,
                Cell linkA,
                Cell linkB,
                int strongDigit,
                int eliminationDigit,
                List<Cell> removals)
            {
                PincerA = pincerA;
                PincerB = pincerB;
                LinkA = linkA;
                LinkB = linkB;
                StrongDigit = strongDigit;
                EliminationDigit = eliminationDigit;
                Removals = removals;
            }
        }

        private class StrongLink
        {
            public Cell CellA;
            public Cell CellB;
            public int Digit;

            public StrongLink(Cell cellA, Cell cellB, int digit)
            {
                CellA = cellA;
                CellB = cellB;
                Digit = digit;
            }
        }

        public string Name => "W-Wing";

        public Difficulty Difficulty => Difficulty.Hard;

        public bool CanApply(Board board)
        {
            return FindPlacement(board) != null;
        }

        private Placement FindPlacement(Board board)
        {
            var bivals = GetBiValueCells(board);
            if (bivals.Count < 2)
            {
                return null;
            }

            for (int i = 0; i < bivals.Count - 1; i++)
            {
                var pincerA = bivals[i];
                var pairA = pincerA.Candidates.OrderBy(x => x).ToList();
                int digitA = pairA[0];
                int digitB = pairA[1];

                for (int j = i + 1; j < bivals.Count; j++)
                {
                    var pincerB = bivals[j];
                    var pairB = pincerB.Candidates.OrderBy(x => x).ToList();

                    // W-Wing requires matching bi-value pair {a,b}.
                    if (pairB[0] != digitA || pairB[1] != digitB)
                    {
                        continue;
                    }

                    // The two pincer cells must not see each other.
                    if (DoCellsSeeEachOther(pincerA, pincerB))
                    {
                        continue;
                    }

                    // Try both possible strong-link digits from the shared pair.
                    var placement =
                        TryFindPlacementForStrongDigit(board, pincerA, pincerB, strongDigit: digitA, eliminationDigit: digitB)
                        ?? TryFindPlacementForStrongDigit(board, pincerA, pincerB, strongDigit: digitB, eliminationDigit: digitA);

                    if (placement != null)
                    {
                        return placement;
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

        private Placement TryFindPlacementForStrongDigit(Board board, Cell pincerA, Cell pincerB, int strongDigit, int eliminationDigit)
        {
            var links = BuildStrongLinks(board, strongDigit);
            if (links.Count == 0)
            {
                return null;
            }

            foreach (var link in links)
            {
                // Avoid degenerate cases where a pincer is itself a strong-link endpoint.
                if (link.CellA == pincerA || link.CellA == pincerB || link.CellB == pincerA || link.CellB == pincerB)
                {
                    continue;
                }

                bool aSeesLeft = DoCellsSeeEachOther(pincerA, link.CellA);
                bool aSeesRight = DoCellsSeeEachOther(pincerA, link.CellB);
                bool bSeesLeft = DoCellsSeeEachOther(pincerB, link.CellA);
                bool bSeesRight = DoCellsSeeEachOther(pincerB, link.CellB);

                // Each pincer must see a different strong-link endpoint.
                bool directOrientation = aSeesLeft && bSeesRight && !aSeesRight && !bSeesLeft;
                bool swappedOrientation = aSeesRight && bSeesLeft && !aSeesLeft && !bSeesRight;
                if (!directOrientation && !swappedOrientation)
                {
                    continue;
                }

                var commonPeers = new HashSet<Cell>(board.GetPeers(pincerA));
                commonPeers.IntersectWith(board.GetPeers(pincerB));

                var removals = commonPeers
                    .Where(cell => cell != pincerA && cell != pincerB)
                    .Where(cell => !cell.Value.HasValue)
                    .Where(cell => cell.Candidates.Contains(eliminationDigit))
                    .OrderBy(cell => cell.Row)
                    .ThenBy(cell => cell.Column)
                    .ToList();

                if (removals.Count == 0)
                {
                    continue;
                }

                var linkA = directOrientation ? link.CellA : link.CellB;
                var linkB = directOrientation ? link.CellB : link.CellA;
                return new Placement(pincerA, pincerB, linkA, linkB, strongDigit, eliminationDigit, removals);
            }

            return null;
        }

        private List<StrongLink> BuildStrongLinks(Board board, int digit)
        {
            var links = new List<StrongLink>();
            var seenKeys = new HashSet<string>();
            int size = board.Size;

            for (int row = 0; row < size; row++)
            {
                AddStrongLinkIfConjugate(links, seenKeys, board.GetRow(row).ToList(), digit);
            }

            for (int column = 0; column < size; column++)
            {
                AddStrongLinkIfConjugate(links, seenKeys, board.GetColumn(column).ToList(), digit);
            }

            for (int box = 0; box < size; box++)
            {
                AddStrongLinkIfConjugate(links, seenKeys, board.GetBox(box).ToList(), digit);
            }

            return links;
        }

        private void AddStrongLinkIfConjugate(List<StrongLink> links, HashSet<string> seenKeys, List<Cell> unitCells, int digit)
        {
            var candidateCells = unitCells
                .Where(cell => !cell.Value.HasValue)
                .Where(cell => cell.Candidates.Contains(digit))
                .OrderBy(cell => cell.Row)
                .ThenBy(cell => cell.Column)
                .ToList();

            if (candidateCells.Count != 2)
            {
                return;
            }

            var a = candidateCells[0];
            var b = candidateCells[1];
            string key = BuildCellPairKey(a, b, digit);
            if (!seenKeys.Add(key))
            {
                return;
            }

            links.Add(new StrongLink(a, b, digit));
        }

        private static string BuildCellPairKey(Cell a, Cell b, int digit)
        {
            int firstRow = a.Row;
            int firstColumn = a.Column;
            int secondRow = b.Row;
            int secondColumn = b.Column;

            bool swap = firstRow > secondRow || (firstRow == secondRow && firstColumn > secondColumn);
            if (swap)
            {
                (firstRow, secondRow) = (secondRow, firstRow);
                (firstColumn, secondColumn) = (secondColumn, firstColumn);
            }

            return $"{digit}:{firstRow},{firstColumn}-{secondRow},{secondColumn}";
        }

        private static bool DoCellsSeeEachOther(Cell first, Cell second)
        {
            if (first == null || second == null)
            {
                return false;
            }

            return first.Row == second.Row || first.Column == second.Column || first.Box == second.Box;
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

            var pincerA = found.PincerA;
            var pincerB = found.PincerB;
            var linkA = found.LinkA;
            var linkB = found.LinkB;
            int strongDigit = found.StrongDigit;
            int eliminationDigit = found.EliminationDigit;

            // Highlight pincer cells with both pair candidates.
            foreach (var witness in new[] { pincerA, pincerB })
            {
                foreach (var candidate in witness.Candidates.OrderBy(x => x))
                {
                    if (!r.UsedCells.Exists(u => u.Row == witness.Row && u.Column == witness.Column && u.Candidate == candidate))
                    {
                        r.UsedCells.Add(new UsedCell { Row = witness.Row, Column = witness.Column, Candidate = candidate });
                    }
                }
            }

            // Highlight both strong-link endpoints with the strong-link digit.
            foreach (var witness in new[] { linkA, linkB })
            {
                if (witness == null)
                {
                    continue;
                }

                if (!r.UsedCells.Exists(u => u.Row == witness.Row && u.Column == witness.Column && u.Candidate == strongDigit))
                {
                    r.UsedCells.Add(new UsedCell { Row = witness.Row, Column = witness.Column, Candidate = strongDigit });
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
                r.Description = $"W-Wing removed {eliminationDigit} from {r.Changes.Count} cell(s) via strong link on {strongDigit}";
            }

            return r;
        }
    }
}

