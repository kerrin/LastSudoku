using System.Text.RegularExpressions;
using Sudoku.Models;
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
        private enum PendingColourDisable
        {
            None = 0,
            Green = 1,
            Amber = 2,
            Red = 3,
            Blue = 4,
        }

        private enum ConfigTabId
        {
            Rules = 0,
            Assistance = 1,
            Generation = 2,
            Colours = 3,
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
        private PendingColourDisable _pendingColourDisable = PendingColourDisable.None;
        private bool _showColouringAutoDisableWarning;

        private const string ColouringRuleTypeName = nameof(ColouringRule);
        private const string ForcingChainCellRuleTypeName = nameof(ForcingChainCellRule);
        private const string ForcingChainUnitRuleTypeName = nameof(ForcingChainUnitRule);

        // Panel layout constants.
        private const float PanelW  = 500f;
        private const float PanelH  = 580f;
        private const float HeaderH = 50f;
        private const float TabBarH = 38f;
        private const float RowH    = 42f;
        private static readonly Difficulty[] DifficultyDisplayOrder =
        {
            Difficulty.Easy,
            Difficulty.Medium,
            Difficulty.Hard,
            Difficulty.Expert,
            Difficulty.Master,
            Difficulty.NotImplemented
        };

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

            RuntimeConfigService.EnsureLoaded();
            ResolveReferences();
            RuntimeConfigService.ApplySavedRuleStates(_registry, _runner);
            EnforceColouringRulePrerequisite();
            SetUnderlyingUiInputEnabled(false);
            _isOpen = true;
        }

        /**
         * Close the configuration panel.
         */
        public void CloseConfigPanel()
        {
            SetUnderlyingUiInputEnabled(true);
            CancelPendingColourDisable();
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
            DrawTabButton(ConfigTabId.Rules, "Rules", 80f);
            GUILayout.Space(4f);
            DrawTabButton(ConfigTabId.Assistance, "Assistance", 100f);
            GUILayout.Space(4f);
            DrawTabButton(ConfigTabId.Generation, "Generation", 100f);
            GUILayout.Space(4f);
            DrawTabButton(ConfigTabId.Colours, "Colours", 80f);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            // ── Separator ─────────────────────────────────────────────────────
            GUI.color = new Color(0.22f, 0.22f, 0.35f, 1f);
            GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(1f));
            GUI.color = prev;

            // ── Scrollable tab content ───────────────────────────────────────
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
            switch (_activeTab)
            {
                case ConfigTabId.Rules:
                    DrawRuleToggles();
                    break;
                case ConfigTabId.Assistance:
                    DrawAssistanceOptions();
                    break;
                case ConfigTabId.Generation:
                    DrawGenerationOptions();
                    break;
                case ConfigTabId.Colours:
                    DrawColourOptions();
                    break;
            }            
        }

        /**
         * Draw one row per registered rule with a toggle checkbox.
         */
        private void DrawRuleToggles()
        {
            EnforceColouringRulePrerequisite();

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

            // Group rules by difficulty so we can display them in sections.
            var groupedRules = new System.Collections.Generic.Dictionary<Difficulty, System.Collections.Generic.List<(ISudokuRule rule, bool enabled)>>();
            for (int i = 0; i < rules.Count; i++)
            {
                var entry = rules[i];
                var difficulty = entry.rule.Difficulty;
                if (!groupedRules.TryGetValue(difficulty, out var list))
                {
                    list = new System.Collections.Generic.List<(ISudokuRule rule, bool enabled)>();
                    groupedRules[difficulty] = list;
                }

                list.Add(entry);
            }

            GUILayout.Space(4f);
            int rowIndex = 0;
            for (int i = 0; i < DifficultyDisplayOrder.Length; i++)
            {
                var difficulty = DifficultyDisplayOrder[i];
                if (!groupedRules.TryGetValue(difficulty, out var list) || list.Count == 0)
                {
                    continue;
                }

                list.Sort((a, b) => string.Compare(
                    SplitPascalCase(a.rule.Name ?? a.rule.GetType().Name),
                    SplitPascalCase(b.rule.Name ?? b.rule.GetType().Name),
                    System.StringComparison.OrdinalIgnoreCase));

                GUILayout.Space(6f);
                GUILayout.Label("  " + SplitPascalCase(difficulty.ToString()), _ruleNameStyle);

                for (int j = 0; j < list.Count; j++)
                {
                    var (rule, enabled) = list[j];
                    string typeName = rule.GetType().Name;
                    string displayName = SplitPascalCase(rule.Name ?? typeName);

                    var rowRect = GUILayoutUtility.GetRect(0f, RowH, GUILayout.ExpandWidth(true));
                    if (rowIndex % 2 == 0)
                    {
                        Color prev = GUI.color;
                        GUI.color = new Color(1f, 1f, 1f, 0.05f);
                        GUI.DrawTexture(rowRect, Texture2D.whiteTexture);
                        GUI.color = prev;
                    }

                    var toggleRect = new Rect(rowRect.x + 10f, rowRect.y + (RowH - 22f) * 0.5f, rowRect.width - 20f, 22f);
                    bool colouringPrerequisiteMet = !IsColouringRuleTypeName(typeName) || ColourSettings.GetEnabledColourCount() >= 2;
                    bool previousGuiEnabled = GUI.enabled;
                    Color previousColor = GUI.color;
                    if (!colouringPrerequisiteMet)
                    {
                        GUI.enabled = false;
                        GUI.color = new Color(previousColor.r, previousColor.g, previousColor.b, previousColor.a * 0.45f);
                    }

                    bool newEnabled = GUI.Toggle(toggleRect, enabled, "  " + displayName, _toggleStyle);

                    if (!colouringPrerequisiteMet)
                    {
                        GUI.enabled = previousGuiEnabled;
                        GUI.color = previousColor;

                        var noteRect = new Rect(rowRect.x + 230f, rowRect.y + (RowH - 18f) * 0.5f, rowRect.width - 240f, 18f);
                        GUI.Label(noteRect, "Requires at least 2 colours", _ruleNameStyle);
                    }

                    ApplyToggleChange(typeName, oldEnabled: enabled, newEnabled);
                    rowIndex++;
                }
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

            GUILayout.Label("  Puzzle Start", _ruleNameStyle);
            GUILayout.Space(4f);

            bool autoFillAllCandidatesOnPuzzleStart = GUILayout.Toggle(
                AssistanceSettings.AutoFillAllCandidatesOnPuzzleStart,
                "Auto fill all candidates",
                _toggleStyle,
                GUILayout.Height(28f),
                GUILayout.ExpandWidth(true));

            if (autoFillAllCandidatesOnPuzzleStart != AssistanceSettings.AutoFillAllCandidatesOnPuzzleStart)
            {
                AssistanceSettings.AutoFillAllCandidatesOnPuzzleStart = autoFillAllCandidatesOnPuzzleStart;
                if (!autoFillAllCandidatesOnPuzzleStart)
                {
                    AssistanceSettings.AutoInitialiseCandidatesOnPuzzleStart = false;
                }

                RuntimeConfigService.SaveCurrent(_registry);
            }

            bool canAutoInitialise = AssistanceSettings.AutoFillAllCandidatesOnPuzzleStart;
            bool previousGuiEnabled = GUI.enabled;
            Color previousGuiColor = GUI.color;
            GUI.enabled = canAutoInitialise;
            if (!canAutoInitialise)
            {
                GUI.color = new Color(previousGuiColor.r, previousGuiColor.g, previousGuiColor.b, previousGuiColor.a * 0.45f);
            }

            bool autoInitialiseCandidatesOnPuzzleStart = GUILayout.Toggle(
                AssistanceSettings.AutoInitialiseCandidatesOnPuzzleStart,
                "Auto initialise candidate",
                _toggleStyle,
                GUILayout.Height(28f),
                GUILayout.ExpandWidth(true));

            GUI.enabled = previousGuiEnabled;
            GUI.color = previousGuiColor;

            if (canAutoInitialise && autoInitialiseCandidatesOnPuzzleStart != AssistanceSettings.AutoInitialiseCandidatesOnPuzzleStart)
            {
                AssistanceSettings.AutoInitialiseCandidatesOnPuzzleStart = autoInitialiseCandidatesOnPuzzleStart;
                RuntimeConfigService.SaveCurrent(_registry);
            }

            GUILayout.Space(10f);
            GUILayout.Label("  Solving", _ruleNameStyle);
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
                RuntimeConfigService.SaveCurrent(_registry);
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
                RuntimeConfigService.SaveCurrent(_registry);
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
                RuntimeConfigService.SaveCurrent(_registry);
            }

            bool generateUniqueSolvable = GUILayout.Toggle(
                GenerationSettings.GenerateUniqueSolvable,
                "Generate Unique Solvable",
                _toggleStyle,
                GUILayout.Height(28f),
                GUILayout.ExpandWidth(true));

            if (generateUniqueSolvable != GenerationSettings.GenerateUniqueSolvable)
            {
                GenerationSettings.GenerateUniqueSolvable = generateUniqueSolvable;
                RuntimeConfigService.SaveCurrent(_registry);
            }

            if (!GenerationSettings.GenerateUniqueSolvable)
            {
                GUILayout.Space(8f);
                GUILayout.Label("  Max Allowed Solutions (Non-Unique)", _ruleNameStyle);

                int currentMaxAllowed = GenerationSettings.MaxAllowedSolutionsWhenNonUnique;
                float sliderValue = GUILayout.HorizontalSlider(
                    currentMaxAllowed,
                    GenerationSettings.MinAllowedSolutionsWhenNonUnique,
                    GenerationSettings.MaxAllowedSolutionsWhenNonUniqueLimit,
                    GUILayout.Height(24f),
                    GUILayout.ExpandWidth(true));
                int newMaxAllowed = Mathf.RoundToInt(sliderValue);

                GUILayout.Label($"  {newMaxAllowed}", _ruleNameStyle);

                if (newMaxAllowed != currentMaxAllowed)
                {
                    GenerationSettings.MaxAllowedSolutionsWhenNonUnique = newMaxAllowed;
                    RuntimeConfigService.SaveCurrent(_registry);
                }
            }

            GUILayout.Space(16f);
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();
        }

        /**
         * Draw colour highlighting settings — which pastel annotation colours are available.
         */
        private void DrawColourOptions()
        {
            GUILayout.Space(8f);
            GUILayout.BeginVertical(_assistanceSectionStyle);
            GUILayout.Space(4f);

            GUILayout.Label("  Enabled Highlight Colours", _ruleNameStyle);
            GUILayout.Space(4f);

            DrawColourToggle("Green", ColourSettings.GreenEnabled, PendingColourDisable.Green);
            DrawColourToggle("Amber", ColourSettings.AmberEnabled, PendingColourDisable.Amber);
            DrawColourToggle("Red", ColourSettings.RedEnabled, PendingColourDisable.Red);
            DrawColourToggle("Blue", ColourSettings.BlueEnabled, PendingColourDisable.Blue);

            if (_showColouringAutoDisableWarning)
            {
                GUILayout.Space(8f);
                GUILayout.Label("  Disabling this colour will auto-disable colour-dependent chain rules (requires at least 2 colours).", _ruleNameStyle);
                GUILayout.Space(4f);

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Disable Colour + Rule", GUILayout.Height(28f)))
                {
                    ConfirmColourDisableWithAutoRuleDisable();
                }

                if (GUILayout.Button("Cancel", GUILayout.Height(28f)))
                {
                    CancelPendingColourDisable();
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.Space(16f);
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
        }

        /**
         * Draw and process one highlight-colour toggle.
         *
         * @param label Display label for the toggle.
         * @param currentValue Current enabled state.
         * @param pendingValue Identifier used when a confirmation is required.
         */
        private void DrawColourToggle(string label, bool currentValue, PendingColourDisable pendingValue)
        {
            RuntimeConfigService.EnsureLoaded();

            bool newValue = GUILayout.Toggle(currentValue, "  " + label, _toggleStyle, GUILayout.Height(28f), GUILayout.ExpandWidth(true));
            if (newValue == currentValue)
            {
                return;
            }

            if (!newValue && RequiresColouringRuleAutoDisableWarning())
            {
                _pendingColourDisable = pendingValue;
                _showColouringAutoDisableWarning = true;
                return;
            }

            SetColourEnabled(pendingValue, newValue);
            EnforceColouringRulePrerequisite();
            RuntimeConfigService.SaveCurrent(_registry);
        }

        /**
         * Commit the pending colour disable and auto-disable Colouring rule.
         */
        private void ConfirmColourDisableWithAutoRuleDisable()
        {
            RuntimeConfigService.EnsureLoaded();

            if (_pendingColourDisable == PendingColourDisable.None)
            {
                _showColouringAutoDisableWarning = false;
                return;
            }

            if (IsRuleEnabledByTypeName(ColouringRuleTypeName))
            {
                _registry?.SetEnabled(ColouringRuleTypeName, false);
                _runner?.HandleRuleToggleChanged(ColouringRuleTypeName, false);
            }

            if (IsRuleEnabledByTypeName(ForcingChainCellRuleTypeName))
            {
                _registry?.SetEnabled(ForcingChainCellRuleTypeName, false);
                _runner?.HandleRuleToggleChanged(ForcingChainCellRuleTypeName, false);
            }
            
            if (IsRuleEnabledByTypeName(ForcingChainUnitRuleTypeName))
            {
                _registry?.SetEnabled(ForcingChainUnitRuleTypeName, false);
                _runner?.HandleRuleToggleChanged(ForcingChainUnitRuleTypeName, false);
            }

            RefreshApplyRulesPanel();
            RefreshCreateModeStatusPanels();

            SetColourEnabled(_pendingColourDisable, false);
            CancelPendingColourDisable();
            RuntimeConfigService.SaveCurrent(_registry);
        }

        /**
         * Clear pending warning state without changing any settings.
         */
        private void CancelPendingColourDisable()
        {
            _pendingColourDisable = PendingColourDisable.None;
            _showColouringAutoDisableWarning = false;
        }

        /**
         * Enable the ColourRule only if the minimum enabled colours prerequisite is met.
         */
        private void EnforceColouringRulePrerequisite()
        {
            if (_registry == null)
            {
                return;
            }

            if (ColourSettings.GetEnabledColourCount() >= 2)
            {
                return;
            }

            bool changed = false;

            if (IsRuleEnabledByTypeName(ColouringRuleTypeName))
            {
                _registry.SetEnabled(ColouringRuleTypeName, false);
                _runner?.HandleRuleToggleChanged(ColouringRuleTypeName, false);
                changed = true;
            }

            if (IsRuleEnabledByTypeName(ForcingChainCellRuleTypeName))
            {
                _registry.SetEnabled(ForcingChainCellRuleTypeName, false);
                _runner?.HandleRuleToggleChanged(ForcingChainCellRuleTypeName, false);
                changed = true;
            }

            if (IsRuleEnabledByTypeName(ForcingChainUnitRuleTypeName))
            {
                _registry.SetEnabled(ForcingChainUnitRuleTypeName, false);
                _runner?.HandleRuleToggleChanged(ForcingChainUnitRuleTypeName, false);
                changed = true;
            }

            if (changed)
            {
                RefreshApplyRulesPanel();
                RefreshCreateModeStatusPanels();
            }
        }

        /**
         * Determine whether disabling one more colour will require auto-disabling ColouringRule.
         *
         * @returns True when warning/confirmation is required.
         */
        private bool RequiresColouringRuleAutoDisableWarning()
        {
            return ColourSettings.GetEnabledColourCount() == 2
                && (IsRuleEnabledByTypeName(ColouringRuleTypeName)
                 || IsRuleEnabledByTypeName(ForcingChainCellRuleTypeName)
                 || IsRuleEnabledByTypeName(ForcingChainUnitRuleTypeName));
        }

        /**
         * Check whether the given rule type name is currently enabled.
         *
         * @param typeName Rule type name.
         * @returns True if the rule exists in registry and is enabled.
         */
        private bool IsRuleEnabledByTypeName(string typeName)
        {
            if (_registry == null || string.IsNullOrEmpty(typeName))
            {
                return false;
            }

            var rules = _registry.GetRulesWithStatus();
            for (int i = 0; i < rules.Count; i++)
            {
                var (rule, enabled) = rules[i];
                if (rule != null && string.Equals(rule.GetType().Name, typeName, System.StringComparison.Ordinal))
                {
                    return enabled;
                }
            }

            return false;
        }

        /**
         * Determine whether a type-name references the Colouring rule.
         *
         * @param typeName Rule type name.
         * @returns True for ColouringRule.
         */
        private static bool IsColouringRuleTypeName(string typeName)
        {
            return string.Equals(typeName, ColouringRuleTypeName, System.StringComparison.Ordinal)
                || string.Equals(typeName, ForcingChainCellRuleTypeName, System.StringComparison.Ordinal)
                || string.Equals(typeName, ForcingChainUnitRuleTypeName, System.StringComparison.Ordinal);
        }

        /**
         * Set one highlight colour enabled-state by identifier.
         *
         * @param colourId Colour identifier.
         * @param enabled Desired enabled state.
         */
        private static void SetColourEnabled(PendingColourDisable colourId, bool enabled)
        {
            if (colourId == PendingColourDisable.Green)
            {
                ColourSettings.GreenEnabled = enabled;
                return;
            }

            if (colourId == PendingColourDisable.Amber)
            {
                ColourSettings.AmberEnabled = enabled;
                return;
            }

            if (colourId == PendingColourDisable.Red)
            {
                ColourSettings.RedEnabled = enabled;
                return;
            }

            if (colourId == PendingColourDisable.Blue)
            {
                ColourSettings.BlueEnabled = enabled;
            }
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

            if (newEnabled && IsColouringRuleTypeName(typeName) && ColourSettings.GetEnabledColourCount() < 2)
            {
                return;
            }

            _registry.SetEnabled(typeName, newEnabled);
            _runner?.HandleRuleToggleChanged(typeName, newEnabled);
            RefreshApplyRulesPanel();
            RefreshCreateModeStatusPanels();
            RuntimeConfigService.SaveCurrent(_registry);
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
