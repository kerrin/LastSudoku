using System.Text;
using UnityEngine;
using Sudoku.Models;
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

        [Tooltip("Show candidate digits inside empty cells")]
        public bool ShowCandidates = true;

        [Tooltip("Top-left screen position for the rendered board")]
        public Vector2 Offset = new Vector2(20, 20);

        private GUIStyle _centerStyle;
        private GUIStyle _candidateStyle;

        private void EnsureStyles()
        {
            if (_centerStyle != null) return;
            var baseLabel = GUI.skin != null ? GUI.skin.label : new GUIStyle();
            _centerStyle = new GUIStyle(baseLabel) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.Max(12, CellSize / 2) };
            _candidateStyle = new GUIStyle(baseLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.Max(6, CellSize / 4),
                padding = new RectOffset(0, 0, 0, 0)
            };
        }

        private void OnValidate()
        {
            _centerStyle = null; // rebuild styles on inspector changes
            _candidateStyle = null;
        }

        private void OnGUI()
        {
            try
            {
                if (Runner == null || Runner.CurrentBoard == null) return;
                EnsureStyles();

                Board board = Runner.CurrentBoard;
                if (board.Cells == null) return;
                int size = board.Size;
            float x0 = Offset.x;
            float y0 = Offset.y;

            // draw cells
            for (int r = 0; r < size; r++)
            {
                for (int c = 0; c < size; c++)
                {
                    Rect cellRect = new Rect(x0 + c * CellSize, y0 + r * CellSize, CellSize, CellSize);
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
                                        DrawHighlight(cellRect, new Color(0.15f, 0.65f, 0.15f, 0.35f));
                                        highlighted = true;
                                    }
                                    // candidate removals
                                    else if (ch.RemovedCandidates != null && ch.RemovedCandidates.Count > 0)
                                    {
                                        DrawHighlight(cellRect, new Color(1f, 0.8f, 0.2f, 0.35f));
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
            float lineWidth = Mathf.Max(2f, CellSize / 8f);
            int boxW = board.BoxWidth;
            int boxH = board.BoxHeight;
            for (int i = 0; i <= size; i++)
            {
                float px = x0 + i * CellSize;
                float py = y0 + i * CellSize;
                // vertical
                bool thickV = boxW > 0 && (i % boxW == 0);
                DrawLine(new Vector2(px, y0), new Vector2(px, y0 + size * CellSize), thickV ? lineWidth : 1f);
                // horizontal
                bool thickH = boxH > 0 && (i % boxH == 0);
                DrawLine(new Vector2(x0, py), new Vector2(x0 + size * CellSize, py), thickH ? lineWidth : 1f);
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
    }
}
