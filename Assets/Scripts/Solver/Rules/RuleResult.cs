using System.Collections.Generic;
using Sudoku.Models;

namespace Sudoku.Solver.Rules
{
    /// <summary>
    /// Describes a single change made to a cell by a rule application.
    /// </summary>
    public class CellChange
    {
        /// <summary>Zero-based row index.</summary>
        public int Row;

        /// <summary>Zero-based column index.</summary>
        public int Column;

        /// <summary>Previous value (null if empty).</summary>
        public int? OldValue;

        /// <summary>New value assigned by the rule (null if none).</summary>
        public int? NewValue;

        /// <summary>Candidate digits removed from the cell as part of the change.</summary>
        public List<int> RemovedCandidates = new List<int>();
    }

    /// <summary>
    /// Result returned by an <see cref="ISudokuRule"/> after attempting to apply it.
    /// Contains whether the rule was applied, a short description, and any cell changes.
    /// </summary>
    public class RuleResult
    {
        /// <summary>True when the rule made at least one change to the board.</summary>
        public bool Applied;

        /// <summary>Short human-readable description of the change.</summary>
        public string Description;

        /// <summary>List of changes performed by the rule.</summary>
        public List<CellChange> Changes = new List<CellChange>();
    }
}
