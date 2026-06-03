using System.Collections.Generic;
using UnityEngine;
using Sudoku.Models;
using Sudoku.Solver.Rules;
using Sudoku.Scripts.UI;

namespace Sudoku.Solver
{
    /**
     * Temporary Simple Unity runner component to load a 9x9 puzzle from inspector rows
     * and execute the registered rule engine. Use the context menu to run steps.
     * Just used for testing.
     */
    public class SolverRunner : MonoBehaviour
    {
        /** When true, ignore incoming preview requests (useful during apply). */
        public bool SuppressPreviewRequests = false;

        [Tooltip("Provide 9 strings each with 9 characters (digits 1-9 or . for empty)")]
        public string[] PuzzleRows = new string[9];

        public RuleRegistry Registry;
        public SolverEngine Engine;

        private Board _board;

        /**
         * Expose the currently loaded board (may be null until loaded).
         */
        public Board CurrentBoard => _board;

        /**
         * Last rule that was applied via the runner (null when none).
         */
        public ISudokuRule LastAppliedRule { get; private set; }

        /**
         * Result of the last rule application (null when none or not applied).
         */
        public RuleResult LastRuleResult { get; private set; }

        /**
            * Result of a hovered/previewed rule. This is not enacted on the board;
            * it is used by UI visualizers to show what a rule would change.
            */
        public RuleResult PreviewRuleResult { get; private set; }
        /** Whether candidates have been initialised for the current board. */
        public bool CandidatesInitialised { get; private set; } = false;

        private void Awake()
        {
            EnsureEngine();
            // Auto-load the puzzle rows when the scene starts so UI visualizers
            // that depend on `CurrentBoard` (e.g. `BoardVisualizer`) will render
            // without requiring manual context-menu actions in the Editor.
            LoadBoardFromRows();
        }

        // Made public so external UI components can ensure the runner has
        // initialized its RuleRegistry and SolverEngine.
        public void EnsureEngine()
        {
            if (Registry == null)
            {
                Registry = new RuleRegistry();
                Registry.RegisterMinimal();
                Registry.RegisterMedium();
                Registry.RegisterAdvanced();
            }
            if (Engine == null) Engine = new SolverEngine(Registry);
        }

        /**
         * Parse the `PuzzleRows` into a standard 9x9 Board. Logs errors if format invalid.
         */
        [ContextMenu("Load Board From Rows")]
        public void LoadBoardFromRows()
        {
            if (PuzzleRows == null || PuzzleRows.Length != 9)
            {
                Debug.LogError("PuzzleRows must contain exactly 9 strings for a 9x9 puzzle.");
                return;
            }

            // Clear other SolverRunner instances in the scene to avoid conflicting models
            var runners = Object.FindObjectsByType<SolverRunner>();
            foreach (var r in runners)
            {
                if (r == this) continue;
#if UNITY_EDITOR
                DestroyImmediate(r.gameObject);
#else
                Destroy(r.gameObject);
#endif
            }

            var board = new Board(9, 3, 3);
            for (int r = 0; r < 9; r++)
            {
                var rowStr = PuzzleRows[r] ?? string.Empty;
                if (rowStr.Length < 9)
                {
                    Debug.LogError($"Row {r} must be at least 9 characters.");
                    return;
                }
                for (int c = 0; c < 9; c++)
                {
                    char ch = rowStr[c];
                    int? val = null;
                    bool isGiven = false;
                    if (ch >= '1' && ch <= '9') { 
                        val = ch - '0'; 
                        isGiven = true; 
                    }
                    var cell = new Cell(r, c, val, isGiven);
                    board.Cells[r, c] = cell;
                }
            }
            _board = board;
            Debug.Log($"SolverRunner.LoadBoardFromRows: runner.EntityId={this.GetEntityId()} board.hash={_board.GetHashCode()}");
            CandidatesInitialised = false;
        }

