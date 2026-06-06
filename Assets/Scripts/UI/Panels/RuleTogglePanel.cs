using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text.RegularExpressions;
using UnityEngine.EventSystems;
using Sudoku.Solver;
using Sudoku.Solver.Rules;

namespace Sudoku.UI.Panels
{

/** 
 * Runtime UI panel that creates a toggle for each registered rule so the
 * user can enable/disable rules without modifying code. Attach this to a
 * GameObject in the scene; it will create a Canvas if none exists.
 */
public class RuleTogglePanel : MonoBehaviour
{
    public SolverRunner Runner;
 
    [Tooltip("Optional: maximum height before the panel becomes scrollable")]
    public float MaxHeight = 400f;

    [Tooltip("Optional: maximum width before the panel becomes scrollable")]
    public float MaxWidth = 280f;

    /** When true, the parent layout group (e.g. an HLG in BoardSidePanel) controls
     *  this panel's width. FinalizeLayout will not impose a fixed minWidth so the
     *  panel can flex-fill its column correctly. Set by BoardSidePanel at runtime. */
    [HideInInspector]
    public bool FlexFillParent = false;

    [Tooltip("Padding in pixels to inset the panel when hosted inside the SidePanel")]
    public float Padding = 8f;

    [Tooltip("When hosted in the SidePanel, an offset (x right, y down) from the SidePanel top-left to place the panel root.\nUse this to treat SidePanel top-left as origin (0,0).")]
    public Vector2 SidePanelOffset = Vector2.zero;

    private RuleRegistry _registry;
    private ApplyRulePanel _applyRulePanel;
    private CreateModeStatusPanel _createModeStatusPanel;
    // Cached reference to the Content transform so external state changes
    // can update toggle visuals without rebuilding the whole list.
    private Transform _togglesParent;

    /**
     * Simplified Start: Validate runner/registry, build panel, then finalize layout.
     */
    private System.Collections.IEnumerator Start()
    {
        if (!InitRunnerAndRegistry()) yield break;

        var panelRootGO = this.gameObject;
        var panelRootRT = EnsureRectTransform(panelRootGO);
        if (panelRootRT == null)
        {
            Debug.LogWarning("RuleTogglePanel: Missing RectTransform on panel root.");
            yield break;
        }

        float rectHeight, rectWidth;
        ConfigurePanelRoot(panelRootRT, out rectHeight, out rectWidth);
        ConfigureAppearance(panelRootGO, rectHeight, rectWidth);

        var togglesParent = EnsureTogglesContainer(panelRootGO);
        if (togglesParent == null)
        {
            Debug.LogWarning("RuleTogglePanel: Expected designer hierarchy under RuleTogglePanel was not found.");
            yield break;
        }

        SetupScrollRect(togglesParent);

        // Remove designer-time objects under any Viewport child (keep Content)
        var viewport = FindChildRecursive(panelRootGO.transform, "Viewport");
        if (viewport != null)
        {
            for (int i = viewport.childCount - 1; i >= 0; --i)
            {
                var child = viewport.GetChild(i);
                if (child.name == "Content") continue;
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }
        }

        _togglesParent = togglesParent;
        // Subscribe so this panel stays in sync when another UI (e.g. ConfigPanel)
        // changes a rule's enabled state through the registry.
        _registry.OnEnabledChanged += OnRegistryRuleEnabledChanged;

        var expectedNames = BuildToggles(togglesParent);
        CleanupExtraToggles(togglesParent, expectedNames);
        // Reorder children to match registry insertion order so both panels
        // display rules in the same sequence regardless of designer placement.
        ReorderTogglesToRegistryOrder(togglesParent);

        // Allow layout to settle then finalize horizontal sizing
        yield return null;
        FinalizeLayout(panelRootGO, panelRootRT, rectWidth);
        AlignWithSidePanel(panelRootGO, panelRootRT);
    }

    // --- Helper methods ---

