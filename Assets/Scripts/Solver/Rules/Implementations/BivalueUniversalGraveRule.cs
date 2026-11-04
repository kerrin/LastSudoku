using System.Collections.Generic;
using Sudoku.Models;
using Board = Sudoku.Models.Board;

namespace Sudoku.Solver.Rules
{
    /**
     * A simple Bivalue Universal Grave (BUG) implementation.
     *
     * This rule assumes the sudoku has a single solution, 
     * so any change to candidates or setting of values that would result in both:
     * * All cells having exactly 2 candidates
     * * Each candidate appearing in exactly 2 cells
     * 
     * We apply this rule when all but one cell is in a bug state and that one cell has three candidates (often called a BUG+1), and removing one of those 3 candidates results in a BUG state.
     * We can deduce that the candidate that removingcauses a bug state must be the solution for that cell, and we can set that value, as anything else would create an invalid solution (BUG state).
     * The candidate that gets set as the valueis the candidate that appears 3 times in a row, column or box.
     */
    public class BivalueUniversalGraveRule : CachedRuleBase
    {
        public override string Name => "Bivalue Universal Grave";

        public override Difficulty Difficulty => Difficulty.Medium;

        private sealed class Deduction
        {
            public Cell Target;
            public int Candidate;
            public List<Cell> WitnessCells;
        }

        public override bool CanApply(Board board)
        {
            return CalculateChanges(board).Apply;
        }

        protected override RuleResult CalculateChangesInternal(Board board)
        {
            var result = new RuleResult();
            Deduction deduction = FindDeduction(board);
            if (deduction == null)
            {
                result.Apply = false;
                return result;
            }

            result.Changes.Add(new CellChange
            {
                Row = deduction.Target.Row,
                Column = deduction.Target.Column,
                NewValue = deduction.Candidate
            });

            foreach (Cell witness in deduction.WitnessCells)
            {
                result.UsedCells.Add(new UsedCell
                {
                    Row = witness.Row,
                    Column = witness.Column,
                    Candidate = deduction.Candidate
                });
            }

            result.Apply = true;
            result.Description =
                $"Placed {deduction.Candidate} at ({deduction.Target.Row},{deduction.Target.Column}) via Bivalue Universal Grave";
            return result;
        }

        private static Deduction FindDeduction(Board board)
        {
            if (board == null || board.Cells == null)
            {
                return null;
            }

            Cell target = null;
            for (int row = 0; row < board.Size; row++)
            {
                for (int column = 0; column < board.Size; column++)
                {
                    Cell cell = board.Cells[row, column];
                    if (cell == null || cell.Value.HasValue)
                    {
                        continue;
                    }

                    int candidateCount = cell.Candidates?.Count ?? 0;
                    if (candidateCount == 2)
                    {
                        continue;
                    }

                    if (candidateCount != 3 || target != null)
                    {
                        return null;
                    }

                    target = cell;
                }
            }

            if (target == null)
            {
                return null;
            }

            int extraCandidate = 0;
            var witnessCells = new List<Cell>();
            for (int row = 0; row < board.Size; row++)
            {
                if (!ValidateUnit(board.GetRow(row), target, board.Size, ref extraCandidate, witnessCells))
                {
                    return null;
                }
            }

            for (int column = 0; column < board.Size; column++)
            {
                if (!ValidateUnit(board.GetColumn(column), target, board.Size, ref extraCandidate, witnessCells))
                {
                    return null;
                }
            }

            for (int box = 0; box < board.Size; box++)
            {
                if (!ValidateUnit(board.GetBox(box), target, board.Size, ref extraCandidate, witnessCells))
                {
                    return null;
                }
            }

            if (extraCandidate == 0 || !target.Candidates.Contains(extraCandidate))
            {
                return null;
            }

            return new Deduction
            {
                Target = target,
                Candidate = extraCandidate,
                WitnessCells = witnessCells
            };
        }

        private static bool ValidateUnit(
            IEnumerable<Cell> cells,
            Cell target,
            int size,
            ref int extraCandidate,
            List<Cell> witnessCells)
        {
            var candidateCounts = new int[size + 1];
            var placedValues = new bool[size + 1];
            bool containsTarget = false;

            foreach (Cell cell in cells)
            {
                if (cell == null)
                {
                    return false;
                }

                if (ReferenceEquals(cell, target))
                {
                    containsTarget = true;
                }

                if (cell.Value.HasValue)
                {
                    int value = cell.Value.Value;
                    if (value < 1 || value > size)
                    {
                        return false;
                    }

                    placedValues[value] = true;
                    continue;
                }

                if (cell.Candidates == null)
                {
                    return false;
                }

                foreach (int candidate in cell.Candidates)
                {
                    if (candidate < 1 || candidate > size)
                    {
                        return false;
                    }

                    candidateCounts[candidate]++;
                }
            }

            for (int candidate = 1; candidate <= size; candidate++)
            {
                int count = candidateCounts[candidate];
                if (placedValues[candidate])
                {
                    if (count != 0)
                    {
                        return false;
                    }

                    continue;
                }

                if (count == 2)
                {
                    continue;
                }

                if (count != 3 || !containsTarget || !target.Candidates.Contains(candidate))
                {
                    return false;
                }

                if (extraCandidate != 0 && extraCandidate != candidate)
                {
                    return false;
                }

                extraCandidate = candidate;
                foreach (Cell cell in cells)
                {
                    if (!cell.Value.HasValue && cell.Candidates.Contains(candidate) && !witnessCells.Contains(cell))
                    {
                        witnessCells.Add(cell);
                    }
                }
            }

            return true;
        }
    }
}
