using System;
using System.Collections.Generic;
using System.Linq;
using Cell = Sudoku.Models.Cell;
using Board = Sudoku.Models.Board;

namespace Sudoku.Solver.Rules
{
    /**
     * Shared helpers for Hidden rules that inspect row/column/box units.
     */
    public abstract class HiddenRuleBase : CachedRuleBase
    {
        /**
         * Enumerate units in solver order: rows, columns, then boxes.
         *
         * @param board Board to inspect.
         * @returns Sequence of units with label, index and cells.
         */
        protected static IEnumerable<(string unitName, int unitIndex, List<Cell> cells)> EnumerateUnitsInSolverOrder(Board board)
        {
            foreach (var row in EnumerateRows(board))
            {
                yield return row;
            }

            foreach (var column in EnumerateColumns(board))
            {
                yield return column;
            }

            foreach (var box in EnumerateBoxes(board))
            {
                yield return box;
            }
        }

        private static IEnumerable<(string unitName, int unitIndex, List<Cell> cells)> EnumerateRows(Board board)
        {
            for (int row = 0; row < board.Size; row++)
            {
                yield return ("row", row, board.GetRow(row).ToList());
            }
        }

        private static IEnumerable<(string unitName, int unitIndex, List<Cell> cells)> EnumerateColumns(Board board)
        {
            for (int column = 0; column < board.Size; column++)
            {
                yield return ("column", column, board.GetColumn(column).ToList());
            }
        }

        private static IEnumerable<(string unitName, int unitIndex, List<Cell> cells)> EnumerateBoxes(Board board)
        {
            for (int box = 0; box < board.Size; box++)
            {
                yield return ("box", box, board.GetBox(box).ToList());
            }
        }
    }

    /**
     * Shared implementation for Hidden Pair/Triple/Quad subset elimination.
     */
    public abstract class HiddenSubsetRuleBase : HiddenRuleBase
    {
        /** Number of digits/cells in the subset. */
        protected abstract int SubsetSize { get; }

        /** Label used in result descriptions (Pair, Triple, Quad). */
        protected abstract string SubsetLabel { get; }

        protected override RuleResult CalculateChangesInternal(Board board)
        {
            foreach (var unit in EnumerateUnitsInSolverOrder(board))
            {
                var hit = TryBuildHiddenSubsetResult(board, unit, SubsetSize, SubsetLabel);
                if (hit != null)
                {
                    return hit;
                }
            }

            return new RuleResult { Apply = false };
        }

        /**
         * Find a hidden subset in one unit and build elimination changes.
         *
         * @param board Board being solved.
         * @param unit Unit metadata and cells.
         * @param subsetSize Required subset size.
         * @param subsetLabel Label used in result text.
         * @returns Rule result when a hidden subset trims candidates, otherwise null.
         */
        private static RuleResult TryBuildHiddenSubsetResult(
            Board board,
            (string unitName, int unitIndex, List<Cell> cells) unit,
            int subsetSize,
            string subsetLabel)
        {
            var emptyCells = unit.cells.Where(c => !c.Value.HasValue).ToList();
            if (emptyCells.Count < subsetSize)
            {
                return null;
            }

            var candidateMap = BuildCandidateMap(board.Size, emptyCells);
            var candidateDigits = candidateMap
                .Where(kvp => kvp.Value.Count >= 2 && kvp.Value.Count <= subsetSize)
                .Select(kvp => kvp.Key)
                .OrderBy(x => x)
                .ToList();

            if (candidateDigits.Count < subsetSize)
            {
                return null;
            }

            foreach (var indexSet in EnumerateIndexCombinations(candidateDigits.Count, subsetSize))
            {
                var subsetDigits = indexSet.Select(index => candidateDigits[index]).ToList();

                var subsetCellsSet = new HashSet<Cell>();
                foreach (int digit in subsetDigits)
                {
                    subsetCellsSet.UnionWith(candidateMap[digit]);
                }

                if (subsetCellsSet.Count != subsetSize)
                {
                    continue;
                }

                var subsetCells = subsetCellsSet
                    .OrderBy(cell => cell.Row)
                    .ThenBy(cell => cell.Column)
                    .ToList();

                var changes = BuildSubsetCellTrims(subsetCells, subsetDigits);
                if (changes.Count == 0)
                {
                    continue;
                }

                var orderedDigits = subsetDigits.OrderBy(x => x).ToArray();
                var result = new RuleResult
                {
                    Apply = true,
                    Description = $"Hidden {subsetLabel} ({string.Join(", ", orderedDigits)}) in {unit.unitName} {unit.unitIndex}"
                };

                result.Changes.AddRange(changes);
                AddUsedCells(result, subsetCells, orderedDigits);
                return result;
            }

            return null;
        }

