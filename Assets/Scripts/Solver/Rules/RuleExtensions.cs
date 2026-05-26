using System.Collections.Generic;
using Sudoku.Models;

namespace Sudoku.Solver.Rules
{
    /**
     * Convenience extension methods for reading and mutating a <see cref="Board"/>.
     * These helpers simplify common unit (row/column/box) iteration and peer lookup.
     */
    public static class RuleExtensions
    {
        /** Return a list of all candidates for the given cell except the specified digit. */
        public static List<int> AllCandidatesExcept(Cell cell, int digit)
        {
            var result = new List<int>();
            foreach (int c in cell.Candidates) if (c != digit) result.Add(c);
            return result;
        }
    }
}
