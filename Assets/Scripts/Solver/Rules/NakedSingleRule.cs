using System.Linq;
using System.Collections.Generic;
using Sudoku.Models;

namespace Sudoku.Solver.Rules
{
    /// <summary>
    /// Implements the Naked Single technique: when a cell has exactly one candidate
    /// it must be that digit.
    /// </summary>
    public class NakedSingleRule : ISudokuRule
    {
        /// <summary>Rule display name.</summary>
        public string Name => "Naked Single";

        /// <summary>Difficulty classification for this rule.</summary>
        public Difficulty Difficulty => Difficulty.Easy;

        /// <summary>
        /// Quick check to see if any cell has exactly one candidate.
        /// </summary>
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

        /// <summary>
        /// Apply the first naked-single found: set the cell and remove the digit
        /// from all peers' candidate sets.
        /// </summary>
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