        /**
         * Build digit -> cells map for empty cells in a unit.
         *
         * @param boardSize Board size (max digit).
         * @param emptyCells Empty cells in one unit.
         * @returns Candidate map keyed by digit.
         */
        private static Dictionary<int, List<Cell>> BuildCandidateMap(int boardSize, List<Cell> emptyCells)
        {
            var candidateMap = new Dictionary<int, List<Cell>>(boardSize);
            for (int digit = 1; digit <= boardSize; digit++)
            {
                candidateMap[digit] = new List<Cell>();
            }

            foreach (var cell in emptyCells)
            {
                foreach (int candidate in cell.Candidates)
                {
                    if (candidateMap.TryGetValue(candidate, out var cellsWithCandidate))
                    {
                        cellsWithCandidate.Add(cell);
                    }
                }
            }

            return candidateMap;
        }

        /**
         * Build removals on subset cells by stripping non-subset digits.
         *
         * @param subsetCells Cells participating in the hidden subset.
         * @param subsetDigits Digits that define the subset.
         * @returns Candidate-removal changes.
         */
        private static List<CellChange> BuildSubsetCellTrims(List<Cell> subsetCells, List<int> subsetDigits)
        {
            var allowedDigits = new HashSet<int>(subsetDigits);
            var changes = new List<CellChange>();

            foreach (var cell in subsetCells)
            {
                var removed = cell.Candidates
                    .Where(candidate => !allowedDigits.Contains(candidate))
                    .Distinct()
                    .OrderBy(x => x)
                    .ToList();

                if (removed.Count == 0)
                {
                    continue;
                }

                changes.Add(new CellChange
                {
                    Row = cell.Row,
                    Column = cell.Column,
                    RemovedCandidates = removed
                });
            }

            return changes;
        }

        /**
         * Mark subset evidence cells and digits as used.
         *
         * @param result Rule result to append used-cells to.
         * @param subsetCells Cells in the subset.
         * @param subsetDigits Digits in the subset.
         */
        private static void AddUsedCells(RuleResult result, List<Cell> subsetCells, IReadOnlyCollection<int> subsetDigits)
        {
            foreach (var cell in subsetCells)
            {
                foreach (int digit in subsetDigits)
                {
                    result.UsedCells.Add(new UsedCell
                    {
                        Row = cell.Row,
                        Column = cell.Column,
                        Candidate = digit
                    });
                }
            }
        }

        /**
         * Enumerate all ascending index combinations of length k from [0, n).
         *
         * @param n Number of available items.
         * @param k Combination size.
         * @returns Index combinations.
         */
        private static IEnumerable<int[]> EnumerateIndexCombinations(int n, int k)
        {
            if (k <= 0 || k > n)
            {
                yield break;
            }

            var indices = Enumerable.Range(0, k).ToArray();
            while (true)
            {
                var snapshot = new int[k];
                indices.CopyTo(snapshot, 0);
                yield return snapshot;

                int pivot = k - 1;
                while (pivot >= 0 && indices[pivot] == n - k + pivot)
                {
                    pivot--;
                }

                if (pivot < 0)
                {
                    yield break;
                }

                indices[pivot]++;
                for (int i = pivot + 1; i < k; i++)
                {
                    indices[i] = indices[i - 1] + 1;
                }
            }
        }
    }
}