using System.Collections.Generic;
using Sudoku.Models;
using UnityEngine;
using Cell = Sudoku.Models.Cell;
using Board = Sudoku.Models.Board;

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

        /** Group identifier used to associate multiple CellChange entries produced
         *  by a single rule application so undo/redo can operate atomically. */
        public int GroupId;
        /** Name of the rule that produced this change (set when copied into the board ChangeLog). */
        public string SourceRuleName;
        /** Description text provided by the rule result (set when copied into the board ChangeLog). */
        public string SourceRuleDescription;
    }

    /**
     * Result returned by an <see cref="ISudokuRule"/> after attempting to apply it.
     * Contains whether the rule was applied, a short description, and any cell changes.
     */
    public class RuleResult
    {
        /** True when the rule wants to make at least one change to the board. */
        public bool Apply;

        /** Short human-readable description of the change. */
        public string Description;

        /** List of changes performed by the rule. */
        public List<CellChange> Changes = new List<CellChange>();

        /**
         * Cells that were consulted/used when deducing the rule result (but may not
         * themselves have changed). This is useful for UI highlighting of contributing
         * cells.
         * If the candidate of the cell is set, that will also be highlighted as the specific candidate that contributed to the deduction
         */
        public List<UsedCell> UsedCells = new List<UsedCell>();

        /**
         * Enact only candidate removals recorded in `Changes` on the provided board.
         * Values recorded in `NewValue` are ignored.
         */
        public void EnactCandidates(Board board)
        {
            foreach (var change in Changes)
            {
                if (change.RemovedCandidates == null || change.RemovedCandidates.Count == 0) continue;
                var cell = board.Cells[change.Row, change.Column];
                foreach (int v in change.RemovedCandidates) cell.Candidates.Remove(v);
            }
        }

        /**
         * Enact both candidate removals and value assignments recorded in `Changes`.
         */
        public void EnactAll(Board board)
        {
            foreach (var change in Changes)
            {
                var cell = board.Cells[change.Row, change.Column];
                if (change.NewValue.HasValue)
                {
                    // Safety check: avoid applying a new value that would immediately
                    // create a duplicate in the cell's peers. If such a conflict is
                    // detected, skip applying the value and log a warning so the
                    // rule engine can continue without corrupting the board.
                    bool conflict = false;
                    foreach (var peer in board.GetPeers(cell))
                    {
                        if (peer.Value.HasValue && peer.Value.Value == change.NewValue.Value)
                        {
                            conflict = true;
                            break;
                        }
                    }
                    if (!conflict)
                    {
                        // set the value and clear the cell's own candidates
                        board.SetValue(cell, change.NewValue.Value);
                    }
                }
                if (change.RemovedCandidates != null && change.RemovedCandidates.Count > 0)
                {
                    foreach (int v in change.RemovedCandidates)
                    {
                        if (cell.Candidates.Contains(v))
                        {
                            cell.Candidates.Remove(v);
                        }
                    }
                }
            }

            // Diagnostic dump: log current candidates/value for every changed cell
            foreach (var changePost in Changes)
            {
                var c = board.Cells[changePost.Row, changePost.Column];
                var candList = c.Candidates != null ? string.Join(",", c.Candidates) : "(null)";

                int peerCount = 0;
                foreach (var peer in board.GetPeers(c))
                {
                    peerCount++;
                }
            }
        }
    }

    /** Minimal coordinate describing a cell that was used during deduction. */
    public class UsedCell
    {
        public int Row;
        public int Column;
        // Optional specific candidate digit relevant to this used cell (null if not applicable)
        public int? Candidate;
    }
}
