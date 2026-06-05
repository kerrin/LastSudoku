using System;
using Sudoku.Models;

namespace Sudoku.Solver.Unsolver
{
    /**
     * Contract for a single unsolver strategy that removes a value from a board cell
     * so that the corresponding solving rule can reinstate it.
     *
     * Each implementation targets one specific <see cref="Sudoku.Solver.Rules.ISudokuRule"/>.
     * Implementations must never permanently corrupt the board on failure — if no
     * applicable move is found, the board must be left unchanged.
     */
    public interface IUnsolveHandler
    {
        /**
         * Type name of the solver rule this handler corresponds to
         * (matches <c>ISudokuRule.GetType().Name</c>).
         */
        string RuleName { get; }

        /**
         * Attempt to remove one value from the board such that the paired solver rule
         * would be the mechanism used to re-determine that value.
         *
         * @param board  The working board (partially solved or fully solved).
         * @param random Random source used to select among equally valid candidates.
         * @returns A <see cref="UnsolveResult"/> indicating whether a move was made.
         */
        UnsolveResult TryUnsolve(Board board, Random random);
    }
}
