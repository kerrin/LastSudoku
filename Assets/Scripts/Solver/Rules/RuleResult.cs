using System.Collections.Generic;
using Sudoku.Models;

namespace Sudoku.Solver.Rules
{
    /**
     * Describes a single change made to a cell by a rule application.
     */
    public class CellChange
    {
        /** Zero-based row index. */
        public int Row;

        /** Zero-based column index. */
        public int Column;

        /** Previous value (null if empty). */
        public int? OldValue;

        /** New value assigned by the rule (null if none). */
        public int? NewValue;

        /** Candidate digits removed from the cell as part of the change. */
        public List<int> RemovedCandidates = new List<int>();
    }

    /**
     * Result returned by an <see cref="ISudokuRule"/> after attempting to apply it.
     * Contains whether the rule was applied, a short description, and any cell changes.
     */
    public class RuleResult
    {
        /** True when the rule made at least one change to the board. */
        public bool Applied;

        /** Short human-readable description of the change. */
        public string Description;

        /** List of changes performed by the rule. */
        public List<CellChange> Changes = new List<CellChange>();

        /**
         * Cells that were consulted/used when deducing the rule result (but may not
         * themselves have changed). This is useful for UI highlighting of contributing
         * cells.
         */
        public List<UsedCell> UsedCells = new List<UsedCell>();
    }

    /** Minimal coordinate describing a cell that was used during deduction. */
    public class UsedCell
    {
        public int Row;
        public int Column;
    }
}
