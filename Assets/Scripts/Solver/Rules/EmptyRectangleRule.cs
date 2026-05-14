using System.Collections.Generic;
using Sudoku.Models;

namespace Sudoku.Solver.Rules
{
    /**
     * A simple Empty-Rectangle / Box-Line reduction implementation.
     *
     * If all candidates for a digit within a box lie in a single row (or column),
     * then that digit can be eliminated from other cells in that row (or column)
     * outside the box.
     */
    public class EmptyRectangleRule : ISudokuRule
    {
        public string Name => "Empty Rectangle (Box-Line)";

        public Difficulty Difficulty => Difficulty.Medium;

        public bool CanApply(Board board)
        {
            int size = board.Size;
            for (int digit = 1; digit <= size; digit++)
            {
                for (int b = 0; b < size; b++)
                {
                    var cells = new List<Cell>();
                    foreach (Cell cell in board.GetBox(b))
                    {
                        if (!cell.Value.HasValue && cell.Candidates.Contains(digit)) cells.Add(cell);
                    }
                    if (cells.Count == 0) continue;

                    // all in same row?
                    bool sameRow = true;
                    int row = cells[0].Row;
                    foreach (Cell c in cells) if (c.Row != row) { sameRow = false; break; }
                    if (sameRow)
                    {
                        // any candidate in that row outside the box?
                        foreach (Cell rc in board.GetRow(row))
                        {
                            if (rc.Box == b) continue;
                            if (!rc.Value.HasValue && rc.Candidates.Contains(digit)) return true;
                        }
                    }

                    // all in same column?
                    bool sameCol = true;
                    int col = cells[0].Column;
                    foreach (Cell c in cells) if (c.Column != col) { sameCol = false; break; }
                    if (sameCol)
                    {
                        foreach (Cell cc in board.GetColumn(col))
                        {
                            if (cc.Box == b) continue;
                            if (!cc.Value.HasValue && cc.Candidates.Contains(digit)) return true;
                        }
                    }
                }
            }
            return false;
        }

        public RuleResult Apply(Board board)
        {
            var result = new RuleResult();
            int size = board.Size;
            for (int digit = 1; digit <= size; digit++)
            {
                for (int b = 0; b < size; b++)
                {
                    var cells = new List<Cell>();
                    foreach (Cell cell in board.GetBox(b))
                    {
                        if (!cell.Value.HasValue && cell.Candidates.Contains(digit)) cells.Add(cell);
                    }
                    if (cells.Count == 0) continue;

                    // row-based elimination
                    bool sameRow = true;
                    int row = cells[0].Row;
                    foreach (Cell c in cells) if (c.Row != row) { sameRow = false; break; }
                    if (sameRow)
                    {
                        bool applied = false;
                        foreach (Cell rc in board.GetRow(row))
                        {
                            if (rc.Box == b) continue;
                            if (!rc.Value.HasValue && rc.Candidates.Remove(digit))
                            {
                                var change = new CellChange { Row = rc.Row, Column = rc.Column };
                                change.RemovedCandidates.Add(digit);
                                result.Changes.Add(change);
                                applied = true;
                            }
                        }
                        if (applied)
                        {
                            result.Applied = true;
                            result.Description = $"Removed {digit} from row {row} outside box {b} via Empty Rectangle";
                            return result;
                        }
                    }

                    // column-based elimination
                    bool sameCol = true;
                    int col = cells[0].Column;
                    foreach (Cell c in cells) if (c.Column != col) { sameCol = false; break; }
                    if (sameCol)
                    {
                        bool applied = false;
                        foreach (Cell cc in board.GetColumn(col))
                        {
                            if (cc.Box == b) continue;
                            if (!cc.Value.HasValue && cc.Candidates.Remove(digit))
                            {
                                var change = new CellChange { Row = cc.Row, Column = cc.Column };
                                change.RemovedCandidates.Add(digit);
                                result.Changes.Add(change);
                                applied = true;
                            }
                        }
                        if (applied)
                        {
                            result.Applied = true;
                            result.Description = $"Removed {digit} from column {col} outside box {b} via Empty Rectangle";
                            return result;
                        }
                    }
                }
            }
            result.Applied = false;
            return result;
        }

        public bool UpdateCandidates(Board board)
        {
            return false;
        }
    }
}