        [ContextMenu("Initialise Candidates")]
        public void InitialiseCandidates()
        {
            if (_board == null) LoadBoardFromRows();
            if (_board == null) return;
            EnsureEngine();
            var result = new RuleResult
            {
                Apply = false,
                Description = "Initialise Candidates"
            };

            // Step over each cell and initialize candidates for empty cells
            // checking the peers for number elimination.
            for (int r = 0; r < _board.Size; r++)
            {
                for (int c = 0; c < _board.Size; c++)
                {
                    Cell cell = _board.Cells[r, c];
                    if (cell.Value.HasValue) continue; // skip filled cells

                    if (cell.Candidates == null)
                    {
                        cell.Candidates = new HashSet<int>();
                    }

                    var oldCandidates = new HashSet<int>(cell.Candidates);
                    var newCandidates = new HashSet<int>();
                    for (int v = 1; v <= _board.Size; v++) newCandidates.Add(v);

                    // Eliminate candidates based on peers' values
                    var peers = _board.GetPeers(cell);
                    foreach (var peer in peers)
                    {
                        if (peer.Value.HasValue)
                        {
                            newCandidates.Remove(peer.Value.Value);
                        }
                    }

                    var removedCandidates = new List<int>();
                    foreach (var candidate in oldCandidates)
                    {
                        if (!newCandidates.Contains(candidate))
                        {
                            removedCandidates.Add(candidate);
                        }
                    }

                    var addedCandidates = new List<int>();
                    foreach (var candidate in newCandidates)
                    {
                        if (!oldCandidates.Contains(candidate))
                        {
                            addedCandidates.Add(candidate);
                        }
                    }

                    if (removedCandidates.Count > 0 || addedCandidates.Count > 0)
                    {
                        result.Changes.Add(new CellChange
                        {
                            Row = r,
                            Column = c,
                            OldValue = cell.Value,
                            RemovedCandidates = removedCandidates,
                            AddedCandidates = addedCandidates
                        });
                    }

                    cell.Candidates.Clear();
                    foreach (var candidate in newCandidates) cell.Candidates.Add(candidate);
                }
            }

            result.Apply = result.Changes.Count > 0;
            if (result.Apply)
            {
                result.Description = $"Initialise Candidates ({result.Changes.Count} cells updated)";

                try
                {
                    if (_board.ChangeLog == null) _board.ChangeLog = new List<CellChange>();
                    if (_board.ChangeLogIndex < _board.ChangeLog.Count)
                    {
                        _board.ChangeLog.RemoveRange(_board.ChangeLogIndex, _board.ChangeLog.Count - _board.ChangeLogIndex);
                    }

                    int gid = _board.NextChangeGroupId;
                    _board.NextChangeGroupId++;

                    foreach (var ch in result.Changes)
                    {
                        _board.ChangeLog.Add(new CellChange
                        {
                            Row = ch.Row,
                            Column = ch.Column,
                            OldValue = ch.OldValue,
                            NewValue = ch.NewValue,
                            ClearValue = ch.ClearValue,
                            ForceSetValue = ch.ForceSetValue,
                            RemovedCandidates = ch.RemovedCandidates != null ? new List<int>(ch.RemovedCandidates) : new List<int>(),
                            AddedCandidates = ch.AddedCandidates != null ? new List<int>(ch.AddedCandidates) : new List<int>(),
                            GroupId = gid,
                            SourceRuleName = "InitialiseCandidates",
                            SourceRuleDescription = result.Description
                        });
                    }

                    _board.ChangeLogIndex = _board.ChangeLog.Count;
                    Debug.Log($"SolverRunner.InitialiseCandidates: appended {result.Changes.Count} changes as group {gid}; runner.EntityId={this.GetEntityId()} board.hash={_board.GetHashCode()} ChangeLogCount={_board.ChangeLog.Count}");
                    ChangeLogRuntimeControls.RefreshButtonStates();
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"SolverRunner.InitialiseCandidates: failed to append changes to ChangeLog: {ex.Message}");
                }
            }

