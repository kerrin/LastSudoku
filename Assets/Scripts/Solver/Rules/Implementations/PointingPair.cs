using System.Collections.Generic;
using System.Linq;
using Cell = Sudoku.Models.Cell;
using Board = Sudoku.Models.Board;

namespace Sudoku.Solver.Rules
{
    /**
     * A simple Pointing Pair implementation.
     *
     * A Pointing Pair (or pointing triple) occurs when all candidates of a digit inside a 3×3 box fall on the same row or column. 
     * The candidate must be placed somewhere in that box, so it must be on that row or column - 
     * which means the digit can be eliminated from the rest of that row or column outside the box.
     */
    public class PointingPairRule : ISudokuRule
    {
        public string Name => "Pointing Pair";

        public Difficulty Difficulty => Difficulty.Medium;

        public bool CanApply(Board board)
        {
            RuleResult result = CalculateChanges(board);
            return  result.Apply;
        }

        public RuleResult CalculateChanges(Board board)
        {
            var result = new RuleResult();
            int size = board.Size;

            for (int digit = 1; digit <= size; digit++)
            {
                for (int box = 0; box < size; box++)
                {
                    var info = FindElimination(board, digit, box);
                    if (info == null || info.Targets.Count == 0)
                    {
                        continue;
                    }

                    foreach (var used in info.Used)
                    {
                        if (!result.UsedCells.Exists(u => u.Row == used.Row && u.Column == used.Column && u.Candidate == digit))
                        {
                            result.UsedCells.Add(new UsedCell { Row = used.Row, Column = used.Column, Candidate = digit });
                        }
                    }

                    var targets = info.Targets
                        .Where(t => !t.Value.HasValue && t.Candidates.Contains(digit))
                        .OrderBy(t => t.Row)
                        .ThenBy(t => t.Column)
                        .ToList();

                    if (targets.Count == 0)
                    {
                        continue;
                    }

                    foreach (var target in targets)
                    {
                        var change = new CellChange { Row = target.Row, Column = target.Column };
                        change.RemovedCandidates.Add(digit);
                        result.Changes.Add(change);
                    }

                    result.Apply = true;
                    result.Description = info.Description;
                    return result;
                }
            }

            result.Apply = false;
            return result;
        }

        private class ElimInfo
        {
            public List<Cell> Used = new List<Cell>();
            public List<Cell> Targets = new List<Cell>();
            public string Description;
        }

        private static ElimInfo FindElimination(Board board, int digit, int boxIndex)
        {
            var candidatesInBox = board.GetBox(boxIndex)
                .Where(c => !c.Value.HasValue && c.Candidates.Contains(digit))
                .ToList();

            // A pointing pair/triple requires at least two candidates in the box.
            if (candidatesInBox.Count < 2)
            {
                return null;
            }

            int row = candidatesInBox[0].Row;
            bool allSameRow = candidatesInBox.All(c => c.Row == row);
            if (allSameRow)
            {
                var targets = board.GetRow(row)
                    .Where(c => c.Box != boxIndex && !c.Value.HasValue && c.Candidates.Contains(digit))
                    .ToList();
                if (targets.Count > 0)
                {
                    return new ElimInfo
                    {
                        Used = candidatesInBox,
                        Targets = targets,
                        Description = $"Removed {digit} from row {row} outside box {boxIndex} via Pointing Pair"
                    };
                }
            }

            int column = candidatesInBox[0].Column;
            bool allSameColumn = candidatesInBox.All(c => c.Column == column);
            if (allSameColumn)
            {
                var targets = board.GetColumn(column)
                    .Where(c => c.Box != boxIndex && !c.Value.HasValue && c.Candidates.Contains(digit))
                    .ToList();
                if (targets.Count > 0)
                {
                    return new ElimInfo
                    {
                        Used = candidatesInBox,
                        Targets = targets,
                        Description = $"Removed {digit} from column {column} outside box {boxIndex} via Pointing Pair"
                    };
                }
            }

            return null;
        }
    }
}
