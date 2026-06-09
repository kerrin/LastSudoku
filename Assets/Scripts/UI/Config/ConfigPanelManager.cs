using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using Sudoku.Solver;
using Sudoku.Solver.Rules;
using Sudoku.UI.Config;
using Sudoku.UI.Panels;

namespace Sudoku.UI
{
    /**
     * Manages the configuration panel overlay.
     *
     * Rendered entirely via IMGUI so it always appears on top of the board,
     * which is also drawn with IMGUI (OnGUI). GUI.depth = -50000 ensures this
     * panel is composited after all other OnGUI callers (lower depth = drawn last
     * = visually on top).
     *
     * No uGUI components are created; all interaction is handled through standard
     * IMGUI controls (GUI.Toggle, GUI.Button, GUI.BeginScrollView, etc.).
     */
    public class ConfigPanelManager : MonoBehaviour
    {
        private enum ConfigTabId
        {
            Rules = 0,
            Assistance = 1,
            Generation = 2,
        }

        private static ConfigPanelManager _instance;

        private bool    _isOpen    = false;
        private Vector2 _scrollPos = Vector2.zero;
        private readonly System.Collections.Generic.List<(GraphicRaycaster raycaster, bool wasEnabled)> _raycasterStates
            = new System.Collections.Generic.List<(GraphicRaycaster raycaster, bool wasEnabled)>();

        // Runtime references resolved on open.
        private SolverRunner   _runner;
        private RuleRegistry   _registry;
        private ApplyRulePanel _applyRulePanel;

        // Cached GUIStyles — built once inside OnGUI where GUI.skin is valid.
        private GUIStyle _titleStyle;
        private GUIStyle _closeBtnStyle;
        private GUIStyle _tabBoxStyle;
        private GUIStyle _ruleNameStyle;
        private GUIStyle _toggleStyle;
        private GUIStyle _scrollBgStyle;
        private GUIStyle _assistanceSectionStyle;
        private bool     _stylesBuilt;
        private ConfigTabId _activeTab = ConfigTabId.Rules;

        // Panel layout constants.
        private const float PanelW  = 500f;
        private const float PanelH  = 580f;
        private const float HeaderH = 50f;
        private const float TabBarH = 38f;
        private const float RowH    = 42f;

        // ─── Lifecycle ───────────────────────────────────────────────────────

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        // ─── Public API ──────────────────────────────────────────────────────

        /**
         * Open the configuration panel.
         */
        public void OpenConfigPanel()
        {
            if (_isOpen) return;
            ResolveReferences();
            SetUnderlyingUiInputEnabled(false);
            _isOpen = true;
        }

        /**
         * Close the configuration panel.
         */
        public void CloseConfigPanel()
        {
            SetUnderlyingUiInputEnabled(true);
            _isOpen = false;
        }

        private void OnDestroy()
        {
            // Safety: if this object is destroyed while open, restore any disabled raycasters.
            SetUnderlyingUiInputEnabled(true);
        }

        // ─── Input ───────────────────────────────────────────────────────────

        private void Update()
        {
            if (!_isOpen) return;
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                CloseConfigPanel();
            }
        }

        // ─── IMGUI Rendering ─────────────────────────────────────────────────

        private void OnGUI()
        {
            if (!_isOpen) return;

            // Lower GUI.depth value = drawn later = visually on top.
            // BoardVisualizer uses default depth (0); -50000 puts us above it.
            GUI.depth = -50000;

            EnsureStyles();

            float sw = Screen.width;
            float sh = Screen.height;

            // Scale panel to fit smaller screens.
            float pw = Mathf.Min(PanelW, sw * 0.92f);
            float ph = Mathf.Min(PanelH, sh * 0.92f);
            float px = (sw - pw) * 0.5f;
            float py = (sh - ph) * 0.5f;
            var panelRect = new Rect(px, py, pw, ph);

            // ── Semi-transparent backdrop ─────────────────────────────────────
            Color prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.72f);
            GUI.DrawTexture(new Rect(0f, 0f, sw, sh), Texture2D.whiteTexture, ScaleMode.StretchToFill, alphaBlend: true);
            GUI.color = prev;

            // ── Close when clicking outside the panel ─────────────────────────
            // Set a flag and close after GUILayout groups are ended to avoid
            // mismatched BeginArea/EndArea when returning early.
            bool closeRequested = false;
            if (Event.current.type == EventType.MouseDown &&
                !panelRect.Contains(Event.current.mousePosition))
            {
                closeRequested = true;
                Event.current.Use();
            }

