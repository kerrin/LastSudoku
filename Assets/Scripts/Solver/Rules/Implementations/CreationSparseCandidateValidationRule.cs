using System.Collections.Generic;
using Sudoku.Models;

namespace Sudoku.Solver.Rules
{
    /**
     * Special-case creation-mode rule for lightweight incremental candidate maintenance.
     *
     * IMPORTANT:
     * - This rule is not registered in RuleRegistry.
     * - It is not part of normal solver execution or UI rule toggles.
     */
    public sealed class CreationSparseCandidateValidationRule
    {
        /**
         * Apply incremental value-edit candidate updates while the board is sparse.
         *
         * @param board Active board.
         * @param editRuleResult RuleResult from the manual edit operation.
         * @param setDigitCount Current filled-digit count.
         * @param sparseCreationDigitThreshold Max digit count for sparse path.
         * @param sparseResyncEveryEdits Frequency for requesting full candidate resync.
         * @param sparseEditCounter Running sparse-edit counter (updated by this method).
         * @param requestFullCandidateResync Output flag requesting full candidate recompute.
         * @returns True when a sparse value-edit update was applied; otherwise false.
         */
        public bool TryApply(
            Board board,
            RuleResult editRuleResult,
            int setDigitCount,
            int sparseCreationDigitThreshold,
            int sparseResyncEveryEdits,
            ref int sparseEditCounter,
            out bool requestFullCandidateResync)
        {
            requestFullCandidateResync = false;

            if (board == null || board.Cells == null)
            {
                return false;
            }

            if (setDigitCount >= sparseCreationDigitThreshold)
            {
                return false;
            }

            var changes = editRuleResult?.Changes;
            if (changes == null || changes.Count == 0)
            {
                return false;
            }

            bool handledAnyValueChange = false;

            for (int i = 0; i < changes.Count; i++)
            {
                var change = changes[i];
                if (change == null)
                {
                    continue;
                }

                bool isValueSet = change.NewValue.HasValue;
                bool isValueClear = change.ClearValue;
                if (!isValueSet && !isValueClear)
                {
                    continue;
                }

                if (change.Row < 0 || change.Column < 0 || change.Row >= board.Size || change.Column >= board.Size)
                {
                    continue;
                }

                var editedCell = board.Cells[change.Row, change.Column];
                if (editedCell == null)
                {
                    continue;
                }

                handledAnyValueChange = true;

                if (isValueSet)
                {
                    int placed = change.NewValue.Value;
                    foreach (var peer in board.GetPeers(editedCell))
                    {
                        if (peer == null || peer.Value.HasValue)
                        {
                            continue;
                        }

                        if (peer.Candidates == null)
                        {
                            peer.Candidates = new HashSet<int>();
                        }

                        peer.Candidates.Remove(placed);
                    }
                }

                if (isValueClear)
                {
                    int? oldValue = change.OldValue;

                    if (editedCell.Candidates == null)
                    {
                        editedCell.Candidates = new HashSet<int>();
                    }

                    editedCell.Candidates.Clear();
                    for (int v = 1; v <= board.Size; v++)
                    {
                        if (IsCandidateLegalForCell(board, editedCell, v))
                        {
                            editedCell.Candidates.Add(v);
                        }
                    }

                    if (oldValue.HasValue)
                    {
                        int restored = oldValue.Value;
                        foreach (var peer in board.GetPeers(editedCell))
                        {
                            if (peer == null || peer.Value.HasValue)
                            {
                                continue;
                            }

                            if (!IsCandidateLegalForCell(board, peer, restored))
                            {
                                continue;
                            }

                            if (peer.Candidates == null)
                            {
                                peer.Candidates = new HashSet<int>();
                            }

                            peer.Candidates.Add(restored);
                        }
                    }
                }
            }

            if (handledAnyValueChange)
            {
                sparseEditCounter++;
                if (sparseResyncEveryEdits > 0 && sparseEditCounter % sparseResyncEveryEdits == 0)
                {
                    requestFullCandidateResync = true;
                }
            }

            return handledAnyValueChange;
        }

        /**
         * Check if placing a candidate at a cell is legal against current solved peers.
         *
         * @param board Active board.
         * @param cell Target unsolved cell.
         * @param candidate Candidate digit to test.
         * @returns True when no row/column/box peer already contains the candidate value.
         */
        private static bool IsCandidateLegalForCell(Board board, Cell cell, int candidate)
        {
            if (board == null || cell == null)
            {
                return false;
            }

            foreach (var peer in board.GetPeers(cell))
            {
                if (peer != null && peer.Value.HasValue && peer.Value.Value == candidate)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
