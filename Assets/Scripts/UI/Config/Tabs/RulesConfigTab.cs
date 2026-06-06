using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using Sudoku.Solver;
using Sudoku.Solver.Rules;
using Sudoku.UI.Panels;

namespace Sudoku.UI.Config
{
    /**
     * Configuration tab for managing which Sudoku solving rules are enabled.
     * Displays a toggle for each registered rule and updates the rule registry
     * when toggles are changed.
     *
     * This tab reuses rule toggle logic from RuleTogglePanel but adapted
     * for integration into the tabbed ConfigPanel system.
     */
    public class RulesConfigTab : ConfigTab
    {
        private SolverRunner _runner;
        private RuleRegistry _registry;
        private ApplyRulePanel _applyRulePanel;
        // Cached content root so OnEnabledChanged can update visuals without a full rebuild.
        private RectTransform _contentRoot;

        public SolverRunner Runner
        {
            get { return _runner; }
            set { _runner = value; }
        }

        public void Awake()
        {
            // Default tab name; caller may override before RegisterTab().
            if (string.IsNullOrEmpty(TabName))
                TabName = "Rules";
        }

        public override void PopulateContent(RectTransform contentRoot)
        {
            _contentRoot = contentRoot;

            if (_runner == null)
            {
                _runner = FindAnyObjectByType<SolverRunner>();
            }

            if (_runner == null)
            {
                CreateErrorMessage(contentRoot, "No SolverRunner found in scene");
                return;
            }

            _runner.EnsureEngine();

            // Unsubscribe from any previous registry before acquiring the new one.
            if (_registry != null)
                _registry.OnEnabledChanged -= OnRegistryRuleEnabledChanged;

            _registry = _runner.Registry;

            if (_registry == null)
            {
                CreateErrorMessage(contentRoot, "Rule registry unavailable");
                return;
            }

            _applyRulePanel = FindAnyObjectByType<ApplyRulePanel>();

            // Build rule toggles
            var rules = new List<(ISudokuRule rule, bool enabled)>();
            try
            {
                rules = _registry.GetRulesWithStatus();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"RulesConfigTab: Failed to get rules: {ex.Message}");
                CreateErrorMessage(contentRoot, "Failed to load rules");
                return;
            }

            if (rules.Count == 0)
            {
                CreateErrorMessage(contentRoot, "No rules available");
                return;
            }

            // Create a toggle for each rule
            foreach (var (rule, enabled) in rules)
            {
                CreateRuleToggle(contentRoot, rule, enabled);
            }

            // Subscribe so this panel stays in sync when puzzle-mode toggles change the registry.
            _registry.OnEnabledChanged += OnRegistryRuleEnabledChanged;
        }

        public override void OnTabActivated()
        {
            // Re-read current state when switching back to this tab after external changes.
            if (_registry == null || _contentRoot == null) return;
            foreach (var (rule, enabled) in _registry.GetRulesWithStatus())
            {
                SyncToggleVisual(rule.GetType().Name, enabled);
            }
        }

        public override void OnTabDeactivated()
        {
            // Unsubscribe when hidden so we don't receive spurious updates
            // while content may be destroyed and repopulated.
            if (_registry != null)
                _registry.OnEnabledChanged -= OnRegistryRuleEnabledChanged;
        }

        private void OnDestroy()
        {
            if (_registry != null)
                _registry.OnEnabledChanged -= OnRegistryRuleEnabledChanged;
        }

        /**
         * Called when the registry's enabled state changes externally (e.g. puzzle-mode toggle).
         * Updates the matching toggle visual without firing its onValueChanged.
         *
         * @param ruleTypeName The type name of the changed rule.
         * @param enabled      The new enabled state.
         */
        private void OnRegistryRuleEnabledChanged(string ruleTypeName, bool enabled)
        {
            SyncToggleVisual(ruleTypeName, enabled);
        }

