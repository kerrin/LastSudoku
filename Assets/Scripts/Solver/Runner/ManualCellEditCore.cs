using System.Collections.Generic;
using Sudoku.Models;
using Sudoku.Solver.Rules;

namespace Sudoku.Solver
{
    public enum ManualCellEditOperation
    {
        None = 0,
        SetValue = 1,
        AddCandidate = 2,
        RemoveCandidate = 3,
        ClearValue = 4,
        UnitCandidateAction = 5
    }

    public class SmartActionResolution
    {
        public bool HasAction;
        public ManualCellEditOperation Operation = ManualCellEditOperation.None;
        public int? Digit;
        public string Label = "Smart";
        public string Description = "No smart action available.";
    }

    public class ManualEditExecutionResult
    {
        public bool Applied;
        public string Description;
        public RuleResult RuleResult;
    }

    /**
     * Domain-level manual cell edit actions used by interaction/UI layers.
     */
    public static class ManualCellEditCore
    {
        /**
         * Resolve a smart-action placeholder for a target cell.
         *
         * @param board Source board.
         * @param row Zero-based row index.
         * @param column Zero-based column index.
         * @returns A deterministic no-op scaffold until Smart behavior is implemented.
         */
        public static SmartActionResolution ResolveSmartAction(Board board, int row, int column)
        {
            if (!TryGetEditableCell(board, row, column, out var cell, out var reason))
            {
                return new SmartActionResolution
                {
                    HasAction = false,
                    Label = "Smart",
                    Description = reason
                };
            }

            return new SmartActionResolution
            {
                HasAction = false,
                Label = "Smart",
                Description = "Smart action is not implemented yet."
            };
        }

        /**
         * Apply manual set-value edit and record it as one changelog group.
         *
         * @param board Source board.
         * @param row Zero-based row index.
         * @param column Zero-based column index.
         * @param value Digit to place.
         * @returns Operation outcome and optional applied RuleResult.
         */
        public static ManualEditExecutionResult ApplySetValue(Board board, int row, int column, int value)
        {
            if (!TryGetEditableCell(board, row, column, out var cell, out var reason))
            {
                return CreateNoOp(reason);
            }

            if (value < 1 || value > board.Size)
            {
                return CreateNoOp($"Value {value} is outside board range 1..{board.Size}.");
            }

            int? previousValue = cell.Value;
            if (previousValue.HasValue && previousValue.Value == value)
            {
                return CreateNoOp("Cell already contains that value.");
            }

            var result = new RuleResult
            {
                Apply = true,
                Description = previousValue.HasValue
                    ? $"Manual replace value at r{row + 1}c{column + 1}: {previousValue.Value}->{value}"
                    : $"Manual set value r{row + 1}c{column + 1}={value}"
            };

            var placed = new CellChange
            {
                Row = row,
                Column = column,
                NewValue = value,
                ForceSetValue = true,
                RemovedCandidates = new List<int>(cell.Candidates)
            };
            result.Changes.Add(placed);

            if (previousValue.HasValue)
            {
                int oldValue = previousValue.Value;
                foreach (var peer in board.GetPeers(cell))
                {
                    if (peer == null || peer.IsGiven || peer.Value.HasValue)
                    {
                        continue;
                    }

                    if (peer.Candidates == null)
                    {
                        peer.Candidates = new HashSet<int>();
                    }

                    if (peer.Candidates.Contains(oldValue))
                    {
                        continue;
                    }

                    result.Changes.Add(new CellChange
                    {
                        Row = peer.Row,
                        Column = peer.Column,
                        AddedCandidates = new List<int> { oldValue }
                    });
                }
            }

            foreach (var peer in board.GetPeers(cell))
            {
                if (peer == null || peer.Candidates == null || !peer.Candidates.Contains(value))
                {
                    continue;
                }

                result.Changes.Add(new CellChange
                {
                    Row = peer.Row,
                    Column = peer.Column,
                    RemovedCandidates = new List<int> { value }
                });
            }

            return ApplyAndRecord(board, result, "ManualSetValue");
        }

        /**
         * Apply manual candidate-add edit and record it as one changelog group.
         *
         * @param board Source board.
         * @param row Zero-based row index.
         * @param column Zero-based column index.
         * @param candidate Candidate digit to add.
         * @returns Operation outcome and optional applied RuleResult.
         */
        public static ManualEditExecutionResult ApplyAddCandidate(Board board, int row, int column, int candidate)
        {
            if (!TryGetEditableCell(board, row, column, out var cell, out var reason))
            {
                return CreateNoOp(reason);
            }

            if (candidate < 1 || candidate > board.Size)
            {
                return CreateNoOp($"Candidate {candidate} is outside board range 1..{board.Size}.");
            }

            bool clearsValue = cell.Value.HasValue;
            if (!clearsValue && cell.Candidates.Contains(candidate))
            {
                return CreateNoOp($"Candidate {candidate} already exists in r{row + 1}c{column + 1}.");
            }

            var result = new RuleResult
            {
                Apply = true,
                Description = clearsValue
                    ? $"Manual add candidate {candidate} at r{row + 1}c{column + 1} (cleared existing value)"
                    : $"Manual add candidate {candidate} at r{row + 1}c{column + 1}"
            };

            var change = new CellChange
            {
                Row = row,
                Column = column,
                ClearValue = clearsValue
            };

            if (!cell.Candidates.Contains(candidate))
            {
                change.AddedCandidates = new List<int> { candidate };
            }

            result.Changes.Add(change);

            return ApplyAndRecord(board, result, "ManualAddCandidate");
        }

