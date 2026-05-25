using System.Collections.Generic;
using System.Linq;
using Sudoku.Models;
using Cell = Sudoku.Models.Cell;
using Board = Sudoku.Models.Board;

namespace Sudoku.Solver.Rules
{
    /**
     * If a row, column or box contains exactly one unsolved cell, place
     * the missing digit into that cell.
     */
    public class LastCellInUnitRule : ISudokuRule
    {
        public string Name => "Last Cell In Unit";

        public Difficulty Difficulty => Difficulty.Easy;

        public bool CanApply(Board board)
        {
            /** returns true if any unit has exactly one empty cell */
            int size = board.Size;
            for (int r = 0; r < size; r++) if (board.GetRow(r).Count(c => !c.Value.HasValue) == 1) return true;
            for (int c = 0; c < size; c++) if (board.GetColumn(c).Count(cell => !cell.Value.HasValue) == 1) return true;
            for (int b = 0; b < size; b++) if (board.GetBox(b).Count(cell => !cell.Value.HasValue) == 1) return true;
            return false;
        }

        public RuleResult CalculateChanges(Board board)
        {
            var result = new RuleResult();
            int size = board.Size;
            for (int r = 0; r < size; r++) if (ProcessSingleEmptyUnit(board.GetRow(r), board, result)) return result;
            for (int c = 0; c < size; c++) if (ProcessSingleEmptyUnit(board.GetColumn(c), board, result)) return result;            /** boxes */
            for (int b = 0; b < size; b++) if (ProcessSingleEmptyUnit(board.GetBox(b), board, result)) return result;

            result.Apply = false;
            return result;
        }

        private bool ProcessSingleEmptyUnit(IEnumerable<Cell> unit, Board board, RuleResult result)
        {
            var empties = unit.Where(cell => !cell.Value.HasValue).ToList();
            if (empties.Count != 1) return false;

            int size = board.Size;
            Cell empty = empties[0];
            var present = unit.Where(c => c.Value.HasValue).Select(c => c.Value.Value).ToHashSet();

            int missing = -1;
            for (int d = 1; d <= size; d++) if (!present.Contains(d)) { missing = d; break; }
            if (missing == -1) return false;

            var change = new CellChange { Row = empty.Row, Column = empty.Column, OldValue = empty.Value, NewValue = missing };
            // When enacting a placement, record candidate removals for the target
            // cell (so EnactCandidates can leave only the missing digit). Do NOT
            // mark every filled unit cell as a "used" cell for UI highlighting;
            // only the deduced cell itself should be shown as the deduction.
            for (int v = 1; v <= size; v++) if (v != missing) change.RemovedCandidates.Add(v);

            result.Changes.Add(change);
            // Record only the empty cell + candidate as the used/deduction cell.
            if (!result.UsedCells.Exists(u => u.Row == empty.Row && u.Column == empty.Column && u.Candidate == missing))
                result.UsedCells.Add(new UsedCell { Row = empty.Row, Column = empty.Column, Candidate = missing });

            foreach (Cell peer in board.GetPeers(empty))
            {
                if (peer.Candidates.Contains(missing))
                {
                    var peerChange = new CellChange { Row = peer.Row, Column = peer.Column };
                    peerChange.RemovedCandidates.Add(missing);
                    result.Changes.Add(peerChange);
                    // Do NOT add peers to UsedCells here; they are changed but are
                    // not the primary deduction to highlight for the Last-Cell rule.
                }
            }

            result.Apply = true;
            result.Description = $"Placed {missing} at ({empty.Row},{empty.Column}) via Last-Cell-In-Unit";
            return true;
        }
    }
}
