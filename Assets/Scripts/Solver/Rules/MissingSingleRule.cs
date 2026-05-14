using System.Collections.Generic;
using System.Linq;
using Sudoku.Models;

namespace Sudoku.Solver.Rules
{
    /// <summary>
    /// Also called "Single Position" — when a digit can only occupy one cell
    /// within a unit (row, column, or box), it must be placed there.
    /// </summary>
    public class MissingSingleRule : ISudokuRule
    {
        /// <summary>Rule display name.</summary>
        public string Name => "Missing Single";

        /// <summary>Difficulty classification for this rule.</summary>
        public Difficulty Difficulty => Difficulty.Easy;

        /// <summary>Return true if any unit contains a digit that has only one candidate position.</summary>
        public bool CanApply(Board board)
        {
            return FindAny(board) != null;
        }

        /// <summary>
        /// Find the first (cell,digit) pair where the digit is the only candidate
        /// within its row, column, or box. Returns null when none found.
        /// </summary>
        private (Cell cell, int digit)? FindAny(Board board)
        {
            int size = board.Size;
            for (int digit = 1; digit <= size; digit++)
            {
                // rows
                for (int r = 0; r < size; r++)
                {
                    var candidates = new List<Cell>();
                    foreach (Cell cell in board.GetRow(r))
                    {
                        if (!cell.Value.HasValue && cell.Candidates.Contains(digit)) candidates.Add(cell);
                    }
                    if (candidates.Count == 1) return (candidates[0], digit);
                }

                // columns
                for (int c = 0; c < size; c++)
                {
                    var candidates = new List<Cell>();
                    foreach (Cell cell in board.GetColumn(c))
                    {
                        if (!cell.Value.HasValue && cell.Candidates.Contains(digit)) candidates.Add(cell);
                    }
                    if (candidates.Count == 1) return (candidates[0], digit);
                }

                // boxes
                for (int b = 0; b < size; b++)
                {
                    var candidates = new List<Cell>();
                    foreach (Cell cell in board.GetBox(b))
                    {
                        if (!cell.Value.HasValue && cell.Candidates.Contains(digit)) candidates.Add(cell);
                    }
                    if (candidates.Count == 1) return (candidates[0], digit);
                }
            }
            return null;
        }

        /// <summary>
        /// Apply the Missing Single found by <see cref="FindAny"/>: set the digit
        /// into the located cell and remove it from peer candidates.
        /// </summary>
        public RuleResult Apply(Board board)
        {
            var r = new RuleResult();
            (Cell cell, int digit)? found = FindAny(board);
            if (found == null)
            {
                r.Applied = false;
                return r;
            }
            (Cell cell, int digit) = found.Value;
            var change = new CellChange { Row = cell.Row, Column = cell.Column, OldValue = cell.Value, NewValue = digit };
            board.SetValue(cell, digit);
            r.Changes.Add(change);
            foreach (Cell peer in board.GetPeers(cell))
            {
                if (peer.Candidates.Remove(digit))
                {
                    var peerChange = new CellChange { Row = peer.Row, Column = peer.Column };
                    peerChange.RemovedCandidates.Add(digit);
                    r.Changes.Add(peerChange);
                }
            }
            r.Applied = true;
            r.Description = $"Placed {digit} at ({cell.Row},{cell.Column}) via Missing Single";
            
            return r;
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