            LastAppliedRule = null;
            LastRuleResult = result;
            PreviewRuleResult = null;
            CandidatesInitialised = true;
            Debug.Log($"SolverRunner.InitialiseCandidates: runner.EntityId={this.GetEntityId()} board.hash={_board?.GetHashCode() ?? 0} candidatesInitialised={CandidatesInitialised}");
        }

        [ContextMenu("Run Next Rule Step")]
        public void RunNextStep()
        {
            if (_board == null) LoadBoardFromRows();
            if (_board == null) return;
            EnsureEngine();
            (ISudokuRule rule, RuleResult result) = Registry.ApplyNext(_board);
            LastAppliedRule = rule;
            LastRuleResult = result;
            if (rule == null || result == null || !result.Apply)
            {
                Debug.Log($"SolverRunner.RunNextStep: no rule applied. runner.EntityId={this.GetEntityId()} board.hash={_board.GetHashCode()}");
                return;
            }
            Debug.Log($"SolverRunner.RunNextStep: applied {rule.GetType().Name} runner.EntityId={this.GetEntityId()} board.hash={_board.GetHashCode()} changes={result.Changes?.Count ?? 0}");
        }

        /**
         * Prepare a preview of what the given rule would change on the current board.
         * The preview is non-destructive and stored in `PreviewRuleResult` for UI use.
         */
        public void PreviewRule(ISudokuRule rule)
        {
            if (SuppressPreviewRequests) return;            
            if (_board == null) LoadBoardFromRows();
            if (_board == null) { PreviewRuleResult = null; return; }
            EnsureEngine();
            if (rule == null) { PreviewRuleResult = null; return; }
            try
            {
                var res = rule.CalculateChanges(_board);
                PreviewRuleResult = (res != null && res.Apply) ? res : new RuleResult { Apply = false };
            }
            catch (System.Exception)
            {
                PreviewRuleResult = new RuleResult { Apply = false, Description = "Preview error" };
            }
        }

        /**
         * Set the runner's `PreviewRuleResult` from a range of entries already
         * recorded in the board's ChangeLog. This allows external UIs to show
         * highlights that correspond exactly to a previously-applied group.
         */
        public void SetPreviewFromChangeLogRange(int startIndex, int endIndex)
        {
            if (_board == null || _board.ChangeLog == null) { PreviewRuleResult = null; return; }
            if (startIndex < 0) startIndex = 0;
            if (endIndex > _board.ChangeLog.Count) endIndex = _board.ChangeLog.Count;
            if (startIndex >= endIndex) { PreviewRuleResult = null; return; }

            var res = new RuleResult { Apply = true, Description = "ChangeLog group preview" };
            for (int i = startIndex; i < endIndex; i++)
            {
                var ch = _board.ChangeLog[i];
                if (ch == null) continue;
                // create a shallow copy to avoid accidental mutation of board.ChangeLog
                var copy = new CellChange
                {
                    Row = ch.Row,
                    Column = ch.Column,
                    OldValue = ch.OldValue,
                    NewValue = ch.NewValue,
                    ClearValue = ch.ClearValue,
                    ForceSetValue = ch.ForceSetValue,
                    GroupId = ch.GroupId,
                    SourceRuleName = ch.SourceRuleName,
                    SourceRuleDescription = ch.SourceRuleDescription,
                    RemovedCandidates = ch.RemovedCandidates != null ? new System.Collections.Generic.List<int>(ch.RemovedCandidates) : new System.Collections.Generic.List<int>(),
                    AddedCandidates = ch.AddedCandidates != null ? new System.Collections.Generic.List<int>(ch.AddedCandidates) : new System.Collections.Generic.List<int>()
                };
                res.Changes.Add(copy);
            }
            PreviewRuleResult = res;
        }