        /**
         * Find the toggle row for a rule by name and update its visual state silently.
         *
         * @param ruleTypeName The type name suffix used to find the row GO.
         * @param enabled      The desired enabled state.
         */
        private void SyncToggleVisual(string ruleTypeName, bool enabled)
        {
            if (_contentRoot == null) return;

            var rowTransform    = _contentRoot.Find(ruleTypeName + "_Toggle");
            if (rowTransform    == null) return;

            var toggleTransform = rowTransform.Find("Toggle");
            if (toggleTransform == null) return;

            var toggle = toggleTransform.GetComponent<Toggle>();
            if (toggle == null) return;

            // Update visual without triggering onValueChanged.
            toggle.SetIsOnWithoutNotify(enabled);
            if (toggle.graphic != null)
            {
                toggle.graphic.gameObject.SetActive(true);
                toggle.graphic.color = new Color(1f, 1f, 1f, enabled ? 1f : 0f);
            }
        }

        /**
         * Create a toggle row for a single rule.
         * 
         * @param parent The parent transform to add the toggle to.
         * @param rule The sudoku rule to create a toggle for.
         * @param enabled Whether the rule is currently enabled.
         */
        private void CreateRuleToggle(Transform parent, ISudokuRule rule, bool enabled)
        {
            var ruleGO = new GameObject(rule.GetType().Name + "_Toggle", typeof(RectTransform));
            ruleGO.transform.SetParent(parent, false);

            var le = ruleGO.AddComponent<LayoutElement>();
            le.minWidth = 0f;
            le.preferredWidth = -1f;
            le.flexibleWidth = 1f;
            le.preferredHeight = 36f;

            var h = ruleGO.AddComponent<HorizontalLayoutGroup>();
            h.childForceExpandHeight = false;
            h.childControlWidth = true;
            h.childForceExpandWidth = false;
            h.spacing = 6f;

            var rowBg = ruleGO.AddComponent<Image>();
            rowBg.color = new Color(0f, 0f, 0f, 0.001f);
            rowBg.raycastTarget = true;

            var rowButton = ruleGO.AddComponent<Button>();
            rowButton.targetGraphic = rowBg;
            rowButton.transition = Selectable.Transition.None;

            // Create toggle checkbox
            var toggleGO = new GameObject("Toggle", typeof(RectTransform));
            toggleGO.transform.SetParent(ruleGO.transform, false);

            var toggle = toggleGO.AddComponent<Toggle>();
            var bgImg = toggleGO.AddComponent<Image>();
            var toggleRect = toggleGO.GetComponent<RectTransform>();

            toggleRect.sizeDelta = new Vector2(30f, 30f);

            var toggleLE = toggleGO.AddComponent<LayoutElement>();
            toggleLE.preferredWidth = 30f;
            toggleLE.preferredHeight = 30f;

            bgImg.color = new Color(1f, 1f, 1f, 0.06f);
            toggle.targetGraphic = bgImg;
            toggle.interactable = true;
            bgImg.raycastTarget = true;

            // Create checkmark
            var checkMarkGO = new GameObject("Checkmark", typeof(RectTransform));
            checkMarkGO.transform.SetParent(toggleGO.transform, false);

            var ckText = checkMarkGO.AddComponent<Text>();
            ckText.text = "✓";
            ckText.font = GetSafeBuiltinFont("Arial.ttf");
            ckText.fontSize = 18;
            ckText.color = Color.white;
            ckText.alignment = TextAnchor.MiddleCenter;
            ckText.raycastTarget = false;

            var ckRect = checkMarkGO.GetComponent<RectTransform>();
            ckRect.sizeDelta = new Vector2(22f, 22f);
            toggle.graphic = ckText;

            // Create label
            var labelGO = new GameObject("Label", typeof(RectTransform));
            labelGO.transform.SetParent(ruleGO.transform, false);

            var label = labelGO.AddComponent<Text>();
            var lblTxt = SplitPascalCase(rule.Name ?? "");
            if (string.IsNullOrEmpty(lblTxt))
                lblTxt = rule.GetType().Name;

            label.text = lblTxt;
            label.font = GetSafeBuiltinFont("Arial.ttf");
            label.fontSize = 16;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleLeft;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 10;
            label.resizeTextMaxSize = 16;
            label.raycastTarget = true;

            var labelRT = labelGO.GetComponent<RectTransform>();
            labelRT.anchorMin = new Vector2(0f, 0f);
            labelRT.anchorMax = new Vector2(1f, 1f);
            labelRT.pivot = new Vector2(0.5f, 0.5f);
            labelRT.offsetMin = new Vector2(8f, 2f);
            labelRT.offsetMax = new Vector2(-8f, -2f);

            var labelLE = labelGO.AddComponent<LayoutElement>();
            labelLE.flexibleWidth = 1f;

            // Setup toggle state and listeners
            toggle.toggleTransition = Toggle.ToggleTransition.None;
            toggle.isOn = enabled;

            string ruleTypeName = rule.GetType().Name;
            toggle.onValueChanged.AddListener((val) =>
            {
                _registry.SetEnabled(ruleTypeName, val);
                _runner?.HandleRuleToggleChanged(ruleTypeName, val);
                RefreshApplyRulesPanel();

                // Update checkmark visibility
                if (toggle.graphic != null)
                {
                    toggle.graphic.gameObject.SetActive(true);
                    toggle.graphic.color = new Color(1f, 1f, 1f, val ? 1f : 0f);
                }
            });

            // Allow clicking anywhere on the row to toggle
            rowButton.onClick.AddListener(() => { toggle.isOn = !toggle.isOn; });

            // Set initial checkmark state
            if (toggle.graphic != null)
            {
                toggle.graphic.gameObject.SetActive(true);
                toggle.graphic.color = new Color(1f, 1f, 1f, enabled ? 1f : 0f);
            }
        }