            // ── Panel background ──────────────────────────────────────────────
            GUI.color = new Color(0.10f, 0.10f, 0.16f, 1f);
            GUI.DrawTexture(panelRect, Texture2D.whiteTexture);
            GUI.color = prev;

            // ── Panel content ─────────────────────────────────────────────────
            GUILayout.BeginArea(panelRect);
            bool closedViaButton = DrawPanelContent(pw);
            GUILayout.EndArea();

            if (closeRequested || closedViaButton)
            {
                CloseConfigPanel();
            }
        }

        // ─── Panel Content ────────────────────────────────────────────────────

        /**
         * Draw the full panel interior (header, tab bar, rule toggles).
         *
         * @param panelWidth Width of the panel area in pixels.
         * @returns True when the close button was clicked.
         */
        private bool DrawPanelContent(float panelWidth)
        {
            bool closeClicked = false;

            // ── Header ────────────────────────────────────────────────────────
            Color prev = GUI.color;
            GUI.color = new Color(0.07f, 0.07f, 0.12f, 1f);
            GUI.DrawTexture(new Rect(0f, 0f, panelWidth, HeaderH), Texture2D.whiteTexture);
            GUI.color = prev;

            GUILayout.BeginHorizontal(GUILayout.Height(HeaderH));
            GUILayout.Space(14f);
            GUILayout.Label("\u2699  Configuration", _titleStyle, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("\u00d7", _closeBtnStyle, GUILayout.Width(36f), GUILayout.Height(36f)))
            {
                closeClicked = true;
            }
            GUILayout.Space(6f);
            GUILayout.EndHorizontal();

            // ── Tab bar ───────────────────────────────────────────────────────
            GUI.color = new Color(0.07f, 0.07f, 0.12f, 1f);
            GUI.DrawTexture(new Rect(0f, HeaderH, panelWidth, TabBarH), Texture2D.whiteTexture);
            GUI.color = prev;

            GUILayout.BeginHorizontal(GUILayout.Height(TabBarH));
            GUILayout.Space(10f);
            DrawTabButton(ConfigTabId.Rules, "Rules", 84f);
            GUILayout.Space(6f);
            DrawTabButton(ConfigTabId.Assistance, "Assistance", 108f);
            GUILayout.Space(6f);
            DrawTabButton(ConfigTabId.Generation, "Generation", 108f);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            // ── Separator ─────────────────────────────────────────────────────
            GUI.color = new Color(0.22f, 0.22f, 0.35f, 1f);
            GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(1f));
            GUI.color = prev;

            // ── Scrollable rule toggles ───────────────────────────────────────
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, _scrollBgStyle, GUILayout.ExpandHeight(true));
            DrawActiveTabContent();
            GUILayout.EndScrollView();

            GUILayout.Space(6f);
            return closeClicked;
        }

        /**
         * Draw a tab-button-like control in the config tab bar.
         *
         * @param tab Tab identifier for selection state.
         * @param label Visible tab title.
         * @param width Preferred tab button width.
         */
        private void DrawTabButton(ConfigTabId tab, string label, float width)
        {
            var style = tab == _activeTab ? _tabBoxStyle : GUI.skin.button;
            if (GUILayout.Button(label, style, GUILayout.Width(width), GUILayout.Height(TabBarH - 8f)))
            {
                _activeTab = tab;
                _scrollPos = Vector2.zero;
            }
        }

        /**
         * Draw the currently selected tab content area.
         */
        private void DrawActiveTabContent()
        {
            if (_activeTab == ConfigTabId.Assistance)
            {
                DrawAssistanceOptions();
                return;
            }

            if (_activeTab == ConfigTabId.Generation)
            {
                DrawGenerationOptions();
                return;
            }

            DrawRuleToggles();
        }

        /**
         * Draw one row per registered rule with a toggle checkbox.
         */
        private void DrawRuleToggles()
        {
            if (_registry == null)
            {
                GUILayout.Space(8f);
                GUILayout.Label("  No rule registry found.", _ruleNameStyle);
                return;
            }

            var rules = _registry.GetRulesWithStatus();
            if (rules.Count == 0)
            {
                GUILayout.Space(8f);
                GUILayout.Label("  No rules available.", _ruleNameStyle);
                return;
            }

            GUILayout.Space(4f);
            for (int i = 0; i < rules.Count; i++)
            {
                var (rule, enabled) = rules[i];
                string typeName    = rule.GetType().Name;
                string displayName = SplitPascalCase(rule.Name ?? typeName);

                // Subtle alternating row tint drawn behind the toggle.
                if (i % 2 == 0)
                {
                    Color prev = GUI.color;
                    GUI.color = new Color(1f, 1f, 1f, 0.05f);
                    var rowRect = GUILayoutUtility.GetRect(0f, RowH, GUILayout.ExpandWidth(true));
                    GUI.DrawTexture(rowRect, Texture2D.whiteTexture);
                    GUI.color = prev;

                    // Draw the toggle control on top of the tinted background.
                    var toggleRect = new Rect(rowRect.x + 10f, rowRect.y + (RowH - 22f) * 0.5f, rowRect.width - 20f, 22f);
                    bool newEnabledA = GUI.Toggle(toggleRect, enabled, "  " + displayName, _toggleStyle);
                    ApplyToggleChange(typeName, enabled, newEnabledA);
                    continue;
                }

                GUILayout.BeginHorizontal(GUILayout.Height(RowH));
                GUILayout.Space(10f);
                bool newEnabled = GUILayout.Toggle(enabled, "  " + displayName, _toggleStyle, GUILayout.ExpandWidth(true));
                GUILayout.Space(8f);
                GUILayout.EndHorizontal();

                ApplyToggleChange(typeName, enabled, newEnabled);
            }

            GUILayout.Space(6f);
        }

        /**
         * Draw Assistance settings and keep this section extensible for future options.
         */
        private void DrawAssistanceOptions()
        {
            GUILayout.Space(8f);
            GUILayout.BeginVertical(_assistanceSectionStyle);
            GUILayout.Space(4f);

            bool hideApplyRules = GUILayout.Toggle(
                AssistanceSettings.HideApplyRules,
                "Hide Apply Rules",
                _toggleStyle,
                GUILayout.Height(28f),
                GUILayout.ExpandWidth(true));

            if (hideApplyRules != AssistanceSettings.HideApplyRules)
            {
                AssistanceSettings.HideApplyRules = hideApplyRules;
                RefreshBoardSidePanelVisibility();
            }

            bool autoCandidateOnSetValue = GUILayout.Toggle(
                AssistanceSettings.AutoCandidateOnSetValue,
                "Enable Auto Candidate on set Value",
                _toggleStyle,
                GUILayout.Height(28f),
                GUILayout.ExpandWidth(true));

            if (autoCandidateOnSetValue != AssistanceSettings.AutoCandidateOnSetValue)
            {
                AssistanceSettings.AutoCandidateOnSetValue = autoCandidateOnSetValue;
            }

            // Reserve visual room for upcoming Assistance options.
            GUILayout.Space(16f);
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();
        }

        /**
         * Draw generation settings that affect random puzzle construction.
         */
        private void DrawGenerationOptions()
        {
            GUILayout.Space(8f);
            GUILayout.BeginVertical(_assistanceSectionStyle);
            GUILayout.Space(4f);

            bool useRotationalSymmetry = GUILayout.Toggle(
                GenerationSettings.UseRotationalSymmetry,
                "Use Rotational Symmetry",
                _toggleStyle,
                GUILayout.Height(28f),
                GUILayout.ExpandWidth(true));

            if (useRotationalSymmetry != GenerationSettings.UseRotationalSymmetry)
            {
                GenerationSettings.UseRotationalSymmetry = useRotationalSymmetry;
            }

            GUILayout.Space(16f);
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();
        }

        /**
         * Apply a rule enabled-state change if the value differs.
         *
         * @param typeName   Rule type name.
         * @param oldEnabled Previous state.
         * @param newEnabled Desired new state.
         */
        private void ApplyToggleChange(string typeName, bool oldEnabled, bool newEnabled)
        {
            if (newEnabled == oldEnabled) return;
            _registry.SetEnabled(typeName, newEnabled);
            _runner?.HandleRuleToggleChanged(typeName, newEnabled);
            RefreshApplyRulesPanel();
            RefreshCreateModeStatusPanels();
        }

        // ─── Helpers ─────────────────────────────────────────────────────────

        private void ResolveReferences()
        {
            if (_runner == null)
            {
                _runner = Object.FindAnyObjectByType<SolverRunner>();
            }

            if (_runner != null && _registry == null)
            {
                _runner.EnsureEngine();
                _registry = _runner.Registry;
            }

            if (_applyRulePanel == null)
            {
                _applyRulePanel = Object.FindAnyObjectByType<ApplyRulePanel>();
            }
        }

        private void RefreshApplyRulesPanel()
        {
            if (_applyRulePanel != null && _applyRulePanel.gameObject != null && _applyRulePanel.gameObject.activeInHierarchy)
            {
                _applyRulePanel.RefreshList();
            }
        }

        /**
         * Force all side panels to reevaluate visibility after Assistance toggle changes.
         */
        private static void RefreshBoardSidePanelVisibility()
        {
            var sidePanels = Resources.FindObjectsOfTypeAll<BoardSidePanel>();
            for (int i = 0; i < sidePanels.Length; i++)
            {
                var sidePanel = sidePanels[i];
                if (sidePanel == null)
                {
                    continue;
                }

                var go = sidePanel.gameObject;
                if (go == null || !go.scene.IsValid() || !go.scene.isLoaded)
                {
                    continue;
                }

                sidePanel.RefreshPanelVisibilityForCurrentMode();
            }
        }

        private static void RefreshCreateModeStatusPanels()
        {
            var panels = Resources.FindObjectsOfTypeAll<CreateModeStatusPanel>();
            for (int i = 0; i < panels.Length; i++)
            {
                var panel = panels[i];
                if (panel == null) continue;
                var go = panel.gameObject;
                if (go == null || !go.scene.IsValid() || !go.scene.isLoaded) continue;
                panel.RefreshStatus(force: true);
            }
        }

        /**
         * Enable or disable uGUI raycasters so clicks don't pass through the IMGUI config panel.
         *
         * @param enabled True to restore previous raycaster states; false to disable all active raycasters.
         */
        private void SetUnderlyingUiInputEnabled(bool enabled)
        {
            if (enabled)
            {
                for (int i = 0; i < _raycasterStates.Count; i++)
                {
                    var entry = _raycasterStates[i];
                    if (entry.raycaster != null)
                    {
                        entry.raycaster.enabled = entry.wasEnabled;
                    }
                }

                _raycasterStates.Clear();
                return;
            }

            _raycasterStates.Clear();
            var raycasters = Resources.FindObjectsOfTypeAll<GraphicRaycaster>();
            for (int i = 0; i < raycasters.Length; i++)
            {
                var raycaster = raycasters[i];
                if (raycaster == null)
                {
                    continue;
                }

                var go = raycaster.gameObject;
                if (go == null || !go.scene.IsValid() || !go.scene.isLoaded)
                {
                    continue;
                }

                _raycasterStates.Add((raycaster, raycaster.enabled));
                raycaster.enabled = false;
            }
        }

        /**
         * Split a PascalCase or camelCase identifier into space-separated words.
         *
         * @param input  Source string.
         * @returns Human-readable label.
         */
        private static string SplitPascalCase(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            var s = Regex.Replace(input, "(?<!^)(?=[A-Z][a-z])", " ");
            s = Regex.Replace(s, "(?<!^)(?=[A-Z]{2,})", " ");
            return s.Replace('_', ' ');
        }

        // ─── GUIStyle Construction ────────────────────────────────────────────

        /**
         * Build and cache GUIStyles on the first OnGUI call.
         * Must be called from within OnGUI so GUI.skin is valid.
         */
        private void EnsureStyles()
        {
            if (_stylesBuilt) return;
            _stylesBuilt = true;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
            };
            _titleStyle.normal.textColor = Color.white;

            _closeBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize  = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            _closeBtnStyle.normal.textColor = Color.white;
            _closeBtnStyle.hover.textColor  = Color.white;
            _closeBtnStyle.active.textColor = Color.white;

            _tabBoxStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize  = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            _tabBoxStyle.normal.textColor = Color.white;

            _ruleNameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 15,
                alignment = TextAnchor.MiddleLeft,
            };
            _ruleNameStyle.normal.textColor = new Color(0.92f, 0.92f, 0.92f, 1f);

            _toggleStyle = new GUIStyle(GUI.skin.toggle)
            {
                fontSize  = 15,
                alignment = TextAnchor.MiddleLeft,
            };
            _toggleStyle.normal.textColor  = new Color(0.92f, 0.92f, 0.92f, 1f);
            _toggleStyle.onNormal.textColor = Color.white;
            _toggleStyle.hover.textColor    = Color.white;
            _toggleStyle.onHover.textColor  = Color.white;

            _scrollBgStyle = new GUIStyle(GUI.skin.scrollView);

            _assistanceSectionStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(10, 10, 10, 10)
            };
            _assistanceSectionStyle.normal.textColor = Color.white;
        }
    }
}
