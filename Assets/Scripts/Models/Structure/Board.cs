using System;
using System.Collections.Generic;
using System.Linq;
using Sudoku.Solver.Rules;

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

        /** In-memory chronological log of all changes applied to this board. */
        public List<CellChange> ChangeLog = new List<CellChange>();

        /** Index into ChangeLog representing the next change to redo. Undo moves this index backwards. */
        public int ChangeLogIndex = 0;

        /** Next group id to assign when recording a new set of changes as an atomic group. */
        public int NextChangeGroupId = 1;

        /** User-authored directional links between candidate/value endpoints in solve mode. */
        public List<DirectionalCellLink> DirectionalLinks = new List<DirectionalCellLink>();

        /** A hash of the board state, used to detect changes. */
        public int StateHash = 0;

        public Board() { }

        /** Construct a board with dimensions and allocate the cell grid. */
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

        /** 
         * Check if the board state matches a given hash. 
         * This can be used to detect if the board has changed since a previous state.
         * @param stateHash The hash to compare against the current board state.
         * @returns True if the current board state matches the provided hash, false otherwise.
         */
        public bool IsSame(int stateHash)
        {
            return GetOrCalculateStateHash() == stateHash;
        }

        /**
         * Get the cached board state hash, calculating it if it is not set.
         *
         * @returns The current cached board state hash.
         */
        public int GetOrCalculateStateHash()
        {
            if (StateHash == 0)
            {
                StateHash = CalculateStateHash();
            }

            return StateHash;
        }

        /**
         * Recalculate and store the board state hash from the current board data.
         *
         * @returns The recalculated board state hash.
         */
        public int RecalculateStateHash()
        {
            StateHash = CalculateStateHash();
            return StateHash;
        }

        /**
         * Invalidate the cached state hash so it will be recalculated on next access.
         */
        public void InvalidateStateHash()
        {
            StateHash = 0;
        }

        /** 
         * Update the stored state hash to reflect the current board state.
         * This should be called after any changes to the board to keep the hash in sync.
         * @returns The new state hash after updating.
         */
        private int CalculateStateHash()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + Size;
                hash = (hash * 31) + BoxWidth;
                hash = (hash * 31) + BoxHeight;

                if (Cells == null)
                {
                    return hash == 0 ? 1 : hash;
                }

                for (int row = 0; row < Size; row++)
                {
                    for (int col = 0; col < Size; col++)
                    {
                        var cell = Cells[row, col];
                        if (cell == null)
                        {
                            hash = (hash * 31) + -1;
                            continue;
                        }

                        hash = (hash * 31) + (cell.Value ?? 0);
                        hash = (hash * 31) + (cell.IsGiven ? 1 : 0);
                        hash = (hash * 31) + (int)cell.Color;

                        int candidateCount = cell.Candidates?.Count ?? 0;
                        hash = (hash * 31) + candidateCount;

                        if (candidateCount > 0)
                        {
                            foreach (int candidate in cell.Candidates.OrderBy(v => v))
                            {
                                hash = (hash * 31) + candidate;
                            }
                        }
                    }
                }

                if (DirectionalLinks != null && DirectionalLinks.Count > 0)
                {
                    var orderedLinks = DirectionalLinks
                        .Where(link => link != null && link.Start != null && link.End != null)
                        .OrderBy(link => (int)link.Kind)
                        .ThenBy(link => link.Start.Row)
                        .ThenBy(link => link.Start.Column)
                        .ThenBy(link => link.Start.Digit)
                        .ThenBy(link => link.End.Row)
                        .ThenBy(link => link.End.Column)
                        .ThenBy(link => link.End.Digit);

                    foreach (var link in orderedLinks)
                    {
                        hash = (hash * 31) + (int)link.Kind;
                        hash = (hash * 31) + link.Start.Row;
                        hash = (hash * 31) + link.Start.Column;
                        hash = (hash * 31) + link.Start.Digit;
                        hash = (hash * 31) + link.End.Row;
                        hash = (hash * 31) + link.End.Column;
                        hash = (hash * 31) + link.End.Digit;
                    }
                }

                return hash == 0 ? 1 : hash;
            }
        }
    }
}
