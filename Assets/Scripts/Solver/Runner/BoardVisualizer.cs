using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Sudoku.Models;
using Cell = Sudoku.Models.Cell;
using Board = Sudoku.Models.Board;
using Sudoku.Solver.Rules;
using Sudoku.UI.Config;
using Sudoku.UI.Menus;
using static System.StringComparison;

namespace Sudoku.Solver
{
    /**
     * Simple Game-view visualizer for a `Board` instance provided by a `SolverRunner`.
     * Draws the grid and cell values/candidates using IMGUI in `OnGUI`.
     */
    [ExecuteInEditMode]
    public class BoardVisualizer : MonoBehaviour
    {
        public enum DirectionalLinkToolMode
        {
            Off = 0,
            Strong = 1,
            Weak = 2,
        }

        public enum NumericRadialActionMode
        {
            SetValue = 0,
            RemoveCandidate = 1,
            AddCandidate = 2,
            ModifierDriven = 3
        }

        public SolverRunner Runner;

        [Tooltip("Pixel size of each cell")]
        public int CellSize = 48;

        [Tooltip("If true, scale cell size so the board fills the screen height")]
        public bool FitToScreenHeight = true;

        [Tooltip("Minimum pixel size for each cell when fitting to screen height")]
        public int MinCellSize = 16;

        [Tooltip("Show candidate digits inside empty cells")]
        public bool ShowCandidates = true;

        [Tooltip("Top-left screen position for the rendered board")]
        public Vector2 Offset = new Vector2(20, 20);

        [Tooltip("Seconds to hold before the board requests a radial open intent")]
        public float HoldOpenThresholdSeconds = 0.35f;

        [Tooltip("Write verbose hold/radial interaction diagnostics to the Console")]
        public bool EnableHoldDebugLogs = true;

        [Tooltip("Default operation for digit segments. In ModifierDriven mode: Shift=RemoveCandidate, Ctrl/Cmd=AddCandidate, otherwise SetValue.")]
        public NumericRadialActionMode DigitActionMode = NumericRadialActionMode.ModifierDriven;

        [Tooltip("Solve-mode directional link tool state.")]
        public DirectionalLinkToolMode LinkToolMode = DirectionalLinkToolMode.Off;

        public enum HoldPhase
        {
            Idle = 0,
            Holding = 1,
            Armed = 2
        }

        private GUIStyle _centerStyle;
        private GUIStyle _givenCenterStyle;
        private GUIStyle _candidateStyle;
        private int _lastComputedCellSize = -1;
        private float _holdStartedAt = -1f;
        private Vector2 _holdStartedPointerPosition;
        private bool _radialOpenIntentRequested;
        private RadialMenuRuntime _radialMenu;
        public HoldPhase CurrentHoldPhase { get; private set; } = HoldPhase.Idle;
        public int SelectedHoldRow { get; private set; } = -1;
        public int SelectedHoldColumn { get; private set; } = -1;
        public RadialMenuSelection LastRadialSelection { get; private set; }
        public string LastRadialOutcomeMessage { get; private set; }
        public RadialMenuRuntime RadialMenu => _radialMenu;
        public bool RadialOpenIntentRequested => _radialOpenIntentRequested;
        // Track the last seen RuleResult so we can render removed candidates
        // in red until a new rule result is produced.
        private Sudoku.Solver.Rules.RuleResult _lastSeenRuleResult;
        // last result logged to avoid spamming logs every frame
        private Sudoku.Solver.Rules.RuleResult _lastLoggedResultToShow;
        private bool _lastLoggedPreviewActive = false;
        private DirectionalLinkEndpoint _pendingDirectionalLinkStart;
        private Texture2D _linkCursorStrongThinTexture;
        private Texture2D _linkCursorStrongBoldTexture;
        private Texture2D _linkCursorWeakThinTexture;
        private Texture2D _linkCursorWeakBoldTexture;
        private CursorVisualState _appliedCursorVisualState = CursorVisualState.Default;
        private DirectionalLinkToolMode _appliedCursorToolMode = DirectionalLinkToolMode.Off;

        private enum CursorVisualState
        {
            Default = 0,
            LinkReady = 1,
            LinkPending = 2,
        }

        /**
         * Current active directional-link tool mode.
         */
        public DirectionalLinkToolMode CurrentLinkToolMode => LinkToolMode;

        /**
         * True when link mode is active and waiting for end endpoint selection.
         */
        public bool IsAwaitingDirectionalLinkEnd => _pendingDirectionalLinkStart != null;

        private void EnsureStyles(int cellSize)
        {
            if (_centerStyle != null && _lastComputedCellSize == cellSize) return;
            _lastComputedCellSize = cellSize;
            var baseLabel = GUI.skin != null ? GUI.skin.label : new GUIStyle();
            _centerStyle = new GUIStyle(baseLabel) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.Max(12, cellSize / 2) };
            _givenCenterStyle = new GUIStyle(_centerStyle) { fontStyle = FontStyle.Bold };
            _candidateStyle = new GUIStyle(baseLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.Max(6, cellSize / 4),
                padding = new RectOffset(0, 0, 0, 0)
            };
        }

        /**
         * Return the computed cell size used at runtime (matches logic in OnGUI).
         */
        public int GetComputedCellSize()
        {
            int size = Runner != null && Runner.CurrentBoard != null ? Runner.CurrentBoard.Size : 9;
            int cellSize = CellSize;
            if (FitToScreenHeight && size > 0)
            {
                float availableHeight = Mathf.Max(32f, Screen.height - Offset.y - 20f);
                cellSize = Mathf.Max(MinCellSize, Mathf.FloorToInt(availableHeight / size));
            }
            return cellSize;
        }

        /**
         * Return the board rectangle in GUI coordinates using the same geometry as OnGUI.
         *
         * @param rect Computed board rectangle when the board geometry is available.
         * @returns True when the board geometry could be resolved.
         */
        public bool TryGetBoardRect(out Rect rect)
        {
            rect = default;
            if (Runner == null || Runner.CurrentBoard == null || Runner.CurrentBoard.Cells == null) return false;

            int size = Runner.CurrentBoard.Size;
            if (size <= 0) return false;

            int cellSize = GetComputedCellSize();
            if (cellSize <= 0) return false;

            rect = new Rect(Offset.x, Offset.y, size * cellSize, size * cellSize);
            return true;
        }

        /**
         * Map a GUI pointer position to a board cell using the board's current geometry.
         *
         * @param screenPosition Pointer position in GUI coordinates.
         * @param row Resolved zero-based row index.
         * @param column Resolved zero-based column index.
         * @returns True when the pointer is inside the rendered board.
         */
        public bool TryGetCellFromScreenPosition(Vector2 screenPosition, out int row, out int column)
        {
            row = -1;
            column = -1;

            if (!TryGetBoardRect(out var boardRect)) return false;
            if (!boardRect.Contains(screenPosition)) return false;

            int cellSize = GetComputedCellSize();
            if (cellSize <= 0) return false;

            column = Mathf.FloorToInt((screenPosition.x - boardRect.x) / cellSize);
            row = Mathf.FloorToInt((screenPosition.y - boardRect.y) / cellSize);

            if (Runner.CurrentBoard == null) return false;
            if (row < 0 || column < 0 || row >= Runner.CurrentBoard.Size || column >= Runner.CurrentBoard.Size)
            {
                row = -1;
                column = -1;
                return false;
            }

            return true;
        }

        /**
         * Compute the center of a board cell in GUI coordinates.
         *
         * @param row Zero-based row index.
         * @param column Zero-based column index.
         * @param center Cell center in GUI coordinates.
         * @returns True when the board geometry is available and the cell is valid.
         */
        public bool TryGetCellCenter(int row, int column, out Vector2 center)
        {
            center = default;
            if (!TryGetBoardRect(out var boardRect)) return false;
            if (Runner == null || Runner.CurrentBoard == null) return false;

            int cellSize = GetComputedCellSize();
            if (cellSize <= 0) return false;
            if (row < 0 || column < 0 || row >= Runner.CurrentBoard.Size || column >= Runner.CurrentBoard.Size) return false;

            center = new Vector2(boardRect.x + (column + 0.5f) * cellSize, boardRect.y + (row + 0.5f) * cellSize);
            return true;
        }

        /**
         * Begin a new hold interaction for a specific cell.
         *
         * @param row Zero-based row index.
         * @param column Zero-based column index.
         * @param pointerPosition Pointer position where the hold started.
         */
        public void BeginCellHold(int row, int column, Vector2 pointerPosition)
        {
            if (!IsBoardInteractionAvailable())
            {
                LogHoldDebug($"BeginCellHold rejected: board unavailable at {pointerPosition}");
                CancelCellHold("board unavailable");
                return;
            }

            SelectedHoldRow = row;
            SelectedHoldColumn = column;
            _holdStartedPointerPosition = pointerPosition;
            _holdStartedAt = Time.realtimeSinceStartup;
            CurrentHoldPhase = HoldPhase.Holding;
            _radialOpenIntentRequested = false;
            LastRadialSelection = null;
            LastRadialOutcomeMessage = string.Empty;
            LogHoldDebug($"BeginCellHold row={row} col={column} pointer={pointerPosition} threshold={HoldOpenThresholdSeconds:0.000}s");
        }

