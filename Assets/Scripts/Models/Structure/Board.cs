using System;

namespace Sudoku.Models
{
    [Serializable]
    public class Board
    {
        // Number of cells per side (e.g. 9 for a standard Sudoku).
        public int Size;

        // Width of a box (e.g. 3 for a 9x9 Sudoku).
        public int BoxWidth;

        // Height of a box (e.g. 3 for a 9x9 Sudoku).
        public int BoxHeight;

        // 2D array of cells, indexed as [row, column].
        public Cell[,] Cells;

        public Board() { }

        // Construct a board with dimensions and allocate the cell grid.
        public Board(int size, int boxWidth, int boxHeight)
        {
            Size = size;
            BoxWidth = boxWidth;
            BoxHeight = boxHeight;
            Cells = new Cell[Size, Size];
        }
    }
}
