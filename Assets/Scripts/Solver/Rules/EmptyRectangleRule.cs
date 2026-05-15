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
                    var info = FindElimination(board, digit, b);
                    if (info != null && info.Targets.Count > 0) return true;
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
                    var info = FindElimination(board, digit, b);
                    if (info == null || info.Targets.Count == 0) continue;

                    // record used cells
                    foreach (Cell used in info.Used)
                    {
                        if (!result.UsedCells.Exists(u => u.Row == used.Row && u.Column == used.Column))
                            result.UsedCells.Add(new UsedCell { Row = used.Row, Column = used.Column });
                    }

                    bool applied = false;
                    foreach (Cell target in info.Targets)
                    {
                        if (!target.Value.HasValue && target.Candidates.Remove(digit))
                        {
                            var change = new CellChange { Row = target.Row, Column = target.Column };
                            change.RemovedCandidates.Add(digit);
                            result.Changes.Add(change);
                            if (!result.UsedCells.Exists(u => u.Row == target.Row && u.Column == target.Column))
                                result.UsedCells.Add(new UsedCell { Row = target.Row, Column = target.Column });
                            applied = true;
                        }
                    }

                    if (applied)
                    {
                        result.Applied = true;
                        result.Description = info.Description;
                        return result;
                    }
                }
            }
            result.Applied = false;
            return result;
        }

        private class ElimInfo
        {
            public List<Cell> Used = new List<Cell>();
            public List<Cell> Targets = new List<Cell>();
            public string Description;
        }

        private ElimInfo FindElimination(Board board, int digit, int boxIndex)
        {
            var cells = new List<Cell>();
            foreach (Cell cell in board.GetBox(boxIndex))
            {
                if (!cell.Value.HasValue && cell.Candidates.Contains(digit)) cells.Add(cell);
            }
            if (cells.Count == 0) return null;

            // all in same row?
            bool sameRow = true;
            int row = cells[0].Row;
            foreach (Cell c in cells) if (c.Row != row) { sameRow = false; break; }
            if (sameRow)
            {
                var targets = new List<Cell>();
                foreach (Cell rc in board.GetRow(row))
                {
                    if (rc.Box == boxIndex) continue;
                    if (!rc.Value.HasValue && rc.Candidates.Contains(digit)) targets.Add(rc);
                }
                if (targets.Count > 0)
                {
                    return new ElimInfo { Used = cells, Targets = targets, Description = $"Removed {digit} from row {row} outside box {boxIndex} via Empty Rectangle" };
                }
            }

            // all in same column?
            bool sameCol = true;
            int col = cells[0].Column;
            foreach (Cell c in cells) if (c.Column != col) { sameCol = false; break; }
            if (sameCol)
            {
                var targets = new List<Cell>();
                foreach (Cell cc in board.GetColumn(col))
                {
                    if (cc.Box == boxIndex) continue;
                    if (!cc.Value.HasValue && cc.Candidates.Contains(digit)) targets.Add(cc);
                }
                if (targets.Count > 0)
                {
                    return new ElimInfo { Used = cells, Targets = targets, Description = $"Removed {digit} from column {col} outside box {boxIndex} via Empty Rectangle" };
                }
            }

            return null;
        }

        public RuleResult ApplyOnlyCandidates(Board board)
        {
            return new RuleResult { Applied = false };
        }
    }
}
