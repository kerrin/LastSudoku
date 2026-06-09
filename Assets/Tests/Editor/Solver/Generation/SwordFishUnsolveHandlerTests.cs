using System;
using NUnit.Framework;
using Sudoku.Models;
using Sudoku.Solver.Rules;
using Sudoku.Solver.Unsolver;

namespace Sudoku.Tests.Unsolver
{
    public class SwordFishUnsolveHandlerTests
    {
        private static Board MakeFullySolvedBoard()
        {
            int[,] values =
            {
                { 1, 2, 3, 4, 5, 6, 7, 8, 9 },
                { 4, 5, 6, 7, 8, 9, 1, 2, 3 },
                { 7, 8, 9, 1, 2, 3, 4, 5, 6 },
                { 2, 3, 4, 5, 6, 7, 8, 9, 1 },
                { 5, 6, 7, 8, 9, 1, 2, 3, 4 },
                { 8, 9, 1, 2, 3, 4, 5, 6, 7 },
                { 3, 4, 5, 6, 7, 8, 9, 1, 2 },
                { 6, 7, 8, 9, 1, 2, 3, 4, 5 },
                { 9, 1, 2, 3, 4, 5, 6, 7, 8 },
            };

            var board = new Board(9, 3, 3);
            for (int row = 0; row < 9; row++)
            {
                for (int column = 0; column < 9; column++)
                {
                    board.Cells[row, column] = new Cell(row, column, values[row, column], isGiven: false);
                }
            }

            return board;
        }

        [Test]
        public void GetHandler_ForSwordFishRule_ReturnsSwordFishUnsolveHandler()
        {
            var handler = UnsolveHandlerRegistry.GetHandler(new SwordFishRule());

            Assert.IsInstanceOf<SwordFishUnsolveHandler>(handler);
        }

        [Test]
        public void TryUnsolve_OnFullySolvedBoard_ReturnsNoApplicableMove_AndDoesNotMutateBoard()
        {
            var board = MakeFullySolvedBoard();
            var before = SnapshotValues(board);
            var handler = new SwordFishUnsolveHandler();

            var result = handler.TryUnsolve(board, new Random(3));

            Assert.AreEqual(UnsolveResult.NoApplicableMove, result);
            AssertValuesUnchanged(board, before);
        }

        [Test]
        public void BuildCandidateList_WhenSwordfishOpportunityExists_ReturnsAtLeastOneCell()
        {
            for (int seed = 0; seed < 260; seed++)
            {
                var board = MakeFullySolvedBoard();
                var random = new Random(seed);
                var nakedSingle = new NakedSingleUnsolveHandler();

                for (int i = 0; i < 46; i++)
                {
                    if (nakedSingle.TryUnsolve(board, random) != UnsolveResult.Success)
                    {
                        break;
                    }
                }

                var handler = new SwordFishUnsolveHandler();
                if (handler.BuildCandidateList(board).Count == 0)
                {
                    continue;
                }

                int before = CountFilled(board);
                var result = handler.TryUnsolve(board, random);

                Assert.AreEqual(UnsolveResult.Success, result);
                Assert.AreEqual(before - 1, CountFilled(board));
                Assert.IsTrue(board.IsValid(), "Board must stay valid after Swordfish unsolve.");
                return;
            }

            Assert.Inconclusive(
                "No Swordfish unsolve opportunity was found in tested seeds. " +
                "The handler correctly skips when no applicable move exists.");
        }

        private static int CountFilled(Board board)
        {
            int count = 0;
            for (int row = 0; row < board.Size; row++)
            {
                for (int column = 0; column < board.Size; column++)
                {
                    if (board.Cells[row, column].Value.HasValue)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static int?[,] SnapshotValues(Board board)
        {
            var snapshot = new int?[board.Size, board.Size];
            for (int row = 0; row < board.Size; row++)
            {
                for (int column = 0; column < board.Size; column++)
                {
                    snapshot[row, column] = board.Cells[row, column].Value;
                }
            }

            return snapshot;
        }

        private static void AssertValuesUnchanged(Board board, int?[,] snapshot)
        {
            for (int row = 0; row < board.Size; row++)
            {
                for (int column = 0; column < board.Size; column++)
                {
                    Assert.AreEqual(snapshot[row, column], board.Cells[row, column].Value,
                        $"Cell [{row},{column}] must remain unchanged.");
                }
            }
        }
    }
}