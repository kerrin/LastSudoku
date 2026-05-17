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

            float x0 = Offset.x;
            float y0 = Offset.y;

            // draw cells
            for (int r = 0; r < size; r++)
            {
                for (int c = 0; c < size; c++)
                {
                    Rect cellRect = new Rect(x0 + c * cellSize, y0 + r * cellSize, cellSize, cellSize);
                    // highlight changes from the last applied rule (placed values / candidate removals)
                    bool highlighted = false;
                    int? usedCandidateForCell = null;
                    if (Runner.LastRuleResult != null && Runner.LastRuleResult.Apply)
                    {
                        var changes = Runner.LastRuleResult.Changes;
                        if (changes != null)
                        {
                            foreach (CellChange ch in changes)
                            {
                                if (ch.Row == r && ch.Column == c)
                                {
                                    // placed value
                                    if (ch.NewValue.HasValue)
                                    {
                                        DrawHighlightBorder(cellRect, new Color(0.1f, 0.8f, 0.1f, 1f));
                                        highlighted = true;
                                    }
                                    // candidate removals
                                    else if (ch.RemovedCandidates != null && ch.RemovedCandidates.Count > 0)
                                    {
                                        DrawHighlightBorder(cellRect, new Color(1f, 0.75f, 0.1f, 1f));
                                        highlighted = true;
                                    }
                                    break;
                                }
                            }
                        }

                        // if this cell wasn't changed but was used to deduce the result, draw the used-cell highlight
                        if (!highlighted && Runner.LastRuleResult.UsedCells != null)
                        {
                            foreach (var uc in Runner.LastRuleResult.UsedCells)
                            {
                                if (uc.Row == r && uc.Column == c)
                                {
                                    DrawHighlight(cellRect, new Color(0.1f, 0.6f, 1f, 0.45f));
                                    if (uc.Candidate.HasValue) usedCandidateForCell = uc.Candidate.Value;
                                    break;
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
                        // draw candidates as small grid of digits; optionally highlight a specific candidate
                        DrawCandidates(cellRect, cell, usedCandidateForCell);
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
            }
            HandlesEndGUI();
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
                return;
            }
        }

        private void DrawCandidates(Rect rect, Cell cell, int? highlightDigit)
        {
            if (cell == null || cell.Candidates == null) return;
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
                if (!cell.Candidates.Contains(d)) continue;
                if (highlightDigit.HasValue && highlightDigit.Value == d)
                {
                    Color prev = GUI.color;
                    GUI.color = new Color(1f, 0.85f, 0.25f, 1f);
                    GUI.Label(r, d.ToString(), _candidateStyle);
                    GUI.color = prev;
                }
                else
                {
                    GUI.Label(r, d.ToString(), _candidateStyle);
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
    }
}
