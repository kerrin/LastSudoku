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
        public RuleResult CalculateChanges(Board board)
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
                        // Remove candidates for value from peers
                        if (peer.Candidates.Contains(value))
                        {
                            var peerChange = new CellChange { Row = peer.Row, Column = peer.Column };
                            peerChange.RemovedCandidates.Add(value);
                            result.Changes.Add(peerChange);                            
                        }
                    }

                    // Record the value placement
                    result.Changes.Add(change);
                    
                    result.Apply = true;
                    result.Description = $"Placed {value} at ({r},{c}) via Naked Single";
                    return result;
                }
            }
            result.Apply = false;
            return result;
        }
    }
}