        /**
         * Set the runner's `LastRuleResult` from a range of entries in the
         * board's ChangeLog so the visualizer renders applied-style highlights
         * (including removed-candidate red marks) matching the recorded group.
         */
        public void SetLastRuleResultFromChangeLogRange(int startIndex, int endIndex)
        {
            if (_board == null || _board.ChangeLog == null) { LastRuleResult = null; return; }
            if (startIndex < 0) startIndex = 0;
            if (endIndex > _board.ChangeLog.Count) endIndex = _board.ChangeLog.Count;
            if (startIndex >= endIndex) { LastRuleResult = null; return; }

            var res = new RuleResult { Apply = true, Description = "ChangeLog group (applied)" };
            for (int i = startIndex; i < endIndex; i++)
            {
                var ch = _board.ChangeLog[i];
                if (ch == null) continue;
                var copy = new CellChange
                {
                    Row = ch.Row,
                    Column = ch.Column,
                    OldValue = ch.OldValue,
                    NewValue = ch.NewValue,
                    ClearValue = ch.ClearValue,
                    ForceSetValue = ch.ForceSetValue,
                    GroupId = ch.GroupId,
                    SourceRuleName = ch.SourceRuleName,
                    SourceRuleDescription = ch.SourceRuleDescription,
                    RemovedCandidates = ch.RemovedCandidates != null ? new System.Collections.Generic.List<int>(ch.RemovedCandidates) : new System.Collections.Generic.List<int>(),
                    AddedCandidates = ch.AddedCandidates != null ? new System.Collections.Generic.List<int>(ch.AddedCandidates) : new System.Collections.Generic.List<int>()
                };
                res.Changes.Add(copy);
            }

            // Clear preview so visualizer prefers the applied result path
            PreviewRuleResult = null;
            LastRuleResult = res;

        }

        /** Prepare a lightweight preview for the Initialise Candidates tool.
         * Highlights all empty cells (used to indicate candidate initialization)
         */
        public void PreviewInitialiseCandidates()
        {
            if (SuppressPreviewRequests) return;
            if (_board == null) LoadBoardFromRows();
            if (_board == null) { PreviewRuleResult = null; return; }
            var res = new RuleResult { Apply = true, Description = "Initialise Candidates (preview)" };
            for (int r = 0; r < _board.Size; r++)
            {
                for (int c = 0; c < _board.Size; c++)
                {
                    var cell = _board.Cells[r, c];
                    if (cell != null && !cell.Value.HasValue)
                    {
                        res.UsedCells.Add(new UsedCell { Row = r, Column = c });
                    }
                }
            }
            PreviewRuleResult = res;
        }

        /** Clear any previewed rule result. */
        public void ClearPreview()
        {
            PreviewRuleResult = null;
        }

        /**
         * Sets an active but empty preview so the board renders with no highlights.
         * Using a non-null Apply=true result with no changes puts the visualizer into
         * previewActive mode while drawing nothing, which correctly hides stale
         * _lastSeenRuleResult highlights (e.g. when hovering the Initial State row).
         */
        public void SetEmptyPreview()
        {
            PreviewRuleResult = new RuleResult { Apply = true, Description = "Empty preview" };
        }

        /**
         * Resolve Smart action metadata for a target cell without mutating board state.
         *
         * @param row Zero-based row index.
         * @param column Zero-based column index.
         * @returns Smart action scaffold result used by higher-level interaction layers.
         */
        public SmartActionResolution ResolveSmartAction(int row, int column)
        {
            if (_board == null) LoadBoardFromRows();
            return ManualCellEditCore.ResolveSmartAction(_board, row, column);
        }

        /**
         * Apply manual SetValue action to the board and record one atomic changelog group.
         *
         * @param row Zero-based row index.
         * @param column Zero-based column index.
         * @param value Value to place.
         * @returns True when the action changed board state.
         */
        public bool ManualSetValue(int row, int column, int value)
        {
            var execution = ExecuteManualSetValue(row, column, value);
            return execution != null && execution.Applied;
        }

