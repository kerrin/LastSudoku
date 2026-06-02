using System.Collections.Generic;
using Cell = Sudoku.Models.Cell;
using Board = Sudoku.Models.Board;
using Sudoku.Models;

namespace Sudoku.Solver.Rules
{
    /**
     * Convenience extension methods for reading and mutating a <see cref="Board"/>.
     * These helpers simplify common unit (row/column/box) iteration and peer lookup.
     */
    public static partial class BoardExtensions
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

        /**
         * Find detailed conflicts in the board. Returns a list of `UsedCell`
         * entries describing cells that violate unit uniqueness (duplicates).
         * Each `UsedCell.Candidate` is set to the duplicated digit.
         */
        public static List<UsedCell> FindConflicts(this Board board)
        {
            var conflicts = CollectConflicts(board);

            // If we already found conflicts, return them now.
            if (conflicts != null && conflicts.Count > 0) return conflicts;

            // No immediate conflicts: attempt to solve a copy of the board with a full rule set
            // so we can detect latent inconsistencies (e.g. candidate-driven contradictions).
            try
            {
                // Create a deep copy of the board so we do not alter the user's board state
                var copy = new Board(board.Size, board.BoxWidth, board.BoxHeight);
                for (int r = 0; r < board.Size; r++)
                {
                    for (int c = 0; c < board.Size; c++)
                    {
                        var src = board.Cells[r, c];
                        copy.Cells[r, c] = src?.Clone();
                    }
                }

                // Create a fresh registry with all rules enabled and attempt to solve the copy
                var registry = new RuleRegistry();
                registry.RegisterDefaults();
                var engine = new SolverEngine(registry);
                var solved = engine.Solve(copy, out var steps);

                if (solved)
                {
                    // If solved, re-check for any duplicates in the solved board state
                    var postConflicts = CollectConflicts(copy);
                    if (postConflicts != null && postConflicts.Count > 0) return postConflicts;
                    // solved and no conflicts -> return empty list
                    return new List<UsedCell>();
                }
                else
                {
                    // Not solvable by the full-rule solver: return a special marker UsedCell
                    // with negative coordinates so the UI can detect and render a global "unsolvable" state.
                    return new List<UsedCell> { new UsedCell { Row = -1, Column = -1, Candidate = null } };
                }
            }
            catch
            {
                // On any unexpected error during the attempt, return the empty conflict list.
                return new List<UsedCell>();
            }
        }

        // Internal helper: collect duplicate used-cells from a given board without
        // invoking the higher-level FindConflicts logic (avoids recursion).
        private static List<UsedCell> CollectConflicts(Board board)
        {
            var conflicts = new List<UsedCell>();
            int size = board.Size;

            void RecordDuplicates(Dictionary<int, List<Cell>> map)
            {
                foreach (var kv in map)
                {
                    int digit = kv.Key;
                    var cells = kv.Value;
                    if (cells.Count > 1)
                    {
                        foreach (var cell in cells)
                        {
                            if (!conflicts.Exists(u => u.Row == cell.Row && u.Column == cell.Column && u.Candidate == digit))
                            {
                                conflicts.Add(new UsedCell { Row = cell.Row, Column = cell.Column, Candidate = digit });
                            }
                        }
                    }
                }
            }

            // Rows
            for (int r = 0; r < size; r++)
            {
                var map = new Dictionary<int, List<Cell>>();
                foreach (var cell in board.GetRow(r))
                {
                    if (cell.Value.HasValue)
                    {
                        int v = cell.Value.Value;
                        if (!map.ContainsKey(v)) map[v] = new List<Cell>();
                        map[v].Add(cell);
                    }
                }
                RecordDuplicates(map);
            }

            // Columns
            for (int c = 0; c < size; c++)
            {
                var map = new Dictionary<int, List<Cell>>();
                foreach (var cell in board.GetColumn(c))
                {
                    if (cell.Value.HasValue)
                    {
                        int v = cell.Value.Value;
                        if (!map.ContainsKey(v)) map[v] = new List<Cell>();
                        map[v].Add(cell);
                    }
                }
                RecordDuplicates(map);
            }

            // Boxes
            for (int b = 0; b < size; b++)
            {
                var map = new Dictionary<int, List<Cell>>();
                foreach (var cell in board.GetBox(b))
                {
                    if (cell.Value.HasValue)
                    {
                        int v = cell.Value.Value;
                        if (!map.ContainsKey(v)) map[v] = new List<Cell>();
                        map[v].Add(cell);
                    }
                }
                RecordDuplicates(map);
            }

            return conflicts;
        }

