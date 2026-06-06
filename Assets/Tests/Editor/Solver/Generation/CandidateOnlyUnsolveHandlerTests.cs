using System;
using NUnit.Framework;
using Sudoku.Models;
using Sudoku.Solver.Unsolver;

namespace Sudoku.Tests.Unsolver
{
    public class CandidateOnlyUnsolveHandlerTests
    {
        [Test]
        public void TryUnsolve_AlwaysReturnsNotSupported()
        {
            var board = new Board(9, 3, 3);
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    board.Cells[r, c] = new Cell(r, c, (r * 3 + c % 3 + 1) % 9 + 1, false);

            var handler = new CandidateOnlyUnsolveHandler("Box Line");
            var result = handler.TryUnsolve(board, new Random(1));
            Assert.AreEqual(UnsolveResult.NotSupported, result);
        }

        [Test]
        public void TryUnsolve_DoesNotMutateBoard()
        {
            var board = new Board(9, 3, 3);
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    board.Cells[r, c] = new Cell(r, c, 1, false);

            // Snapshot values before.
            var before = new int?[9, 9];
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    before[r, c] = board.Cells[r, c].Value;

            new CandidateOnlyUnsolveHandler("Skyscraper").TryUnsolve(board, new Random(42));

            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    Assert.AreEqual(before[r, c], board.Cells[r, c].Value,
                        $"Cell [{r},{c}] must not be modified by CandidateOnlyUnsolveHandler.");
        }

        [Test]
        public void RuleName_ReturnsConstructorArgument()
        {
            var handler = new CandidateOnlyUnsolveHandler("X-Wing");
            Assert.AreEqual("X-Wing", handler.RuleName);
        }

        [Test]
        public void TryUnsolve_MultipleCalls_AlwaysNotSupported()
        {
            var board = new Board(9, 3, 3);
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    board.Cells[r, c] = new Cell(r, c, null, false);

            var handler = new CandidateOnlyUnsolveHandler("YWingRule");
            for (int i = 0; i < 5; i++)
                Assert.AreEqual(UnsolveResult.NotSupported, handler.TryUnsolve(board, new Random(i)));
        }
    }
}
