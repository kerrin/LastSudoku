using System;
using NUnit.Framework;
using Sudoku.Models;
using Sudoku.Solver.Rules;

namespace Sudoku.Tests.Models
{
    /**
     * Tests for random solved Sudoku board generation.
     */
    public class RandomSolvedBoardGeneratorTests
    {
        /**
         * Generated boards should always be fully populated and valid.
         *
         * @param None.
         * @returns Nothing.
         */
        [Test]
        public void GenerateRandomSolvedBoard_ReturnsValidSolvedBoard()
        {
            var random = new Random(12345);

            var board = RandomSolvedBoardGenerator.GenerateRandomSolvedBoard(random, out int shuffleCount);

            Assert.IsNotNull(board);
            Assert.That(shuffleCount, Is.GreaterThanOrEqualTo(RandomSolvedBoardGenerator.MinShuffleOperations));
            Assert.That(shuffleCount, Is.LessThanOrEqualTo(RandomSolvedBoardGenerator.MaxShuffleOperations));
            Assert.IsTrue(board.IsValid(), "Generated board should satisfy Sudoku row/column/box constraints.");

            for (int row = 0; row < 9; row++)
            {
                for (int col = 0; col < 9; col++)
                {
                    var value = board.Cells[row, col].Value;
                    Assert.IsTrue(value.HasValue, $"Cell [{row},{col}] should be filled.");
                    Assert.That(value.Value, Is.InRange(1, 9), $"Cell [{row},{col}] should be in range 1..9.");
                }
            }
        }

        /**
         * Multiple generations should continue to produce valid solved boards.
         *
         * @param None.
         * @returns Nothing.
         */
        [Test]
        public void GenerateRandomSolvedBoard_MultipleRuns_AllValid()
        {
            var random = new Random(777);

            for (int i = 0; i < 25; i++)
            {
                var board = RandomSolvedBoardGenerator.GenerateRandomSolvedBoard(random, out int shuffleCount);

                Assert.IsNotNull(board, $"Board at run {i} should not be null.");
                Assert.That(shuffleCount, Is.GreaterThanOrEqualTo(RandomSolvedBoardGenerator.MinShuffleOperations));
                Assert.That(shuffleCount, Is.LessThanOrEqualTo(RandomSolvedBoardGenerator.MaxShuffleOperations));
                Assert.IsTrue(board.IsValid(), $"Board at run {i} should be valid.");
            }
        }

        /**
         * Generation should be deterministic when using the same seed.
         *
         * @param None.
         * @returns Nothing.
         */
        [Test]
        public void GenerateRandomSolvedBoard_SameSeed_IsDeterministic()
        {
            var boardA = RandomSolvedBoardGenerator.GenerateRandomSolvedBoard(new Random(9001));
            var boardB = RandomSolvedBoardGenerator.GenerateRandomSolvedBoard(new Random(9001));

            for (int row = 0; row < 9; row++)
            {
                for (int col = 0; col < 9; col++)
                {
                    Assert.AreEqual(boardA.Cells[row, col].Value, boardB.Cells[row, col].Value,
                        $"Boards must match at [{row},{col}] when seed is identical.");
                }
            }
        }

        /**
         * Different seeds should usually produce different solved boards.
         *
         * @param None.
         * @returns Nothing.
         */
        [Test]
        public void GenerateRandomSolvedBoard_DifferentSeeds_UsuallyDifferentBoards()
        {
            var boardA = RandomSolvedBoardGenerator.GenerateRandomSolvedBoard(new Random(1));
            var boardB = RandomSolvedBoardGenerator.GenerateRandomSolvedBoard(new Random(2));

            bool anyDifference = false;
            for (int row = 0; row < 9 && !anyDifference; row++)
            {
                for (int col = 0; col < 9; col++)
                {
                    if (boardA.Cells[row, col].Value != boardB.Cells[row, col].Value)
                    {
                        anyDifference = true;
                        break;
                    }
                }
            }

            Assert.IsTrue(anyDifference, "Different seeds should not produce identical solved boards.");
        }
    }
}
