using System.Collections.Generic;
using Sudoku.Models;

namespace Sudoku.Solver.Rules
{
    /**
     * Right-angle rule.
     *
     * For each box and digit, look for a 2x2 corner within the box where three
     * of the four corner cells contain the digit as a candidate. If the fourth
     * corner cell is empty and the corresponding row (outside the box) has no
     * candidates for that digit and the corresponding column (outside the box)
     * has no candidates for that digit, then the digit must be placed in the
     * fourth corner of the 2x2 and can be assigned.
     */
    public class RightAngleRule : ISudokuRule
    {
        public string Name => "Right Angle";

        public Difficulty Difficulty => Difficulty.Medium;

        public bool CanApply(Board board)
        {
            return FindPlacement(board) != null;
        }

        private (Cell target, int digit)? FindPlacement(Board board)
        {
            int size = board.Size;
            int boxesPerRow = size / board.BoxWidth;

            for (int digit = 1; digit <= size; digit++)
            {
                int boxCount = (board.Size / board.BoxWidth) * (board.Size / board.BoxHeight);
                for (int box = 0; box < boxCount; box++)
                {
                    int startBoxRow = (box / boxesPerRow) * board.BoxHeight;
                    int startBoxCol = (box % boxesPerRow) * board.BoxWidth;

                    // iterate every 2x2 sub-square inside the box (top-left offsets)
                    for (int rOff = 0; rOff <= board.BoxHeight - 2; rOff++)
                    for (int cOff = 0; cOff <= board.BoxWidth - 2; cOff++)
                    {
                        // collect the four corner cells of the 2x2
                        var coords = new (int r, int c)[4]
                        {
                            (startBoxRow + rOff,     startBoxCol + cOff),
                            (startBoxRow + rOff,     startBoxCol + cOff + 1),
                            (startBoxRow + rOff + 1, startBoxCol + cOff),
                            (startBoxRow + rOff + 1, startBoxCol + cOff + 1),
                        };

                        int have = 0;
                        (int r, int c) missing = (-1, -1);
                        for (int i = 0; i < 4; i++)
                        {
                            var (r, c) = coords[i];
                            var cell = board.Cells[r, c];
                            if (!cell.Value.HasValue && cell.Candidates.Contains(digit)) have++;
                            else missing = (r, c);
                        }

                        // Need exactly three corners that contain candidate 'digit'
                        if (have != 3) continue;

                        var target = board.Cells[missing.r, missing.c];
                        if (target.Value.HasValue) continue;

                        // Check the row outside this box: none of the cells in the same row
                        // but outside the current box may contain the digit as a candidate.
                        bool rowHasOther = false;
                        for (int c = 0; c < size; c++)
                        {
                            if (c >= startBoxCol && c < startBoxCol + board.BoxWidth) continue; // skip cells inside box
                            var cell = board.Cells[target.Row, c];
                            if (!cell.Value.HasValue && cell.Candidates.Contains(digit)) { rowHasOther = true; break; }
                        }
                        if (rowHasOther) continue;

                        // Check the column outside this box similarly
                        bool colHasOther = false;
                        for (int r = 0; r < size; r++)
                        {
                            if (r >= startBoxRow && r < startBoxRow + board.BoxHeight) continue;
                            var cell = board.Cells[r, target.Column];
                            if (!cell.Value.HasValue && cell.Candidates.Contains(digit)) { colHasOther = true; break; }
                        }
                        if (colHasOther) continue;

                        // Both row and column outside the box have no other candidates -> forced
                        return (target, digit);
                    }
                }
            }
            return null;
        }

        public RuleResult Apply(Board board)
        {
            var found = FindPlacement(board);
            var r = new RuleResult();
            if (found == null)
            {
                r.Applied = false;
                return r;
            }
            var (target, digit) = found.Value;

            if (!r.UsedCells.Exists(u => u.Row == target.Row && u.Column == target.Column))
                r.UsedCells.Add(new UsedCell { Row = target.Row, Column = target.Column });

            int? old = target.Value;
            target.Value = digit;
            target.Candidates.Clear();

            var change = new CellChange { Row = target.Row, Column = target.Column, OldValue = old, NewValue = digit };
            r.Changes.Add(change);
            r.Applied = true;
            r.Description = $"Right angle placed {digit} at ({target.Row},{target.Column})";
            return r;
        }

        public bool UpdateCandidates(Board board)
        {
            return false;
        }
    }
}
