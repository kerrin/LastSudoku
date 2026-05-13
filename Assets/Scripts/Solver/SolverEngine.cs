using System.Collections.Generic;
using Sudoku.Models;
using Sudoku.Solver.Rules;

namespace Sudoku.Solver
{
    /// <summary>
    /// Lightweight solver engine that uses a <see cref="RuleRegistry"/> to
    /// iteratively apply techniques until the puzzle is solved or no progress is made.
    /// </summary>
    public class SolverEngine
    {
        public RuleRegistry Registry { get; }

        public SolverEngine(RuleRegistry registry = null)
        {
            Registry = registry ?? new RuleRegistry();
            if (registry == null) Registry.RegisterDefaults();
        }

        /// <summary>
        /// Attempt to solve the board by repeatedly applying registered rules.
        /// Returns true if the board is fully solved after the run; also returns
        /// the list of steps that were performed.
        /// </summary>
        public bool Solve(Board board, int maxSteps, out List<(ISudokuRule rule, RuleResult result)> steps)
        {
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

        /// <summary>
        /// Convenience overload with a default step limit.
        /// </summary>
        public bool Solve(Board board, out List<(ISudokuRule rule, RuleResult result)> steps)
        {
            return Solve(board, 1000, out steps);
        }
    }
}