        /**
         * Apply manual SetValue action and return the full execution outcome.
         *
         * @param row Zero-based row index.
         * @param column Zero-based column index.
         * @param value Value to place.
         * @returns Full execution result including no-op descriptions.
         */
        public ManualEditExecutionResult ExecuteManualSetValue(int row, int column, int value)
        {
            if (_board == null) LoadBoardFromRows();
            var execution = ManualCellEditCore.ApplySetValue(_board, row, column, value);
            LastAppliedRule = null;
            LastRuleResult = execution.RuleResult;
            PreviewRuleResult = null;
            RefreshRuntimeControlsAfterManualEdit(execution);
            return execution;
        }

        /**
         * Apply manual AddCandidate action to the board and record one atomic changelog group.
         *
         * @param row Zero-based row index.
         * @param column Zero-based column index.
         * @param candidate Candidate to add.
         * @returns True when the action changed board state.
         */
        public bool ManualAddCandidate(int row, int column, int candidate)
        {
            var execution = ExecuteManualAddCandidate(row, column, candidate);
            return execution != null && execution.Applied;
        }

        /**
         * Apply manual AddCandidate action and return the full execution outcome.
         *
         * @param row Zero-based row index.
         * @param column Zero-based column index.
         * @param candidate Candidate to add.
         * @returns Full execution result including no-op descriptions.
         */
        public ManualEditExecutionResult ExecuteManualAddCandidate(int row, int column, int candidate)
        {
            if (_board == null) LoadBoardFromRows();
            var execution = ManualCellEditCore.ApplyAddCandidate(_board, row, column, candidate);
            LastAppliedRule = null;
            LastRuleResult = execution.RuleResult;
            PreviewRuleResult = null;
            RefreshRuntimeControlsAfterManualEdit(execution);
            return execution;
        }

        /**
         * Clear a solved cell and restore its candidates.
         *
         * @param row Zero-based row index.
         * @param column Zero-based column index.
         * @returns Full execution result including no-op descriptions.
         */
        public ManualEditExecutionResult ExecuteManualClearValue(int row, int column)
        {
            if (_board == null) LoadBoardFromRows();
            var execution = ManualCellEditCore.ApplyClearValue(_board, row, column);
            LastAppliedRule = null;
            LastRuleResult = execution.RuleResult;
            PreviewRuleResult = null;
            RefreshRuntimeControlsAfterManualEdit(execution);
            return execution;
        }

        /**
         * Apply manual RemoveCandidate action to the board and record one atomic changelog group.
         *
         * @param row Zero-based row index.
         * @param column Zero-based column index.
         * @param candidate Candidate to remove.
         * @returns True when the action changed board state.
         */
        public bool ManualRemoveCandidate(int row, int column, int candidate)
        {
            var execution = ExecuteManualRemoveCandidate(row, column, candidate);
            return execution != null && execution.Applied;
        }

        /**
         * Apply manual RemoveCandidate action and return the full execution outcome.
         *
         * @param row Zero-based row index.
         * @param column Zero-based column index.
         * @param candidate Candidate to remove.
         * @returns Full execution result including no-op descriptions.
         */
        public ManualEditExecutionResult ExecuteManualRemoveCandidate(int row, int column, int candidate)
        {
            if (_board == null) LoadBoardFromRows();
            var execution = ManualCellEditCore.ApplyRemoveCandidate(_board, row, column, candidate);
            LastAppliedRule = null;
            LastRuleResult = execution.RuleResult;
            PreviewRuleResult = null;
            RefreshRuntimeControlsAfterManualEdit(execution);
            return execution;
        }

