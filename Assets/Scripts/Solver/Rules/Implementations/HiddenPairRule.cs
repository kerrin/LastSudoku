using System.Collections.Generic;
using System.Linq;
using Cell = Sudoku.Models.Cell;
using Board = Sudoku.Models.Board;

namespace Sudoku.Solver.Rules
{
    /**
     * Hidden Pair is when there are exactly two candidates in a unit (row, column, or box) that appear only in two cells.
        * All other candidates can be removed from those two cells.
     */
    public class HiddenPairRule : ISudokuRule
    {
        public string Name => "Hidden Pair";

        public Difficulty Difficulty => Difficulty.Hard;

        public bool CanApply(Board board)
        {
            return CalculateChanges(board).Apply;
        }

        public RuleResult CalculateChanges(Board board)
        {
            var result = new RuleResult();

            var rowHit = FindAndBuildHiddenPair(board, EnumerateRows(board));
            if (rowHit != null)
            {
                return rowHit;
            }

            var columnHit = FindAndBuildHiddenPair(board, EnumerateColumns(board));
            if (columnHit != null)
            {
                return columnHit;
            }

            var boxHit = FindAndBuildHiddenPair(board, EnumerateBoxes(board));
            if (boxHit != null)
            {
                return boxHit;
            }

            result.Apply = false;
            return result;
        }

        /**
         * Search a sequence of units and return the first hidden-pair elimination result.
         */
        private static RuleResult FindAndBuildHiddenPair(Board board, IEnumerable<(string unitName, int unitIndex, List<Cell> cells)> units)
        {
            foreach (var unit in units)
            {
                var emptyCells = unit.cells.Where(c => !c.Value.HasValue).ToList();
                if (emptyCells.Count < 2)
                {
                    continue;
                }

                var candidateMap = new Dictionary<int, List<Cell>>();
                for (int digit = 1; digit <= board.Size; digit++)
                {
                    candidateMap[digit] = new List<Cell>();
                }

                foreach (var cell in emptyCells)
                {
                    foreach (int candidate in cell.Candidates)
                    {
                        if (candidateMap.TryGetValue(candidate, out var cellsWithCandidate))
                        {
                            cellsWithCandidate.Add(cell);
                        }
                    }
                }

                for (int a = 1; a <= board.Size; a++)
                {
                    if (candidateMap[a].Count != 2)
                    {
                        continue;
                    }

                    for (int b = a + 1; b <= board.Size; b++)
                    {
                        if (candidateMap[b].Count != 2)
                        {
                            continue;
                        }

                        var firstPairCells = candidateMap[a];
                        var secondPairCells = candidateMap[b];
                        if (!ReferenceEquals(firstPairCells[0], secondPairCells[0]) && !ReferenceEquals(firstPairCells[0], secondPairCells[1]))
                        {
                            continue;
                        }

                        if (!ReferenceEquals(firstPairCells[1], secondPairCells[0]) && !ReferenceEquals(firstPairCells[1], secondPairCells[1]))
                        {
                            continue;
                        }

                        var pairCellA = firstPairCells[0];
                        var pairCellB = firstPairCells[1];

                        var pairAllowed = new HashSet<int> { a, b };
                        var pairCells = new List<Cell> { pairCellA, pairCellB };

                        var changes = new List<CellChange>();
                        foreach (var pairCell in pairCells)
                        {
                            var remove = new List<int>();
                            foreach (int candidate in pairCell.Candidates)
                            {
                                if (!pairAllowed.Contains(candidate))
                                {
                                    remove.Add(candidate);
                                }
                            }

                            if (remove.Count > 0)
                            {
                                changes.Add(new CellChange
                                {
                                    Row = pairCell.Row,
                                    Column = pairCell.Column,
                                    RemovedCandidates = remove
                                });
                            }
                        }

                        if (changes.Count == 0)
                        {
                            continue;
                        }

                        var result = new RuleResult
                        {
                            Apply = true,
                            Description = $"Hidden Pair ({a},{b}) in {unit.unitName} {unit.unitIndex}"
                        };

                        result.Changes.AddRange(changes);
                        foreach (var pairCell in pairCells)
                        {
                            result.UsedCells.Add(new UsedCell { Row = pairCell.Row, Column = pairCell.Column, Candidate = a });
                            result.UsedCells.Add(new UsedCell { Row = pairCell.Row, Column = pairCell.Column, Candidate = b });
                        }

                        return result;
                    }
                }
            }

            return null;
        }

        private static IEnumerable<(string unitName, int unitIndex, List<Cell> cells)> EnumerateRows(Board board)
        {
            for (int row = 0; row < board.Size; row++)
            {
                yield return ("row", row, board.GetRow(row).ToList());
            }
        }

        private static IEnumerable<(string unitName, int unitIndex, List<Cell> cells)> EnumerateColumns(Board board)
        {
            for (int column = 0; column < board.Size; column++)
            {
                yield return ("column", column, board.GetColumn(column).ToList());
            }
        }

        private static IEnumerable<(string unitName, int unitIndex, List<Cell> cells)> EnumerateBoxes(Board board)
        {
            for (int box = 0; box < board.Size; box++)
            {
                yield return ("box", box, board.GetBox(box).ToList());
            }
        }
    }

}