    /**
     * Ensures the ScrollArea -> ViewPort -> Content hierarchy is fully wired up:
     * adds a ScrollRect to ScrollArea if missing, adds RectMask2D to ViewPort for
     * content clipping, and builds a vertical scrollbar that auto-hides when content
     * fits entirely within the panel.
     *
     * @param contentTransform The Content RectTransform returned by EnsureTogglesContainer.
     */
    private void SetupScrollRect(Transform contentTransform)
    {
        // Navigate upward from Content to reach ViewPort then ScrollArea.
        var vpTransform     = contentTransform?.parent;
        var scrollTransform = vpTransform?.parent;
        if (vpTransform == null || scrollTransform == null) return;

        var viewportGO = vpTransform.gameObject;
        var scrollGO   = scrollTransform.gameObject;

        // Viewport: require designer-authored image + clipping mask.
        var vpImg = viewportGO.GetComponent<Image>();
        if (vpImg == null)
        {
            Debug.LogWarning("RuleTogglePanel: Expected Image on ViewPort.");
            return;
        }
        vpImg.raycastTarget = false;
        if (viewportGO.GetComponent<RectMask2D>() == null)
        {
            Debug.LogWarning("RuleTogglePanel: Expected RectMask2D on ViewPort.");
            return;
        }

        // Ensure both rects fill their containers.
        // NOTE: ScrollArea is a direct child of RuleToggles, which has its own VerticalLayoutGroup.
        // That layout group controls ScrollArea's height via ILayoutElement, not via anchors.
        // Add a LayoutElement with flexibleHeight so ScrollArea takes all remaining space.
        var scrollLE = scrollGO.GetComponent<LayoutElement>();
        if (scrollLE == null)
        {
            Debug.LogWarning("RuleTogglePanel: Expected LayoutElement on ScrollArea.");
            return;
        }

        var scrollRT = scrollGO.GetComponent<RectTransform>();
        if (scrollRT == null)
        {
            Debug.LogWarning("RuleTogglePanel: Expected RectTransform on ScrollArea.");
            return;
        }

        var vpRT = viewportGO.GetComponent<RectTransform>();
        if (vpRT == null)
        {
            Debug.LogWarning("RuleTogglePanel: Expected RectTransform on ViewPort.");
            return;
        }

        // Require designer-authored ScrollRect on ScrollArea.
        var scrollRect = scrollGO.GetComponent<ScrollRect>();
        if (scrollRect == null)
        {
            Debug.LogWarning("RuleTogglePanel: Expected ScrollRect on ScrollArea.");
            return;
        }

        scrollRect.horizontal        = false;
        scrollRect.scrollSensitivity = 30f;
        scrollRect.content           = contentTransform.GetComponent<RectTransform>();
        scrollRect.viewport          = vpRT;

        // Fix Content so it spans the viewport width. The rows are laid out
        // inside this RectTransform, so if it keeps a zero width then every row
        // collapses to the toggle child only.
        var contentRT = contentTransform.GetComponent<RectTransform>();
        if (contentRT == null)
        {
            Debug.LogWarning("RuleTogglePanel: Expected RectTransform on Content.");
            return;
        }

        var scrollbarComp = scrollGO.transform.Find("VerticalScrollbar")?.GetComponent<Scrollbar>();
        if (scrollbarComp != null)
        {
            scrollRect.verticalScrollbar = scrollbarComp;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe to prevent stale delegates after this panel is destroyed.
        if (_registry != null)
            _registry.OnEnabledChanged -= OnRegistryRuleEnabledChanged;
    }

    /**
     * Called by the registry when any rule's enabled state changes externally.
     * Updates only the checkmark graphic colour so the visual matches without
     * triggering the toggle's onValueChanged and causing a re-evaluation loop.
     *
     * @param ruleTypeName The type name of the changed rule.
     * @param enabled      The new enabled state.
     */
    private void OnRegistryRuleEnabledChanged(string ruleTypeName, bool enabled)
    {
        if (_togglesParent == null) return;

        var rowTransform    = _togglesParent.Find(ruleTypeName + "_Toggle");
        if (rowTransform    == null) return;

        var toggleTransform = rowTransform.Find("Toggle");
        if (toggleTransform == null) return;

        var toggle = toggleTransform.GetComponent<Toggle>();
        if (toggle == null) return;

        // Update the graphic colour directly so we don't fire onValueChanged
        // (which would call back into the registry and the solver).
        if (toggle.graphic != null)
        {
            toggle.graphic.gameObject.SetActive(true);
            toggle.graphic.color = new Color(1f, 1f, 1f, enabled ? 1f : 0f);
        }

        // Also sync isOn silently by suppressing the listener temporarily.
        toggle.SetIsOnWithoutNotify(enabled);
    }

    private bool InitRunnerAndRegistry()
    {
        if (Runner == null) Runner = FindAnyObjectByType<SolverRunner>();
        if (Runner == null)
        {
            Debug.LogWarning("RuleTogglePanel: No SolverRunner found in scene.");
            return false;
        }

        Runner.EnsureEngine();
        _registry = Runner.Registry;
        if (_registry == null)
        {
            Debug.LogWarning("RuleTogglePanel: Runner did not provide a RuleRegistry.");
            return false;
        }

        return true;
    }

    private RectTransform EnsureRectTransform(GameObject go)
    {
        return go.GetComponent<RectTransform>();
    }

    private void ConfigurePanelRoot(RectTransform panelRootRT, out float rectHeight, out float rectWidth)
    {
        int count = 0;
        try { count = _registry.GetRulesWithStatus().Count; } catch { count = 0; }
        float CalculatedHeight = 34f + 4f + (36f + 2f) * count + 4f;
        rectHeight = Mathf.Min(MaxHeight, CalculatedHeight);
        rectWidth = MaxWidth;
        panelRootRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rectHeight);
        // When FlexFillParent is true the parent HLG controls horizontal size; don't fight it.
        if (!FlexFillParent)
            panelRootRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, rectWidth);
    }

