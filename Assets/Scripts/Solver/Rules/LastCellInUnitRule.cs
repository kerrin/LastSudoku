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

            /** helper to handle a unit */
            bool HandleUnit(IEnumerable<Cell> unit)
            {
                var empties = unit.Where(cell => !cell.Value.HasValue).ToList();
                if (empties.Count != 1) return false;
                Cell empty = empties[0];
                /** find missing digit */
                var present = unit.Where(c => c.Value.HasValue).Select(c => c.Value.Value).ToHashSet();
                int missing = -1;
                for (int d = 1; d <= size; d++) if (!present.Contains(d)) { missing = d; break; }
                if (missing == -1) return false;
                var change = new CellChange { Row = empty.Row, Column = empty.Column, OldValue = empty.Value, NewValue = missing };
                // mark present cells in the unit as used for deduction
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

            /** rows */
            for (int r = 0; r < size; r++) if (HandleUnit(board.GetRow(r))) return result;
            /** columns */
            for (int c = 0; c < size; c++) if (HandleUnit(board.GetColumn(c))) return result;
            /** boxes */
            for (int b = 0; b < size; b++) if (HandleUnit(board.GetBox(b))) return result;

            result.Applied = false;
            return result;
        }

        public RuleResult ApplyOnlyCandidates(Board board)
        {
            var result = new RuleResult();
            bool changed = false;
            int size = board.Size;
            for (int r = 0; r < size; r++)
            {
                for (int c = 0; c < size; c++)
                {
                    Cell cell = board.Cells[r, c];
                    if (cell.Value.HasValue)
                    {
                        if (cell.Candidates.Count != 0)
                        {
                            var change = new CellChange { Row = r, Column = c };
                            foreach (int rem in cell.Candidates) change.RemovedCandidates.Add(rem);
                            cell.Candidates.Clear();
                            result.Changes.Add(change);
                            changed = true;
                        }
                        continue;
                    }
                    var present = new bool[size + 1];
                    foreach (Cell peer in board.GetPeers(cell)) if (peer.Value.HasValue) present[peer.Value.Value] = true;
                    var newCandidates = new HashSet<int>();
                    for (int d = 1; d <= size; d++) if (!present[d]) newCandidates.Add(d);
                    if (!newCandidates.SetEquals(cell.Candidates))
                    {
                        var change = new CellChange { Row = r, Column = c };
                        foreach (int old in new List<int>(cell.Candidates))
                        {
                            if (!newCandidates.Contains(old)) change.RemovedCandidates.Add(old);
                        }
                        cell.Candidates = newCandidates;
                        if (change.RemovedCandidates.Count > 0) result.Changes.Add(change);
                        changed = true;
                    }
                }
            }
            result.Applied = changed;
            if (changed) result.Description = "Updated candidate sets via Last-Cell-In-Unit candidate refresh";
            return result;
        }
    }
}
