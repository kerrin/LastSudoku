using System;
using System.Collections.Generic;
using Sudoku.Models;
using Sudoku.Solver.Rules;

namespace Sudoku.Solver.Unsolver
{
    /**
     * Unsolve handler for the Naked Single rule.
     *
     * A cell <c>C</c> with value <c>V</c> is a valid Naked Single unsolve candidate when
     * all eight other digits (1..Size except V) already appear as set values among C's peers.
     * After removing V from C, the NakedSingle rule can re-place V by observing that every
     * other digit is visible in the peer set.
     */
    public class NakedSingleUnsolveHandler : IUnsolveHandler
    {
        public string RuleName => nameof(NakedSingleRule);

        public UnsolveResult TryUnsolve(Board board, Random random)
        {
            var candidates = BuildCandidateList(board);
            if (candidates.Count == 0) return UnsolveResult.NoApplicableMove;

            var chosen = candidates[random.Next(candidates.Count)];
            chosen.Value = null;
            chosen.IsGiven = false;
            return UnsolveResult.Success;
        }

        /**
         * Collect all non-given cells whose value can be safely removed for a Naked Single.
         * Exposed for testing convenience.
         */
        public List<Cell> BuildCandidateList(Board board)
        {
            var result = new List<Cell>();
            for (int r = 0; r < board.Size; r++)
            {
                for (int c = 0; c < board.Size; c++)
                {
                    var cell = board.Cells[r, c];
                    if (!cell.Value.HasValue || cell.IsGiven) continue;

                    if (AllOtherValuesVisibleInPeers(board, cell))
                        result.Add(cell);
                }
            }
            return result;
        }

        /**
         * Returns true when every digit in 1..Size except cell.Value appears as a set value
         * in at least one of the cell's peers (row ∪ column ∪ box, excluding the cell itself).
         */
        private static bool AllOtherValuesVisibleInPeers(Board board, Cell cell)
        {
            int value = cell.Value.Value;
            var peerValues = new HashSet<int>();
            foreach (var peer in board.GetPeers(cell))
                if (peer.Value.HasValue) peerValues.Add(peer.Value.Value);

            for (int d = 1; d <= board.Size; d++)
            {
                if (d == value) continue;
                if (!peerValues.Contains(d)) return false;
            }
            return true;
        }
    }
}
