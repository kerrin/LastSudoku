using System.Collections.Generic;
using System.Linq;
using Board = Sudoku.Models.Board;

namespace Sudoku.Solver.Rules
{
    /**
     * "Swordfish" is similar to X-wing but uses three sets of cells instead of two.
     *
     * If a candidate appears 6 times across three different rows, and those digits are in columns that form 3 pairs,
     * then that candidate can be removed from all other cells in those three columns. The same logic applies with rows and columns swapped.
     * https://sudoku.com/sudoku-rules/swordfish/
     */
    public class SwordFishRule : ISudokuRule
    {
        public string Name => "Swordfish";

        public Difficulty Difficulty => Difficulty.Master;
        public bool CanApply(Board board)
        {
            return FindElimination(board) != null;
        }

        public RuleResult CalculateChanges(Board board)
        {
            var found = FindElimination(board);
            var result = new RuleResult();
            if (found == null)
            {
                result.Apply = false;
                return result;
            }

            var (digit, witnesses, removals) = found.Value;
            foreach (var witness in witnesses.Distinct().OrderBy(x => x.r).ThenBy(x => x.c))
            {
                result.UsedCells.Add(new UsedCell { Row = witness.r, Column = witness.c, Candidate = digit });
            }

            foreach (var rem in removals.Distinct().OrderBy(x => x.r).ThenBy(x => x.c))
            {
                var cell = board.Cells[rem.r, rem.c];
                if (!cell.Value.HasValue && cell.Candidates.Contains(digit))
                {
                    var change = new CellChange { Row = rem.r, Column = rem.c };
                    change.RemovedCandidates.Add(digit);
                    result.Changes.Add(change);
                }
            }

            result.Apply = result.Changes.Count > 0;
            if (result.Apply)
            {
                result.Description = $"Swordfish removed {digit} from {result.Changes.Count} cell(s)";
            }

            return result;
        }

        private (int digit, List<(int r, int c)> witnesses, List<(int r, int c)> removals)? FindElimination(Board board)
        {
            int size = board.Size;
            for (int digit = 1; digit <= size; digit++)
            {
                // Row-based Swordfish (strict variant): pick 3 rows with exactly
                // two candidate positions each, and combined columns union size 3.
                var rowToColumns = new List<(int line, List<int> indices)>();
                for (int row = 0; row < size; row++)
                {
                    var cols = new List<int>();
                    for (int column = 0; column < size; column++)
                    {
                        var cell = board.Cells[row, column];
                        if (!cell.Value.HasValue && cell.Candidates.Contains(digit))
                        {
                            cols.Add(column);
                        }
                    }

                    if (cols.Count == 2)
                    {
                        rowToColumns.Add((row, cols));
                    }
                }

                var rowHit = FindOrientationHit(board, digit, rowToColumns, isRowBased: true);
                if (rowHit != null)
                {
                    return rowHit;
                }

                // Column-based Swordfish (strict variant): transpose row/column roles.
                var colToRows = new List<(int line, List<int> indices)>();
                for (int column = 0; column < size; column++)
                {
                    var rows = new List<int>();
                    for (int row = 0; row < size; row++)
                    {
                        var cell = board.Cells[row, column];
                        if (!cell.Value.HasValue && cell.Candidates.Contains(digit))
                        {
                            rows.Add(row);
                        }
                    }

                    if (rows.Count == 2)
                    {
                        colToRows.Add((column, rows));
                    }
                }

                var colHit = FindOrientationHit(board, digit, colToRows, isRowBased: false);
                if (colHit != null)
                {
                    return colHit;
                }
            }

            return null;
        }

        private static (int digit, List<(int r, int c)> witnesses, List<(int r, int c)> removals)? FindOrientationHit(
            Board board,
            int digit,
            List<(int line, List<int> indices)> lines,
            bool isRowBased)
        {
            int size = board.Size;
            for (int i = 0; i < lines.Count; i++)
            {
                for (int j = i + 1; j < lines.Count; j++)
                {
                    for (int k = j + 1; k < lines.Count; k++)
                    {
                        var selected = new[] { lines[i], lines[j], lines[k] };
                        var union = new HashSet<int>(selected[0].indices);
                        union.UnionWith(selected[1].indices);
                        union.UnionWith(selected[2].indices);
                        if (union.Count != 3)
                        {
                            continue;
                        }

                        bool allSubset = selected.All(s => s.indices.All(union.Contains));
                        if (!allSubset)
                        {
                            continue;
                        }

                        var witnessLines = selected.Select(s => s.line).ToHashSet();
                        var witnesses = new List<(int r, int c)>();
                        foreach (var line in selected)
                        {
                            foreach (int idx in line.indices)
                            {
                                witnesses.Add(isRowBased ? (line.line, idx) : (idx, line.line));
                            }
                        }

                        var removals = new List<(int r, int c)>();
                        foreach (int idx in union)
                        {
                            for (int line = 0; line < size; line++)
                            {
                                if (witnessLines.Contains(line))
                                {
                                    continue;
                                }

                                int row = isRowBased ? line : idx;
                                int column = isRowBased ? idx : line;
                                var cell = board.Cells[row, column];
                                if (!cell.Value.HasValue && cell.Candidates.Contains(digit))
                                {
                                    removals.Add((row, column));
                                }
                            }
                        }

                        if (removals.Count > 0)
                        {
                            return (digit, witnesses, removals);
                        }
                    }
                }
            }

            return null;
        }
    }

}
