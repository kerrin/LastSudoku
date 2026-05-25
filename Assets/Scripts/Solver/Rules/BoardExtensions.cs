using System.Collections.Generic;
using System.Linq;
using Sudoku.Models;
using Cell = Sudoku.Models.Cell;
using Board = Sudoku.Models.Board;

namespace Sudoku.Solver.Rules
{
    /**
     * Convenience extension methods for reading and mutating a <see cref="Board"/>.
     * These helpers simplify common unit (row/column/box) iteration and peer lookup.
     */
    public static class BoardExtensions
    {
        /**
         * Enumerate cells in the specified zero-based row.
         */
        public static IEnumerable<Cell> GetRow(this Board board, int row)
        {
            for (int c = 0; c < board.Size; c++) yield return board.Cells[row, c];
        }

        /**
         * Enumerate cells in the specified zero-based column.
         */
        public static IEnumerable<Cell> GetColumn(this Board board, int col)
        {
            for (int r = 0; r < board.Size; r++) yield return board.Cells[r, col];
        }

        /**
         * Enumerate cells in the specified box index (0-based). Box indexing is row-major.
         */
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

        /**
         * Return the set of peer cells that share a row, column, or box with the given cell.
         * The returned sequence is de-duplicated.
         */
        public static IEnumerable<Cell> GetPeers(this Board board, Cell cell)
        {
            var seen = new HashSet<Cell>();
            foreach (Cell c in board.GetRow(cell.Row)) if (!ReferenceEquals(c, cell)) seen.Add(c);
            foreach (Cell c in board.GetColumn(cell.Column)) if (!ReferenceEquals(c, cell)) seen.Add(c);
            foreach (Cell c in board.GetBox(cell.Box)) if (!ReferenceEquals(c, cell)) seen.Add(c);
            return seen;
        }

        /**
         * Set the specified cell value and clear candidate sets accordingly.
         */
        public static void SetValue(this Board board, Cell cell, int value)
        {
            cell.Value = value;
            // Clear this cell's candidates and remove the placed value from all peers
            cell.Candidates.Clear();
            foreach (var peer in board.GetPeers(cell))
            {
                if (peer.Candidates != null) peer.Candidates.Remove(value);
            }
        }

        /**
         * Validate the current board state.
         * Each unit (row, column, box) must not contain the same solved digit more than once.
         */
        public static bool IsValid(this Board board)
        {
            int size = board.Size;

            // Check rows
            for (int r = 0; r < size; r++)
            {
                var seen = new bool[size + 1];
                foreach (Cell cell in board.GetRow(r))
                {
                    if (cell.Value.HasValue)
                    {
                        int v = cell.Value.Value;
                        if (v < 1 || v > size) return false;
                        if (seen[v]) return false;
                        seen[v] = true;
                    }
                }
            }

            // Check columns
            for (int c = 0; c < size; c++)
            {
                var seen = new bool[size + 1];
                foreach (Cell cell in board.GetColumn(c))
                {
                    if (cell.Value.HasValue)
                    {
                        int v = cell.Value.Value;
                        if (v < 1 || v > size) return false;
                        if (seen[v]) return false;
                        seen[v] = true;
                    }
                }
            }

            // Check boxes
            for (int b = 0; b < size; b++)
            {
                var seen = new bool[size + 1];
                foreach (Cell cell in board.GetBox(b))
                {
                    if (cell.Value.HasValue)
                    {
                        int v = cell.Value.Value;
                        if (v < 1 || v > size) return false;
                        if (seen[v]) return false;
                        seen[v] = true;
                    }
                }
            }

            return true;
        }
    }
}
