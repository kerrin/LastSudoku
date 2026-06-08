using System.Collections.Generic;
using Sudoku.Models;

namespace Sudoku.Solver.Unsolver
{
    public static class UnsolveValueSemantics
    {
        public static bool CellRepresentsValue(Cell cell, int value)
        {
            if (cell == null)
            {
                return false;
            }

            if (cell.Value.HasValue)
            {
                return cell.Value.Value == value;
            }

            return cell.Candidates != null
                && cell.Candidates.Count == 1
                && cell.Candidates.Contains(value);
        }

        public static bool TryGetEffectiveValue(Cell cell, out int value)
        {
            value = 0;
            if (cell == null)
            {
                return false;
            }

            if (cell.Value.HasValue)
            {
                value = cell.Value.Value;
                return true;
            }

            if (cell.Candidates != null && cell.Candidates.Count == 1)
            {
                foreach (int candidate in cell.Candidates)
                {
                    value = candidate;
                    return true;
                }
            }

            return false;
        }

        public static bool UnitContainsValue(IEnumerable<Cell> unit, int value)
        {
            foreach (var cell in unit)
            {
                if (CellRepresentsValue(cell, value))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
