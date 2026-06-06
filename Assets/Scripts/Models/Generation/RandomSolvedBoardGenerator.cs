using System;
using Sudoku.Solver.Rules;

namespace Sudoku.Models
{
    /**
     * Generates random solved Sudoku boards by applying symmetry-preserving
     * row/column transformations to a known solved seed layout.
     */
    public static class RandomSolvedBoardGenerator
    {
        public const int MinShuffleOperations = 10;
        public const int MaxShuffleOperations = 100;

        private const int GridSize = 9;
        private const int BoxSize = 3;
        private const string SeedSolvedPuzzleCode = "D8nur3T0Mjgp71a04REhDGYf6c8ag24nGt0B7x3KdJauEHc2";

        /**
         * Generate a random solved Sudoku board.
         *
         * @param random Optional random source. When null, a new instance is created.
         * @returns A valid solved 9x9 Sudoku board.
         */
        public static Board GenerateRandomSolvedBoard(Random random = null)
        {
            return GenerateRandomSolvedBoard(random, out _);
        }

        /**
         * Generate a random solved Sudoku board and report how many shuffle operations were used.
         *
         * @param random Optional random source. When null, a new instance is created.
         * @param shuffleOperationCount Outputs the number of transformations applied.
         * @returns A valid solved 9x9 Sudoku board.
         */
        public static Board GenerateRandomSolvedBoard(Random random, out int shuffleOperationCount)
        {
            random ??= new Random();

            var board = PuzzleCodeGenerator.DecodeBoardFromCode(SeedSolvedPuzzleCode);
            if (!IsFullySolvedAndValid(board))
            {
                throw new InvalidOperationException("Seed solved puzzle code did not decode into a valid solved board.");
            }

            shuffleOperationCount = random.Next(MinShuffleOperations, MaxShuffleOperations + 1);
            for (int i = 0; i < shuffleOperationCount; i++)
            {
                ApplyRandomTransformation(board, random);
            }

            if (!IsFullySolvedAndValid(board))
            {
                throw new InvalidOperationException("Generated board is invalid after random transformations.");
            }

            return board;
        }

        /**
         * Apply one random symmetry-preserving transformation.
         *
         * @param board Board to mutate.
         * @param random Random source.
         * @returns Nothing.
         */
        private static void ApplyRandomTransformation(Board board, Random random)
        {
            int action = random.Next(0, 4);
            switch (action)
            {
                case 0:
                    SwapRandomRowWithinBoxRow(board, random);
                    break;
                case 1:
                    SwapRandomColumnWithinBoxColumn(board, random);
                    break;
                case 2:
                    SwapRandomBoxRow(board, random);
                    break;
                default:
                    SwapRandomBoxColumn(board, random);
                    break;
            }
        }

        /**
         * Swap two rows that belong to the same 3-row band.
         *
         * @param board Board to mutate.
         * @param random Random source.
         * @returns Nothing.
         */
        private static void SwapRandomRowWithinBoxRow(Board board, Random random)
        {
            int band = random.Next(0, BoxSize);
            int rowOffsetA = random.Next(0, BoxSize);
            int rowOffsetB = GetDifferentIndex(random, rowOffsetA, BoxSize);

            int rowA = (band * BoxSize) + rowOffsetA;
            int rowB = (band * BoxSize) + rowOffsetB;
            SwapRows(board, rowA, rowB);
        }

        /**
         * Swap two columns that belong to the same 3-column stack.
         *
         * @param board Board to mutate.
         * @param random Random source.
         * @returns Nothing.
         */
        private static void SwapRandomColumnWithinBoxColumn(Board board, Random random)
        {
            int stack = random.Next(0, BoxSize);
            int colOffsetA = random.Next(0, BoxSize);
            int colOffsetB = GetDifferentIndex(random, colOffsetA, BoxSize);

            int colA = (stack * BoxSize) + colOffsetA;
            int colB = (stack * BoxSize) + colOffsetB;
            SwapColumns(board, colA, colB);
        }

