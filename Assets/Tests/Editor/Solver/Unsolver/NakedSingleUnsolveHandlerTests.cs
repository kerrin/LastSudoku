using System;
using NUnit.Framework;
using Sudoku.Models;
using Sudoku.Solver.Rules;
using Sudoku.Solver.Unsolver;
using Sudoku.Tests.Editor;

namespace Sudoku.Tests.Unsolver
{
    public class NakedSingleUnsolveHandlerTests
    {
        private static Board MakeFullyFilledBoard()
        {
            // Row-by-row valid 9x9 solution.
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
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    board.Cells[r, c] = new Cell(r, c, values[r, c], isGiven: false);
            return board;
        }

        [Test]
        public void TryUnsolve_OnFullySolvedBoard_ReturnsSuccess()
        {
            var board = MakeFullyFilledBoard();
            var handler = new NakedSingleUnsolveHandler();
            var result = handler.TryUnsolve(board, new Random(42));
            Assert.AreEqual(UnsolveResult.Success, result);
        }

        [Test]
        public void TryUnsolve_OnFullySolvedBoard_RemovesExactlyOneValue()
        {
            var board = MakeFullyFilledBoard();
            int filledBefore = CountFilledCells(board);
            new NakedSingleUnsolveHandler().TryUnsolve(board, new Random(42));
            int filledAfter = CountFilledCells(board);
            Assert.AreEqual(filledBefore - 1, filledAfter);
        }

        [Test]
        public void TryUnsolve_RemovedCell_HasNoValue()
        {
            var board = MakeFullyFilledBoard();
            var handler = new NakedSingleUnsolveHandler();
            handler.TryUnsolve(board, new Random(99));

            int nullCount = 0;
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    if (!board.Cells[r, c].Value.HasValue) nullCount++;

            Assert.AreEqual(1, nullCount);
        }

        [Test]
        public void TryUnsolve_RemovedCell_IsMarkedNotGiven()
        {
            var board = MakeFullyFilledBoard();
            new NakedSingleUnsolveHandler().TryUnsolve(board, new Random(7));
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    if (!board.Cells[r, c].Value.HasValue)
                        Assert.IsFalse(board.Cells[r, c].IsGiven, "Removed cell must not be IsGiven.");
        }

        [Test]
        public void TryUnsolve_GivenCell_IsNeverRemoved()
        {
            var board = MakeFullyFilledBoard();
            // Mark all cells as given; handler should find nothing.
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    board.Cells[r, c].IsGiven = true;

            var result = new NakedSingleUnsolveHandler().TryUnsolve(board, new Random(1));
            Assert.AreEqual(UnsolveResult.NoApplicableMove, result);
        }

        [Test]
        public void TryUnsolve_EmptyBoard_ReturnsNoApplicableMove()
        {
            var board = TestHelpers.CreateEmptyBoard();
            var result = new NakedSingleUnsolveHandler().TryUnsolve(board, new Random(1));
            Assert.AreEqual(UnsolveResult.NoApplicableMove, result);
        }

        [Test]
        public void BuildCandidateList_FullySolvedBoard_ReturnsAllNonGivenCells()
        {
            var board = MakeFullyFilledBoard();
            var handler = new NakedSingleUnsolveHandler();
            var candidates = handler.BuildCandidateList(board);
            // Every cell satisfies the Naked Single condition in a fully solved board.
            Assert.AreEqual(81, candidates.Count);
        }

        [Test]
        public void TryUnsolve_RemovedCellAllOtherPeerValuesStillPresent()
        {
            var board = MakeFullyFilledBoard();
            var handler = new NakedSingleUnsolveHandler();
            handler.TryUnsolve(board, new Random(55));

            // Find the emptied cell.
            Cell removed = null;
            for (int r = 0; r < 9 && removed == null; r++)
                for (int c = 0; c < 9 && removed == null; c++)
                    if (!board.Cells[r, c].Value.HasValue) removed = board.Cells[r, c];

            Assert.IsNotNull(removed);

            // Collect peer values.
            var peerValues = new System.Collections.Generic.HashSet<int>();
            foreach (var peer in board.GetPeers(removed))
                if (peer.Value.HasValue) peerValues.Add(peer.Value.Value);

            // All 8 remaining digits must be visible (NakedSingle can re-place the value).
            Assert.AreEqual(8, peerValues.Count,
                "After removal, 8 distinct values should still be visible in peers.");
        }

        [Test]
        public void TryUnsolve_RepeatedUntilExhausted_LeavesValidBoard()
        {
            var board = MakeFullyFilledBoard();
            var handler = new NakedSingleUnsolveHandler();
            var rng = new Random(123);

            while (handler.TryUnsolve(board, rng) == UnsolveResult.Success) { }

            Assert.IsTrue(board.IsValid(), "Board must remain valid after all Naked Single unsolves.");
        }

        private static int CountFilledCells(Board board)
        {
            int count = 0;
            for (int r = 0; r < board.Size; r++)
                for (int c = 0; c < board.Size; c++)
                    if (board.Cells[r, c].Value.HasValue) count++;
            return count;
        }
    }
}
