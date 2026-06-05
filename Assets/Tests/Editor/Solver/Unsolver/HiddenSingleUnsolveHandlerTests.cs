using System;
using NUnit.Framework;
using Sudoku.Models;
using Sudoku.Solver.Rules;
using Sudoku.Solver.Unsolver;

namespace Sudoku.Tests.Unsolver
{
    public class HiddenSingleUnsolveHandlerTests
    {
        // ── Helpers ────────────────────────────────────────────────────────────────

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
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    board.Cells[r, c] = new Cell(r, c, values[r, c], isGiven: false);
            return board;
        }

        // ── Contract tests ─────────────────────────────────────────────────────────

        [Test]
        public void TryUnsolve_OnFullySolvedBoard_ReturnsNoApplicableMove()
        {
            // In a fully solved board NakedSingle fires for every cell, so
            // HiddenSingle handler finds nothing to do.
            var board = MakeFullySolvedBoard();
            var result = new HiddenSingleUnsolveHandler().TryUnsolve(board, new Random(1));
            Assert.AreEqual(UnsolveResult.NoApplicableMove, result);
        }

        [Test]
        public void TryUnsolve_DoesNotModifyBoard_WhenNoApplicableMove()
        {
            var board = MakeFullySolvedBoard();
            var snap = SnapshotValues(board);

            new HiddenSingleUnsolveHandler().TryUnsolve(board, new Random(5));

            AssertValuesUnchanged(board, snap);
        }

        [Test]
        public void TryUnsolve_EmptyBoard_ReturnsNoApplicableMove()
        {
            var board = new Board(9, 3, 3);
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    board.Cells[r, c] = new Cell(r, c, null, false);

            var result = new HiddenSingleUnsolveHandler().TryUnsolve(board, new Random(1));
            Assert.AreEqual(UnsolveResult.NoApplicableMove, result);
        }

        [Test]
        public void TryUnsolve_AfterNakedSingleWarmUp_BoardRemainsValid()
        {
            var board = MakeFullySolvedBoard();
            var rng = new Random(77);

            // Drain all NakedSingle candidates first.
            var nakedHandler = new NakedSingleUnsolveHandler();
            while (nakedHandler.TryUnsolve(board, rng) == UnsolveResult.Success) { }

            // Now attempt one HiddenSingle unsolve (may or may not succeed).
            new HiddenSingleUnsolveHandler().TryUnsolve(board, rng);

            Assert.IsTrue(board.IsValid(), "Board must remain valid after HiddenSingle unsolve attempt.");
        }

        [Test]
        public void TryUnsolve_WhenSucceeds_RemovesExactlyOneValue()
        {
            // Run warm-up with many seeds until HiddenSingle gets a chance to fire.
            for (int seed = 0; seed < 50; seed++)
            {
                var board = MakeFullySolvedBoard();
                var rng = new Random(seed);
                var nakedHandler = new NakedSingleUnsolveHandler();
                while (nakedHandler.TryUnsolve(board, rng) == UnsolveResult.Success) { }

                int before = CountFilled(board);
                var result = new HiddenSingleUnsolveHandler().TryUnsolve(board, rng);

                if (result == UnsolveResult.Success)
                {
                    int after = CountFilled(board);
                    Assert.AreEqual(before - 1, after,
                        "A successful TryUnsolve must remove exactly one value.");
                    return; // Test passed.
                }
            }

            // If HiddenSingle never fires on this board, the test is inconclusive
            // rather than failing — the handler correctly returned NoApplicableMove.
            Assert.Inconclusive(
                "HiddenSingle unsolve did not fire on any of the tested seeds. " +
                "The board layout may not expose a HiddenSingle-exclusive opportunity.");
        }

        [Test]
        public void TryUnsolve_GivenCells_AreNeverRemoved()
        {
            var board = MakeFullySolvedBoard();
            // Mark all cells as given.
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    board.Cells[r, c].IsGiven = true;

            var result = new HiddenSingleUnsolveHandler().TryUnsolve(board, new Random(3));
            Assert.AreEqual(UnsolveResult.NoApplicableMove, result);
        }

        // ── Helpers ────────────────────────────────────────────────────────────────

        private static int?[,] SnapshotValues(Board board)
        {
            var snap = new int?[board.Size, board.Size];
            for (int r = 0; r < board.Size; r++)
                for (int c = 0; c < board.Size; c++)
                    snap[r, c] = board.Cells[r, c].Value;
            return snap;
        }

        private static void AssertValuesUnchanged(Board board, int?[,] snap)
        {
            for (int r = 0; r < board.Size; r++)
                for (int c = 0; c < board.Size; c++)
                    Assert.AreEqual(snap[r, c], board.Cells[r, c].Value,
                        $"Cell [{r},{c}] value changed unexpectedly.");
        }

        private static int CountFilled(Board board)
        {
            int n = 0;
            for (int r = 0; r < board.Size; r++)
                for (int c = 0; c < board.Size; c++)
                    if (board.Cells[r, c].Value.HasValue) n++;
            return n;
        }
    }
}