        /**
         * Swap two full box-rows (each containing 3 rows).
         *
         * @param board Board to mutate.
         * @param random Random source.
         * @returns Nothing.
         */
        private static void SwapRandomBoxRow(Board board, Random random)
        {
            int boxRowA = random.Next(0, BoxSize);
            int boxRowB = GetDifferentIndex(random, boxRowA, BoxSize);

            for (int i = 0; i < BoxSize; i++)
            {
                int rowA = (boxRowA * BoxSize) + i;
                int rowB = (boxRowB * BoxSize) + i;
                SwapRows(board, rowA, rowB);
            }
        }

        /**
         * Swap two full box-columns (each containing 3 columns).
         *
         * @param board Board to mutate.
         * @param random Random source.
         * @returns Nothing.
         */
        private static void SwapRandomBoxColumn(Board board, Random random)
        {
            int boxColA = random.Next(0, BoxSize);
            int boxColB = GetDifferentIndex(random, boxColA, BoxSize);

            for (int i = 0; i < BoxSize; i++)
            {
                int colA = (boxColA * BoxSize) + i;
                int colB = (boxColB * BoxSize) + i;
                SwapColumns(board, colA, colB);
            }
        }

        /**
         * Pick an index in [0, upperExclusive) that differs from the excluded index.
         *
         * @param random Random source.
         * @param excludedIndex Index that must not be returned.
         * @param upperExclusive Exclusive upper bound.
         * @returns A different valid index.
         */
        private static int GetDifferentIndex(Random random, int excludedIndex, int upperExclusive)
        {
            int index = random.Next(0, upperExclusive - 1);
            if (index >= excludedIndex)
            {
                index++;
            }

            return index;
        }

        /**
         * Swap two board rows in-place.
         *
         * @param board Board to mutate.
         * @param rowA First row index.
         * @param rowB Second row index.
         * @returns Nothing.
         */
        private static void SwapRows(Board board, int rowA, int rowB)
        {
            if (rowA == rowB)
            {
                return;
            }

            for (int c = 0; c < GridSize; c++)
            {
                (board.Cells[rowA, c], board.Cells[rowB, c]) = (board.Cells[rowB, c], board.Cells[rowA, c]);
                EnsureCellMetadata(board, rowA, c);
                EnsureCellMetadata(board, rowB, c);
            }
        }

        /**
         * Swap two board columns in-place.
         *
         * @param board Board to mutate.
         * @param colA First column index.
         * @param colB Second column index.
         * @returns Nothing.
         */
        private static void SwapColumns(Board board, int colA, int colB)
        {
            if (colA == colB)
            {
                return;
            }

            for (int r = 0; r < GridSize; r++)
            {
                (board.Cells[r, colA], board.Cells[r, colB]) = (board.Cells[r, colB], board.Cells[r, colA]);
                EnsureCellMetadata(board, r, colA);
                EnsureCellMetadata(board, r, colB);
            }
        }

        /**
         * Ensure a cell exists and that row/column/box metadata is synchronized after swaps.
         *
         * @param board Board containing the cell.
         * @param row Row index.
         * @param col Column index.
         * @returns Nothing.
         */
        private static void EnsureCellMetadata(Board board, int row, int col)
        {
            if (board.Cells[row, col] == null)
            {
                board.Cells[row, col] = new Cell(row, col, null, false);
            }

            var cell = board.Cells[row, col];
            cell.Row = row;
            cell.Column = col;
            cell.Box = (row / board.BoxHeight) * (board.Size / board.BoxWidth) + (col / board.BoxWidth);
        }

        /**
         * Verify all 81 cells are present, filled with values 1..9, and satisfy Sudoku constraints.
         *
         * @param board Board to validate.
         * @returns True when the board is fully solved and valid; otherwise false.
         */
        private static bool IsFullySolvedAndValid(Board board)
        {
            if (board == null)
            {
                return false;
            }

            if (board.Size != GridSize || board.BoxWidth != BoxSize || board.BoxHeight != BoxSize)
            {
                return false;
            }

            for (int r = 0; r < GridSize; r++)
            {
                for (int c = 0; c < GridSize; c++)
                {
                    var cell = board.Cells[r, c];
                    if (cell == null || !cell.Value.HasValue || cell.Value.Value < 1 || cell.Value.Value > GridSize)
                    {
                        return false;
                    }
                }
            }

            return board.IsValid();
        }
    }
}
