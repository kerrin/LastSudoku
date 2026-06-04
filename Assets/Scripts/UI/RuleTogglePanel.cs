using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text.RegularExpressions;
using UnityEngine.EventSystems;
using Sudoku.Solver;
using Sudoku.Solver.Rules;
using Sudoku.Scripts.UI;

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

    /**
     * Simplified Start: Validate runner/registry, build panel, then finalize layout.
     */
    private System.Collections.IEnumerator Start()
    {
        if (!InitRunnerAndRegistry()) yield break;

        var panelRootGO = this.gameObject;
        var panelRootRT = EnsureRectTransform(panelRootGO);

        float rectHeight, rectWidth;
        ConfigurePanelRoot(panelRootRT, out rectHeight, out rectWidth);
        ConfigureAppearance(panelRootGO, rectHeight, rectWidth);

        var togglesParent = EnsureTogglesContainer(panelRootGO);
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

        var expectedNames = BuildToggles(togglesParent);
        CleanupExtraToggles(togglesParent, expectedNames);

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

        // Viewport: transparent image (required by RectMask2D) + clipping mask.
        var vpImg = viewportGO.GetComponent<Image>();
        if (vpImg == null) vpImg = viewportGO.AddComponent<Image>();
        vpImg.color = new Color(0f, 0f, 0f, 0f);
        vpImg.raycastTarget = false;
        if (viewportGO.GetComponent<RectMask2D>() == null)
            viewportGO.AddComponent<RectMask2D>();

        // Ensure both rects fill their containers.
        // NOTE: ScrollArea is a direct child of RuleToggles, which has its own VerticalLayoutGroup.
        // That layout group controls ScrollArea's height via ILayoutElement, not via anchors.
        // Add a LayoutElement with flexibleHeight so ScrollArea takes all remaining space.
        var scrollLE = scrollGO.GetComponent<LayoutElement>();
        if (scrollLE == null) scrollLE = scrollGO.AddComponent<LayoutElement>();
        // Neutralize any designer-time fixed sizing so runtime layout remains stable.
        scrollLE.minHeight = 0f;
        scrollLE.preferredHeight = -1f;
        scrollLE.flexibleHeight = 1f;
        scrollLE.preferredWidth = -1f;
        scrollLE.flexibleWidth = 1f;

        var scrollRT = scrollGO.GetComponent<RectTransform>();
        scrollRT.anchorMin = Vector2.zero;
        scrollRT.anchorMax = Vector2.one;
        scrollRT.offsetMin = Vector2.zero;
        scrollRT.offsetMax = Vector2.zero;

        var vpRT = viewportGO.GetComponent<RectTransform>();
        if (vpRT == null) vpRT = viewportGO.AddComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero;
        vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = Vector2.zero;
        vpRT.offsetMax = Vector2.zero;

        // Add ScrollRect to ScrollArea if the designer left it out.
        var scrollRect = scrollGO.GetComponent<ScrollRect>();
        if (scrollRect == null) scrollRect = scrollGO.AddComponent<ScrollRect>();
        scrollRect.horizontal        = false;
        scrollRect.scrollSensitivity = 30f;
        scrollRect.content           = contentTransform.GetComponent<RectTransform>();
        scrollRect.viewport          = vpRT;

        // Fix Content so it spans the viewport width. The rows are laid out
        // inside this RectTransform, so if it keeps a zero width then every row
        // collapses to the toggle child only.
        var contentRT = contentTransform.GetComponent<RectTransform>();
        contentRT.pivot            = new Vector2(0.5f, 1f);
        contentRT.anchorMin        = new Vector2(0f, 1f);
        contentRT.anchorMax        = new Vector2(1f, 1f);
        contentRT.anchoredPosition = Vector2.zero;
        contentRT.offsetMin        = Vector2.zero;
        contentRT.offsetMax        = Vector2.zero;

        // Vertical scrollbar — create once, reuse if it already exists.
        var existingSb    = scrollGO.transform.Find("VerticalScrollbar");
        var scrollbarGO   = existingSb != null
            ? existingSb.gameObject
            : new GameObject("VerticalScrollbar",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Scrollbar));
        if (existingSb == null) scrollbarGO.transform.SetParent(scrollGO.transform, false);
        var scrollbarRT = scrollbarGO.GetComponent<RectTransform>();
        scrollbarRT.anchorMin        = new Vector2(1f, 0f);
        scrollbarRT.anchorMax        = new Vector2(1f, 1f);
        scrollbarRT.pivot            = new Vector2(1f, 0.5f);
        scrollbarRT.sizeDelta        = new Vector2(12f, 0f);
        scrollbarRT.anchoredPosition = Vector2.zero;
        scrollbarGO.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f, 0.8f);

        var existingSa    = scrollbarGO.transform.Find("SlidingArea");
        var slidingAreaGO = existingSa != null
            ? existingSa.gameObject
            : new GameObject("SlidingArea", typeof(RectTransform));
        if (existingSa == null) slidingAreaGO.transform.SetParent(scrollbarGO.transform, false);
        var slidingAreaRT = slidingAreaGO.GetComponent<RectTransform>();
        slidingAreaRT.anchorMin = Vector2.zero;
        slidingAreaRT.anchorMax = Vector2.one;
        slidingAreaRT.offsetMin = new Vector2(2f, 6f);
        slidingAreaRT.offsetMax = new Vector2(-2f, -6f);

        var existingH = slidingAreaGO.transform.Find("Handle");
        var handleGO  = existingH != null
            ? existingH.gameObject
            : new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        if (existingH == null) handleGO.transform.SetParent(slidingAreaGO.transform, false);
        var handleRT = handleGO.GetComponent<RectTransform>();
        handleRT.anchorMin = Vector2.zero;
        handleRT.anchorMax = Vector2.one;
        handleRT.offsetMin = Vector2.zero;
        handleRT.offsetMax = Vector2.zero;
        var handleImg = handleGO.GetComponent<Image>();
        handleImg.color = new Color(0.6f, 0.6f, 0.6f, 1f);

        var scrollbarComp = scrollbarGO.GetComponent<Scrollbar>();
        scrollbarComp.handleRect    = handleRT;
        scrollbarComp.direction     = Scrollbar.Direction.BottomToTop;
        scrollbarComp.targetGraphic = handleImg;

        scrollRect.verticalScrollbar           = scrollbarComp;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
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
        var rt = go.GetComponent<RectTransform>();
        if (rt == null) rt = go.AddComponent<RectTransform>();
        // When hosted in a 2-column HLG the parent controls position and size;
        // setting a fixed anchor here would fight the layout system.
        if (!FlexFillParent)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(Padding, -Padding);
        }
        return rt;
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
        var panelImg = panelRootGO.GetComponent<Image>();
        if (panelImg == null) panelImg = panelRootGO.AddComponent<Image>();
        panelImg.color = new Color(0f, 0f, 0f, 0.5f);
        panelImg.raycastTarget = false;
        var outline = panelRootGO.GetComponent<Outline>();
        if (outline == null) outline = panelRootGO.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.06f);
        outline.effectDistance = new Vector2(1f, -1f);

        var rootLE = panelRootGO.GetComponent<LayoutElement>();
        if (rootLE == null) rootLE = panelRootGO.AddComponent<LayoutElement>();
        rootLE.preferredHeight = rectHeight;
        rootLE.preferredWidth = FlexFillParent ? -1f : rectWidth;
        rootLE.flexibleWidth = FlexFillParent ? 1f : 0f;

        var rootLayout = panelRootGO.GetComponent<VerticalLayoutGroup>();
        if (rootLayout == null) rootLayout = panelRootGO.AddComponent<VerticalLayoutGroup>();
        rootLayout.childControlHeight = true;
        rootLayout.childControlWidth = true;
        rootLayout.childForceExpandHeight = false;
        // Always expand children to fill the panel width, whether fixed or flex.
        rootLayout.childForceExpandWidth = true;
        rootLayout.childAlignment = TextAnchor.UpperLeft;
        rootLayout.spacing = 4;
        rootLayout.padding = new RectOffset(4, 4, 4, 4);
    }

    private Transform EnsureTogglesContainer(GameObject panelRootGO)
    {
        Transform togglesTrans = panelRootGO.transform.Find("RuleToggles");
        GameObject RuleTogglesGO;
        if (togglesTrans != null)
        {
            RuleTogglesGO = togglesTrans.gameObject;
        }
        else
        {
            RuleTogglesGO = new GameObject("RuleToggles", typeof(RectTransform));
            RuleTogglesGO.transform.SetParent(panelRootGO.transform, false);
        }

        var RuleTogglesLE = RuleTogglesGO.GetComponent<LayoutElement>();
        if (RuleTogglesLE == null) RuleTogglesLE = RuleTogglesGO.AddComponent<LayoutElement>();
        RuleTogglesLE.flexibleHeight = 1f;

        // Designer-provided Content may be a direct child or nested under a Scroll/Viewport.
        // Prefer reusing any existing descendant named "Content" rather than creating
        // a new one (which caused duplicate Content folders when designers nest it).
        Transform contentTransform = RuleTogglesGO.transform.Find("Content");
        if (contentTransform == null)
        {
            contentTransform = FindChildRecursive(RuleTogglesGO.transform, "Content");
        }

        if (contentTransform == null)
        {
            var createdContent = new GameObject("Content", typeof(RectTransform));
            createdContent.transform.SetParent(RuleTogglesGO.transform, false);
            contentTransform = createdContent.transform;
            var contentVlg = createdContent.AddComponent<VerticalLayoutGroup>();
            contentVlg.childForceExpandHeight = false;
            contentVlg.childControlHeight = true;
            contentVlg.childControlWidth = true;
            contentVlg.childForceExpandWidth = true;
            contentVlg.childAlignment = TextAnchor.UpperLeft;
            contentVlg.spacing = 2f;
            contentVlg.padding = new RectOffset(0,0,0,0);
            var csf = createdContent.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
        else
        {
            // Update designer-provided Content to ensure it has the expected layout
            var existingGO = contentTransform.gameObject;
            var existingVlg = existingGO.GetComponent<VerticalLayoutGroup>();
            if (existingVlg == null)
            {
                existingVlg = existingGO.AddComponent<VerticalLayoutGroup>();
            }
            existingVlg.childForceExpandHeight = false;
            existingVlg.childControlHeight = true;
            existingVlg.childControlWidth = true;
            existingVlg.childForceExpandWidth = true;
            existingVlg.childAlignment = TextAnchor.UpperLeft;
            existingVlg.spacing = 2f;
            existingVlg.padding = new RectOffset(0,0,0,0);

            var csf = existingGO.GetComponent<ContentSizeFitter>();
            if (csf == null) csf = existingGO.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
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
                // Sync designer-provided row to the current size/font settings so
                // that runtime and designer-mode rows look identical.
                var rowLE = existing.GetComponent<LayoutElement>();
                if (rowLE != null)
                {
                    rowLE.minWidth = 0f;
                    rowLE.preferredWidth = -1f;
                    rowLE.flexibleWidth = 1f;
                    rowLE.preferredHeight = 36f;
                }

                var rowHLG = existing.GetComponent<HorizontalLayoutGroup>();
                if (rowHLG != null)
                {
                    rowHLG.childControlWidth = true;
                    rowHLG.childControlHeight = true;
                    rowHLG.childForceExpandWidth = false;
                    rowHLG.childForceExpandHeight = false;
                    rowHLG.childAlignment = TextAnchor.MiddleLeft;
                    rowHLG.spacing = 6f;
                }
                var rowRT = existing.GetComponent<RectTransform>();
                if (rowRT != null)
                {
                    rowRT.anchorMin = new Vector2(0f, 1f);
                    rowRT.anchorMax = new Vector2(1f, 1f);
                    rowRT.pivot = new Vector2(0.5f, 1f);
                    rowRT.offsetMin = Vector2.zero;
                    rowRT.offsetMax = Vector2.zero;
                }

                var existingToggleGO = existing.transform.Find("Toggle");
                if (existingToggleGO != null)
                {
                    var tRT = existingToggleGO.GetComponent<RectTransform>();
                    var tLE = existingToggleGO.GetComponent<LayoutElement>();
                    var tImg = existingToggleGO.GetComponent<Image>();
                    if (tRT != null) tRT.sizeDelta = new Vector2(30f, 30f);
                    if (tLE != null)
                    {
                        tLE.minWidth = 30f;
                        tLE.minHeight = 30f;
                        tLE.preferredWidth = 30f;
                        tLE.preferredHeight = 30f;
                        tLE.flexibleWidth = 0f;
                        tLE.flexibleHeight = 0f;
                    }
                    if (tImg != null)
                    {
                        tImg.color = new Color(1f, 1f, 1f, 0.06f);
                        tImg.raycastTarget = true;
                    }
                    var ckGO = existingToggleGO.transform.Find("Checkmark");
                    if (ckGO != null)
                    {
                        var ckTxt = ckGO.GetComponent<Text>();
                        if (ckTxt != null)
                        {
                            ckTxt.fontSize = 18;
                            ckTxt.resizeTextForBestFit = false;
                            // Keep toggle graphic visible but don't block row-level clicks.
                            ckTxt.raycastTarget = false;
                        }
                        // Must match the font size — scene saves 18x18 but fontSize 18 needs more room.
                        var ckRT = ckGO.GetComponent<RectTransform>();
                        if (ckRT != null)
                        {
                            ckRT.anchorMin = new Vector2(0.5f, 0.5f);
                            ckRT.anchorMax = new Vector2(0.5f, 0.5f);
                            ckRT.pivot = new Vector2(0.5f, 0.5f);
                            ckRT.anchoredPosition = Vector2.zero;
                            ckRT.sizeDelta = new Vector2(22f, 22f);
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
                    label.fontSize = 16;
                    label.resizeTextMinSize = 10;
                    label.resizeTextMaxSize = 16;
                    label.horizontalOverflow = HorizontalWrapMode.Overflow;
                    label.verticalOverflow = VerticalWrapMode.Truncate;
                    label.resizeTextForBestFit = true;
                    // Let the row Button own clicks; labels should not intercept them.
                    label.raycastTarget = false;

                    var labelRT = label.GetComponent<RectTransform>();
                    if (labelRT != null)
                    {
                        labelRT.anchorMin = new Vector2(0f, 0f);
                        labelRT.anchorMax = new Vector2(1f, 1f);
                        labelRT.pivot = new Vector2(0.5f, 0.5f);
                        labelRT.offsetMin = new Vector2(8f, 2f);
                        labelRT.offsetMax = new Vector2(-8f, -2f);
                    }

                    var lblLE = label.GetComponent<LayoutElement>();
                    if (lblLE != null)
                    {
                        lblLE.minWidth = 0f;
                        lblLE.preferredWidth = -1f;
                        lblLE.flexibleWidth = 1f;
                    }
                }
                else if (tmpLabel != null) {
                    var ttxt = SplitPascalCase(entry.rule.Name ?? "");
                    if (string.IsNullOrEmpty(ttxt)) ttxt = ruleTypeName;
                    tmpLabel.text = ttxt;
                    tmpLabel.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
                    tmpLabel.enableAutoSizing = true;
                    tmpLabel.fontSizeMin = 10;
                    tmpLabel.fontSizeMax = 16;
                    // Let the row Button own clicks; labels should not intercept them.
                    tmpLabel.raycastTarget = false;

                    var tmpLabelRT = tmpLabel.GetComponent<RectTransform>();
                    if (tmpLabelRT != null)
                    {
                        tmpLabelRT.anchorMin = new Vector2(0f, 0f);
                        tmpLabelRT.anchorMax = new Vector2(1f, 1f);
                        tmpLabelRT.pivot = new Vector2(0.5f, 0.5f);
                        tmpLabelRT.offsetMin = new Vector2(8f, 2f);
                        tmpLabelRT.offsetMax = new Vector2(-8f, -2f);
                    }

                    var tmpLE = tmpLabel.GetComponent<LayoutElement>();
                    if (tmpLE != null)
                    {
                        tmpLE.minWidth = 0f;
                        tmpLE.preferredWidth = -1f;
                        tmpLE.flexibleWidth = 1f;
                    }
                }

                if (toggle != null)
                {
                    // Use None transition so PlayEffect never starts a fade coroutine that
                    // can conflict with our direct colour management below.
                    toggle.toggleTransition = Toggle.ToggleTransition.None;
                    // Ensure direct click handling is always available in Play mode.
                    toggle.interactable = true;
                    toggle.onValueChanged.RemoveAllListeners();
                    toggle.isOn = entry.enabled;
                    string capturedName = ruleTypeName;
                    toggle.onValueChanged.AddListener((val) =>
                    {
                        _registry.SetEnabled(capturedName, val);
                        RefreshApplyRulesPanel();
                        // Keep the GO always active; control visibility via alpha so
                        // there is no risk of StartCoroutine failing on an inactive GO.
                        if (toggle.graphic != null)
                        {
                            toggle.graphic.gameObject.SetActive(true);
                            toggle.graphic.color = new Color(1f, 1f, 1f, val ? 1f : 0f);
                        }
                    });
                    var rowButton = existing.GetComponent<Button>();
                    if (rowButton != null)
                    {
                        var rowBg = existing.GetComponent<Image>();
                        if (rowBg != null)
                        {
                            // Keep an effectively transparent but raycastable row target.
                            rowBg.color = new Color(0f, 0f, 0f, 0.001f);
                            rowBg.raycastTarget = true;
                            rowButton.targetGraphic = rowBg;
                        }

                        rowButton.transition = Selectable.Transition.None;

                        rowButton.onClick.RemoveAllListeners();
                        rowButton.onClick.AddListener(() => { toggle.isOn = !toggle.isOn; });
                    }

                    // Force the correct visual state immediately via direct colour assignment.
                    if (toggle.graphic != null)
                    {
                        toggle.graphic.gameObject.SetActive(true);
                        toggle.graphic.color = new Color(1f, 1f, 1f, entry.enabled ? 1f : 0f);
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
        if (Runner != null)
        {
            Runner.RunCreationSolveAnalysisIfNeeded();
        }

        if (_createModeStatusPanel == null)
        {
            _createModeStatusPanel = FindAnyObjectByType<CreateModeStatusPanel>();
        }

        if (_applyRulePanel != null)
        {
            _applyRulePanel.RefreshList();
        }

        if (_createModeStatusPanel != null)
        {
            _createModeStatusPanel.RefreshStatus();
        }

        ChangeLogRuntimeControls.RefreshButtonStates();
    }

    private Canvas CreateDefaultCanvas()
    {
        var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        if (Object.FindAnyObjectByType<EventSystem>() == null)
        {
            var eventSystemGO = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        return canvas;
    }
}
