using System;
using Sudoku.Models;

namespace Sudoku.Solver.Unsolver
{
    /**
     * Stub unsolve handler for candidate-only rules (e.g. Box-Line, Skyscraper,
     * X-Wing, Y-Wing).
     *
     * These rules eliminate candidates but never place a value directly. Unsolving
     * them by removing a value is not meaningful in the MVP, so this handler always
     * returns <see cref="UnsolveResult.NotSupported"/> and never mutates the board.
     */
    public class CandidateOnlyUnsolveHandler : IUnsolveHandler
    {
        public string RuleName { get; }

        public CandidateOnlyUnsolveHandler(string ruleName)
        {
            RuleName = ruleName;
        }

        /** Always returns <see cref="UnsolveResult.NotSupported"/>; board is never modified. */
        public UnsolveResult TryUnsolve(Board board, Random random)
        {
            return UnsolveResult.NotSupported;
        }
    }
}
