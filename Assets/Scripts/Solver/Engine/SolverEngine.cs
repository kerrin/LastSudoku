using System.Collections.Generic;
using Sudoku.Models;
using Sudoku.Solver.Rules;

namespace Sudoku.Solver
{
    /**
     * Lightweight solver engine that uses a <see cref="RuleRegistry"/> to
     * iteratively apply techniques until the puzzle is solved or no progress is made.
     */
    public class SolverEngine
    {
        public RuleRegistry Registry { get; }

        public SolverEngine(RuleRegistry registry = null)
        {
            Registry = registry ?? new RuleRegistry();
            if (registry == null) {
                Registry.RegisterMinimal();
                Registry.RegisterMedium();
                Registry.RegisterAdvanced();
            }
        }

        /**
         * Attempt to solve the board by repeatedly applying registered rules.
         * Returns true if the board is fully solved after the run; also returns
         * the list of steps that were performed.
         */
        public bool Solve(Board board, int maxSteps, out List<(ISudokuRule rule, RuleResult result)> steps)
        {
            // First make sure all candidates are up to date before we start applying rules.
            // Recompute candidates from the current board state so rules operate on
            // a consistent set of pencilmarks. For solved cells we clear any
            // candidates; for empty cells we initialise the full range and then
            // eliminate values already present in peers.
            for (int r = 0; r < board.Size; r++)
            {
                for (int c = 0; c < board.Size; c++)
                {
                    var cell = board.Cells[r, c];
                    if (cell.Value.HasValue)
                    {
                        cell.Candidates.Clear();
                        continue;
                    }

                    // Ensure candidates contain the full domain 1..Size
                    cell.Candidates.Clear();
                    for (int v = 1; v <= board.Size; v++) cell.Candidates.Add(v);

                    // Remove any candidate equal to a peer's placed value
                    foreach (var peer in board.GetPeers(cell))
                    {
                        if (peer.Value.HasValue) cell.Candidates.Remove(peer.Value.Value);
                    }
                }
            }
            // then we can apply rules in sequence until we either solve the board or reach the step limit
            steps = Registry.ApplyAll(board, maxSteps);
            // check whether solved
            for (int r = 0; r < board.Size; r++)
            {
                for (int c = 0; c < board.Size; c++)
                {
                    if (!board.Cells[r, c].Value.HasValue) return false;
                }
            }
            return true;
        }

        /**
         * Convenience overload with a default step limit.
         */
        public bool Solve(Board board, out List<(ISudokuRule rule, RuleResult result)> steps)
        {
            return Solve(board, 1000, out steps);
        }
    }
}