        /**
         * Clear a solved cell and restore the full candidate set to the cell and the removed peer digit.
         *
         * @param board Source board.
         * @param row Zero-based row index.
         * @param column Zero-based column index.
         * @returns Operation outcome and optional applied RuleResult.
         */
        public static ManualEditExecutionResult ApplyClearValue(Board board, int row, int column)
        {
            if (!TryGetEditableCell(board, row, column, out var cell, out var reason))
            {
                return CreateNoOp(reason);
            }

            if (!cell.Value.HasValue)
            {
                return CreateNoOp("Cell is already empty.");
            }

            int clearedValue = cell.Value.Value;
            var result = new RuleResult
            {
                Apply = true,
                Description = $"Manual clear value at r{row + 1}c{column + 1} and restore candidates"
            };

            var clearedCell = new CellChange
            {
                Row = row,
                Column = column,
                ClearValue = true,
                AddedCandidates = new List<int>()
            };
            for (int v = 1; v <= board.Size; v++)
            {
                clearedCell.AddedCandidates.Add(v);
            }
            result.Changes.Add(clearedCell);

            foreach (var peer in board.GetPeers(cell))
            {
                if (peer == null || peer.Value.HasValue || peer.IsGiven)
                {
                    continue;
                }

                if (peer.Candidates == null)
                {
                    peer.Candidates = new HashSet<int>();
                }

                if (peer.Candidates.Contains(clearedValue))
                {
                    continue;
                }

                result.Changes.Add(new CellChange
                {
                    Row = peer.Row,
                    Column = peer.Column,
                    AddedCandidates = new List<int> { clearedValue }
                });
            }

            return ApplyAndRecord(board, result, "ManualClearValue");
        }

        /**
         * Apply candidate removal/addition to the distinct row/column/box units anchored
         * at a selected cell, and record the full mutation as one changelog group.
         *
         * @param board Source board.
         * @param row Zero-based anchor row index.
         * @param column Zero-based anchor column index.
         * @param candidate Candidate digit to remove or add.
         * @param addToUnsolvedCells When true, add candidate to unsolved editable cells; otherwise remove candidate where present.
         * @returns Operation outcome and optional applied RuleResult.
         */
        public static ManualEditExecutionResult ApplyUnitCandidateAction(Board board, int row, int column, int candidate, bool addToUnsolvedCells)
        {
            if (!TryGetAnyCell(board, row, column, out var anchorCell, out var reason))
            {
                return CreateNoOp(reason);
            }

            if (candidate < 1 || candidate > board.Size)
            {
                return CreateNoOp($"Candidate {candidate} is outside board range 1..{board.Size}.");
            }

            var unitCells = new HashSet<Cell>();
            foreach (var unitCell in board.GetRow(anchorCell.Row)) unitCells.Add(unitCell);
            foreach (var unitCell in board.GetColumn(anchorCell.Column)) unitCells.Add(unitCell);
            foreach (var unitCell in board.GetBox(anchorCell.Box)) unitCells.Add(unitCell);

            var result = new RuleResult
            {
                Apply = true,
                Description = addToUnsolvedCells
                    ? $"Manual add candidate {candidate} to row/column/box at r{row + 1}c{column + 1}"
                    : $"Manual remove candidate {candidate} from row/column/box at r{row + 1}c{column + 1}"
            };

            foreach (var unitCell in unitCells)
            {
                if (unitCell == null || unitCell.IsGiven)
                {
                    continue;
                }

                if (addToUnsolvedCells)
                {
                    if (unitCell.Value.HasValue)
                    {
                        continue;
                    }

                    if (unitCell.Candidates.Contains(candidate))
                    {
                        continue;
                    }

                    result.Changes.Add(new CellChange
                    {
                        Row = unitCell.Row,
                        Column = unitCell.Column,
                        AddedCandidates = new List<int> { candidate }
                    });
                    continue;
                }

                if (!unitCell.Candidates.Contains(candidate))
                {
                    continue;
                }

                result.Changes.Add(new CellChange
                {
                    Row = unitCell.Row,
                    Column = unitCell.Column,
                    RemovedCandidates = new List<int> { candidate }
                });
            }

            if (result.Changes.Count == 0)
            {
                return CreateNoOp(addToUnsolvedCells
                    ? $"Candidate {candidate} is already present in all editable unsolved row/column/box cells."
                    : $"Candidate {candidate} is not present in editable row/column/box cells.");
            }

            return ApplyAndRecord(board, result, addToUnsolvedCells ? "ManualUnitAddCandidate" : "ManualUnitRemoveCandidate");
        }

