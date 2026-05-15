using System.Collections.Generic;
using System.Linq;
using Sudoku.Models;

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

        public RuleResult Apply(Board board)
        {
            var result = new RuleResult();
            int size = board.Size;
            for (int r = 0; r < size; r++) if (ProcessSingleEmptyUnit(board.GetRow(r), board, result)) return result;
            for (int c = 0; c < size; c++) if (ProcessSingleEmptyUnit(board.GetColumn(c), board, result)) return result;            /** boxes */
            for (int b = 0; b < size; b++) if (ProcessSingleEmptyUnit(board.GetBox(b), board, result)) return result;

            result.Applied = false;
            return result;
        }

        public RuleResult ApplyOnlyCandidates(Board board)
        {
            var result = new RuleResult();
            int size = board.Size;
            // First try unit-level candidate deductions (rows, columns, boxes)
            for (int r = 0; r < size; r++) if (ProcessSingleEmptyUnitCandidates(board.GetRow(r), board, result)) { result.Applied = true; return result; }
            for (int c = 0; c < size; c++) if (ProcessSingleEmptyUnitCandidates(board.GetColumn(c), board, result)) { result.Applied = true; return result; }
            for (int b = 0; b < size; b++) if (ProcessSingleEmptyUnitCandidates(board.GetBox(b), board, result)) { result.Applied = true; return result; }
            
            result.Applied = false;
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
            foreach (Cell p in unit.Where(c => c.Value.HasValue))
            {
                if (!result.UsedCells.Exists(u => u.Row == p.Row && u.Column == p.Column))
                    result.UsedCells.Add(new UsedCell { Row = p.Row, Column = p.Column });
            }

            board.SetValue(empty, missing);
            result.Changes.Add(change);

            foreach (Cell peer in board.GetPeers(empty))
            {
                if (peer.Candidates.Remove(missing))
                {
                    var peerChange = new CellChange { Row = peer.Row, Column = peer.Column };
                    peerChange.RemovedCandidates.Add(missing);
                    result.Changes.Add(peerChange);
                    if (!result.UsedCells.Exists(u => u.Row == peer.Row && u.Column == peer.Column))
                        result.UsedCells.Add(new UsedCell { Row = peer.Row, Column = peer.Column });
                }
            }
            result.Applied = true;
            result.Description = $"Placed {missing} at ({empty.Row},{empty.Column}) via Last-Cell-In-Unit";
            return true;
        }

        private bool ProcessSingleEmptyUnitCandidates(IEnumerable<Cell> unit, Board board, RuleResult result)
        {
            var empties = unit.Where(cell => !cell.Value.HasValue).ToList();
            if (empties.Count != 1) return false;

            int size = board.Size;
            Cell empty = empties[0];
            var present = unit.Where(c => c.Value.HasValue).Select(c => c.Value.Value).ToHashSet();

            var newCandidates = new HashSet<int>();
            for (int d = 1; d <= size; d++) if (!present.Contains(d)) newCandidates.Add(d);

            if (!newCandidates.SetEquals(empty.Candidates))
            {
                var change = new CellChange { Row = empty.Row, Column = empty.Column };
                foreach (int old in new List<int>(empty.Candidates)) if (!newCandidates.Contains(old)) change.RemovedCandidates.Add(old);
                empty.Candidates = newCandidates;
                if (change.RemovedCandidates.Count > 0) result.Changes.Add(change);
            }

            if (newCandidates.Count == 1)
            {
                int missing = newCandidates.First();
                foreach (Cell peer in board.GetPeers(empty))
                {
                    if (peer.Value.HasValue) continue;
                    if (peer.Candidates.Remove(missing))
                    {
                        var peerChange = new CellChange { Row = peer.Row, Column = peer.Column };
                        peerChange.RemovedCandidates.Add(missing);
                        result.Changes.Add(peerChange);
                    }
                }
            }

            return true;
        }
    }
}
