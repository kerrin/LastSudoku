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
            // Basic sanitization to avoid zero/invalid box sizes which can
            // cause divide/modulo by zero in visualizers and algorithms.
            Size = size > 0 ? size : 9;
            BoxWidth = boxWidth;
            BoxHeight = boxHeight;

            // If provided box sizes are invalid or their product doesn't match Size,
            // pick sensible defaults: for 9 use 3x3, for perfect-square sizes use sqrt x sqrt,
            // otherwise fall back to 1 x Size.
            if (BoxWidth <= 0 || BoxHeight <= 0 || BoxWidth * BoxHeight != Size)
            {
                if (Size == 9)
                {
                    BoxWidth = 3;
                    BoxHeight = 3;
                }
                else
                {
                    int root = (int)Math.Sqrt(Size);
                    if (root * root == Size)
                    {
                        BoxWidth = root;
                        BoxHeight = root;
                    }
                    else
                    {
                        BoxWidth = 1;
                        BoxHeight = Size;
                    }
                }
            }

            Cells = new Cell[Size, Size];
        }
    }
}