        /**
         * Apply manual candidate-remove edit and record it as one changelog group.
         *
         * @param board Source board.
         * @param row Zero-based row index.
         * @param column Zero-based column index.
         * @param candidate Candidate digit to remove.
         * @returns Operation outcome and optional applied RuleResult.
         */
        public static ManualEditExecutionResult ApplyRemoveCandidate(Board board, int row, int column, int candidate)
        {
            if (!TryGetEditableCell(board, row, column, out var cell, out var reason))
            {
                return CreateNoOp(reason);
            }

            if (candidate < 1 || candidate > board.Size)
            {
                return CreateNoOp($"Candidate {candidate} is outside board range 1..{board.Size}.");
            }

            if (!cell.Candidates.Contains(candidate))
            {
                return CreateNoOp($"Candidate {candidate} is not present in r{row + 1}c{column + 1}.");
            }

            var result = new RuleResult
            {
                Apply = true,
                Description = $"Manual remove candidate {candidate} at r{row + 1}c{column + 1}"
            };
            result.Changes.Add(new CellChange
            {
                Row = row,
                Column = column,
                RemovedCandidates = new List<int> { candidate }
            });

            return ApplyAndRecord(board, result, "ManualRemoveCandidate");
        }

        private static ManualEditExecutionResult ApplyAndRecord(Board board, RuleResult result, string sourceRuleName)
        {
            if (board == null)
            {
                return CreateNoOp("Board is null.");
            }

            if (result == null || !result.Apply || result.Changes == null || result.Changes.Count == 0)
            {
                return CreateNoOp("No changes to apply.");
            }

            foreach (var change in result.Changes)
            {
                var changeCell = board.Cells[change.Row, change.Column];
                change.OldValue = changeCell?.Value;
            }

            result.EnactAll(board);

            if (board.ChangeLog == null)
            {
                board.ChangeLog = new List<CellChange>();
            }

            if (board.ChangeLogIndex < board.ChangeLog.Count)
            {
                board.ChangeLog.RemoveRange(board.ChangeLogIndex, board.ChangeLog.Count - board.ChangeLogIndex);
            }

            int groupId = board.NextChangeGroupId;
            board.NextChangeGroupId++;

            foreach (var change in result.Changes)
            {
                board.ChangeLog.Add(new CellChange
                {
                    Row = change.Row,
                    Column = change.Column,
                    OldValue = change.OldValue,
                    NewValue = change.NewValue,
                    ClearValue = change.ClearValue,
                    ForceSetValue = change.ForceSetValue,
                    RemovedCandidates = change.RemovedCandidates != null ? new List<int>(change.RemovedCandidates) : new List<int>(),
                    AddedCandidates = change.AddedCandidates != null ? new List<int>(change.AddedCandidates) : new List<int>(),
                    GroupId = groupId,
                    SourceRuleName = sourceRuleName,
                    SourceRuleDescription = result.Description
                });
            }

            board.ChangeLogIndex = board.ChangeLog.Count;

            return new ManualEditExecutionResult
            {
                Applied = true,
                Description = result.Description,
                RuleResult = result
            };
        }

        private static ManualEditExecutionResult CreateNoOp(string description)
        {
            return new ManualEditExecutionResult
            {
                Applied = false,
                Description = description,
                RuleResult = new RuleResult
                {
                    Apply = false,
                    Description = description
                }
            };
        }

        private static bool TryGetEditableCell(Board board, int row, int column, out Cell cell, out string reason)
        {
            cell = null;
            reason = string.Empty;

            if (board == null)
            {
                reason = "Board is null.";
                return false;
            }

            if (board.Cells == null)
            {
                reason = "Board cells are not initialised.";
                return false;
            }

            if (row < 0 || row >= board.Size || column < 0 || column >= board.Size)
            {
                reason = "Cell coordinates are outside board bounds.";
                return false;
            }

            cell = board.Cells[row, column];
            if (cell == null)
            {
                reason = "Target cell is missing.";
                return false;
            }

            if (cell.IsGiven)
            {
                reason = "Given cells cannot be edited manually.";
                return false;
            }

            if (cell.Candidates == null)
            {
                cell.Candidates = new HashSet<int>();
            }

            return true;
        }

        private static bool TryGetAnyCell(Board board, int row, int column, out Cell cell, out string reason)
        {
            cell = null;
            reason = string.Empty;

            if (board == null)
            {
                reason = "Board is null.";
                return false;
            }

            if (board.Cells == null)
            {
                reason = "Board cells are not initialised.";
                return false;
            }

            if (row < 0 || row >= board.Size || column < 0 || column >= board.Size)
            {
                reason = "Cell coordinates are outside board bounds.";
                return false;
            }

            cell = board.Cells[row, column];
            if (cell == null)
            {
                reason = "Target cell is missing.";
                return false;
            }

            if (cell.Candidates == null)
            {
                cell.Candidates = new HashSet<int>();
            }

            return true;
        }
    }
}