        /**
         * Advance an active hold interaction and arm the radial open intent when the threshold is reached.
         *
         * @param pointerPosition Current pointer position in GUI coordinates.
         */
        public void UpdateCellHold(Vector2 pointerPosition)
        {
            if (CurrentHoldPhase == HoldPhase.Idle) return;
            if (!IsBoardInteractionAvailable())
            {
                LogHoldDebug("UpdateCellHold cancelled: board unavailable");
                CancelCellHold("board unavailable");
                return;
            }

            _holdStartedPointerPosition = pointerPosition;

            if (CurrentHoldPhase == HoldPhase.Armed)
            {
                if (_radialMenu != null && _radialMenu.IsOpen)
                {
                    _radialMenu.UpdatePointer(pointerPosition);
                }
                return;
            }

            if (!TryGetCellFromScreenPosition(pointerPosition, out int row, out int column) || row != SelectedHoldRow || column != SelectedHoldColumn)
            {
                LogHoldDebug($"UpdateCellHold cancelled: pointer left cell. pointer={pointerPosition} selected=r{SelectedHoldRow}c{SelectedHoldColumn}");
                CancelCellHold("pointer left cell");
                return;
            }

            if (CurrentHoldPhase == HoldPhase.Holding && _holdStartedAt >= 0f && Time.realtimeSinceStartup - _holdStartedAt >= HoldOpenThresholdSeconds)
            {
                CurrentHoldPhase = HoldPhase.Armed;
                _radialOpenIntentRequested = true;
                LogHoldDebug($"Hold armed after {(Time.realtimeSinceStartup - _holdStartedAt):0.000}s at r{SelectedHoldRow}c{SelectedHoldColumn}");
                OpenRadialMenu();
            }
        }

        /**
         * End the current hold interaction without preserving intent.
         */
        public void CancelCellHold(string reason = null)
        {
            if (_radialMenu != null)
            {
                _radialMenu.Close();
            }
            if (!string.IsNullOrEmpty(reason)) LogHoldDebug($"CancelCellHold: {reason}");
            CurrentHoldPhase = HoldPhase.Idle;
            SelectedHoldRow = -1;
            SelectedHoldColumn = -1;
            LastRadialSelection = null;
            LastRadialOutcomeMessage = string.Empty;
            _holdStartedAt = -1f;
            _holdStartedPointerPosition = default;
            _radialOpenIntentRequested = false;
        }

        /**
         * Clear the current hold interaction after the pointer is released.
         * If the radial open intent has already been armed, the intent flag is preserved
         * until an outer controller consumes it.
         */
        public void ReleaseCellHold(string reason = null)
        {
            if (_radialMenu != null)
            {
                _radialMenu.Close();
            }
            if (!string.IsNullOrEmpty(reason)) LogHoldDebug($"ReleaseCellHold: {reason}");
            if (CurrentHoldPhase == HoldPhase.Armed)
            {
                CurrentHoldPhase = HoldPhase.Idle;
                SelectedHoldRow = -1;
                SelectedHoldColumn = -1;
                _holdStartedAt = -1f;
                _holdStartedPointerPosition = default;
                return;
            }

            CancelCellHold();
        }

        /**
         * Consume a pending radial-open intent, clearing the armed state.
         *
         * @returns True if there was an intent to consume.
         */
        public bool ConsumeRadialOpenIntent()
        {
            if (!_radialOpenIntentRequested) return false;
            _radialOpenIntentRequested = false;
            return true;
        }

        /**
         * Returns whether the board has enough state to accept hold interactions.
         *
         * @returns True when the board and its backing cells are ready.
         */
        public bool IsBoardInteractionAvailable()
        {
            return Runner != null && Runner.CurrentBoard != null && Runner.CurrentBoard.Cells != null && Runner.CurrentBoard.Size > 0;
        }

        /**
         * Determine whether board edits should be restricted to value-only operations.
         *
         * @returns True when the runner is in puzzle creation mode.
         */
        private bool IsValueOnlyEditMode()
        {
            return Runner != null && Runner.IsPuzzleCreationMode;
        }

        /**
         * Set the directional-link tool mode and refresh cursor/selection stage.
         *
         * @param mode Requested directional-link mode.
         */
        public void SetDirectionalLinkToolMode(DirectionalLinkToolMode mode)
        {
            LinkToolMode = mode;
            if (mode == DirectionalLinkToolMode.Off)
            {
                _pendingDirectionalLinkStart = null;
            }

            ApplyCursorForLinkToolState();
        }

        /**
         * Force the directional-link tool off and clear any pending endpoint.
         */
        public void DisableDirectionalLinkTool()
        {
            SetDirectionalLinkToolMode(DirectionalLinkToolMode.Off);
        }

        private bool IsDirectionalLinkModeActive()
        {
            return LinkToolMode == DirectionalLinkToolMode.Strong || LinkToolMode == DirectionalLinkToolMode.Weak;
        }

        private DirectionalLinkKind ResolveCurrentDirectionalLinkKind()
        {
            return LinkToolMode == DirectionalLinkToolMode.Weak
                ? DirectionalLinkKind.Weak
                : DirectionalLinkKind.Strong;
        }

        private void RefreshDirectionalLinkRuntimeState()
        {
            if (Runner != null && Runner.IsPuzzleCreationMode && LinkToolMode != DirectionalLinkToolMode.Off)
            {
                LinkToolMode = DirectionalLinkToolMode.Off;
                _pendingDirectionalLinkStart = null;
            }

            ApplyCursorForLinkToolState();
        }

        private void ApplyCursorForLinkToolState()
        {
            CursorVisualState targetState;
            if (!Application.isPlaying || !IsDirectionalLinkModeActive())
            {
                targetState = CursorVisualState.Default;
            }
            else
            {
                targetState = _pendingDirectionalLinkStart == null
                    ? CursorVisualState.LinkReady
                    : CursorVisualState.LinkPending;
            }

            if (targetState == _appliedCursorVisualState)
            {
                if (targetState == CursorVisualState.Default || _appliedCursorToolMode == LinkToolMode)
                {
                    return;
                }
            }

            if (targetState == CursorVisualState.Default)
            {
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                _appliedCursorVisualState = targetState;
                _appliedCursorToolMode = DirectionalLinkToolMode.Off;
                return;
            }

            EnsureLinkCursorTextures();
            bool isBold = targetState == CursorVisualState.LinkPending;
            var texture = LinkToolMode == DirectionalLinkToolMode.Weak
                ? (isBold ? _linkCursorWeakBoldTexture : _linkCursorWeakThinTexture)
                : (isBold ? _linkCursorStrongBoldTexture : _linkCursorStrongThinTexture);
            Cursor.SetCursor(texture, new Vector2(2f, 2f), CursorMode.Auto);
            _appliedCursorVisualState = targetState;
            _appliedCursorToolMode = LinkToolMode;
        }

        private void EnsureLinkCursorTextures()
        {
            if (_linkCursorStrongThinTexture == null)
            {
                _linkCursorStrongThinTexture = BuildLinkCursorTexture(new Color(0.92f, 0.14f, 0.14f, 1f), 1);
            }

            if (_linkCursorStrongBoldTexture == null)
            {
                _linkCursorStrongBoldTexture = BuildLinkCursorTexture(new Color(0.92f, 0.14f, 0.14f, 1f), 3);
            }

            if (_linkCursorWeakThinTexture == null)
            {
                _linkCursorWeakThinTexture = BuildLinkCursorTexture(new Color(0.14f, 0.82f, 0.28f, 1f), 1);
            }

            if (_linkCursorWeakBoldTexture == null)
            {
                _linkCursorWeakBoldTexture = BuildLinkCursorTexture(new Color(0.14f, 0.82f, 0.28f, 1f), 3);
            }
        }