    private void ConfigureAppearance(GameObject panelRootGO, float rectHeight, float rectWidth)
    {
        // Static panel appearance is owned by designer mode.
    }

    private Transform EnsureTogglesContainer(GameObject panelRootGO)
    {
        Transform togglesTrans = panelRootGO.transform.Find("RuleToggles");
        if (togglesTrans == null)
        {
            return null;
        }

        GameObject RuleTogglesGO = togglesTrans.gameObject;

        // Designer-provided Content may be a direct child or nested under a Scroll/Viewport.
        Transform contentTransform = RuleTogglesGO.transform.Find("Content");
        if (contentTransform == null)
        {
            contentTransform = FindChildRecursive(RuleTogglesGO.transform, "Content");
        }

        if (contentTransform == null)
        {
            return null;
        }

        return contentTransform;
    }

    private System.Collections.Generic.HashSet<string> BuildToggles(Transform togglesParent)
    {
        var expectedNames = new System.Collections.Generic.HashSet<string>();
        var rules = new List<(ISudokuRule rule, bool enabled)>();
        try
        {
            rules = _registry.GetRulesWithStatus();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"RuleTogglePanel: GetRulesWithStatus threw: {ex.Message}");
        }

        foreach (var entry in rules)
        {
            string ruleTypeName = entry.rule.GetType().Name;
            string toggleName = ruleTypeName + "_Toggle";
            expectedNames.Add(toggleName);

            var existing = togglesParent.Find(toggleName);
            if (existing != null)
            {
                var existingToggleGO = existing.transform.Find("Toggle");
                if (existingToggleGO != null)
                {
                    var ckGO = existingToggleGO.transform.Find("Checkmark");
                    if (ckGO != null)
                    {
                        var ckTxt = ckGO.GetComponent<Text>();
                        if (ckTxt != null)
                        {
                            ckTxt.raycastTarget = false;
                        }
                    }
                }

                // reuse designer-provided row when possible
                var toggleTransform = existing.transform.Find("Toggle");
                Toggle toggle = null;
                if (toggleTransform != null) toggle = toggleTransform.GetComponent<Toggle>();
                var labelTransform = existing.transform.Find("Label");
                Text label = null;
                TextMeshProUGUI tmpLabel = null;
                if (labelTransform != null)
                {
                    label = labelTransform.GetComponent<Text>();
                    tmpLabel = labelTransform.GetComponent<TextMeshProUGUI>();
                }

                if (label != null) {
                    var txt = SplitPascalCase(entry.rule.Name ?? "");
                    if (string.IsNullOrEmpty(txt)) txt = ruleTypeName;
                    label.text = txt;
                    label.raycastTarget = false;
                }
                else if (tmpLabel != null) {
                    var ttxt = SplitPascalCase(entry.rule.Name ?? "");
                    if (string.IsNullOrEmpty(ttxt)) ttxt = ruleTypeName;
                    tmpLabel.text = ttxt;
                    tmpLabel.raycastTarget = false;
                }

                if (toggle != null)
                {
                    toggle.onValueChanged.RemoveAllListeners();
                    toggle.isOn = entry.enabled;
                    string capturedName = ruleTypeName;
                    toggle.onValueChanged.AddListener((val) =>
                    {
                        _registry.SetEnabled(capturedName, val);
                        Runner?.HandleRuleToggleChanged(capturedName, val);
                        RefreshApplyRulesPanel();
                    });
                    var rowButton = existing.GetComponent<Button>();
                    if (rowButton != null)
                    {
                        rowButton.onClick.RemoveAllListeners();
                        rowButton.onClick.AddListener(() => { toggle.isOn = !toggle.isOn; });
                    }
                }
                else
                {
                    CreateRuleToggle(togglesParent, entry.rule, entry.enabled);
                }
            }
            else
            {
                CreateRuleToggle(togglesParent, entry.rule, entry.enabled);
            }
        }

