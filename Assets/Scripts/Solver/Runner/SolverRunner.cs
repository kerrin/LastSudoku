using System.Collections.Generic;
using UnityEngine;
using Sudoku.Models;
using Sudoku.Solver.Rules;
using Sudoku.Solver.Unsolver;
using Sudoku.UI.Config;
using Sudoku.UI.Panels;

namespace Sudoku.Solver
{
    public enum BoardInteractionMode
    {
        Puzzle = 0,
        PuzzleCreation = 1
    }

    /**
     * Simple Unity runner component to load a 9x9 puzzle from inspector rows
     * and execute the registered rule engine. Use the context menu to run steps.
     * Just used for testing.
     */
    public class SolverRunner : MonoBehaviour
    {
        private const int SparseCreationDigitThreshold = 12;
        private const int SinglesOnlyCreationDigitWindow = 8;
        private const int CandidateSinglePropagationStartDigit = 8;
        private const int SparseCandidateResyncEveryEdits = 5;
        private const string NakedSingleRuleTypeName = nameof(NakedSingleRule);
        private const string HiddenSingleRuleTypeName = nameof(HiddenSingleRule);

        /** When true, ignore incoming preview requests (useful during apply). */
        public bool SuppressPreviewRequests = false;

        [Tooltip("Provide 9 strings each with 9 characters (digits 1-9 or . for empty)")]
        public string[] PuzzleRows = new string[9];

        public RuleRegistry Registry;
        public SolverEngine Engine;

        private Board _board;
        private readonly List<string> _lastCreationSolveRuleNames = new List<string>();
        private int _sparseCreationEditCounter;
        private readonly CreationSparseCandidateValidationRule _creationSparseCandidateValidationRule = new CreationSparseCandidateValidationRule();
        private readonly CreationSingleCandidatePropagationRule _creationSingleCandidatePropagationRule = new CreationSingleCandidatePropagationRule();

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
        /** Current board interaction mode used by UI and edit operations. */
        public BoardInteractionMode InteractionMode { get; private set; } = BoardInteractionMode.Puzzle;
        /** True when the board is being edited as a blank/new puzzle (value-only edits). */
        public bool IsPuzzleCreationMode => InteractionMode == BoardInteractionMode.PuzzleCreation;
        /** Latest creation-mode solver analysis status message shown by runtime UI. */
        public string LastCreationSolveStatusMessage { get; private set; } = string.Empty;
        /** True when the latest creation-mode solver analysis found a complete solution. */
        public bool LastCreationSolveFoundSolution { get; private set; }
        /** True when the latest creation-mode solver analysis found a solution with currently selected rules. */
        public bool LastCreationSolveFoundWithSelectedRules { get; private set; }
        /** Ordered unique rule names used by the latest creation-mode solver analysis. */
        public IReadOnlyList<string> LastCreationSolveRuleNames => _lastCreationSolveRuleNames;
        /** True when the current board state has no immediate contradictions. */
        public bool LastBoardStateIsPossible { get; private set; } = true;
        /** Message describing the latest board-state validation result. */
        public string LastBoardStateValidationMessage { get; private set; } = "Board state has no immediate contradictions.";
        /** Cells involved in the latest board-state validation issue (when any). */
        public IReadOnlyList<UsedCell> LastBoardStateConflictCells => _lastBoardStateConflictCells;

        private readonly List<UsedCell> _lastBoardStateConflictCells = new List<UsedCell>();

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

                // Remove only the duplicate SolverRunner component so we never
                // accidentally delete designer-authored GameObject hierarchies.
                if (Application.isPlaying)
                {
                    Destroy(r);
                }
#if UNITY_EDITOR
                else
                {
                    DestroyImmediate(r);
                }
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
            SetInteractionMode(BoardInteractionMode.Puzzle);
            ClearCreationSolveAnalysis();
            ValidateCurrentBoardState();
        }

        /**
         * Create an empty 9x9 board for puzzle creation mode.
         * Cells start blank and editable, with valid candidate marks.
         */
        public void CreateBlankBoard()
        {
            var board = new Board(9, 3, 3);
            for (int r = 0; r < board.Size; r++)
            {
                for (int c = 0; c < board.Size; c++)
                {
                    var cell = new Cell(r, c, null, false);
                    board.Cells[r, c] = cell;
                }
            }

            _board = board;
            EnsureEngine();
            EnableAllRegisteredRules();
            SyncCandidatesForCurrentBoard(skipFullSolveCheck: true);
            CandidatesInitialised = true;
            LastAppliedRule = null;
            LastRuleResult = null;
            PreviewRuleResult = null;
            SetInteractionMode(BoardInteractionMode.PuzzleCreation);
        }

        /**
         * Set the interaction mode used for board edits and runtime UI behavior.
         *
         * @param mode Target interaction mode.
         */
        public void SetInteractionMode(BoardInteractionMode mode)
        {
            InteractionMode = mode;
            if (mode != BoardInteractionMode.PuzzleCreation)
            {
                ClearCreationSolveAnalysis();
            }
        }