        private static Texture2D BuildLinkCursorTexture(Color strokeColor, int thickness)
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color(0f, 0f, 0f, 0f);
            }

            // Arrow cursor body pointing to the top-left.
            DrawCursorLine(pixels, size, 21, 4, 4, 21, strokeColor, thickness);
            // Arrowhead at the top-left tip.
            DrawCursorLine(pixels, size, 4, 21, 12, 21, strokeColor, thickness);
            DrawCursorLine(pixels, size, 4, 21, 4, 13, strokeColor, thickness);

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private static void DrawCursorLine(Color[] pixels, int size, int x0, int y0, int x1, int y1, Color color, int thickness)
        {
            int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
            if (steps <= 0)
            {
                return;
            }

            int half = Mathf.Max(0, thickness / 2);

            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t));
                int y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
                for (int oy = -half; oy <= half; oy++)
                {
                    int py = y + oy;
                    if (py < 0 || py >= size) continue;
                    for (int ox = -half; ox <= half; ox++)
                    {
                        int px = x + ox;
                        if (px < 0 || px >= size) continue;
                        pixels[py * size + px] = color;
                    }
                }
            }
        }

        private void LogHoldDebug(string message)
        {
            if (!EnableHoldDebugLogs) return;
            Debug.Log($"[BoardVisualizer Hold] {message}", this);
        }

        private RadialMenuRuntime EnsureRadialMenu()
        {
            if (_radialMenu != null) return _radialMenu;

            _radialMenu = Object.FindAnyObjectByType<RadialMenuRuntime>();
            if (_radialMenu != null) return _radialMenu;

            var canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasGO.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            var host = new GameObject("RadialMenuRuntimeHost", typeof(RectTransform));
            host.transform.SetParent(canvas.transform, false);
            _radialMenu = host.AddComponent<RadialMenuRuntime>();
            return _radialMenu;
        }

        private void OpenRadialMenu()
        {
            if (!TryGetCellCenter(SelectedHoldRow, SelectedHoldColumn, out var center)) return;

            var menu = EnsureRadialMenu();
            menu.LinkSelectionMode = IsDirectionalLinkModeActive();
            if (Runner != null && Runner.CurrentBoard != null &&
                SelectedHoldRow >= 0 && SelectedHoldColumn >= 0 &&
                SelectedHoldRow < Runner.CurrentBoard.Size && SelectedHoldColumn < Runner.CurrentBoard.Size)
            {
                var cell = Runner.CurrentBoard.Cells[SelectedHoldRow, SelectedHoldColumn];
                menu.ValueOnlyMode = IsValueOnlyEditMode();
                menu.SetCellContext(cell?.Value, cell?.Candidates, cell != null && cell.IsGiven);
                menu.SetCellColourContext(cell != null ? cell.DigitColors : null);
            }
            LogHoldDebug($"OpenRadialMenu at {center} using label='No Action' menu={(menu != null ? menu.name : "(null)")}");
            menu.Open(center, "No Action");
            menu.UpdatePointer(center);
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            RefreshDirectionalLinkRuntimeState();
            PollHoldTimer();
        }

        private void OnDisable()
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            _appliedCursorVisualState = CursorVisualState.Default;
        }

        private string GetSmartActionLabel()
        {
            if (Runner == null || Runner.CurrentBoard == null) return "Smart";

            try
            {
                var resolution = ManualCellEditCore.ResolveSmartAction(Runner.CurrentBoard, SelectedHoldRow, SelectedHoldColumn);
                if (resolution != null)
                {
                    if (!string.IsNullOrWhiteSpace(resolution.Label)) return resolution.Label;
                    if (!string.IsNullOrWhiteSpace(resolution.Description)) return resolution.Description;
                }
            }
            catch
            {
            }

            return "Smart";
        }

        private void OnValidate()
        {
            _centerStyle = null; // rebuild styles on inspector changes
            _candidateStyle = null;
            _lastComputedCellSize = -1;
        }

        private void OnGUI()
        {
            try
            {
                if (Runner == null || Runner.CurrentBoard == null) return;
                Board board = Runner.CurrentBoard;
                if (board.Cells == null) return;
                int size = board.Size;

                int cellSize = CellSize;
                if (FitToScreenHeight && size > 0)
                {
                    float availableHeight = Mathf.Max(32f, Screen.height - Offset.y - 20f);
                    cellSize = Mathf.Max(MinCellSize, Mathf.FloorToInt(availableHeight / size));
                }

                EnsureStyles(cellSize);

                // Detect a new LastRuleResult and store it only when a rule actually
                // applied. This prevents non-applying status messages from clearing
                // the stored result and therefore preserves removed-candidate
                // highlights until the next real rule run.
                // When LastRuleResult is explicitly cleared (set to null), also clear
                // _lastSeenRuleResult so stale highlights don't persist (e.g. after
                // jumping to Initial State).
                if (Runner != null && Runner.LastRuleResult != null && Runner.LastRuleResult.Apply && Runner.LastRuleResult != _lastSeenRuleResult)
                {
                    _lastSeenRuleResult = Runner.LastRuleResult;
                }
                else if (Runner != null && Runner.LastRuleResult == null)
                {
                    _lastSeenRuleResult = null;
                }

                // Choose which RuleResult to use for display: prefer a hovered
                // preview (only if it actually applies) so mouseover highlights
                // show instantly, then the last applied rule (stored in
                // `_lastSeenRuleResult`), then the Runner.LastRuleResult fallback.
                var activePreview = Runner.PreviewRuleResult != null && Runner.PreviewRuleResult.Apply;
                var resultToShow = activePreview ? Runner.PreviewRuleResult : (_lastSeenRuleResult ?? Runner.LastRuleResult);
                var validationConflictCells = Runner.LastBoardStateConflictCells;
                bool colouringChainEvidenceActive = resultToShow != null &&
                                                   resultToShow.UsedCells != null &&
                                                   resultToShow.UsedCells.Exists(u =>
                                                       !string.IsNullOrWhiteSpace(u.HighlightTag) &&
                                                       (string.Equals(u.HighlightTag, "TargetA", OrdinalIgnoreCase) ||
                                                        string.Equals(u.HighlightTag, "TargetB", OrdinalIgnoreCase)));

                // Log when the display source changes or preview toggles (throttled)
                if (resultToShow != _lastLoggedResultToShow || activePreview != _lastLoggedPreviewActive)
                {
                    string src = activePreview ? "PreviewRuleResult" : ( _lastSeenRuleResult != null ? "LastSeenApplied" : "LastRuleResult" );
                    string desc = resultToShow != null ? resultToShow.Description ?? "(no desc)" : "(none)";
                    _lastLoggedResultToShow = resultToShow;
                    _lastLoggedPreviewActive = activePreview;
                }

            float x0 = Offset.x;
            float y0 = Offset.y;

            // draw cells
            for (int r = 0; r < size; r++)
            {
                for (int c = 0; c < size; c++)
                {
                    Rect cellRect = new Rect(x0 + c * cellSize, y0 + r * cellSize, cellSize, cellSize);
                    // highlight changes from the last applied rule (placed values / candidate removals)
                    System.Collections.Generic.Dictionary<int, Color> usedCandidateHighlightColors = null;
                    // If the result actually applied, show placed values and remove highlights
                    if (resultToShow != null && resultToShow.Apply)
                    {
                        var changes = resultToShow.Changes;
                        if (changes != null)
                        {
                            foreach (CellChange ch in changes)
                            {
                                if (ch.Row == r && ch.Column == c)
                                {
                                    if (ch.NewValue.HasValue)
                                    {
                                        DrawHighlightBorder(cellRect, new Color(0.1f, 0.8f, 0.1f, 1f));
                                    }
                                    break;
                                }
                            }
                        }
                    }

                    // mark cells that were used to deduce the rule result or are reported
                    // as conflicts by validation. For validation conflicts (Apply==false)
                    // render a red-ish highlight so the user can spot invalid cells.
                    if (resultToShow != null && resultToShow.UsedCells != null)
                    {
                        // Special marker: a UsedCell with negative coordinates indicates
                        // the board is globally unsolvable (detected by attempting to
                        // solve a copy). Render the whole board with a dark-yellow tint
                        // in this case so the user can easily notice the unsolvable state.
                        bool unsolvable = resultToShow.UsedCells.Exists(u => u.Row < 0);
                        if (unsolvable)
                        {
                            var unsolvableColor = new Color(0.8f, 0.6f, 0.05f, 0.55f);
                            DrawHighlight(cellRect, unsolvableColor);
                        }
                        else
                        {
                            foreach (var uc in resultToShow.UsedCells)
                            {
                                if (uc.Row == r && uc.Column == c)
                                {
                                    if (usedCandidateHighlightColors == null) usedCandidateHighlightColors = new System.Collections.Generic.Dictionary<int, Color>();
                                    var highlightColor = resultToShow.Apply ? new Color(0.1f, 0.6f, 1f, 0.45f) : new Color(1f, 0.2f, 0.2f, 0.55f);
                                    if (!string.IsNullOrWhiteSpace(uc.HighlightTag))
                                    {
                                        if (string.Equals(uc.HighlightTag, "Target", OrdinalIgnoreCase))
                                        {
                                            highlightColor = ResolveChainHighlightColor("TargetA");
                                        }
                                        else if (string.Equals(uc.HighlightTag, "TargetA", OrdinalIgnoreCase))
                                        {
                                            highlightColor = ResolveChainHighlightColor("TargetA");
                                        }
                                        else if (string.Equals(uc.HighlightTag, "TargetB", OrdinalIgnoreCase))
                                        {
                                            highlightColor = ResolveChainHighlightColor("TargetB");
                                        }
                                        else if (string.Equals(uc.HighlightTag, "Deduction", OrdinalIgnoreCase))
                                        {
                                            highlightColor = colouringChainEvidenceActive
                                                ? ResolveChainHighlightColor("TargetA", 0.50f)
                                                : new Color(1f, 0.58f, 0.1f, 0.50f);
                                        }
                                        else if (string.Equals(uc.HighlightTag, "Failure", OrdinalIgnoreCase))
                                        {
                                            highlightColor = colouringChainEvidenceActive
                                                ? ResolveChainHighlightColor("TargetB", 0.62f)
                                                : new Color(1f, 0.2f, 0.2f, 0.62f);
                                        }
                                    }

                                    if (uc.Candidate.HasValue)
                                    {
                                        int candidate = uc.Candidate.Value;
                                        if (!usedCandidateHighlightColors.TryGetValue(candidate, out var existingColor))
                                        {
                                            usedCandidateHighlightColors[candidate] = highlightColor;
                                        }
                                        else if (GetHighlightPriority(highlightColor) > GetHighlightPriority(existingColor))
                                        {
                                            usedCandidateHighlightColors[candidate] = highlightColor;
                                        }
                                    }
                                    else
                                    {
                                        DrawHighlight(cellRect, highlightColor);
                                    }
                                }
                            }
                        }
                    }

                    if (validationConflictCells != null)
                    {
                        for (int i = 0; i < validationConflictCells.Count; i++)
                        {
                            var conflict = validationConflictCells[i];
                            if (conflict != null && conflict.Row == r && conflict.Column == c)
                            {
                                DrawHighlight(cellRect, new Color(1f, 0.18f, 0.18f, 0.62f));
                                break;
                            }
                        }
                    }

                    ApplyPendingDirectionalStartHighlight(r, c, cellRect, ref usedCandidateHighlightColors);

                    // background
                    GUI.Box(cellRect, "");

                    Cell cell = board.Cells[r, c];
                    if (cell == null) continue;
                    if (cell.Value.HasValue)
                    {
                        DrawColourBackground(cellRect, GetOrderedColoursForDigit(cell, cell.Value.Value));
                        // draw solved digit centered
                        GUI.Label(cellRect, cell.Value.Value.ToString(), cell.IsGiven ? _givenCenterStyle : _centerStyle);
                    }
                    else if (ShowCandidates)
                    {
                        // draw candidates as small grid of digits; optionally highlight specific candidates
                        DrawCandidates(cellRect, cell, usedCandidateHighlightColors);
                    }
                }
            }

            // draw heavier box lines for box boundaries (guard against zero box sizes)
            HandlesBeginGUI();
            float lineWidth = Mathf.Max(2f, cellSize / 8f);
            int boxW = board.BoxWidth;
            int boxH = board.BoxHeight;
            for (int i = 0; i <= size; i++)
            {
                float px = x0 + i * cellSize;
                float py = y0 + i * cellSize;
                // vertical
                bool thickV = boxW > 0 && (i % boxW == 0);
                DrawLine(new Vector2(px, y0), new Vector2(px, y0 + size * cellSize), thickV ? lineWidth : 1f);
                // horizontal
                bool thickH = boxH > 0 && (i % boxH == 0);
                DrawLine(new Vector2(x0, py), new Vector2(x0 + size * cellSize, py), thickH ? lineWidth : 1f);
                // nothing else to do here; visibility is controlled by `_lastSeenRuleResult`
            }
            HandlesEndGUI();

                DrawDirectionalLinksOverlay();
                DrawRuleEvidenceDirectionalLinks(resultToShow);

                if (_radialMenu != null && _radialMenu.IsOpen)
                {
                    // Draw after the board in the same IMGUI pass to guarantee front-most layering.
                    _radialMenu.RenderOverlayOnGUI();
                }

                if (Application.isPlaying)
                {
                    PollHoldTimer();
                    HandlePointerInput();
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
                return;
            }
        }

        /**
         * While selecting a directional link, highlight the staged start digit until the end endpoint is chosen.
         *
         * @param row Current rendered row.
         * @param column Current rendered column.
         * @param cellRect Current rendered cell rectangle.
         * @param candidateHighlightColors Candidate highlight map for the rendered cell.
         */
        private void ApplyPendingDirectionalStartHighlight(
            int row,
            int column,
            Rect cellRect,
            ref System.Collections.Generic.Dictionary<int, Color> candidateHighlightColors)
        {
            if (!IsDirectionalLinkModeActive() || _pendingDirectionalLinkStart == null)
            {
                return;
            }

            if (_pendingDirectionalLinkStart.Row != row || _pendingDirectionalLinkStart.Column != column)
            {
                return;
            }

            if (Runner == null || Runner.CurrentBoard == null || Runner.CurrentBoard.Cells == null)
            {
                return;
            }

            var cell = Runner.CurrentBoard.Cells[row, column];
            if (cell == null)
            {
                return;
            }

            Color pendingColor = LinkToolMode == DirectionalLinkToolMode.Weak
                ? new Color(0.14f, 0.82f, 0.28f, 0.6f)
                : new Color(0.96f, 0.28f, 0.28f, 0.6f);

            if (cell.Value.HasValue)
            {
                if (cell.Value.Value == _pendingDirectionalLinkStart.Digit)
                {
                    DrawHighlightBorder(cellRect, pendingColor);
                }

                return;
            }

            if (cell.Candidates == null || !cell.Candidates.Contains(_pendingDirectionalLinkStart.Digit))
            {
                return;
            }

            if (candidateHighlightColors == null)
            {
                candidateHighlightColors = new System.Collections.Generic.Dictionary<int, Color>();
            }

            // Force the staged candidate to stay visible as the active start endpoint.
            candidateHighlightColors[_pendingDirectionalLinkStart.Digit] = pendingColor;
        }

        private void HandlePointerInput()
        {
            if (!IsBoardInteractionAvailable())
            {
                CancelCellHold();
                return;
            }

            var evt = Event.current;
            if (evt == null) return;

            if (IsDirectionalLinkModeActive() && evt.type == EventType.MouseDown && evt.button == 1)
            {
                if (TryRemoveDirectionalLinkNearPointer(evt.mousePosition, out var deleteOutcome))
                {
                    LastRadialOutcomeMessage = deleteOutcome;
                    evt.Use();
                    return;
                }
            }

            if (_radialMenu != null && _radialMenu.IsOpen)
            {
                if (evt.type == EventType.MouseDrag || evt.type == EventType.MouseMove)
                {
                    LogHoldDebug($"Pointer moved over open radial at {evt.mousePosition}");
                    _radialMenu.UpdatePointer(evt.mousePosition);
                    evt.Use();
                    return;
                }

                if (evt.type == EventType.MouseUp && evt.button == 0)
                {
                    LogHoldDebug($"Pointer released over open radial at {evt.mousePosition}");
                    LastRadialSelection = _radialMenu.ReleasePointer(evt.mousePosition);
                    ApplyRadialSelection(LastRadialSelection, evt.modifiers);
                    ConsumeRadialOpenIntent();
                    ReleaseCellHold("radial selection committed");
                    evt.Use();
                    return;
                }

                if (evt.type == EventType.MouseLeaveWindow)
                {
                    LogHoldDebug("Pointer left window while radial was open");
                    LastRadialSelection = null;
                    CancelCellHold("pointer left window while radial open");
                    return;
                }
            }

            if (evt.type == EventType.MouseDown && evt.button == 0)
            {
                LogHoldDebug($"MouseDown at {evt.mousePosition}");
                if (TryGetCellFromScreenPosition(evt.mousePosition, out int row, out int column))
                {
                    LogHoldDebug($"MouseDown hit r{row}c{column}");
                    BeginCellHold(row, column, evt.mousePosition);
                    evt.Use();
                }
                else
                {
                    LogHoldDebug($"MouseDown missed board at {evt.mousePosition}");
                    CancelCellHold("mouse down missed board");
                }
                return;
            }

            if (CurrentHoldPhase == HoldPhase.Idle) return;

            if (CurrentHoldPhase == HoldPhase.Armed)
            {
                if (_radialMenu != null && _radialMenu.IsOpen)
                {
                    _radialMenu.UpdatePointer(evt.mousePosition);
                }
                return;
            }

            if (evt.type == EventType.MouseDrag || evt.type == EventType.MouseMove)
            {
                UpdateCellHold(evt.mousePosition);
                return;
            }

            if (evt.type == EventType.MouseUp && evt.button == 0)
            {
                if (TryGetCellFromScreenPosition(evt.mousePosition, out int row, out int column) && row == SelectedHoldRow && column == SelectedHoldColumn)
                {
                    LogHoldDebug($"MouseUp on selected cell r{row}c{column} before radial opened");
                    ReleaseCellHold("released before radial opened");
                }
                else
                {
                    LogHoldDebug($"MouseUp cancelled hold at {evt.mousePosition}");
                    CancelCellHold("mouse up outside selected cell");
                }
                evt.Use();
            }
            else if (evt.type == EventType.MouseLeaveWindow)
            {
                LogHoldDebug("Pointer left window before radial opened");
                CancelCellHold("pointer left window before radial opened");
            }
        }

        /**
         * Execute the selected radial action against the currently held cell.
         *
         * @param selection Final radial selection captured on release.
         * @param modifiers Active keyboard modifiers at release time.
         */
        public void ApplyRadialSelection(RadialMenuSelection selection, EventModifiers modifiers)
        {
            LastRadialSelection = selection;

            if (Runner == null || Runner.CurrentBoard == null)
            {
                LastRadialOutcomeMessage = "Board interaction is unavailable.";
                LogHoldDebug($"Radial action no-op: {LastRadialOutcomeMessage}");
                return;
            }

            if (SelectedHoldRow < 0 || SelectedHoldColumn < 0)
            {
                LastRadialOutcomeMessage = "No selected cell for radial action.";
                Runner.PublishManualNoOpOutcome(LastRadialOutcomeMessage);
                LogHoldDebug($"Radial action no-op: {LastRadialOutcomeMessage}");
                return;
            }

            if (selection == null || selection.SegmentId == RadialMenuSegmentId.None)
            {
                LastRadialOutcomeMessage = "No action selected.";
                Runner.PublishManualNoOpOutcome(LastRadialOutcomeMessage);
                LogHoldDebug($"Radial action no-op: {LastRadialOutcomeMessage}");
                return;
            }

            if (IsDirectionalLinkModeActive())
            {
                LastRadialOutcomeMessage = ApplyDirectionalLinkSelection(selection);
                if (!string.IsNullOrWhiteSpace(LastRadialOutcomeMessage))
                {
                    LogHoldDebug($"Radial directional-link action completed: outcome='{LastRadialOutcomeMessage}'");
                }

                return;
            }

            if (IsColourAction(selection.DigitActionType))
            {
                var colourOutcome = ApplyColourSelection(selection);
                LastRadialOutcomeMessage = colourOutcome;
                if (!string.IsNullOrWhiteSpace(colourOutcome))
                {
                    LogHoldDebug($"Radial colour action completed: digit={(selection.Digit.HasValue ? selection.Digit.Value.ToString() : "(none)")} colour={selection.SelectedColour} clearAll={selection.ClearAllColours} outcome='{colourOutcome}'");
                }
                return;
            }

            if (IsValueOnlyEditMode())
            {
                if (!selection.Digit.HasValue)
                {
                    LastRadialOutcomeMessage = "No action selected.";
                    Runner.PublishManualNoOpOutcome(LastRadialOutcomeMessage);
                    LogHoldDebug($"Radial action no-op: {LastRadialOutcomeMessage}");
                    return;
                }

                var valueOnlyExecution = selection.DigitActionType == RadialDigitActionType.ClearValue
                    ? Runner.ExecuteManualClearValue(SelectedHoldRow, SelectedHoldColumn)
                    : Runner.ExecuteManualSetValue(SelectedHoldRow, SelectedHoldColumn, selection.Digit.Value);

                LastRadialOutcomeMessage = valueOnlyExecution != null && !string.IsNullOrWhiteSpace(valueOnlyExecution.Description)
                    ? valueOnlyExecution.Description
                    : (valueOnlyExecution != null && valueOnlyExecution.Applied ? "Manual edit applied." : "No manual edit applied.");

                if (Runner.LastRuleResult != null && Runner.LastRuleResult.Apply)
                {
                    _lastSeenRuleResult = Runner.LastRuleResult;
                }

                LogHoldDebug($"Radial action completed: operation=ValueOnly digit={selection.Digit.Value} applied={(valueOnlyExecution != null && valueOnlyExecution.Applied)} outcome='{LastRadialOutcomeMessage}'");
                return;
            }

            if (selection.SegmentId == RadialMenuSegmentId.TopNoAction)
            {
                // Remove the current cell's value from all peers in its row, column, and box
                var cellValue = Runner.CurrentBoard.Cells[SelectedHoldRow, SelectedHoldColumn].Value;
                if (!cellValue.HasValue || cellValue.Value == 0)
                {
                    LastRadialOutcomeMessage = "Cell has no value to remove from peers.";
                    Runner.PublishManualNoOpOutcome(LastRadialOutcomeMessage);
                    LogHoldDebug($"Radial action no-op: {LastRadialOutcomeMessage}");
                    return;
                }

                int valueToRemove = cellValue.Value;
                bool addToUnsolvedCells = (modifiers & EventModifiers.Shift) != 0;
                var topNoActionExecution = Runner.ExecuteManualUnitCandidateAction(SelectedHoldRow, SelectedHoldColumn, valueToRemove, addToUnsolvedCells);
                LastRadialOutcomeMessage = topNoActionExecution != null && !string.IsNullOrWhiteSpace(topNoActionExecution.Description)
                    ? topNoActionExecution.Description
                    : (topNoActionExecution != null && topNoActionExecution.Applied ? "Manual edit applied." : "No manual edit applied.");

                if (Runner.LastRuleResult != null && Runner.LastRuleResult.Apply)
                {
                    _lastSeenRuleResult = Runner.LastRuleResult;
                }

                LogHoldDebug($"Radial action completed: operation=RemoveValueFromPeers digit={valueToRemove} applied={(topNoActionExecution != null && topNoActionExecution.Applied)} outcome='{LastRadialOutcomeMessage}'");
                return;
            }

            var selectedCell = Runner.CurrentBoard.Cells[SelectedHoldRow, SelectedHoldColumn];
            if (selectedCell != null && selectedCell.IsGiven)
            {
                LastRadialOutcomeMessage = "Given cells only allow removing seen-cell candidates.";
                Runner.PublishManualNoOpOutcome(LastRadialOutcomeMessage);
                LogHoldDebug($"Radial action no-op: {LastRadialOutcomeMessage}");
                return;
            }

            if (selection.SegmentId == RadialMenuSegmentId.SmartCenter)
            {
                LastRadialOutcomeMessage = "No action selected.";
                Runner.PublishManualNoOpOutcome(LastRadialOutcomeMessage);
                LogHoldDebug($"Radial center no-op: {LastRadialOutcomeMessage}");
                return;
            }

            if (!selection.Digit.HasValue)
            {
                LastRadialOutcomeMessage = "Selected segment does not map to a digit.";
                Runner.PublishManualNoOpOutcome(LastRadialOutcomeMessage);
                LogHoldDebug($"Radial action no-op: {LastRadialOutcomeMessage}");
                return;
            }

            if (selection.DigitActionType == RadialDigitActionType.ClearValue)
            {
                var clearExecution = Runner.ExecuteManualClearValue(SelectedHoldRow, SelectedHoldColumn);
                LastRadialOutcomeMessage = clearExecution != null && !string.IsNullOrWhiteSpace(clearExecution.Description)
                    ? clearExecution.Description
                    : (clearExecution != null && clearExecution.Applied ? "Manual edit applied." : "No manual edit applied.");

                if (Runner.LastRuleResult != null && Runner.LastRuleResult.Apply)
                {
                    _lastSeenRuleResult = Runner.LastRuleResult;
                }

                LogHoldDebug($"Radial action completed: operation=ClearValue digit={selection.Digit.Value} applied={(clearExecution != null && clearExecution.Applied)} outcome='{LastRadialOutcomeMessage}'");
                return;
            }

            var operation = ResolveDigitOperation(modifiers);
            if (selection.DigitActionType == RadialDigitActionType.RemoveCandidate)
            {
                operation = ManualCellEditOperation.RemoveCandidate;
            }
            else if (selection.DigitActionType == RadialDigitActionType.AddCandidate)
            {
                operation = ManualCellEditOperation.AddCandidate;
            }

            ManualEditExecutionResult execution;
            if (selection.DigitActionType == RadialDigitActionType.UnitCandidateAction)
            {
                bool addToUnsolvedCells = (modifiers & EventModifiers.Shift) != 0;
                execution = Runner.ExecuteManualUnitCandidateAction(SelectedHoldRow, SelectedHoldColumn, selection.Digit.Value, addToUnsolvedCells);
                operation = addToUnsolvedCells ? ManualCellEditOperation.AddCandidate : ManualCellEditOperation.RemoveCandidate;
            }
            else switch (operation)
            {
                case ManualCellEditOperation.RemoveCandidate:
                    execution = Runner.ExecuteManualRemoveCandidate(SelectedHoldRow, SelectedHoldColumn, selection.Digit.Value);
                    break;
                case ManualCellEditOperation.AddCandidate:
                    execution = Runner.ExecuteManualAddCandidate(SelectedHoldRow, SelectedHoldColumn, selection.Digit.Value);
                    break;
                case ManualCellEditOperation.SetValue:
                default:
                    execution = Runner.ExecuteManualSetValue(SelectedHoldRow, SelectedHoldColumn, selection.Digit.Value);
                    break;
            }

            LastRadialOutcomeMessage = execution != null && !string.IsNullOrWhiteSpace(execution.Description)
                ? execution.Description
                : (execution != null && execution.Applied ? "Manual edit applied." : "No manual edit applied.");

            if (Runner.LastRuleResult != null && Runner.LastRuleResult.Apply)
            {
                _lastSeenRuleResult = Runner.LastRuleResult;
            }

            LogHoldDebug($"Radial action completed: operation={operation} digit={selection.Digit.Value} applied={(execution != null && execution.Applied)} outcome='{LastRadialOutcomeMessage}'");
        }

        /**
         * Resolve which numeric manual operation should run for a selected digit.
         *
         * @param modifiers Active keyboard modifiers at release time.
         * @returns The manual operation to execute.
         */
        public ManualCellEditOperation ResolveDigitOperation(EventModifiers modifiers)
        {
            if (DigitActionMode == NumericRadialActionMode.SetValue) return ManualCellEditOperation.SetValue;
            if (DigitActionMode == NumericRadialActionMode.RemoveCandidate) return ManualCellEditOperation.RemoveCandidate;
            if (DigitActionMode == NumericRadialActionMode.AddCandidate) return ManualCellEditOperation.AddCandidate;

            if ((modifiers & EventModifiers.Shift) != 0)
            {
                return ManualCellEditOperation.RemoveCandidate;
            }

            if ((modifiers & EventModifiers.Control) != 0 || (modifiers & EventModifiers.Command) != 0)
            {
                return ManualCellEditOperation.AddCandidate;
            }

            return ManualCellEditOperation.SetValue;
        }

        private static bool IsColourAction(RadialDigitActionType actionType)
        {
            return actionType == RadialDigitActionType.ColourClearAll
                || actionType == RadialDigitActionType.ColourGreen
                || actionType == RadialDigitActionType.ColourAmber
                || actionType == RadialDigitActionType.ColourRed
                || actionType == RadialDigitActionType.ColourBlue;
        }

        /**
         * Toggle a colour annotation for the current cell and target digit.
         *
         * @param selection Radial colour-ring selection.
         * @returns Human-readable status text.
         */
        private string ApplyColourSelection(RadialMenuSelection selection)
        {
            if (Runner == null || Runner.CurrentBoard == null)
            {
                return "Board interaction is unavailable.";
            }

            if (selection == null || !IsColourAction(selection.DigitActionType))
            {
                return "No colour action selected.";
            }

            if (!selection.Digit.HasValue)
            {
                return "No target digit available for colour annotation.";
            }

            if (SelectedHoldRow < 0 || SelectedHoldColumn < 0)
            {
                return "No selected cell for colour annotation.";
            }

            var board = Runner.CurrentBoard;
            if (board.Cells == null || SelectedHoldRow >= board.Size || SelectedHoldColumn >= board.Size)
            {
                return "Selected cell is unavailable.";
            }

            var cell = board.Cells[SelectedHoldRow, SelectedHoldColumn];
            if (cell == null)
            {
                return "Selected cell is unavailable.";
            }

            int digit = selection.Digit.Value;
            if (digit < 1 || digit > board.Size)
            {
                return "Selected digit is outside the board range.";
            }

            bool digitExists = cell.Value.HasValue
                ? cell.Value.Value == digit
                : cell.Candidates != null && cell.Candidates.Contains(digit);
            if (!digitExists)
            {
                return "Colour annotations can only be set on digits already present in the cell.";
            }

            if (cell.DigitColors == null)
            {
                cell.DigitColors = new System.Collections.Generic.Dictionary<int, System.Collections.Generic.HashSet<HighlightColor>>();
            }

            var execution = Runner.ExecuteManualDigitColourChange(
                SelectedHoldRow,
                SelectedHoldColumn,
                digit,
                selection.SelectedColour,
                selection.ClearAllColours);

            if (execution != null && execution.Applied)
            {
                return selection.ClearAllColours
                    ? $"Cleared all colours for digit {digit}."
                    : (execution.Description ?? $"Updated colour annotations for digit {digit}.");
            }

            return execution != null && !string.IsNullOrWhiteSpace(execution.Description)
                ? execution.Description
                : "No colour annotation change applied.";
        }

        /**
         * Apply one radial selection step for the directional-link workflow.
         *
         * @param selection Final radial selection captured on release.
         * @returns Human-readable status text.
         */
        private string ApplyDirectionalLinkSelection(RadialMenuSelection selection)
        {
            if (Runner == null || Runner.CurrentBoard == null)
            {
                return "Board interaction is unavailable.";
            }

            if (selection == null || !selection.Digit.HasValue)
            {
                return "Select a digit to place a directional link endpoint.";
            }

            int digit = selection.Digit.Value;
            if (SelectedHoldRow < 0 || SelectedHoldColumn < 0)
            {
                return "No selected cell for directional link.";
            }

            if (_pendingDirectionalLinkStart == null)
            {
                _pendingDirectionalLinkStart = new DirectionalLinkEndpoint
                {
                    Row = SelectedHoldRow,
                    Column = SelectedHoldColumn,
                    Digit = digit,
                };

                ApplyCursorForLinkToolState();
                return $"Directional link start set at r{SelectedHoldRow + 1}c{SelectedHoldColumn + 1}:{digit}. Select an end endpoint.";
            }

            var kind = ResolveCurrentDirectionalLinkKind();
            bool linkExists = ContainsDirectionalLink(
                _pendingDirectionalLinkStart.Row,
                _pendingDirectionalLinkStart.Column,
                _pendingDirectionalLinkStart.Digit,
                SelectedHoldRow,
                SelectedHoldColumn,
                digit,
                kind);

            var execution = linkExists
                ? Runner.ExecuteManualRemoveDirectionalLink(
                    _pendingDirectionalLinkStart.Row,
                    _pendingDirectionalLinkStart.Column,
                    _pendingDirectionalLinkStart.Digit,
                    SelectedHoldRow,
                    SelectedHoldColumn,
                    digit,
                    kind)
                : Runner.ExecuteManualAddDirectionalLink(
                    _pendingDirectionalLinkStart.Row,
                    _pendingDirectionalLinkStart.Column,
                    _pendingDirectionalLinkStart.Digit,
                    SelectedHoldRow,
                    SelectedHoldColumn,
                    digit,
                    kind);

            if (execution != null && execution.Applied)
            {
                _pendingDirectionalLinkStart = null;
                ApplyCursorForLinkToolState();
                return execution.Description ?? "Directional link added.";
            }

            string failedMessage = execution != null && !string.IsNullOrWhiteSpace(execution.Description)
                ? execution.Description
                : "No directional link change applied.";
            return failedMessage;
        }

        private bool ContainsDirectionalLink(
            int startRow,
            int startColumn,
            int startDigit,
            int endRow,
            int endColumn,
            int endDigit,
            DirectionalLinkKind kind)
        {
            if (Runner == null || Runner.CurrentBoard == null || Runner.CurrentBoard.DirectionalLinks == null)
            {
                return false;
            }

            var links = Runner.CurrentBoard.DirectionalLinks;
            for (int i = 0; i < links.Count; i++)
            {
                var existing = links[i];
                if (existing == null || existing.Start == null || existing.End == null)
                {
                    continue;
                }

                if (existing.Kind != kind)
                {
                    continue;
                }

                if (existing.Start.Row == startRow
                    && existing.Start.Column == startColumn
                    && existing.Start.Digit == startDigit
                    && existing.End.Row == endRow
                    && existing.End.Column == endColumn
                    && existing.End.Digit == endDigit)
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryRemoveDirectionalLinkNearPointer(Vector2 pointerPosition, out string outcome)
        {
            outcome = string.Empty;
            if (Runner == null || Runner.CurrentBoard == null || Runner.CurrentBoard.DirectionalLinks == null)
            {
                return false;
            }

            if (!TryFindClosestDirectionalLink(pointerPosition, 12f, out var link))
            {
                return false;
            }

            var execution = Runner.ExecuteManualRemoveDirectionalLink(
                link.Start.Row,
                link.Start.Column,
                link.Start.Digit,
                link.End.Row,
                link.End.Column,
                link.End.Digit,
                link.Kind);

            if (execution != null && execution.Applied)
            {
                outcome = execution.Description ?? "Directional link removed.";
                return true;
            }

            outcome = execution != null ? execution.Description : "Directional link was not removed.";
            return !string.IsNullOrWhiteSpace(outcome);
        }

        private bool TryFindClosestDirectionalLink(Vector2 pointerPosition, float maxDistance, out DirectionalCellLink nearest)
        {
            nearest = null;
            if (Runner == null || Runner.CurrentBoard == null || Runner.CurrentBoard.DirectionalLinks == null)
            {
                return false;
            }

            float bestDistance = maxDistance;
            var links = Runner.CurrentBoard.DirectionalLinks;
            for (int i = 0; i < links.Count; i++)
            {
                var link = links[i];
                if (link == null || link.Start == null || link.End == null)
                {
                    continue;
                }

                if (!TryGetDirectionalEndpointPosition(link.Start, out var startPos)
                    || !TryGetDirectionalEndpointPosition(link.End, out var endPos))
                {
                    continue;
                }

                float distance = DistancePointToSegment(pointerPosition, startPos, endPos);
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    nearest = link;
                }
            }

            return nearest != null;
        }

        private static float DistancePointToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float magnitudeSq = ab.sqrMagnitude;
            if (magnitudeSq <= 0.0001f)
            {
                return Vector2.Distance(point, a);
            }

            float t = Vector2.Dot(point - a, ab) / magnitudeSq;
            t = Mathf.Clamp01(t);
            var projection = a + ab * t;
            return Vector2.Distance(point, projection);
        }

        private bool TryGetDirectionalEndpointPosition(DirectionalLinkEndpoint endpoint, out Vector2 position)
        {
            position = default;
            if (endpoint == null)
            {
                return false;
            }

            if (Runner == null || Runner.CurrentBoard == null || Runner.CurrentBoard.Cells == null)
            {
                return false;
            }

            int size = Runner.CurrentBoard.Size;
            if (endpoint.Row < 0 || endpoint.Column < 0 || endpoint.Row >= size || endpoint.Column >= size)
            {
                return false;
            }

            if (!TryGetBoardRect(out var boardRect))
            {
                return false;
            }

            int cellSize = GetComputedCellSize();
            if (cellSize <= 0)
            {
                return false;
            }

            var cell = Runner.CurrentBoard.Cells[endpoint.Row, endpoint.Column];
            if (cell == null)
            {
                return false;
            }

            var cellRect = new Rect(
                boardRect.x + endpoint.Column * cellSize,
                boardRect.y + endpoint.Row * cellSize,
                cellSize,
                cellSize);

            if (cell.Value.HasValue)
            {
                if (cell.Value.Value != endpoint.Digit)
                {
                    return false;
                }

                position = new Vector2(cellRect.center.x, cellRect.center.y);
                return true;
            }

            if (cell.Candidates == null || !cell.Candidates.Contains(endpoint.Digit))
            {
                return false;
            }

            int idx = endpoint.Digit - 1;
            int rr = idx / 3;
            int cc = idx % 3;
            float cs = cellRect.width / 3f;
            var candidateRect = new Rect(cellRect.x + cc * cs + 2f, cellRect.y + rr * cs + 2f, cs - 4f, cs - 4f);
            position = new Vector2(candidateRect.center.x, candidateRect.center.y);
            return true;
        }

        private void DrawDirectionalLinksOverlay()
        {
            if (Runner == null || Runner.CurrentBoard == null || Runner.CurrentBoard.DirectionalLinks == null)
            {
                return;
            }

            var links = Runner.CurrentBoard.DirectionalLinks;
            for (int i = 0; i < links.Count; i++)
            {
                var link = links[i];
                if (link == null || link.Start == null || link.End == null)
                {
                    continue;
                }

                if (!TryGetDirectionalEndpointPosition(link.Start, out var startPos)
                    || !TryGetDirectionalEndpointPosition(link.End, out var endPos))
                {
                    continue;
                }

                DrawDirectionalArrow(startPos, endPos, link.Kind);
            }
        }

        /**
         * Draw directional links attached to the current rule preview/applied result.
         *
         * @param result Rule result currently used for board evidence rendering.
         */
        private void DrawRuleEvidenceDirectionalLinks(RuleResult result)
        {
            if (result == null || result.UsedDirectionalLinks == null || result.UsedDirectionalLinks.Count == 0)
            {
                return;
            }

            for (int i = 0; i < result.UsedDirectionalLinks.Count; i++)
            {
                var link = result.UsedDirectionalLinks[i];
                if (link == null || link.Start == null || link.End == null)
                {
                    continue;
                }

                if (!TryGetDirectionalEndpointPosition(link.Start, out var startPos)
                    || !TryGetDirectionalEndpointPosition(link.End, out var endPos))
                {
                    continue;
                }

                // Slightly thinner and translucent than user-authored links so both layers can coexist.
                DrawDirectionalArrow(startPos, endPos, link.Kind, alphaMultiplier: 0.82f, widthMultiplier: 0.82f);
            }
        }

        private void DrawDirectionalArrow(Vector2 start, Vector2 end, DirectionalLinkKind kind)
        {
            DrawDirectionalArrow(start, end, kind, alphaMultiplier: 1f, widthMultiplier: 1f);
        }

        private void DrawDirectionalArrow(Vector2 start, Vector2 end, DirectionalLinkKind kind, float alphaMultiplier, float widthMultiplier)
        {
            float width = Mathf.Max(1f, GetComputedCellSize() * 0.026f * Mathf.Max(0.1f, widthMultiplier));
            Color color = kind == DirectionalLinkKind.Strong
                ? new Color(0.96f, 0.28f, 0.28f, 1f)
                : new Color(0.14f, 0.82f, 0.28f, 1f);
            color.a *= Mathf.Clamp01(alphaMultiplier);

            var direction = (end - start);
            float length = direction.magnitude;
            if (length <= 0.001f)
            {
                return;
            }

            var normalized = direction / length;
            float headLength = Mathf.Max(7f, GetComputedCellSize() * 0.17f);
            var lineEnd = end - normalized * headLength;

            if (kind == DirectionalLinkKind.Strong)
            {
                DrawLineColored(start, lineEnd, width, color);
            }
            else
            {
                DrawDottedLine(start, lineEnd, width, color);
            }

            float sideAngle = 25f * Mathf.Deg2Rad;
            var left = new Vector2(
                normalized.x * Mathf.Cos(sideAngle) - normalized.y * Mathf.Sin(sideAngle),
                normalized.x * Mathf.Sin(sideAngle) + normalized.y * Mathf.Cos(sideAngle));
            var right = new Vector2(
                normalized.x * Mathf.Cos(-sideAngle) - normalized.y * Mathf.Sin(-sideAngle),
                normalized.x * Mathf.Sin(-sideAngle) + normalized.y * Mathf.Cos(-sideAngle));

            DrawLineColored(end, end - left * headLength, width, color);
            DrawLineColored(end, end - right * headLength, width, color);
        }

        private void DrawDottedLine(Vector2 start, Vector2 end, float width, Color color)
        {
            var direction = end - start;
            float length = direction.magnitude;
            if (length <= 0.001f)
            {
                return;
            }

            var normalized = direction / length;
            float dashLength = Mathf.Max(4f, GetComputedCellSize() * 0.12f);
            float gapLength = Mathf.Max(3f, GetComputedCellSize() * 0.09f);
            float progress = 0f;
            while (progress < length)
            {
                float segmentEnd = Mathf.Min(progress + dashLength, length);
                var a = start + normalized * progress;
                var b = start + normalized * segmentEnd;
                DrawLineColored(a, b, width, color);
                progress += dashLength + gapLength;
            }
        }

        private void PollHoldTimer()
        {
            if (CurrentHoldPhase != HoldPhase.Holding) return;
            if (!IsBoardInteractionAvailable())
            {
                CancelCellHold();
                return;
            }

            if (_holdStartedAt < 0f) return;
            float elapsed = Time.realtimeSinceStartup - _holdStartedAt;
            if (elapsed < HoldOpenThresholdSeconds) return;

            if (!TryGetCellFromScreenPosition(_holdStartedPointerPosition, out int row, out int column) || row != SelectedHoldRow || column != SelectedHoldColumn)
            {
                LogHoldDebug($"PollHoldTimer failed: pointer no longer on selected cell at {_holdStartedPointerPosition}");
                CancelCellHold("poll found pointer off cell");
                return;
            }

            CurrentHoldPhase = HoldPhase.Armed;
            _radialOpenIntentRequested = true;
            LogHoldDebug($"PollHoldTimer armed radial after {elapsed:0.000}s at r{SelectedHoldRow}c{SelectedHoldColumn}");
            OpenRadialMenu();
        }

        private void DrawCandidates(Rect rect, Cell cell, System.Collections.Generic.Dictionary<int, Color> highlightDigits)
        {
            if (cell == null || cell.Candidates == null) return;
            // Temporary debug: if this cell is affected by the preview or the
            // last applied result, dump the live candidate list so we can
            // correlate model state with EnactAll logs.
            try
            {
                var previewResLocal = Runner != null ? Runner.PreviewRuleResult : null;
                var appliedResLocal = _lastSeenRuleResult;
                bool cellAffected = false;
                System.Action<RuleResult> checkRes = (res) =>
                {
                    if (res == null || res.Changes == null) return;
                    foreach (var ch in res.Changes)
                    {
                        if (ch == null) continue;
                        if (ch.Row == cell.Row && ch.Column == cell.Column)
                        {
                            cellAffected = true; break;
                        }
                        if (ch.NewValue.HasValue)
                        {
                            var boardLocal = Runner != null ? Runner.CurrentBoard : null;
                            if (boardLocal != null && ch.Row >= 0 && ch.Column >= 0 && ch.Row < boardLocal.Size && ch.Column < boardLocal.Size)
                            {
                                var originBox = boardLocal.Cells[ch.Row, ch.Column].Box;
                                if ((cell.Row == ch.Row || cell.Column == ch.Column || cell.Box == originBox) && !(cell.Row == ch.Row && cell.Column == ch.Column))
                                {
                                    cellAffected = true; break;
                                }
                            }
                        }
                    }
                };

                checkRes(previewResLocal);
                checkRes(appliedResLocal);

                if (cellAffected)
                {
                    var liveSb = new StringBuilder();
                    if (cell.Candidates != null)
                    {
                        foreach (var cd in cell.Candidates)
                        {
                            liveSb.Append(cd).Append(',');
                        }
                    }
                    var liveStr = liveSb.Length > 0 ? liveSb.ToString(0, liveSb.Length - 1) : "(none)";
                    var previewActiveLocal = previewResLocal != null && previewResLocal.Apply;
                    var appliedDesc = _lastSeenRuleResult != null ? _lastSeenRuleResult.Description : (Runner.LastRuleResult != null ? Runner.LastRuleResult.Description : "(none)");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
            }
            // small 3x3 layout for candidates assuming up to 9 digits
            int size = 3;
            float cs = rect.width / size;
            StringBuilder sb = new StringBuilder();
            for (int d = 1; d <= 9; d++)
            {
                int idx = d - 1;
                int rr = idx / size;
                int cc = idx % size;
                Rect r = new Rect(rect.x + cc * cs + 2, rect.y + rr * cs + 2, cs - 4, cs - 4);
                bool hasCandidate = cell.Candidates.Contains(d);
                if (hasCandidate)
                {
                    DrawColourBackground(r, GetOrderedColoursForDigit(cell, d));
                }
                bool previewActive = Runner != null && Runner.PreviewRuleResult != null && Runner.PreviewRuleResult.Apply;
                // Determine if this digit was removed by the last seen rule result for this cell.
                // Consider both previewed changes (hover) and the last actually applied rule
                // so the UI highlights either case appropriately. Check removals regardless
                // of whether the candidate currently exists so previewed removals are shown
                // when hovering the rule.
                var previewRes = Runner != null ? Runner.PreviewRuleResult : null;
                var appliedRes = _lastSeenRuleResult;

                // Helper to test a RuleResult for removals/placements that affect this cell
                System.Action<RuleResult, System.Action<bool>> inspectWithCallback = (res, cb) =>
                {
                    if (res == null || res.Changes == null) return;
                    foreach (var ch in res.Changes)
                    {
                        if (ch == null) continue;
                        // explicit removal recorded for this cell
                        if (ch.Row == cell.Row && ch.Column == cell.Column && ch.RemovedCandidates != null && ch.RemovedCandidates.Contains(d))
                        {
                            cb(true);
                            break;
                        }

                        // implied removals: if the rule placed `d` in another cell, that
                        // placement removes `d` from all its peers. Treat peers as recently
                        // removed even if the rule didn't emit explicit RemovedCandidates.
                        if (ch.NewValue.HasValue && ch.NewValue.Value == d && !ch.ValueOnlySet)
                        {
                            var board = Runner != null ? Runner.CurrentBoard : null;
                            if (board != null && ch.Row >= 0 && ch.Column >= 0 && ch.Row < board.Size && ch.Column < board.Size)
                            {
                                var originBox = board.Cells[ch.Row, ch.Column].Box;
                                if ((cell.Row == ch.Row || cell.Column == ch.Column || cell.Box == originBox) && !(cell.Row == ch.Row && cell.Column == ch.Column))
                                {
                                    cb(true);
                                    break;
                                }
                            }
                        }
                    }
                };

                // Determine whether the preview would remove this candidate and whether
                // a previously applied rule removed it. We'll use these flags to decide
                // drawing behavior: when a preview is active, hide candidates removed
                // by either the preview or by prior applied rules; when no preview is
                // active, draw previously-applied removals in red.
                bool willBeRemovedByPreview = false;
                // Mark a candidate as "will be removed" by the preview regardless of
                // whether it still exists on the board. This ensures that hovering a
                // past ChangeLog group correctly shows the removed candidates in red,
                // even though those candidates have already been removed from the board.
                inspectWithCallback(previewRes, (val) => { if (val) willBeRemovedByPreview = true; });

                bool wasRemovedByApplied = false;
                // For previously-applied removals, mark them even when the candidate
                // no longer exists so UI can show what the rule removed when it ran.
                inspectWithCallback(appliedRes, (val) => { if (val) wasRemovedByApplied = true; });

                bool isHighlighted = false;
                Color candidateHighlightColor = new Color(1f, 0.85f, 0.25f, 1f);
                if (highlightDigits != null && highlightDigits.TryGetValue(d, out var mappedColor))
                {
                    isHighlighted = true;
                    candidateHighlightColor = mappedColor;
                }

                // Candidate-level evidence should tint the candidate slot itself.
                if (isHighlighted)
                {
                    DrawHighlight(r, new Color(candidateHighlightColor.r, candidateHighlightColor.g, candidateHighlightColor.b, 0.35f));
                    DrawCandidateBorder(r, new Color(candidateHighlightColor.r, candidateHighlightColor.g, candidateHighlightColor.b, 1f));
                }

                if (previewActive)
                {
                    // Preview active: show candidates the preview will remove in red,
                    // but hide candidates removed by previously applied rules so the
                    // preview view represents the board as-if the preview were applied.
                    if (willBeRemovedByPreview)
                    {
                        Color prev = GUI.color;
                        GUI.color = Color.red;
                        GUI.Label(r, d.ToString(), _candidateStyle);
                        GUI.color = prev;
                    }
                    else if (wasRemovedByApplied)
                    {
                        // previously-applied removal: hide it while previewing
                    }
                    else if (hasCandidate)
                    {
                        GUI.Label(r, d.ToString(), _candidateStyle);
                    }
                }
                else
                {
                    // No active preview: show previously applied removals in red,
                    // otherwise draw existing candidates normally.
                    if (wasRemovedByApplied)
                    {
                        Color prev = GUI.color;
                        GUI.color = Color.red;
                        GUI.Label(r, d.ToString(), _candidateStyle);
                        GUI.color = prev;
                    }
                    else if (hasCandidate)
                    {
                        GUI.Label(r, d.ToString(), _candidateStyle);
                    }
                }
            }
        }

        /** Minimal wrapper for drawing lines using GL via Handles style when in OnGUI. */
        private void HandlesBeginGUI()
        {
            // nothing for now - reserved for future GL calls
        }

        private void HandlesEndGUI()
        {
            // nothing for now
        }

        private Texture2D _lineTex;
        private void DrawLine(Vector2 a, Vector2 b, float width)
        {
            if (_lineTex == null)
            {
                _lineTex = new Texture2D(1, 1);
                _lineTex.SetPixel(0, 0, Color.white);
                _lineTex.Apply();
            }
            var angle = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
            var length = Vector2.Distance(a, b);
            var previous = GUI.color;
            GUI.color = Color.black;
            GUIUtility.RotateAroundPivot(angle, a);
            GUI.DrawTexture(new Rect(a.x, a.y - width / 2f, length, width), _lineTex);
            GUIUtility.RotateAroundPivot(-angle, a);
            GUI.color = previous;
        }

        private void DrawLineColored(Vector2 a, Vector2 b, float width, Color color)
        {
            if (_lineTex == null)
            {
                _lineTex = new Texture2D(1, 1);
                _lineTex.SetPixel(0, 0, Color.white);
                _lineTex.Apply();
            }

            var angle = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
            var length = Vector2.Distance(a, b);
            var previous = GUI.color;
            GUI.color = color;
            GUIUtility.RotateAroundPivot(angle, a);
            GUI.DrawTexture(new Rect(a.x, a.y - width / 2f, length, width), _lineTex);
            GUIUtility.RotateAroundPivot(-angle, a);
            GUI.color = previous;
        }

        private Texture2D _highlightTex;
        private void DrawHighlight(Rect rect, Color color)
        {
            if (_highlightTex == null)
            {
                _highlightTex = new Texture2D(1, 1);
                _highlightTex.SetPixel(0, 0, Color.white);
                _highlightTex.Apply();
            }
            Color prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, _highlightTex);
            GUI.color = prev;
        }

        /**
         * Rank highlight colors so failure/deduction evidence can override base chain colors for candidate slots.
         *
         * @param color Candidate highlight color.
         * @returns Relative priority where larger values are stronger.
         */
        private static int GetHighlightPriority(Color color)
        {
            if (ApproximatelyColor(color, new Color(1f, 0.2f, 0.2f, 0.62f))) return 3; // Failure
            if (ApproximatelyColor(color, new Color(1f, 0.58f, 0.1f, 0.50f))) return 2; // Deduction
            return 1; // Chain colours and default evidence
        }

        /**
         * Compare colors with tolerance for float math.
         *
         * @param a First color.
         * @param b Second color.
         * @returns True when components are approximately equal.
         */
        private static bool ApproximatelyColor(Color a, Color b)
        {
            const float epsilon = 0.005f;
            return Mathf.Abs(a.r - b.r) <= epsilon &&
                   Mathf.Abs(a.g - b.g) <= epsilon &&
                   Mathf.Abs(a.b - b.b) <= epsilon;
        }

        /**
         * Resolve chain highlight colours from currently enabled colour settings.
         * Uses the first two enabled colours in display order.
         *
         * @param tag Chain tag: TargetA or TargetB.
         * @returns Candidate-slot tint colour for the requested chain side.
         */
        private static Color ResolveChainHighlightColor(string tag, float alpha = 0.52f)
        {
            var enabled = ColourSettings.GetEnabledColours();

            HighlightColor first = HighlightColor.Green;
            HighlightColor second = HighlightColor.Blue;
            if (enabled.Count > 0)
            {
                first = enabled[0];
            }

            if (enabled.Count > 1)
            {
                second = enabled[1];
            }

            var baseColor = string.Equals(tag, "TargetB", OrdinalIgnoreCase)
                ? HighlightColorPalette.ToColor(second)
                : HighlightColorPalette.ToColor(first);

            baseColor.a = alpha;
            return baseColor;
        }

        private void DrawHighlightBorder(Rect rect, Color color)
        {
            if (_highlightTex == null)
            {
                _highlightTex = new Texture2D(1, 1);
                _highlightTex.SetPixel(0, 0, Color.white);
                _highlightTex.Apply();
            }
            Color prev = GUI.color;
            GUI.color = color;
            float thickness = Mathf.Max(4f, rect.width / 12f);
            // top
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), _highlightTex);
            // bottom
            GUI.DrawTexture(new Rect(rect.x, rect.y + rect.height - thickness, rect.width, thickness), _highlightTex);
            // left
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), _highlightTex);
            // right
            GUI.DrawTexture(new Rect(rect.x + rect.width - thickness, rect.y, thickness, rect.height), _highlightTex);
            GUI.color = prev;
        }

        private void DrawCandidateBorder(Rect rect, Color color)
        {
            if (_highlightTex == null)
            {
                _highlightTex = new Texture2D(1, 1);
                _highlightTex.SetPixel(0, 0, Color.white);
                _highlightTex.Apply();
            }
            Color prev = GUI.color;
            GUI.color = color;
            float thickness = Mathf.Max(1f, rect.width / 8f);
            // top
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), _highlightTex);
            // bottom
            GUI.DrawTexture(new Rect(rect.x, rect.y + rect.height - thickness, rect.width, thickness), _highlightTex);
            // left
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), _highlightTex);
            // right
            GUI.DrawTexture(new Rect(rect.x + rect.width - thickness, rect.y, thickness, rect.height), _highlightTex);
            GUI.color = prev;
        }

        /**
         * Gather the active colours for a digit in stable display order.
         *
         * @param cell Board cell being rendered.
         * @param digit Digit to inspect.
         * @returns Ordered list of active colours for the digit.
         */
        private static System.Collections.Generic.List<HighlightColor> GetOrderedColoursForDigit(Cell cell, int digit)
        {
            var result = new System.Collections.Generic.List<HighlightColor>(4);
            if (cell == null || cell.DigitColors == null)
            {
                return result;
            }

            if (!cell.DigitColors.TryGetValue(digit, out var colours) || colours == null || colours.Count == 0)
            {
                return result;
            }

            HighlightColor[] order =
            {
                HighlightColor.Green,
                HighlightColor.Amber,
                HighlightColor.Red,
                HighlightColor.Blue,
            };

            for (int i = 0; i < order.Length; i++)
            {
                if (colours.Contains(order[i]))
                {
                    result.Add(order[i]);
                }
            }

            return result;
        }

        /**
         * Draw a split colour background using 1, 2, or 3/4 pastel colours.
         *
         * @param rect Target rectangle.
         * @param colours Ordered highlight colours.
         */
        private void DrawColourBackground(Rect rect, System.Collections.Generic.List<HighlightColor> colours)
        {
            if (colours == null || colours.Count == 0)
            {
                return;
            }

            if (colours.Count == 1)
            {
                DrawHighlight(rect, HighlightColorPalette.ToColor(colours[0]));
                return;
            }

            if (colours.Count == 2)
            {
                float half = rect.width * 0.5f;
                DrawHighlight(new Rect(rect.x, rect.y, half, rect.height), HighlightColorPalette.ToColor(colours[0]));
                DrawHighlight(new Rect(rect.x + half, rect.y, rect.width - half, rect.height), HighlightColorPalette.ToColor(colours[1]));
                return;
            }

            float halfWidth = rect.width * 0.5f;
            float halfHeight = rect.height * 0.5f;
            Rect[] quadrants =
            {
                new Rect(rect.x, rect.y, halfWidth, halfHeight),
                new Rect(rect.x + halfWidth, rect.y, rect.width - halfWidth, halfHeight),
                new Rect(rect.x, rect.y + halfHeight, halfWidth, rect.height - halfHeight),
                new Rect(rect.x + halfWidth, rect.y + halfHeight, rect.width - halfWidth, rect.height - halfHeight),
            };

            int limit = Mathf.Min(colours.Count, quadrants.Length);
            for (int i = 0; i < limit; i++)
            {
                DrawHighlight(quadrants[i], HighlightColorPalette.ToColor(colours[i]));
            }
        }
    }
}