        /**
         * Create an error message in the content area.
         * 
         * @param parent The parent transform to add the message to.
         * @param message The error message to display.
         */
        private void CreateErrorMessage(Transform parent, string message)
        {
            var msgGO = new GameObject("ErrorMessage", typeof(RectTransform));
            msgGO.transform.SetParent(parent, false);

            var msgText = msgGO.AddComponent<Text>();
            msgText.text      = message;
            msgText.font      = GetSafeBuiltinFont("Arial.ttf");
            msgText.fontSize  = 14;
            msgText.color     = Color.yellow;
            msgText.alignment = TextAnchor.MiddleCenter;
            msgText.raycastTarget = false;

            var msgLE = msgGO.AddComponent<LayoutElement>();
            msgLE.preferredHeight = 40f;
            msgLE.flexibleWidth   = 1f;
        }

        /**
         * Convert PascalCase text to space-separated words.
         * 
         * @param input The PascalCase string to convert.
         * @returns The space-separated string.
         */
        private string SplitPascalCase(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            var withSpaces = Regex.Replace(input, "(?<!^)(?=[A-Z][a-z])", " ");
            withSpaces = Regex.Replace(withSpaces, "(?<!^)(?=[A-Z]{2,})", " ");
            return withSpaces.Replace('_', ' ');
        }

        /**
         * Get a builtin Unity font, with fallback if the preferred one isn't available.
         * 
         * @param preferred The preferred font name (e.g., "Arial.ttf").
         * @returns The resolved font, or null if unavailable.
         */
        private Font GetSafeBuiltinFont(string preferred)
        {
            Font f = null;
            try
            {
                f = Resources.GetBuiltinResource<Font>(preferred);
            }
            catch
            {
                // Ignore and fall back to LegacyRuntime.ttf below.
            }

            if (f == null)
            {
                try
                {
                    f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
                catch { }
            }

            return f;
        }

        /**
         * Refresh the ApplyRulePanel if it exists so it reflects the current rule state.
         */
        private void RefreshApplyRulesPanel()
        {
            if (_applyRulePanel != null)
            {
                _applyRulePanel.RefreshList();
            }
        }
    }
}