        return expectedNames;
    }

    private void CleanupExtraToggles(Transform removalParent, System.Collections.Generic.HashSet<string> expectedNames)
    {
        for (int i = removalParent.childCount - 1; i >= 0; --i)
        {
            var child = removalParent.GetChild(i);
            if (child.name == "PlaceholderToggle") continue;
            if (!expectedNames.Contains(child.name))
            {
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }
        }
    }

    /**
     * Reorder the toggle children inside the content container to match the
     * order in which rules appear in the registry. This ensures both the
     * puzzle-mode panel and the config panel display rules in the same sequence
     * regardless of how designer-placed objects were originally ordered.
     *
     * @param togglesParent The Content transform that holds one GO per rule.
     */
    private void ReorderTogglesToRegistryOrder(Transform togglesParent)
    {
        if (_registry == null || togglesParent == null) return;

        var rules = _registry.GetRulesWithStatus();
        for (int i = 0; i < rules.Count; i++)
        {
            string expectedName = rules[i].rule.GetType().Name + "_Toggle";
            var child = togglesParent.Find(expectedName);
            if (child != null)
                child.SetSiblingIndex(i);
        }
    }

    private void FinalizeLayout(GameObject panelRootGO, RectTransform panelRootRT, float rectWidth)
    {
        // In FlexFillParent mode the parent HLG controls our width; do not impose a
        // fixed width or minWidth that would fight the layout system.
        // Also re-apply the full flex LE in case ConfigureAppearance ran before
        // FlexFillParent was set to true by the host (BoardSidePanel).
        var rootLE = panelRootGO.GetComponent<LayoutElement>();
        if (FlexFillParent)
        {
            if (rootLE != null)
            {
                rootLE.preferredWidth  = -1f;
                rootLE.preferredHeight = -1f;
                rootLE.flexibleWidth   = 1f;
                rootLE.flexibleHeight  = 1f;
                rootLE.minWidth        = -1f;
            }
            return;
        }

        panelRootRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, rectWidth);
        if (rootLE != null)
        {
            rootLE.preferredWidth = rectWidth;
            rootLE.minWidth = rectWidth;
        }
    }

    private void AlignWithSidePanel(GameObject panelRootGO, RectTransform panelRootRT)
    {
        if (SidePanelOffset == Vector2.zero) return;
        Transform cur = panelRootGO.transform;
        Transform side = null;
        while (cur != null)
        {
            if (cur.name == "SidePanel") { side = cur; break; }
            cur = cur.parent;
        }
        if (side != null)
        {
            var sideRT = side.GetComponent<RectTransform>();
            var parentRT = panelRootGO.transform.parent as RectTransform;
            if (sideRT != null && parentRT != null)
            {
                Vector3[] corners = new Vector3[4];
                sideRT.GetWorldCorners(corners);
                Vector3 topLeftWorld = corners[1];
                Vector2 topLeftLocal;
                Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, topLeftWorld);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRT, screenPoint, null, out topLeftLocal);
                Vector2 desired = topLeftLocal + new Vector2(SidePanelOffset.x, -SidePanelOffset.y);
                panelRootRT.anchoredPosition = desired;
            }
        }
    }

    private Transform FindChildRecursive(Transform parent, string name)
    {
        if (parent == null) return null;
        for (int i = 0; i < parent.childCount; i++)
        {
            var c = parent.GetChild(i);
            if (string.Equals(c.name, name, System.StringComparison.OrdinalIgnoreCase)) return c;
            var r = FindChildRecursive(c, name);
            if (r != null) return r;
        }
        return null;
    }

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
        // Keep effectively transparent but still raycastable so row click works reliably.
        rowBg.color = new Color(0f, 0f, 0f, 0.001f);
        rowBg.raycastTarget = true;
        var rowButton = ruleGO.AddComponent<Button>();
        rowButton.targetGraphic = rowBg;
        rowButton.transition = Selectable.Transition.None;

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
        // Allow direct clicking on the toggle; row click remains enabled too.
        toggle.interactable = true;
        bgImg.raycastTarget = true;

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

        var labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(ruleGO.transform, false);
        var label = labelGO.AddComponent<Text>();
        var lblTxt = SplitPascalCase(rule.Name ?? "");
        if (string.IsNullOrEmpty(lblTxt)) lblTxt = rule.GetType().Name;
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
        var labelRTRuntime = labelGO.GetComponent<RectTransform>();
        labelRTRuntime.anchorMin = new Vector2(0f, 0f);
        labelRTRuntime.anchorMax = new Vector2(1f, 1f);
        labelRTRuntime.pivot = new Vector2(0.5f, 0.5f);
        labelRTRuntime.offsetMin = new Vector2(8f, 2f);
        labelRTRuntime.offsetMax = new Vector2(-8f, -2f);
        var labelLE = labelGO.AddComponent<LayoutElement>();
        labelLE.flexibleWidth = 1f;

        // Use None transition so PlayEffect never starts a fade coroutine that can
        // conflict with our direct colour management below.
        toggle.toggleTransition = Toggle.ToggleTransition.None;
        toggle.isOn = enabled;
        string ruleTypeName = rule.GetType().Name;
        toggle.onValueChanged.AddListener((val) =>
        {
            _registry.SetEnabled(ruleTypeName, val);
            Runner?.HandleRuleToggleChanged(ruleTypeName, val);
            RefreshApplyRulesPanel();
            // Keep the GO always active; control visibility via alpha.
            if (toggle.graphic != null)
            {
                toggle.graphic.gameObject.SetActive(true);
                toggle.graphic.color = new Color(1f, 1f, 1f, val ? 1f : 0f);
            }
        });
        rowButton.onClick.AddListener(() => { toggle.isOn = !toggle.isOn; });
        // Force the correct visual state immediately via direct colour assignment.
        if (toggle.graphic != null)
        {
            toggle.graphic.gameObject.SetActive(true);
            toggle.graphic.color = new Color(1f, 1f, 1f, enabled ? 1f : 0f);
        }
    }

    private string SplitPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        var withSpaces = Regex.Replace(input, "(?<!^)(?=[A-Z][a-z])", " ");
        withSpaces = Regex.Replace(withSpaces, "(?<!^)(?=[A-Z]{2,})", " ");
        return withSpaces.Replace('_', ' ');
    }

    private Font GetSafeBuiltinFont(string preferred)
    {
        Font f = null;
        try
        {
            f = Resources.GetBuiltinResource<Font>(preferred);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"GetBuiltinResource('{preferred}') failed: {ex.Message}. Trying LegacyRuntime.ttf");
        }

        if (f == null)
        {
            try
            {
                f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"GetBuiltinResource('LegacyRuntime.ttf') failed: {ex.Message}");
            }
        }

        if (f == null)
        {
            try
            {
                f = Font.CreateDynamicFontFromOSFont("Arial", 14);
                if (f != null) Debug.LogWarning($"Using OS font fallback for '{preferred}'.");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"CreateDynamicFontFromOSFont failed: {ex.Message}");
            }
        }

        if (f == null) Debug.LogWarning($"No builtin or OS font available for '{preferred}'. UI text may be invisible.");
        return f;
    }

    private void RefreshApplyRulesPanel()
    {
        if (_applyRulePanel == null)
        {
            _applyRulePanel = FindAnyObjectByType<ApplyRulePanel>();
        }

        if (_createModeStatusPanel == null)
        {
            _createModeStatusPanel = FindAnyObjectByType<CreateModeStatusPanel>();
        }

        if (_applyRulePanel != null && _applyRulePanel.gameObject != null && _applyRulePanel.gameObject.activeInHierarchy)
        {
            _applyRulePanel.RefreshList();
        }

        if (_createModeStatusPanel != null)
        {
            _createModeStatusPanel.RefreshStatus();
        }

        ChangeLogRuntimeControls.RefreshButtonStates();
    }
}
}
