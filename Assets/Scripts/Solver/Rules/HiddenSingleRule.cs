using System.Collections.Generic;
using Sudoku.Models;

namespace Sudoku.Solver.Rules
{
    /**
     * Hidden Single (also called Single Position) — for a given digit in a unit
     * (row/column/box), if only one cell can contain that digit according to candidates,
     * place it there.
     */
    public class HiddenSingleRule : ISudokuRule
    {
        public string Name => "Hidden Single";

        public Difficulty Difficulty => Difficulty.Easy;

        public bool CanApply(Board board)
        {
            return FindAny(board) != null;
        }

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

        public RuleResult Apply(Board board)
        {
            var result = new RuleResult();
            (Cell cell, int digit)? found = FindAny(board);
            if (found == null)
            {
                result.Applied = false;
                return result;
            }
            (Cell cell, int digit) = found.Value;
            var change = new CellChange { Row = cell.Row, Column = cell.Column, OldValue = cell.Value, NewValue = digit };
            board.SetValue(cell, digit);
            result.Changes.Add(change);
            foreach (Cell peer in board.GetPeers(cell))
            {
                if (peer.Candidates.Remove(digit))
                {
                    var peerChange = new CellChange { Row = peer.Row, Column = peer.Column };
                    peerChange.RemovedCandidates.Add(digit);
                    result.Changes.Add(peerChange);
                }
            }
            result.Applied = true;
            result.Description = $"Placed {digit} at ({cell.Row},{cell.Column}) via Hidden Single";
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
