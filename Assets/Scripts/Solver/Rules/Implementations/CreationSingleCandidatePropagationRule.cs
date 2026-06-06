using Sudoku.Models;

namespace Sudoku.Solver.Rules
{
    /**
     * Special-case creation-mode rule that propagates eliminations from cells
     * that already have exactly one candidate.
     *
     * IMPORTANT:
     * - This rule is not registered in RuleRegistry.
     * - It is not part of normal solver execution or UI rule toggles.
     */
    public sealed class CreationSingleCandidatePropagationRule
    {
        /**
         * Propagate naked-single candidate eliminations across peers without placing values.
         *
         * @param board Active board.
         */
        public void Apply(Board board)
        {
            if (board == null || board.Cells == null)
            {
                return;
            }

            int safetyBudget = board.Size * board.Size * board.Size;
            bool anyRemoved;

            do
            {
                anyRemoved = false;

                for (int r = 0; r < board.Size; r++)
                {
                    for (int c = 0; c < board.Size; c++)
                    {
                        var cell = board.Cells[r, c];
                        if (cell == null || cell.Value.HasValue || cell.Candidates == null || cell.Candidates.Count != 1)
                        {
                            continue;
                        }

                        int singleDigit = 0;
                        foreach (int candidate in cell.Candidates)
                        {
                            singleDigit = candidate;
                            break;
                        }

                        if (singleDigit < 1 || singleDigit > board.Size)
                        {
                            continue;
                        }

                        foreach (var peer in board.GetPeers(cell))
                        {
                            if (peer == null || peer.Value.HasValue)
                            {
                                continue;
                            }

                            if (peer.Candidates == null)
                            {
                                peer.Candidates = new System.Collections.Generic.HashSet<int>();
                            }

                            if (peer.Candidates.Remove(singleDigit))
                            {
                                anyRemoved = true;
                            }
                        }
                    }
                }

                safetyBudget--;
            }
            while (anyRemoved && safetyBudget > 0);
        }
    }
}
