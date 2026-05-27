using System.Text;
using UnityEngine;
using Sudoku.Models;
using Cell = Sudoku.Models.Cell;
using Board = Sudoku.Models.Board;
using Sudoku.Solver.Rules;

namespace Sudoku.Solver
{
    /**
     * Simple Game-view visualizer for a `Board` instance provided by a `SolverRunner`.
     * Draws the grid and cell values/candidates using IMGUI in `OnGUI`.
     */
    [ExecuteInEditMode]
    public class BoardVisualizer : MonoBehaviour
    {
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

        private GUIStyle _centerStyle;
        private GUIStyle _candidateStyle;
        private int _lastComputedCellSize = -1;
        // Track the last seen RuleResult so we can render removed candidates
        // in red until a new rule result is produced.
        private Sudoku.Solver.Rules.RuleResult _lastSeenRuleResult;
        // last result logged to avoid spamming logs every frame
        private Sudoku.Solver.Rules.RuleResult _lastLoggedResultToShow;
        private bool _lastLoggedPreviewActive = false;

        private void EnsureStyles(int cellSize)
        {
            if (_centerStyle != null && _lastComputedCellSize == cellSize) return;
            _lastComputedCellSize = cellSize;
            var baseLabel = GUI.skin != null ? GUI.skin.label : new GUIStyle();
            _centerStyle = new GUIStyle(baseLabel) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.Max(12, cellSize / 2) };
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
                if (Runner != null && Runner.LastRuleResult != null && Runner.LastRuleResult.Apply && Runner.LastRuleResult != _lastSeenRuleResult)
                {
                    _lastSeenRuleResult = Runner.LastRuleResult;
                }

                // Choose which RuleResult to use for display: prefer a hovered
                // preview (only if it actually applies) so mouseover highlights
                // show instantly, then the last applied rule (stored in
                // `_lastSeenRuleResult`), then the Runner.LastRuleResult fallback.
                var activePreview = Runner.PreviewRuleResult != null && Runner.PreviewRuleResult.Apply;
                var resultToShow = activePreview ? Runner.PreviewRuleResult : (_lastSeenRuleResult ?? Runner.LastRuleResult);

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
                    System.Collections.Generic.HashSet<int> usedCandidatesForCell = null;
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
                                    if (usedCandidatesForCell == null) usedCandidatesForCell = new System.Collections.Generic.HashSet<int>();
                                    var highlightColor = resultToShow.Apply ? new Color(0.1f, 0.6f, 1f, 0.45f) : new Color(1f, 0.2f, 0.2f, 0.55f);
                                    DrawHighlight(cellRect, highlightColor);
                                    if (uc.Candidate.HasValue) usedCandidatesForCell.Add(uc.Candidate.Value);
                                }
                            }
                        }
                    }

                    // background
                    GUI.Box(cellRect, "");

                    Cell cell = board.Cells[r, c];
                    if (cell == null) continue;
                    if (cell.Value.HasValue)
                    {
                        // draw solved digit centered
                        GUI.Label(cellRect, cell.Value.Value.ToString(), _centerStyle);
                    }
                    else if (ShowCandidates)
                    {
                        // draw candidates as small grid of digits; optionally highlight specific candidates
                        DrawCandidates(cellRect, cell, usedCandidatesForCell);
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
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
                return;
            }
        }

        private void DrawCandidates(Rect rect, Cell cell, System.Collections.Generic.HashSet<int> highlightDigits)
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
                        if (ch.NewValue.HasValue && ch.NewValue.Value == d)
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
                // Only consider a candidate as "will be removed" by the preview if
                // that candidate actually exists in the cell right now. This avoids
                // showing red for digits that were never present (e.g. when a rule
                // records RemovedCandidates for a cell as "all except X").
                inspectWithCallback(previewRes, (val) => { if (val && hasCandidate) willBeRemovedByPreview = true; });

                bool wasRemovedByApplied = false;
                // Similarly, for previously-applied removals only mark them if the
                // candidate was present in the live board at the time of drawing.
                inspectWithCallback(appliedRes, (val) => { if (val && hasCandidate) wasRemovedByApplied = true; });

                bool isHighlighted = highlightDigits != null && highlightDigits.Contains(d);
                // Draw a small yellow border around candidates used in deductions so
                // the highlight remains visible even when the candidate digit itself
                // is drawn in red for recent removals.
                if (isHighlighted)
                {
                    DrawCandidateBorder(r, new Color(1f, 0.85f, 0.25f, 1f));
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
                _lineTex.SetPixel(0, 0, Color.black);
                _lineTex.Apply();
            }
            var angle = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
            var length = Vector2.Distance(a, b);
            GUIUtility.RotateAroundPivot(angle, a);
            GUI.DrawTexture(new Rect(a.x, a.y - width / 2f, length, width), _lineTex);
            GUIUtility.RotateAroundPivot(-angle, a);
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
    }
}
