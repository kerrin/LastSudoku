using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Sudoku.Models;
using Sudoku.Solver;
using Sudoku.UI.Config;

namespace Sudoku.UI.Panels
{
    /**
     * Side panel section that exposes buttons for clearing colour annotations.
     *
     * Shown only in Solve mode when at least one highlight colour is enabled.
     * Rebuilds its button list whenever the set of enabled colours changes.
     */
    public class ColourClearPanel : MonoBehaviour
    {
        /** Runner used to access the current board when clearing colours. */
        public SolverRunner Runner;

        private int _builtColourMask = -1;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            RebuildButtons();
        }

        private void Update()
        {
            if (!Application.isPlaying) return;

            // Rebuild whenever any enabled-colour flag changes while the panel is live.
            int currentMask = GetEnabledColourMask();
            if (currentMask != _builtColourMask)
            {
                RebuildButtons();
            }
        }

        /**
         * Rebuild the button list to match the currently enabled colours.
         * Safe to call multiple times; clears all existing buttons first.
         */
        public void RebuildButtons()
        {
            _builtColourMask = GetEnabledColourMask();

            // Remove previous buttons.
            foreach (Transform child in transform)
            {
                Destroy(child.gameObject);
            }

            if (!ColourSettings.AnyEnabled) return;

            EnsureVerticalLayout();

            // ── "Clear All Colours" ─────────────────────────────────────────
            AddButton("Clear All Colours", ClearAllColours, new Color(0.55f, 0.55f, 0.60f, 1f));

            var enabledColours = ColourSettings.GetEnabledColours();
            foreach (var colour in enabledColours)
            {
                // Capture for closure.
                var c = colour;
                var pastel = HighlightColorPalette.ToColor(c);
                string name = HighlightColorPalette.ToFullName(c);

                AddButton($"Clear {name} (All)",        () => ClearColour(c, valueCells: true,  candidateCells: true),  pastel);
                AddButton($"Clear {name} (Candidates)", () => ClearColour(c, valueCells: false, candidateCells: true),  pastel);
                AddButton($"Clear {name} (Values)",     () => ClearColour(c, valueCells: true,  candidateCells: false), pastel);
            }
        }

        private static int GetEnabledColourMask()
        {
            int mask = 0;
            if (ColourSettings.GreenEnabled) mask |= 1 << 0;
            if (ColourSettings.AmberEnabled) mask |= 1 << 1;
            if (ColourSettings.RedEnabled) mask |= 1 << 2;
            if (ColourSettings.BlueEnabled) mask |= 1 << 3;
            return mask;
        }

        // ─── Layout ──────────────────────────────────────────────────────────

        private void EnsureVerticalLayout()
        {
            var vlg = GetComponent<VerticalLayoutGroup>();
            if (vlg != null) return;

            vlg = gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 3f;
            vlg.childControlHeight = false;
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(4, 4, 6, 4);
        }

        private void AddButton(string label, System.Action onClick, Color buttonColour)
        {
            var btnGO = new GameObject(label, typeof(RectTransform));
            btnGO.transform.SetParent(transform, false);

            var rt = btnGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, 24f);

            // Darken the pastel colour for the button background.
            var bg = btnGO.AddComponent<Image>();
            var darkened = new Color(buttonColour.r * 0.50f, buttonColour.g * 0.50f, buttonColour.b * 0.55f, 0.92f);
            bg.color = darkened;

            var btn = btnGO.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor      = darkened;
            colors.highlightedColor = new Color(buttonColour.r * 0.70f, buttonColour.g * 0.70f, buttonColour.b * 0.75f, 1f);
            colors.pressedColor     = new Color(buttonColour.r * 0.35f, buttonColour.g * 0.35f, buttonColour.b * 0.40f, 1f);
            colors.selectedColor    = darkened;
            btn.colors = colors;
            btn.targetGraphic = bg;
            btn.onClick.AddListener(() => onClick());

            var textGO = new GameObject("Label", typeof(RectTransform));
            textGO.transform.SetParent(btnGO.transform, false);

            var textRT = textGO.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = new Vector2(5f, 0f);
            textRT.offsetMax = new Vector2(-5f, 0f);

            var txt = textGO.AddComponent<Text>();
            txt.text = label;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (txt.font == null) txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.fontSize = 11;
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleLeft;
            txt.raycastTarget = false;
        }

        // ─── Clear Operations ─────────────────────────────────────────────────

        /**
         * Clear all colour annotations from every cell on the board.
         */
        private void ClearAllColours()
        {
            if (Runner?.CurrentBoard?.Cells == null) return;

            int size = Runner.CurrentBoard.Size;
            for (int r = 0; r < size; r++)
            {
                for (int c = 0; c < size; c++)
                {
                    var cell = Runner.CurrentBoard.Cells[r, c];
                    cell?.DigitColors.Clear();
                }
            }
        }

        /**
         * Clear one specific colour from cells matching the given filter.
         *
         * @param colour          The highlight colour to remove.
         * @param valueCells      When true, remove from cells that have a set value.
         * @param candidateCells  When true, remove from cells that only have candidates.
         */
        private void ClearColour(HighlightColor colour, bool valueCells, bool candidateCells)
        {
            if (Runner?.CurrentBoard?.Cells == null) return;

            int size = Runner.CurrentBoard.Size;
            for (int r = 0; r < size; r++)
            {
                for (int c = 0; c < size; c++)
                {
                    var cell = Runner.CurrentBoard.Cells[r, c];
                    if (cell == null) continue;

                    bool isValueCell = cell.Value.HasValue;
                    if (isValueCell  && !valueCells)     continue;
                    if (!isValueCell && !candidateCells) continue;

                    // Remove the colour from every digit entry; clean up empty sets.
                    var keys = new List<int>(cell.DigitColors.Keys);
                    foreach (int digit in keys)
                    {
                        if (cell.DigitColors.TryGetValue(digit, out var colours))
                        {
                            colours.Remove(colour);
                            if (colours.Count == 0)
                            {
                                cell.DigitColors.Remove(digit);
                            }
                        }
                    }
                }
            }
        }
    }
}