            /**
             * Undo the most recent change recorded in `board.ChangeLog`.
             * Returns true when a change was undone, false when there is nothing to undo.
             */
            public static bool UndoLast(this Board board)
            {
                if (board == null || board.ChangeLog == null) return false;
                if (board.ChangeLogIndex <= 0) return false;

                int lastIdx = board.ChangeLogIndex - 1;
                var last = board.ChangeLog[lastIdx];
                if (last == null) { board.ChangeLogIndex = lastIdx; return false; }

                int gid = last.GroupId;

                // Find group start
                int start = lastIdx;
                while (start >= 0 && board.ChangeLog[start].GroupId == gid) start--;
                start++;

                // Undo entries in reverse order for the group
                for (int i = lastIdx; i >= start; i--)
                {
                    var ch = board.ChangeLog[i];
                    if (ch == null) continue;
                    if (ch.Row < 0 || ch.Row >= board.Size || ch.Column < 0 || ch.Column >= board.Size) continue;

                    var cell = board.Cells[ch.Row, ch.Column];

                    // Revert value assignment if present
                    if (ch.NewValue.HasValue || ch.ClearValue)
                    {
                        if (ch.OldValue.HasValue)
                        {
                            board.SetValue(cell, ch.OldValue.Value);
                        }
                        else
                        {
                            // Clear assigned value and attempt to restore candidates from RemovedCandidates
                            cell.Value = null;
                            cell.Candidates.Clear();
                            if (ch.RemovedCandidates != null)
                            {
                                foreach (var v in ch.RemovedCandidates) cell.Candidates.Add(v);
                            }
                            if (ch.NewValue.HasValue) cell.Candidates.Add(ch.NewValue.Value);
                        }
                    }

                    // Restore removed candidates
                    if (ch.RemovedCandidates != null && ch.RemovedCandidates.Count > 0)
                    {
                        foreach (var v in ch.RemovedCandidates)
                        {
                            if (!cell.Candidates.Contains(v)) cell.Candidates.Add(v);
                        }
                    }

                    // Revert manually-added candidates.
                    if (ch.AddedCandidates != null && ch.AddedCandidates.Count > 0)
                    {
                        foreach (var v in ch.AddedCandidates)
                        {
                            if (cell.Candidates.Contains(v)) cell.Candidates.Remove(v);
                        }
                    }
                }

                board.ChangeLogIndex = start;
                return true;
            }

            /**
             * Redo the next change at `board.ChangeLogIndex` if available.
             */
            public static bool RedoNext(this Board board)
            {
                if (board == null || board.ChangeLog == null) return false;
                if (board.ChangeLogIndex >= board.ChangeLog.Count) return false;

                int idx = board.ChangeLogIndex;
                var first = board.ChangeLog[idx];
                if (first == null) return false;

                int gid = first.GroupId;

                // Find group end (exclusive)
                int end = idx;
                while (end < board.ChangeLog.Count && board.ChangeLog[end].GroupId == gid) end++;

                // Apply entries in order for the group
                for (int i = idx; i < end; i++)
                {
                    var ch = board.ChangeLog[i];
                    if (ch == null) continue;
                    if (ch.Row < 0 || ch.Row >= board.Size || ch.Column < 0 || ch.Column >= board.Size) continue;

                    var cell = board.Cells[ch.Row, ch.Column];

                    if (ch.ClearValue)
                    {
                        cell.Value = null;
                    }

                    if (ch.NewValue.HasValue)
                    {
                        board.SetValue(cell, ch.NewValue.Value);
                    }

                    if (ch.RemovedCandidates != null && ch.RemovedCandidates.Count > 0)
                    {
                        foreach (var v in ch.RemovedCandidates)
                        {
                            if (cell.Candidates.Contains(v)) cell.Candidates.Remove(v);
                        }
                    }

                    if (ch.AddedCandidates != null && ch.AddedCandidates.Count > 0)
                    {
                        foreach (var v in ch.AddedCandidates)
                        {
                            if (!cell.Candidates.Contains(v)) cell.Candidates.Add(v);
                        }
                    }
                }

                board.ChangeLogIndex = end;
                return true;
            }