        /**
         * Apply a row/column/box candidate action anchored to one cell.
         *
         * @param row Zero-based row index.
         * @param column Zero-based column index.
         * @param candidate Candidate to add/remove.
         * @param addToUnsolvedCells True to add candidate to unsolved editable unit cells; false to remove candidate from editable unit cells.
         * @returns Full execution result including no-op descriptions.
         */
        public ManualEditExecutionResult ExecuteManualUnitCandidateAction(int row, int column, int candidate, bool addToUnsolvedCells)
        {
            if (_board == null) LoadBoardFromRows();
            var execution = ManualCellEditCore.ApplyUnitCandidateAction(_board, row, column, candidate, addToUnsolvedCells);
            LastAppliedRule = null;
            LastRuleResult = execution.RuleResult;
            PreviewRuleResult = null;
            RefreshRuntimeControlsAfterManualEdit(execution);
            return execution;
        }

        /**
         * Publish a non-mutating manual outcome so runtime UI can show a clear no-op result.
         *
         * @param description Outcome text that explains why no edit was applied.
         */
        public void PublishManualNoOpOutcome(string description)
        {
            LastAppliedRule = null;
            LastRuleResult = new RuleResult
            {
                Apply = false,
                Description = string.IsNullOrWhiteSpace(description) ? "No manual action applied." : description
            };
            PreviewRuleResult = null;
        }

        /**
         * Refresh runtime undo/redo controls after a manual edit changes the changelog.
         *
         * @param execution Manual edit execution result to inspect for side effects.
         */
        private static void RefreshRuntimeControlsAfterManualEdit(ManualEditExecutionResult execution)
        {
            if (execution == null || !execution.Applied)
            {
                return;
            }

            ChangeLogRuntimeControls.RefreshButtonStates();
        }

        /**
         * Execute a specific rule against the current board (if applicable and enabled).
         */
        public void RunRule(ISudokuRule rule)
        {
            if (_board == null) LoadBoardFromRows();
            if (_board == null) return;
            EnsureEngine();
            if (rule == null) return;
            if (!Registry.IsEnabled(rule)) return;
            RuleResult res = null;
            try
            {
                res = rule.CalculateChanges(_board);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"RunRule: CalculateChanges threw for {rule.GetType().Name}: {ex.Message}");
                return;
            }
            if (res == null || !res.Apply) return;
            // Before enacting, populate OldValue for each change (mirror RuleRegistry behavior)
            try
            {
                foreach (var ch in res.Changes)
                {
                    try
                    {
                        var cell = _board.Cells[ch.Row, ch.Column];
                        ch.OldValue = cell?.Value;
                    }
                    catch { }
                }
            }
            catch { }

            // Enact all recorded changes
            res.EnactAll(_board);

