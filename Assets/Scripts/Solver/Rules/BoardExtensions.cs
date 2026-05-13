using System.Collections.Generic;
using System.Linq;
using Sudoku.Models;

namespace Sudoku.Solver.Rules
{
    /// <summary>
    /// Convenience extension methods for reading and mutating a <see cref="Board"/>.
    /// These helpers simplify common unit (row/column/box) iteration and peer lookup.
    /// </summary>
    public static class BoardExtensions
    {
        /// <summary>
        /// Enumerate cells in the specified zero-based row.
        /// </summary>
        public static IEnumerable<Cell> GetRow(this Board board, int row)
        {
            for (int c = 0; c < board.Size; c++) yield return board.Cells[row, c];
        }

        /// <summary>
        /// Enumerate cells in the specified zero-based column.
        /// </summary>
        public static IEnumerable<Cell> GetColumn(this Board board, int col)
        {
            for (int r = 0; r < board.Size; r++) yield return board.Cells[r, col];
        }

        /// <summary>
        /// Enumerate cells in the specified box index (0-based). Box indexing is row-major.
        /// </summary>
        public static IEnumerable<Cell> GetBox(this Board board, int boxIndex)
        {
            int boxW = board.BoxWidth;
            int boxH = board.BoxHeight;
            int boxesPerRow = board.Size / boxW;
            int startBoxRow = (boxIndex / boxesPerRow) * boxH;
            int startBoxCol = (boxIndex % boxesPerRow) * boxW;
            for (int r = 0; r < boxH; r++)
                for (int c = 0; c < boxW; c++)
                    yield return board.Cells[startBoxRow + r, startBoxCol + c];
        }

        /// <summary>
        /// Return the set of peer cells that share a row, column, or box with the given <paramref name="cell"/>.
        /// The returned sequence is de-duplicated.
        /// </summary>
        public static IEnumerable<Cell> GetPeers(this Board board, Cell cell)
        {
            var seen = new HashSet<Cell>();
            foreach (var c in board.GetRow(cell.Row)) if (!ReferenceEquals(c, cell)) seen.Add(c);
            foreach (var c in board.GetColumn(cell.Column)) if (!ReferenceEquals(c, cell)) seen.Add(c);
            foreach (var c in board.GetBox(cell.Box)) if (!ReferenceEquals(c, cell)) seen.Add(c);
            return seen;
        }

        /// <summary>
        /// Assign a solved <paramref name="value"/> to <paramref name="cell"/> and clear its candidates.
        /// This mutates the board in place.
        /// </summary>
        public static void SetValue(this Board board, Cell cell, int value)
        {
            cell.Value = value;
            cell.Candidates.Clear();
        }
    }
}
