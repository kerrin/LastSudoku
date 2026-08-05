using System.Collections.Generic;
using System.Linq;
using Cell = Sudoku.Models.Cell;
using Board = Sudoku.Models.Board;

namespace Sudoku.Solver.Rules
{
    /**
     * Shared implementation for Naked Pair/Triple/Quad subset elimination rules.
     */
    public abstract class NakedSubsetRuleBase : CachedRuleBase
    {
        /** Number of cells and union-candidates required for the subset. */
        protected abstract int SubsetSize { get; }

        /** Label used in descriptions (Pair, Triple, Quad). */
        protected abstract string SubsetLabel { get; }

        /**
         * Find the first applicable naked-subset elimination in rows, columns, then boxes.
         */
        protected override RuleResult CalculateChangesInternal(Board board)
        {
            var rowHit = FindAndBuildNakedSubset(board, EnumerateRows(board), SubsetSize, SubsetLabel);
            if (rowHit != null)
            {
                return rowHit;
            }

            var columnHit = FindAndBuildNakedSubset(board, EnumerateColumns(board), SubsetSize, SubsetLabel);
            if (columnHit != null)
            {
                return columnHit;
            }

            var boxHit = FindAndBuildNakedSubset(board, EnumerateBoxes(board), SubsetSize, SubsetLabel);
            if (boxHit != null)
            {
                return boxHit;
            }

            return new RuleResult { Apply = false };
        }

        /**
         * Search a sequence of units and return the first naked-subset elimination result.
         */
        private static RuleResult FindAndBuildNakedSubset(
            Board board,
            IEnumerable<(string unitName, int unitIndex, List<Cell> cells)> units,
            int subsetSize,
            string subsetLabel)
        {
            foreach (var unit in units)
            {
                var emptyCells = unit.cells
                    .Where(c => !c.Value.HasValue && c.Candidates.Count >= 2 && c.Candidates.Count <= subsetSize)
                    .ToList();

                if (emptyCells.Count < subsetSize)
                {
                    continue;
                }

                foreach (var indices in EnumerateIndexCombinations(emptyCells.Count, subsetSize))
                {
                    var subsetCells = indices.Select(index => emptyCells[index]).ToList();
                    var subsetDigits = new HashSet<int>();
                    foreach (var cell in subsetCells)
                    {
                        subsetDigits.UnionWith(cell.Candidates);
                    }

                    if (subsetDigits.Count != subsetSize)
                    {
                        continue;
                    }

                    var changes = BuildEliminationChanges(unit.cells, subsetCells, subsetDigits);
                    if (changes.Count == 0)
                    {
                        continue;
                    }

                    var orderedDigits = subsetDigits.OrderBy(d => d).ToArray();
                    var result = new RuleResult
                    {
                        Apply = true,
                        Description = $"Naked {subsetLabel} ({string.Join(", ", orderedDigits)}) in {unit.unitName} {unit.unitIndex}"
                    };

                    result.Changes.AddRange(changes);
                    AddUsedCells(result, subsetCells, subsetDigits);
                    return result;
                }
            }

            return null;
        }

        /**
         * Build candidate removals for cells in the unit that are outside the subset.
         */
        private static List<CellChange> BuildEliminationChanges(List<Cell> unitCells, List<Cell> subsetCells, HashSet<int> subsetDigits)
        {
            var subsetSet = new HashSet<Cell>(subsetCells);
            var changes = new List<CellChange>();

            foreach (var cell in unitCells)
            {
                if (cell.Value.HasValue || subsetSet.Contains(cell))
                {
                    continue;
                }

                var removed = cell.Candidates.Where(subsetDigits.Contains).Distinct().OrderBy(x => x).ToList();
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
         * Mark subset cells/candidates as used in the deduction.
         */
        private static void AddUsedCells(RuleResult result, List<Cell> subsetCells, HashSet<int> subsetDigits)
        {
            foreach (var cell in subsetCells)
            {
                foreach (int candidate in subsetDigits)
                {
                    result.UsedCells.Add(new UsedCell { Row = cell.Row, Column = cell.Column, Candidate = candidate });
                }
            }
        }

        /**
         * Enumerate all ascending index combinations of length k from [0, n).
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
}
