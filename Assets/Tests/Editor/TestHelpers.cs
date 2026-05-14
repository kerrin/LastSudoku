using Sudoku.Models;

namespace Sudoku.Tests.Editor
{
    /// <summary>
    /// Shared test helpers used by multiple unit tests in this folder.
    /// Keeps board creation logic centralized so tests remain concise.
    /// </summary>
    internal static class TestHelpers
    {
        /// <summary>
        /// Create an empty 9x9 <see cref="Board"/> with fresh <see cref="Cell"/> instances.
        /// Tests manipulate the returned board's cells directly (candidates/values).
        /// </summary>
        public static Board CreateEmptyBoard()
        {
            var board = new Board(9, 3, 3);
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    board.Cells[r, c] = new Cell(r, c, null, false);
            return board;
        }
    }
}
