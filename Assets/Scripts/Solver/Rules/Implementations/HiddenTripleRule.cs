using System.Collections.Generic;
using System.Linq;
using Cell = Sudoku.Models.Cell;
using Board = Sudoku.Models.Board;

namespace Sudoku.Solver.Rules
{
    /**
     * Hidden Triple is when there are exactly three candidates in a unit (row, column, or box) that appear only in three cells.
     * These three candidates can be removed from all other cells in that unit.
     */
    public class HiddenTripleRule : ISudokuRule
    {
        public string Name => "Hidden Triple";

        public Difficulty Difficulty => Difficulty.Hard;
        public bool CanApply(Board board)
        {
            return CalculateChanges(board).Apply;
        }

        public RuleResult CalculateChanges(Board board)
        {
            var rowHit = FindAndBuildHiddenTriple(board, EnumerateRows(board));
            if (rowHit != null)
            {
                return rowHit;
            }

            var columnHit = FindAndBuildHiddenTriple(board, EnumerateColumns(board));
            if (columnHit != null)
            {
                return columnHit;
            }

            var boxHit = FindAndBuildHiddenTriple(board, EnumerateBoxes(board));
            if (boxHit != null)
            {
                return boxHit;
            }

            return new RuleResult { Apply = false };
        }

        /**
         * Search a sequence of units and return the first hidden-triple elimination result.
         */
        private static RuleResult FindAndBuildHiddenTriple(Board board, IEnumerable<(string unitName, int unitIndex, List<Cell> cells)> units)
        {
            foreach (var unit in units)
            {
                var emptyCells = unit.cells.Where(c => !c.Value.HasValue).ToList();
                if (emptyCells.Count < 3)
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
                    if (candidateMap[a].Count != 3)
                    {
                        continue;
                    }

                    for (int b = a + 1; b <= board.Size; b++)
                    {
                        if (candidateMap[b].Count != 3 || !HasSameCellSet(candidateMap[a], candidateMap[b]))
                        {
                            continue;
                        }

                        for (int c = b + 1; c <= board.Size; c++)
                        {
                            if (candidateMap[c].Count != 3 || !HasSameCellSet(candidateMap[a], candidateMap[c]))
                            {
                                continue;
                            }

                            var tripleDigits = new[] { a, b, c };
                            var allowedDigits = new HashSet<int>(tripleDigits);
                            var tripleCells = candidateMap[a];

                            var changes = new List<CellChange>();
                            foreach (var tripleCell in tripleCells)
                            {
                                var remove = new List<int>();
                                foreach (int candidate in tripleCell.Candidates)
                                {
                                    if (!allowedDigits.Contains(candidate))
                                    {
                                        remove.Add(candidate);
                                    }
                                }

                                if (remove.Count > 0)
                                {
                                    changes.Add(new CellChange
                                    {
                                        Row = tripleCell.Row,
                                        Column = tripleCell.Column,
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
                                Description = $"Hidden Triple ({string.Join(", ", tripleDigits.OrderBy(x => x))}) in {unit.unitName} {unit.unitIndex}"
                            };

                            result.Changes.AddRange(changes);
                            foreach (var tripleCell in tripleCells)
                            {
                                result.UsedCells.Add(new UsedCell { Row = tripleCell.Row, Column = tripleCell.Column, Candidate = a });
                                result.UsedCells.Add(new UsedCell { Row = tripleCell.Row, Column = tripleCell.Column, Candidate = b });
                                result.UsedCells.Add(new UsedCell { Row = tripleCell.Row, Column = tripleCell.Column, Candidate = c });
                            }

                            return result;
                        }
                    }
                }
            }

            return null;
        }

        private static bool HasSameCellSet(List<Cell> first, List<Cell> second)
        {
            if (first == null || second == null || first.Count != 3 || second.Count != 3)
            {
                return false;
            }

            var firstSet = new HashSet<Cell>(first);
            return second.All(firstSet.Contains);
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
