using System.Collections.Generic;
using System.Linq;
using Sudoku.Models;
using Cell = Sudoku.Models.Cell;
using Board = Sudoku.Models.Board;

namespace Sudoku.Solver.Rules
{
    /**
     * A simple Box-Line / Empty Rectangle / Intersection Removal implementation.
     *
     * If all candidates for a digit within a box lie in a single row (or column),
     * then that digit can be eliminated from other cells in that row (or column)
     * outside the box.
     */
    public class BoxLineRule : ISudokuRule
    {
        public string Name => "Box Line";

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

        public RuleResult CalculateChanges(Board board)
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
                        if (!result.UsedCells.Exists(u => u.Row == used.Row && u.Column == used.Column && u.Candidate == digit))
                            result.UsedCells.Add(new UsedCell { Row = used.Row, Column = used.Column, Candidate = digit });
                    }

                    // Apply removals to all eligible target cells in the same row/column
                    var targets = info.Targets.OrderBy(t => t.Row).ThenBy(t => t.Column).Where(t => !t.Value.HasValue && t.Candidates.Contains(digit)).ToList();
                    if (targets.Count > 0)
                    {
                        foreach (var trg in targets)
                        {
                            var change = new CellChange { Row = trg.Row, Column = trg.Column };
                            change.RemovedCandidates.Add(digit);
                            result.Changes.Add(change);
                        }

                        result.Apply = true;
                        result.Description = info.Description;
                        return result;
                    }
                }
            }
            result.Apply = false;
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
            if (cells.Count == 0) return null; // All cells in box are filled or don't contain the digit as a candidate, so no elimination possible.

            foreach(Cell checkCell in cells) {
                // all in same row?
                int row = checkCell.Row;
                bool sameRow = true;
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
                        // Also add the other cells in the row without values in the box as "used" cells for UI highlighting, since they contributed to the deduction.
                        foreach (Cell rc in board.GetRow(row))
                        {
                            if (rc.Box != boxIndex) continue; // Not in the box, so not a used cell for this deduction.
                            if (!rc.Value.HasValue && !rc.Candidates.Contains(digit) && !cells.Exists(c => c.Row == rc.Row && c.Column == rc.Column))
                                cells.Add(rc); // Same row, and doesn't have the digit as a candidate, so it contributed to the deduction
                        }
                        return new ElimInfo { Used = cells, Targets = targets, Description = $"Removed {digit} from row {row} outside box {boxIndex} via Box-Line" };
                    }
                }
                // all in same column?
                int col = checkCell.Column;
                bool sameCol = true;
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
                        // Also add the other cells in the column without values in the box as "used" cells for UI highlighting, since they contributed to the deduction.
                        foreach (Cell cc in board.GetColumn(col))
                        {
                            if (cc.Box != boxIndex) continue; // Not in the box, so not a used cell for this deduction.
                            if (!cc.Value.HasValue && !cc.Candidates.Contains(digit) && !cells.Exists(c => c.Row == cc.Row && c.Column == cc.Column))
                                cells.Add(cc); // Same column, and doesn't have the digit as a candidate, so it contributed to the deduction
                        }
                        return new ElimInfo { Used = cells, Targets = targets, Description = $"Removed {digit} from column {col} outside box {boxIndex} via Box-Line" };
                    }
                }
            }

            return null;
        }
    }
}