            /**
             * Undo multiple steps (up to `steps`). Returns number of steps actually undone.
             */
            public static int UndoSteps(this Board board, int steps)
            {
                int undone = 0;
                for (int i = 0; i < steps; i++) if (UndoLast(board)) undone++; else break;
                return undone;
            }

            /**
             * Redo multiple steps (up to `steps`). Returns number of steps actually redone.
             */
            public static int RedoSteps(this Board board, int steps)
            {
                int redone = 0;
                for (int i = 0; i < steps; i++) if (RedoNext(board)) redone++; else break;
                return redone;
            }

            /**
             * Seek the board to an absolute ChangeLog index by repeatedly
             * undoing or redoing steps until the desired index is reached.
             * This is deterministic and idempotent: calling it multiple times
             * with the same index will have no further effect.
             */
            public static void SeekChangeLogIndex(this Board board, int targetIndex)
            {
                if (board == null || board.ChangeLog == null) return;
                if (targetIndex < 0) targetIndex = 0;
                if (targetIndex > board.ChangeLog.Count) targetIndex = board.ChangeLog.Count;

                while (board.ChangeLogIndex < targetIndex)
                {
                    if (!RedoNext(board)) break;
                }

                while (board.ChangeLogIndex > targetIndex)
                {
                    if (!UndoLast(board)) break;
                }
            }
    }

    public static partial class BoardExtensions
    {
        /**
         * Produce a grouped, human-friendly summary of the board's ChangeLog suitable for UI display.
         */
        public static List<ChangeLogGroupSummary> GetChangeLogSummary(this Board board)
        {
            var groups = new List<ChangeLogGroupSummary>();
            if (board == null || board.ChangeLog == null) return groups;

            int idx = 0;
            while (idx < board.ChangeLog.Count)
            {
                var first = board.ChangeLog[idx];
                if (first == null) { idx++; continue; }
                int gid = first.GroupId;
                var g = new ChangeLogGroupSummary()
                {
                    GroupId = gid,
                    RuleName = first.SourceRuleName,
                    StartIndex = idx
                };

                int valuesAdded = 0;
                int candRemoved = 0;

                while (idx < board.ChangeLog.Count && board.ChangeLog[idx].GroupId == gid)
                {
                    var ch = board.ChangeLog[idx];
                    if (ch == null) { idx++; continue; }
                    var entry = new ChangeLogEntrySummary
                    {
                        Row = ch.Row,
                        Column = ch.Column,
                        OldValue = ch.OldValue,
                        NewValue = ch.NewValue,
                        RemovedCandidatesCount = ch.RemovedCandidates != null ? ch.RemovedCandidates.Count : 0,
                        AddedCandidatesCount = ch.AddedCandidates != null ? ch.AddedCandidates.Count : 0
                    };
                    g.Entries.Add(entry);
                    if (ch.NewValue.HasValue) valuesAdded++;
                    if (ch.RemovedCandidates != null) candRemoved += ch.RemovedCandidates.Count;
                    if (ch.AddedCandidates != null) g.CandidatesAddedCount += ch.AddedCandidates.Count;
                    idx++;
                }

                    g.EndIndex = idx;
                g.ChangesCount = g.Entries.Count;
                g.ValuesAddedCount = valuesAdded;
                g.CandidatesRemovedCount = candRemoved;
                    g.Description = first.SourceRuleDescription;
                groups.Add(g);
            }

            return groups;
        }
    }
}