            // Append a deep copy of each recorded change to the board's in-memory change log
            try
            {
                if (_board.ChangeLog == null) _board.ChangeLog = new System.Collections.Generic.List<CellChange>();
                if (_board.ChangeLogIndex < _board.ChangeLog.Count)
                {
                    _board.ChangeLog.RemoveRange(_board.ChangeLogIndex, _board.ChangeLog.Count - _board.ChangeLogIndex);
                }
                int gid = _board.NextChangeGroupId;
                _board.NextChangeGroupId++;
                foreach (var ch in res.Changes)
                {
                    var copy = new CellChange
                    {
                        Row = ch.Row,
                        Column = ch.Column,
                        OldValue = ch.OldValue,
                        NewValue = ch.NewValue,
                        ClearValue = ch.ClearValue,
                        ForceSetValue = ch.ForceSetValue,
                        RemovedCandidates = ch.RemovedCandidates != null ? new System.Collections.Generic.List<int>(ch.RemovedCandidates) : new System.Collections.Generic.List<int>(),
                        AddedCandidates = ch.AddedCandidates != null ? new System.Collections.Generic.List<int>(ch.AddedCandidates) : new System.Collections.Generic.List<int>(),
                        GroupId = gid,
                        SourceRuleName = rule.GetType().Name,
                        SourceRuleDescription = res.Description
                    };
                    _board.ChangeLog.Add(copy);
                }
                Debug.Log($"SolverRunner.RunRule: appended {res.Changes.Count} changes as group {gid}; runner.EntityId={this.GetEntityId()} board.hash={_board.GetHashCode()} ChangeLogCount={_board.ChangeLog.Count}");
                _board.ChangeLogIndex = _board.ChangeLog.Count;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"SolverRunner.RunRule: failed to append changes to ChangeLog: {ex.Message}");
            }

            LastAppliedRule = rule;
            LastRuleResult = res;
            PreviewRuleResult = null;
            Debug.Log($"SolverRunner.RunRule: enacted {rule.GetType().Name} runner.EntityId={this.GetEntityId()} board.hash={_board.GetHashCode()} changes={res.Changes?.Count ?? 0}");
        }

        [ContextMenu("Run Solve")]
        public void RunSolve()
        {
            if (_board == null) LoadBoardFromRows();
            if (_board == null) return;
            EnsureEngine();
            var solved = Engine.Solve(_board, out List<(ISudokuRule rule, RuleResult result)> steps);
            // store last applied step if any
            if (steps != null && steps.Count > 0)
            {
                (ISudokuRule rule, RuleResult result) last = steps[steps.Count - 1];
                LastAppliedRule = last.rule;
                LastRuleResult = last.result;
            }
            else
            {
                LastAppliedRule = null;
                LastRuleResult = null;
            }
            Debug.Log($"SolverRunner.RunSolve: runner.EntityId={this.GetEntityId()} board.hash={_board.GetHashCode()} steps={steps?.Count ?? 0} solved={solved}");
        }

        [ContextMenu("Reset Candidates for Empty Cells")]
        public void ResetCandidates()
        {
            if (_board == null) LoadBoardFromRows();
            if (_board == null) return;
            for (int r = 0; r < _board.Size; r++)
                for (int c = 0; c < _board.Size; c++)
                {
                    Cell cell = _board.Cells[r, c];
                    if (!cell.Value.HasValue)
                    {
                        cell.Candidates.Clear();
                        for (int v = 1; v <= _board.Size; v++) cell.Candidates.Add(v);
                    }
                }
            CandidatesInitialised = true;
        }

        [ContextMenu("Validate Board")]
        public bool ValidateBoard()
        {
            if (_board == null) LoadBoardFromRows();
            if (_board == null)  return false;

            // Perform a full conflict discovery. `FindConflicts` performs immediate
            // duplicate detection and — if no immediate conflicts — will attempt to
            // solve a copy of the board with all rules to discover latent
            // contradictions. This is more thorough than `IsValid` alone.
            var conflicts = _board.FindConflicts();
            if (conflicts != null && conflicts.Count > 0)
            {
                // Special sentinel: a UsedCell with negative coordinates indicates
                // the board was found unsolvable by the full-rule solver.
                bool unsolvable = conflicts.Exists(u => u.Row < 0);
                var errMsg = unsolvable ? "Board is UNSOLVABLE by the full solver." : "Board is INVALID: duplicate found in a unit.";
                Debug.LogError(errMsg + "\n" + BoardToString(_board));
                LastAppliedRule = null;
                LastRuleResult = new RuleResult { Apply = false, Description = errMsg };
                LastRuleResult.UsedCells.AddRange(conflicts);
                return false;
            }

            // No conflicts found
            var okMsg = "Board is valid.";
            LastAppliedRule = null;
            LastRuleResult = new RuleResult { Apply = false, Description = okMsg };
            return true;
        }

        private string BoardToString(Board board)
        {
            var lines = new List<string>();
            for (int r = 0; r < board.Size; r++)
            {
                var chars = new char[board.Size];
                for (int c = 0; c < board.Size; c++)
                {
                    var v = board.Cells[r, c].Value;
                    chars[c] = v.HasValue ? (char)('0' + v.Value) : '.';
                }
                lines.Add(new string(chars));
            }
            return string.Join("\n", lines);
        }
    }
}
