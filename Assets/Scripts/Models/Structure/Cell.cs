using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sudoku.Models
{
    // Pastel highlight colours that can be applied to digits by the user.
    public enum HighlightColor
    {
        None = 0,
        Green = 1,
        Amber = 2,
        Red = 3,
        Blue = 4,
    }

    // Simple enum to allow marking or highlighting a cell in the UI or solver.
    public enum CellColor
    {
        None,
        Highlight,
        UsedInDeduction,
        Given,
        Solved,
    }

    [Serializable]
    public class Cell
    {
        // The solved value for the cell (1..9). Null when cell is empty.
        public int? Value;

        // True if this cell was part of the original puzzle (cannot be changed by the solver).
        public bool IsGiven;

        // Remaining candidate values for this cell (used by pencilmarks / solver techniques).
        public HashSet<int> Candidates = new HashSet<int>();

        // Optional color/state marker used by UI or algorithms.
        public CellColor Color = CellColor.None;

        // Per-digit colour annotations set by the user.
        // Key = digit (1..9); Value = set of active highlight colours for that digit.
        // For value cells, only the key matching Value is relevant.
        // For candidate cells, any candidate digit can have colours assigned.
        public Dictionary<int, HashSet<HighlightColor>> DigitColors = new Dictionary<int, HashSet<HighlightColor>>();

        // Zero-based row index of the cell on the board.
        public int Row;

        // Zero-based column index of the cell on the board.
        public int Column;

        // Box index (0-based) the cell belongs to. Computed as (row/BoxHeight)*BoxWidth + (col/BoxWidth).
        public int Box;

        public Cell() { }

        /** Convenience constructor to initialize position and optional value. */
        public Cell(int row, int column, int? value = null, bool isGiven = false)
        {
            Row = row;
            Column = column;
            Box = ComputeBox(row, column);
            Value = value;
            IsGiven = isGiven;
            if (!value.HasValue)
            {
                for (int v = 1; v <= 9; v++) Candidates.Add(v);
            }
        }

        /** Compute the 0-based box index for a standard 3x3 box layout. */
        public static int ComputeBox(int row, int column)
        {
            return (row / 3) * 3 + (column / 3);
        }

        /** Produce a shallow clone of the cell (candidates are copied). */
        public Cell Clone()
        {
            var c = new Cell
            {
                Value = Value,
                IsGiven = IsGiven,
                Candidates = new HashSet<int>(Candidates),
                Color = Color,
                Row = Row,
                Column = Column,
                Box = Box
            };

            // Deep-copy per-digit colour annotations.
            foreach (var kvp in DigitColors)
            {
                c.DigitColors[kvp.Key] = new HashSet<HighlightColor>(kvp.Value);
            }

            return c;
        }

        public override string ToString()
        {
            return $"Cell({Row},{Column}): Value={Value}, Given={IsGiven}, Candidates=[{string.Join(",", Candidates)}], Color={Color}";
        }
    }
}
