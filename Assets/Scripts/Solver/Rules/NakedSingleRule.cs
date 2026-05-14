using System.Linq;
using System.Collections.Generic;
using Sudoku.Models;

namespace Sudoku.Solver.Rules
{
    /**
     * Implements the Naked Single technique: when a cell has exactly one candidate
     * it must be that digit.
     */
    public class NakedSingleRule : ISudokuRule
    {
        /** Rule display name. */
        public string Name => "Naked Single";

        /** Difficulty classification for this rule. */
        public Difficulty Difficulty => Difficulty.Easy;

        /**
         * Quick check to see if any cell has exactly one candidate.
         */
        public bool CanApply(Board board)
        {
            for (int r = 0; r < board.Size; r++)
                for (int c = 0; c < board.Size; c++)
                {
                    Cell cell = board.Cells[r, c];
                    if (!cell.Value.HasValue && cell.Candidates.Count == 1) return true;
                }
            return false;
        }

        /**
         * Apply the first naked-single found: set the cell and remove the digit
         * from all peers' candidate sets.
         */
        public RuleResult Apply(Board board)
        {
            var result = new RuleResult();
            for (int r = 0; r < board.Size; r++)
            {
                for (int c = 0; c < board.Size; c++)
                {
                    Cell cell = board.Cells[r, c];
                    if (cell.Value.HasValue) continue;
                    if (cell.Candidates.Count != 1) continue;
                    int value = cell.Candidates.First();
                    var change = new CellChange { Row = r, Column = c, OldValue = cell.Value, NewValue = value };
                    // mark peers with values as used for deduction
                    foreach (Cell peer in board.GetPeers(cell))
                    {
                        if (peer.Value.HasValue && !result.UsedCells.Exists(u => u.Row == peer.Row && u.Column == peer.Column))
                            result.UsedCells.Add(new UsedCell { Row = peer.Row, Column = peer.Column });
                    }

                    board.SetValue(cell, value);
                    result.Changes.Add(change);
                    // remove candidate from peers — record removals as separate changes per peer
                    foreach (Cell peer in board.GetPeers(cell))
                    {
                        if (peer.Candidates.Remove(value))
                        {
                            var peerChange = new CellChange { Row = peer.Row, Column = peer.Column };
                            peerChange.RemovedCandidates.Add(value);
                            result.Changes.Add(peerChange);
                            if (!result.UsedCells.Exists(u => u.Row == peer.Row && u.Column == peer.Column))
                                result.UsedCells.Add(new UsedCell { Row = peer.Row, Column = peer.Column });
                        }
                    }
                    result.Applied = true;
                    result.Description = $"Placed {value} at ({r},{c}) via Naked Single";
                    return result;
                }
            }
            result.Applied = false;
            return result;
        }

        public bool UpdateCandidates(Board board)
        {
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
                            cell.Candidates.Clear();
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
                        cell.Candidates = newCandidates;
                        changed = true;
                    }
                }
            }
            return changed;
        }
    }
}
