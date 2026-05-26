using System.Linq;
using Cell = Sudoku.Models.Cell;
using Board = Sudoku.Models.Board;
using System.Collections.Generic;

namespace Sudoku.Solver.Rules
{
    /**
     * Implements the Naked Single technique:
     * If a cell can see all but one digit as set values, it must be that digit.
     * If you use this method of deducing, then it doesn't matter if the candidates are incorrect or incomplete.
     * 
     * Or another way to say it, when a cell has exactly one candidate
     * it must be that digit, but that relies on the candidates being correct, and would also be found by the Hidden Single technique.
     */
    public class NakedSingleRule : ISudokuRule
    {
        /** Rule display name. */
        public string Name => "Naked Single";

        /** Difficulty classification for this rule. */
        public Difficulty Difficulty => Difficulty.Easy;

        /**
         * Quick check to see if this rule can be applied to the given board.
         */
        public bool CanApply(Board board)
        {
           return CalculateChanges(board).Apply;
        }

        /**
         * Apply the first naked-single found: set the cell and remove the digit
         * from all peers' candidate sets.
         */
        public RuleResult CalculateChanges(Board board)
        {
            var result = new RuleResult();
            //Check for set values, incase the candidates are incomplete or incorrect.
            for (int r = 0; r < board.Size; r++)
            {
                for (int c = 0; c < board.Size; c++)
                {
                    Cell cell = board.Cells[r, c];
                    if (cell.Value.HasValue) continue;
                    int[] UniqueFoundDigits = board.GetPeers(cell).Where(p => p.Value.HasValue).Select(p => p.Value.Value).Distinct().ToArray();
                    if(UniqueFoundDigits.Length == board.Size - 1)
                    {
                        int digit = Enumerable.Range(1, board.Size).First(d => !UniqueFoundDigits.Contains(d));
                        result.UsedCells.Add(new UsedCell { Row = r, Column = c, Candidate = digit });
                        result.Changes.Add(new CellChange { Row = r, Column = c, NewValue = digit });
                        var FindDigits = new List<int>(UniqueFoundDigits);
                        // mark peers with values as used for deduction
                        foreach (Cell peer in board.GetPeers(cell))
                        {
                            // Remove candidates for value from peers
                            if (peer.Candidates.Contains(digit))
                            {
                                CellChange peerChange = new CellChange { Row = peer.Row, Column = peer.Column };
                                peerChange.RemovedCandidates.Add(digit);
                                result.Changes.Add(peerChange);
                            }

                            if(!peer.Value.HasValue) continue; // We only need to check cells with values for marking used for de3duction
                            int foundDigit = peer.Value.Value;
                            if(!FindDigits.Contains(foundDigit)) continue; // Only mark each digit once as used for deduction, even if multiple peers have it as a value
                            // Mark peer with value as used for deduction
                            if (peer.Value.HasValue && !result.UsedCells.Exists(u => u.Row == peer.Row && u.Column == peer.Column && u.Candidate == peer.Value.Value))
                            {
                                FindDigits.Remove(foundDigit);
                                result.UsedCells.Add(new UsedCell { Row = peer.Row, Column = peer.Column, Candidate = peer.Value.Value });
                            }
                        }

                        result.Apply = true;
                        result.Description = $"Placed {digit} at ({r},{c}) via Naked Single (by seeing all other digits in peers)";
                        return result;
                    }
                }
            }
            result.Apply = false;
            return result;
        }
    }
}