        /**
         * Clears the currently loaded board and resets transient runner state.
         */
        [ContextMenu("Unload Board")]
        public void UnloadBoard()
        {
            _board = null;
            LastAppliedRule = null;
            LastRuleResult = null;
            PreviewRuleResult = null;
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
                Description = "Reinitialise Candidates"
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
                    var newCandidates = BuildLegalCandidatesForCell(cell);

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
                result.Description = $"Reinitialise Candidates ({result.Changes.Count} cells updated)";

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
                            ValueOnlySet = ch.ValueOnlySet,
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

        /**
         * Determine whether reinitialising candidates would remove at least one
         * currently-marked candidate from any unsolved cell.
         *
         * @returns True when at least one existing candidate is currently illegal.
         */
        public bool CanReinitialiseCandidatesRemoveAny()
        {
            if (_board == null)
            {
                LoadBoardFromRows();
            }

            if (_board == null || _board.Cells == null)
            {
                return false;
            }

            for (int r = 0; r < _board.Size; r++)
            {
                for (int c = 0; c < _board.Size; c++)
                {
                    var cell = _board.Cells[r, c];
                    if (cell == null || cell.Value.HasValue || cell.Candidates == null || cell.Candidates.Count == 0)
                    {
                        continue;
                    }

                    var legalCandidates = BuildLegalCandidatesForCell(cell);
                    foreach (int existingCandidate in cell.Candidates)
                    {
                        if (!legalCandidates.Contains(existingCandidate))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /**
         * Build legal candidates for a single cell from board peer values.
         *
         * @param cell Target cell on the current board.
         * @returns Candidate set containing only currently legal digits.
         */
        private HashSet<int> BuildLegalCandidatesForCell(Cell cell)
        {
            var legalCandidates = new HashSet<int>();
            if (cell == null || _board == null)
            {
                return legalCandidates;
            }

            for (int v = 1; v <= _board.Size; v++)
            {
                legalCandidates.Add(v);
            }

            foreach (var peer in _board.GetPeers(cell))
            {
                if (peer != null && peer.Value.HasValue)
                {
                    legalCandidates.Remove(peer.Value.Value);
                }
            }

            return legalCandidates;
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
                    ValueOnlySet = ch.ValueOnlySet,
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
                    ValueOnlySet = ch.ValueOnlySet,
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
            var res = new RuleResult { Apply = true, Description = "Reinitialise Candidates (preview)" };
            for (int r = 0; r < _board.Size; r++)
            {
                for (int c = 0; c < _board.Size; c++)
                {
                    var cell = _board.Cells[r, c];
                    if (cell == null || cell.Value.HasValue || cell.Candidates == null || cell.Candidates.Count == 0)
                    {
                        continue;
                    }

                    var legalCandidates = BuildLegalCandidatesForCell(cell);
                    bool hasRemovableCandidate = false;
                    foreach (int candidate in cell.Candidates)
                    {
                        if (!legalCandidates.Contains(candidate))
                        {
                            hasRemovableCandidate = true;
                            break;
                        }
                    }

                    if (hasRemovableCandidate)
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
         * Load a traced generation snapshot into the active runner so play-mode UI can
         * inspect puzzle generation step-by-step.
         */
        public void LoadDebugBoardSnapshot(Board snapshot, RuleResult highlightResult)
        {
            if (snapshot == null)
            {
                return;
            }

            _board = PuzzleGenerator.CloneBoard(snapshot);
            CandidatesInitialised = true;
            LastAppliedRule = null;
            LastRuleResult = highlightResult;
            PreviewRuleResult = null;
            SetInteractionMode(BoardInteractionMode.PuzzleCreation);
            ValidateCurrentBoardState(skipFullSolveCheck: true);
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
            ManualEditExecutionResult execution;
            if (IsPuzzleCreationMode)
            {
                var cell = _board != null && _board.Cells != null && row >= 0 && column >= 0 && row < _board.Size && column < _board.Size
                    ? _board.Cells[row, column]
                    : null;

                if (cell == null)
                {
                    execution = new ManualEditExecutionResult
                    {
                        Applied = false,
                        Description = "Target cell is missing.",
                        RuleResult = new RuleResult
                        {
                            Apply = false,
                            Description = "Target cell is missing."
                        }
                    };
                }
                else if (cell.Value.HasValue && cell.Value.Value != value)
                {
                    execution = new ManualEditExecutionResult
                    {
                        Applied = false,
                        Description = "Clear the current value first, then choose a valid candidate.",
                        RuleResult = new RuleResult
                        {
                            Apply = false,
                            Description = "Clear the current value first, then choose a valid candidate."
                        }
                    };
                }
                else if (!cell.Value.HasValue && (cell.Candidates == null || !cell.Candidates.Contains(value)))
                {
                    execution = new ManualEditExecutionResult
                    {
                        Applied = false,
                        Description = $"Value {value} is not a valid candidate for r{row + 1}c{column + 1}.",
                        RuleResult = new RuleResult
                        {
                            Apply = false,
                            Description = $"Value {value} is not a valid candidate for r{row + 1}c{column + 1}."
                        }
                    };
                }
                else
                {
                    execution = ManualCellEditCore.ApplySetValueValueOnly(_board, row, column, value);
                }
            }
            else
            {
                if (AssistanceSettings.AutoCandidateOnSetValue)
                {
                    execution = ManualCellEditCore.ApplySetValue(_board, row, column, value);
                }
                else
                {
                    var peerCandidateSnapshot = CapturePeerCandidates(row, column);
                    execution = ManualCellEditCore.ApplySetValueValueOnly(_board, row, column, value);
                    if (execution != null && execution.Applied)
                    {
                        RestorePeerCandidates(peerCandidateSnapshot);
                    }
                }
            }
            LastAppliedRule = null;
            LastRuleResult = execution.RuleResult;
            PreviewRuleResult = null;
            FinalizeManualExecution(execution);
            return execution;
        }

        /**
         * Capture peer candidate sets for a target cell so value-only edits can
         * preserve peer markings when assistance auto-candidate is disabled.
         *
         * @param row Zero-based row index.
         * @param column Zero-based column index.
         * @returns Snapshot map keyed by peer cell reference.
         */
        private Dictionary<Cell, HashSet<int>> CapturePeerCandidates(int row, int column)
        {
            var snapshot = new Dictionary<Cell, HashSet<int>>();
            if (_board == null || _board.Cells == null)
            {
                return snapshot;
            }

            if (row < 0 || row >= _board.Size || column < 0 || column >= _board.Size)
            {
                return snapshot;
            }

            var cell = _board.Cells[row, column];
            if (cell == null)
            {
                return snapshot;
            }

            foreach (var peer in _board.GetPeers(cell))
            {
                if (peer == null)
                {
                    continue;
                }

                snapshot[peer] = peer.Candidates != null
                    ? new HashSet<int>(peer.Candidates)
                    : new HashSet<int>();
            }

            return snapshot;
        }

        /**
         * Restore peer candidate sets captured before a value-only placement.
         *
         * @param snapshot Snapshot map returned by CapturePeerCandidates.
         */
        private static void RestorePeerCandidates(Dictionary<Cell, HashSet<int>> snapshot)
        {
            if (snapshot == null || snapshot.Count == 0)
            {
                return;
            }

            foreach (var pair in snapshot)
            {
                var peer = pair.Key;
                if (peer == null)
                {
                    continue;
                }

                if (peer.Candidates == null)
                {
                    peer.Candidates = new HashSet<int>();
                }
                else
                {
                    peer.Candidates.Clear();
                }

                foreach (int candidate in pair.Value)
                {
                    peer.Candidates.Add(candidate);
                }
            }
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
            ManualEditExecutionResult execution;
            if (IsPuzzleCreationMode)
            {
                execution = new ManualEditExecutionResult
                {
                    Applied = false,
                    Description = "Puzzle creation mode only supports setting or clearing cell values.",
                    RuleResult = new RuleResult
                    {
                        Apply = false,
                        Description = "Puzzle creation mode only supports setting or clearing cell values."
                    }
                };
            }
            else
            {
                execution = ManualCellEditCore.ApplyAddCandidate(_board, row, column, candidate);
            }
            LastAppliedRule = null;
            LastRuleResult = execution.RuleResult;
            PreviewRuleResult = null;
            FinalizeManualExecution(execution);
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
            ManualEditExecutionResult execution;
            if (IsPuzzleCreationMode)
            {
                var cell = _board != null && _board.Cells != null && row >= 0 && column >= 0 && row < _board.Size && column < _board.Size
                    ? _board.Cells[row, column]
                    : null;
                if (cell == null || !cell.Value.HasValue)
                {
                    execution = new ManualEditExecutionResult
                    {
                        Applied = false,
                        Description = "Cell is already empty.",
                        RuleResult = new RuleResult
                        {
                            Apply = false,
                            Description = "Cell is already empty."
                        }
                    };
                }
                else
                {
                    execution = ManualCellEditCore.ApplySetValueValueOnly(_board, row, column, cell.Value.Value);
                }
            }
            else
            {
                execution = ManualCellEditCore.ApplyClearValue(_board, row, column);
            }
            LastAppliedRule = null;
            LastRuleResult = execution.RuleResult;
            PreviewRuleResult = null;
            FinalizeManualExecution(execution);
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
            ManualEditExecutionResult execution;
            if (IsPuzzleCreationMode)
            {
                execution = new ManualEditExecutionResult
                {
                    Applied = false,
                    Description = "Puzzle creation mode only supports setting or clearing cell values.",
                    RuleResult = new RuleResult
                    {
                        Apply = false,
                        Description = "Puzzle creation mode only supports setting or clearing cell values."
                    }
                };
            }
            else
            {
                execution = ManualCellEditCore.ApplyRemoveCandidate(_board, row, column, candidate);
            }
            LastAppliedRule = null;
            LastRuleResult = execution.RuleResult;
            PreviewRuleResult = null;
            FinalizeManualExecution(execution);
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
            ManualEditExecutionResult execution;
            if (IsPuzzleCreationMode)
            {
                execution = new ManualEditExecutionResult
                {
                    Applied = false,
                    Description = "Puzzle creation mode only supports setting or clearing cell values.",
                    RuleResult = new RuleResult
                    {
                        Apply = false,
                        Description = "Puzzle creation mode only supports setting or clearing cell values."
                    }
                };
            }
            else
            {
                execution = ManualCellEditCore.ApplyUnitCandidateAction(_board, row, column, candidate, addToUnsolvedCells);
            }
            LastAppliedRule = null;
            LastRuleResult = execution.RuleResult;
            PreviewRuleResult = null;
            FinalizeManualExecution(execution);
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
         * Run all post-edit UI refresh and creation-solve analysis hooks.
         *
         * @param execution Manual edit execution result.
         */
        private void FinalizeManualExecution(ManualEditExecutionResult execution)
        {
            RefreshRuntimeControlsAfterManualEdit(execution);
            if (execution != null && execution.Applied)
            {
                if (IsPuzzleCreationMode)
                {
                    int setDigitCount = CountSetDigits();

                    bool requestFullCandidateResync;
                    bool usedSparsePath = _creationSparseCandidateValidationRule.TryApply(
                        _board,
                        execution?.RuleResult,
                        setDigitCount,
                        SparseCreationDigitThreshold,
                        SparseCandidateResyncEveryEdits,
                        ref _sparseCreationEditCounter,
                        out requestFullCandidateResync);

                    if (usedSparsePath)
                    {
                        if (requestFullCandidateResync)
                        {
                            SyncCandidatesForCurrentBoard(skipFullSolveCheck: true, validateState: false);
                        }

                        if (setDigitCount >= CandidateSinglePropagationStartDigit)
                        {
                            _creationSingleCandidatePropagationRule.Apply(_board);
                        }

                        ValidateCurrentBoardState(skipFullSolveCheck: true);
                        ClearCreationSolveAnalysis();
                        LastCreationSolveStatusMessage = $"Lightweight candidate sync active until {SparseCreationDigitThreshold} digits are set.";
                        return;
                    }

                    _sparseCreationEditCounter = 0;
                    SyncCandidatesForCurrentBoard(skipFullSolveCheck: true, validateState: false);

                    if (setDigitCount >= CandidateSinglePropagationStartDigit)
                    {
                        _creationSingleCandidatePropagationRule.Apply(_board);
                    }

                    // Single optimized pass that solves once and validates in one go
                    RunOptimizedCreationAnalysis();
                }
                else
                {
                    _sparseCreationEditCounter = 0;
                    ValidateCurrentBoardState();
                }
            }
        }

        /**
         * Recompute all candidates from the current values on the board.
         * Solved cells have empty candidate sets. Unsolved cells keep only legal values.
         *
         * @param skipFullSolveCheck When true, validation skips latent conflict solving.
         * @param validateState When true, run board-state validation after candidate sync.
         */
        public void SyncCandidatesForCurrentBoard(bool skipFullSolveCheck = false, bool validateState = true)
        {
            if (_board == null || _board.Cells == null)
            {
                return;
            }

            for (int r = 0; r < _board.Size; r++)
            {
                for (int c = 0; c < _board.Size; c++)
                {
                    var cell = _board.Cells[r, c];
                    if (cell == null)
                    {
                        continue;
                    }

                    if (cell.Candidates == null)
                    {
                        cell.Candidates = new HashSet<int>();
                    }

                    cell.Candidates.Clear();
                    if (cell.Value.HasValue)
                    {
                        continue;
                    }

                    for (int v = 1; v <= _board.Size; v++)
                    {
                        cell.Candidates.Add(v);
                    }

                    foreach (var peer in _board.GetPeers(cell))
                    {
                        if (peer != null && peer.Value.HasValue)
                        {
                            cell.Candidates.Remove(peer.Value.Value);
                        }
                    }
                }
            }

            if (validateState)
            {
                ValidateCurrentBoardState(skipFullSolveCheck: skipFullSolveCheck);
            }
        }

        /**
         * Validate whether the current board state is still potentially solvable.
         * Detects duplicate-value conflicts, zero-candidate dead cells, and full-solver contradictions.
         * 
         * @param skipFullSolveCheck When true, skip the expensive full-solver check for latent conflicts.
         *                           Use this when you'll be solving separately anyway (optimization).
         */
        public void ValidateCurrentBoardState(bool skipFullSolveCheck = false)
        {
            _lastBoardStateConflictCells.Clear();

            if (_board == null || _board.Cells == null)
            {
                LastBoardStateIsPossible = true;
                LastBoardStateValidationMessage = "No active board.";
                return;
            }

            if (!_board.IsValid())
            {
                LastBoardStateIsPossible = false;
                LastBoardStateValidationMessage = "Invalid board: duplicate value exists in a row, column, or box.";

                var immediateConflicts = _board.FindConflicts(skipFullSolve: true);
                if (immediateConflicts != null)
                {
                    for (int i = 0; i < immediateConflicts.Count; i++)
                    {
                        var conflict = immediateConflicts[i];
                        if (conflict == null || conflict.Row < 0 || conflict.Column < 0)
                        {
                            continue;
                        }

                        _lastBoardStateConflictCells.Add(conflict);
                    }
                }

                return;
            }

            for (int r = 0; r < _board.Size; r++)
            {
                for (int c = 0; c < _board.Size; c++)
                {
                    var cell = _board.Cells[r, c];
                    if (cell == null || cell.Value.HasValue)
                    {
                        continue;
                    }

                    if (cell.Candidates == null || cell.Candidates.Count == 0)
                    {
                        LastBoardStateIsPossible = false;
                        LastBoardStateValidationMessage = $"Invalid board: row {r + 1}, column {c + 1} has no possible candidates.";
                        _lastBoardStateConflictCells.Add(new UsedCell { Row = r, Column = c, Candidate = null });
                        return;
                    }
                }
            }

            // Use skipFullSolve optimization: skip expensive solver check if we'll solve separately
            var conflicts = _board.FindConflicts(skipFullSolve: skipFullSolveCheck);
            if (conflicts != null && conflicts.Count > 0)
            {
                bool unsolvable = conflicts.Exists(c => c != null && (c.Row < 0 || c.Column < 0));
                LastBoardStateIsPossible = false;
                LastBoardStateValidationMessage = unsolvable
                    ? "Board is currently unsolvable with the full rule set."
                    : "Invalid board: conflicts were detected.";

                for (int i = 0; i < conflicts.Count; i++)
                {
                    var conflict = conflicts[i];
                    if (conflict == null || conflict.Row < 0 || conflict.Column < 0)
                    {
                        continue;
                    }

                    _lastBoardStateConflictCells.Add(conflict);
                }

                return;
            }

            LastBoardStateIsPossible = true;
            LastBoardStateValidationMessage = "Board state is currently possible.";
        }

        /**
         * Check if the current board has any values set.
         * Used to skip solving for completely empty boards (optimization).
         * 
         * @returns True if at least one cell has a value, false if board is completely empty.
         */
        private bool BoardHasAnyValues()
        {
            if (_board == null || _board.Cells == null)
            {
                return false;
            }

            for (int r = 0; r < _board.Size; r++)
            {
                for (int c = 0; c < _board.Size; c++)
                {
                    var cell = _board.Cells[r, c];
                    if (cell != null && cell.Value.HasValue)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /**
         * Count solved cells that currently contain a digit.
         *
         * @returns Number of cells with a non-null value.
         */
        private int CountSetDigits()
        {
            if (_board == null || _board.Cells == null)
            {
                return 0;
            }

            int count = 0;
            for (int r = 0; r < _board.Size; r++)
            {
                for (int c = 0; c < _board.Size; c++)
                {
                    var cell = _board.Cells[r, c];
                    if (cell != null && cell.Value.HasValue)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        /**
         * Handle a rule toggle change and conditionally re-run creation-mode analysis.
         *
         * Re-evaluation policy:
         * - Enabling any rule: always re-evaluate.
         * - Disabling a rule: re-evaluate only if that rule was used in the last solution path.
         *
         * @param ruleTypeName Type name of the toggled rule.
         * @param enabled New enabled state.
         */
        public void HandleRuleToggleChanged(string ruleTypeName, bool enabled)
        {
            if (!IsPuzzleCreationMode)
            {
                return;
            }

            bool shouldReevaluate = enabled;
            if (!enabled)
            {
                shouldReevaluate = !string.IsNullOrWhiteSpace(ruleTypeName) && _lastCreationSolveRuleNames.Contains(ruleTypeName);
            }

            if (!shouldReevaluate)
            {
                return;
            }

            if (_board == null || _board.Cells == null)
            {
                ClearCreationSolveAnalysis();
                return;
            }

            SyncCandidatesForCurrentBoard(skipFullSolveCheck: true, validateState: false);
            RunOptimizedCreationAnalysis();
        }

        /**
         * Return true when the current creation progress is inside the singles-only window.
         *
         * Window definition: from digit #9 inclusive for the next 6 digits.
         *
         * @param setDigitCount Count of currently solved/filled cells.
         * @returns True when singles-only solving should be used.
         */
        private static bool IsSinglesOnlyCreationWindow(int setDigitCount)
        {
            int start = SparseCreationDigitThreshold;
            int endExclusive = SparseCreationDigitThreshold + SinglesOnlyCreationDigitWindow;
            return setDigitCount >= start && setDigitCount < endExclusive;
        }

        /**
         * Restrict a registry to only Naked/Hidden Single rules while preserving their enabled state.
         *
         * @param registry Target registry clone to mutate.
         */
        private static void RestrictRegistryToSinglesOnly(RuleRegistry registry)
        {
            if (registry == null)
            {
                return;
            }

            var withStatus = registry.GetRulesWithStatus();
            for (int i = 0; i < withStatus.Count; i++)
            {
                var entry = withStatus[i];
                if (entry.rule == null)
                {
                    continue;
                }

                string typeName = entry.rule.GetType().Name;
                bool isSinglesRule = typeName == NakedSingleRuleTypeName || typeName == HiddenSingleRuleTypeName;
                if (!isSinglesRule)
                {
                    registry.SetEnabled(typeName, false);
                }
            }
        }

        /**
         * Determine whether at least one singles rule is enabled in the provided registry.
         *
         * @param registry Registry clone to inspect.
         * @returns True when Naked Single and/or Hidden Single is enabled.
         */
        private static bool HasAnyEnabledSinglesRule(RuleRegistry registry)
        {
            if (registry == null)
            {
                return false;
            }

            var withStatus = registry.GetRulesWithStatus();
            for (int i = 0; i < withStatus.Count; i++)
            {
                var entry = withStatus[i];
                if (entry.rule == null || !entry.enabled)
                {
                    continue;
                }

                string typeName = entry.rule.GetType().Name;
                if (typeName == NakedSingleRuleTypeName || typeName == HiddenSingleRuleTypeName)
                {
                    return true;
                }
            }

            return false;
        }

        /**
         * Optimized creation-mode analysis: solve ONCE instead of 3+ times.
         * 
         * Strategy:
         * 1. If board is empty, skip solving entirely
         * 2. Validate immediate conflicts and candidate availability (skip expensive full solve)
         * 3. Solve with enabled rules only - track which rules were used
         * 4. If that fails, continue solving with ALL rules - track additional rules needed
         * 5. Update candidates for unsolved cells based on final solver state
         * 6. All validation and status updates done in single pass
         */
        private void RunOptimizedCreationAnalysis()
        {
            if (!IsPuzzleCreationMode)
            {
                return;
            }

            if (_board == null || _board.Cells == null)
            {
                ClearCreationSolveAnalysis();
                ValidateCurrentBoardState(skipFullSolveCheck: false);
                return;
            }

            // If board is completely empty, skip solving - nothing to validate or solve
            if (!BoardHasAnyValues())
            {
                ClearCreationSolveAnalysis();
                LastBoardStateIsPossible = true;
                LastBoardStateValidationMessage = "Board is empty (no cells filled yet).";
                return;
            }

            // First validate immediate conflicts and candidate availability
            // Skip the expensive full-solve check since we'll solve right after anyway
            ValidateCurrentBoardState(skipFullSolveCheck: true);
            if (!LastBoardStateIsPossible)
            {
                // Board has immediate conflicts or zero-candidate cells - don't attempt solving
                ClearCreationSolveAnalysis();
                return;
            }

            int setDigitCount = CountSetDigits();
            bool singlesOnlyWindow = IsSinglesOnlyCreationWindow(setDigitCount);

            if (singlesOnlyWindow)
            {
                var boardCopyForSingles = CloneBoardForSolve(_board);
                var registryCopyForSingles = CloneRegistry(includeAllRules: false);
                RestrictRegistryToSinglesOnly(registryCopyForSingles);

                _lastCreationSolveRuleNames.Clear();

                if (!HasAnyEnabledSinglesRule(registryCopyForSingles))
                {
                    LastCreationSolveFoundSolution = false;
                    LastCreationSolveFoundWithSelectedRules = false;
                    LastCreationSolveStatusMessage = "Singles-only window active: enable Naked Single and/or Hidden Single to evaluate progress.";
                    return;
                }

                var singlesEngine = new SolverEngine(registryCopyForSingles);
                bool singlesSolved = singlesEngine.Solve(boardCopyForSingles, out var singlesSteps);
                var singlesRuleNames = ExtractRuleNamesFromSteps(singlesSteps);
                AppendDistinctRules(_lastCreationSolveRuleNames, singlesRuleNames);

                UpdateCandidatesFromSolvedBoard(boardCopyForSingles);

                LastCreationSolveFoundSolution = singlesSolved;
                LastCreationSolveFoundWithSelectedRules = singlesSolved;
                LastCreationSolveStatusMessage = singlesSolved
                    ? "Singles-only window: solution found using enabled singles rules."
                    : "Singles-only window: no complete solution yet with enabled singles rules.";
                return;
            }

            // Attempt to solve with enabled rules first, tracking which rules get used
            var boardCopyForEnabledRules = CloneBoardForSolve(_board);
            var registryCopyForEnabledRules = CloneRegistry(includeAllRules: false);
            var engineForEnabledRules = new SolverEngine(registryCopyForEnabledRules);
            bool solvedWithEnabled = engineForEnabledRules.Solve(boardCopyForEnabledRules, out var enabledSteps);

            var enabledRuleNames = ExtractRuleNamesFromSteps(enabledSteps);
            var allRuleNames = new List<string>();

            Board finalSolvedBoard = boardCopyForEnabledRules;
            bool solvedWithAll = solvedWithEnabled;

            // If enabled rules didn't fully solve, continue from that partial state with ALL rules.
            // This avoids restarting from the original puzzle and reduces fallback cost.
            if (!solvedWithEnabled)
            {
                var boardCopyForAllRules = boardCopyForEnabledRules;
                var registryCopyForAllRules = CloneRegistry(includeAllRules: true);
                var engineForAllRules = new SolverEngine(registryCopyForAllRules);
                solvedWithAll = engineForAllRules.Solve(boardCopyForAllRules, out var allSteps);

                // Track which additional rules were needed beyond the enabled ones
                var allStepRuleNames = ExtractRuleNamesFromSteps(allSteps);
                foreach (var ruleName in allStepRuleNames)
                {
                    if (!enabledRuleNames.Contains(ruleName))
                    {
                        allRuleNames.Add(ruleName);
                    }
                }

                finalSolvedBoard = boardCopyForAllRules;
            }

            // Update candidates for unsolved cells based on the final solve attempt
            UpdateCandidatesFromSolvedBoard(finalSolvedBoard);

            // Update creation-mode analysis results
            _lastCreationSolveRuleNames.Clear();
            AppendDistinctRules(_lastCreationSolveRuleNames, enabledRuleNames);
            AppendDistinctRules(_lastCreationSolveRuleNames, allRuleNames);

            LastCreationSolveFoundSolution = solvedWithEnabled || solvedWithAll;
            LastCreationSolveFoundWithSelectedRules = solvedWithEnabled;
            
            if (solvedWithEnabled)
            {
                LastCreationSolveStatusMessage = "Solution found using the currently enabled rules.";
            }
            else if (solvedWithAll)
            {
                LastCreationSolveStatusMessage = "Solution found only when all rules were allowed.";
            }
            else
            {
                LastCreationSolveStatusMessage = "No complete solution found yet with enabled rules or with all rules.";
            }
        }

        /**
         * Extract unique rule names from solver steps in order of first appearance.
         * 
         * @param steps Solver steps from a solve attempt (tuples of rule and result).
         * @returns Ordered unique list of rule type names.
         */
        private List<string> ExtractRuleNamesFromSteps(System.Collections.Generic.List<(ISudokuRule rule, RuleResult result)> steps)
        {
            var ruleNames = new List<string>();
            if (steps == null)
            {
                return ruleNames;
            }

            foreach (var (rule, result) in steps)
            {
                if (rule == null)
                {
                    continue;
                }

                string name = rule.GetType().Name;
                if (!ruleNames.Contains(name))
                {
                    ruleNames.Add(name);
                }
            }

            return ruleNames;
        }

        /**
         * Update unsolved-cell candidates using a solved board state.
         * This method never changes board values; it only updates candidate sets 
         * for cells that are currently empty on the active board.
         * 
         * @param solvedBoard Board state from a solve attempt (may be complete or partial).
         */
        private void UpdateCandidatesFromSolvedBoard(Board solvedBoard)
        {
            if (_board == null || _board.Cells == null || solvedBoard == null || solvedBoard.Cells == null)
            {
                return;
            }

            for (int r = 0; r < _board.Size; r++)
            {
                for (int c = 0; c < _board.Size; c++)
                {
                    var targetCell = _board.Cells[r, c];
                    if (targetCell == null || targetCell.Value.HasValue)
                    {
                        continue;
                    }

                    if (targetCell.Candidates == null)
                    {
                        targetCell.Candidates = new HashSet<int>();
                    }

                    targetCell.Candidates.Clear();

                    var solvedCell = solvedBoard.Cells[r, c];
                    if (solvedCell != null && solvedCell.Value.HasValue)
                    {
                        int solvedValue = solvedCell.Value.Value;
                        if (solvedValue >= 1 && solvedValue <= _board.Size)
                        {
                            targetCell.Candidates.Add(solvedValue);
                        }
                        continue;
                    }

                    if (solvedCell != null && solvedCell.Candidates != null && solvedCell.Candidates.Count > 0)
                    {
                        foreach (int candidate in solvedCell.Candidates)
                        {
                            if (candidate >= 1 && candidate <= _board.Size)
                            {
                                targetCell.Candidates.Add(candidate);
                            }
                        }
                        continue;
                    }

                    // Fall back to legal peer-based candidates if solver didn't leave candidate info
                    for (int v = 1; v <= _board.Size; v++)
                    {
                        targetCell.Candidates.Add(v);
                    }

                    foreach (var peer in _board.GetPeers(targetCell))
                    {
                        if (peer != null && peer.Value.HasValue)
                        {
                            targetCell.Candidates.Remove(peer.Value.Value);
                        }
                    }
                }
            }
        }

        /**
         * Enable every rule currently registered in this runner's registry.
         */
        private void EnableAllRegisteredRules()
        {
            if (Registry == null)
            {
                return;
            }

            var withStatus = Registry.GetRulesWithStatus();
            for (int i = 0; i < withStatus.Count; i++)
            {
                var entry = withStatus[i];
                if (entry.rule == null)
                {
                    continue;
                }

                Registry.SetEnabled(entry.rule.GetType().Name, true);
            }
        }

        /**
         * Clear cached puzzle-creation solver analysis state.
         */
        private void ClearCreationSolveAnalysis()
        {
            LastCreationSolveFoundSolution = false;
            LastCreationSolveFoundWithSelectedRules = false;
            LastCreationSolveStatusMessage = string.Empty;
            _lastCreationSolveRuleNames.Clear();
        }

        /**
         * Attempt to solve a cloned board, optionally forcing all rules enabled.
         *
         * @param includeAllRules When true, all registered rule types are enabled for this solve attempt.
         * @param ruleNames Ordered unique rule names used in the solve attempt.
         * @returns True if the cloned board reached a full solution.
         */
        private bool TrySolveBoardCopy(bool includeAllRules, out List<string> ruleNames)
        {
            ruleNames = new List<string>();
            if (_board == null || _board.Cells == null)
            {
                return false;
            }

            var boardCopy = CloneBoardForSolve(_board);
            var registryCopy = CloneRegistry(includeAllRules);
            var engine = new SolverEngine(registryCopy);
            bool solved = engine.Solve(boardCopy, out var steps);

            if (steps != null)
            {
                for (int i = 0; i < steps.Count; i++)
                {
                    var rule = steps[i].rule;
                    if (rule == null)
                    {
                        continue;
                    }

                    string name = rule.GetType().Name;
                    if (!ruleNames.Contains(name))
                    {
                        ruleNames.Add(name);
                    }
                }
            }

            return solved;
        }

        /**
         * Create a deep-copy board containing only values/givens and empty candidate sets.
         *
         * @param source Source board.
         * @returns Cloned board for analysis.
         */
        private static Board CloneBoardForSolve(Board source)
        {
            var copy = new Board(source.Size, source.BoxWidth, source.BoxHeight);
            for (int r = 0; r < source.Size; r++)
            {
                for (int c = 0; c < source.Size; c++)
                {
                    var sourceCell = source.Cells[r, c];
                    var cloned = new Cell(r, c, sourceCell?.Value, sourceCell != null && sourceCell.IsGiven);
                    cloned.Candidates.Clear();
                    copy.Cells[r, c] = cloned;
                }
            }

            return copy;
        }

        /**
         * Clone the current rule registry into a new instance, preserving enable state when requested.
         *
         * @param includeAllRules When true, all cloned rules stay enabled.
         * @returns Cloned rule registry.
         */
        private RuleRegistry CloneRegistry(bool includeAllRules)
        {
            var clone = new RuleRegistry();
            if (Registry == null)
            {
                clone.RegisterMinimal();
                clone.RegisterMedium();
                clone.RegisterAdvanced();
                return clone;
            }

            foreach (var rule in Registry.Rules)
            {
                if (rule == null)
                {
                    continue;
                }

                if (System.Activator.CreateInstance(rule.GetType()) is ISudokuRule recreated)
                {
                    clone.Register(recreated);
                    if (!includeAllRules && !Registry.IsEnabled(rule))
                    {
                        clone.SetEnabled(recreated.GetType().Name, false);
                    }
                }
            }

            return clone;
        }

        /**
         * Append rule names to a destination list while preserving insertion order and uniqueness.
         *
         * @param destination Target list.
         * @param source Source names to append.
         */
        private static void AppendDistinctRules(List<string> destination, IEnumerable<string> source)
        {
            if (destination == null || source == null)
            {
                return;
            }

            foreach (var ruleName in source)
            {
                if (string.IsNullOrWhiteSpace(ruleName))
                {
                    continue;
                }

                if (!destination.Contains(ruleName))
                {
                    destination.Add(ruleName);
                }
            }
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
                        ValueOnlySet = ch.ValueOnlySet,
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
            ValidateCurrentBoardState();
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
