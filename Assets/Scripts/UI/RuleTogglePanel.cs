using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text.RegularExpressions;
using UnityEngine.EventSystems;
using Sudoku.Solver;
using Sudoku.Solver.Rules;

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
    public float MaxWidth = 200f;

    [Tooltip("Padding in pixels to inset the panel when hosted inside the SidePanel")]
    public float Padding = 8f;

    [Tooltip("When hosted in the SidePanel, an offset (x right, y down) from the SidePanel top-left to place the panel root.\nUse this to treat SidePanel top-left as origin (0,0).")]
    public Vector2 SidePanelOffset = Vector2.zero;

    private RuleRegistry _registry;

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
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(Padding, -Padding);
        return rt;
    }

    private void ConfigurePanelRoot(RectTransform panelRootRT, out float rectHeight, out float rectWidth)
    {
        int count = 0;
        try { count = _registry.GetRulesWithStatus().Count; } catch { count = 0; }
        float CalculatedHeight = 26f + 4f + (28f + 2f) * count + 4f;
        rectHeight = Mathf.Min(MaxHeight, CalculatedHeight);
        rectWidth = MaxWidth;
        panelRootRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rectHeight);
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
        rootLE.preferredWidth = rectWidth;
        rootLE.flexibleWidth = 0f;

        var rootLayout = panelRootGO.GetComponent<VerticalLayoutGroup>();
        if (rootLayout == null) rootLayout = panelRootGO.AddComponent<VerticalLayoutGroup>();
        rootLayout.childControlHeight = true;
        rootLayout.childControlWidth = true;
        rootLayout.childForceExpandHeight = false;
        rootLayout.childForceExpandWidth = false;
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
                    label.horizontalOverflow = HorizontalWrapMode.Overflow;
                    label.verticalOverflow = VerticalWrapMode.Truncate;
                    label.resizeTextForBestFit = true;
                }
                else if (tmpLabel != null) {
                    var ttxt = SplitPascalCase(entry.rule.Name ?? "");
                    if (string.IsNullOrEmpty(ttxt)) ttxt = ruleTypeName;
                    tmpLabel.text = ttxt;
                    tmpLabel.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
                    tmpLabel.enableAutoSizing = true;
                    tmpLabel.fontSizeMin = 10;
                    tmpLabel.fontSizeMax = 14;
                }

                if (toggle != null)
                {
                    toggle.onValueChanged.RemoveAllListeners();
                    toggle.isOn = entry.enabled;
                    string capturedName = ruleTypeName;
                    toggle.onValueChanged.AddListener((val) =>
                    {
                        _registry.SetEnabled(capturedName, val);
                        if (toggle.graphic != null) toggle.graphic.gameObject.SetActive(val);
                    });
                    var rowButton = existing.GetComponent<Button>();
                    if (rowButton != null)
                    {
                        rowButton.onClick.RemoveAllListeners();
                        rowButton.onClick.AddListener(() => { toggle.isOn = !toggle.isOn; });
                    }
                    if (toggle.graphic != null) toggle.graphic.gameObject.SetActive(entry.enabled);
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
        panelRootRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, rectWidth);
        var rootLE = panelRootGO.GetComponent<LayoutElement>();
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
        le.preferredHeight = 28f;

        var h = ruleGO.AddComponent<HorizontalLayoutGroup>();
        h.childForceExpandHeight = false;
        h.childControlWidth = true;
        h.childForceExpandWidth = false;
        h.spacing = 6f;
        var rowBg = ruleGO.AddComponent<Image>();
        rowBg.color = new Color(0f, 0f, 0f, 0f);
        var rowButton = ruleGO.AddComponent<Button>();
        rowButton.targetGraphic = rowBg;

        var toggleGO = new GameObject("Toggle", typeof(RectTransform));
        toggleGO.transform.SetParent(ruleGO.transform, false);
        var toggle = toggleGO.AddComponent<Toggle>();
        var bgImg = toggleGO.AddComponent<Image>();
        var toggleRect = toggleGO.GetComponent<RectTransform>();
        toggleRect.sizeDelta = new Vector2(26f, 26f);
        var toggleLE = toggleGO.AddComponent<LayoutElement>();
        toggleLE.preferredWidth = 26f;
        toggleLE.preferredHeight = 26f;
        bgImg.color = new Color(1f, 1f, 1f, 0.06f);
        toggle.targetGraphic = bgImg;
        toggle.interactable = false;
        bgImg.raycastTarget = false;

        var checkMarkGO = new GameObject("Checkmark", typeof(RectTransform));
        checkMarkGO.transform.SetParent(toggleGO.transform, false);
        var ckText = checkMarkGO.AddComponent<Text>();
        ckText.text = "✓";
        ckText.font = GetSafeBuiltinFont("Arial.ttf");
        ckText.fontSize = 14;
        ckText.color = Color.white;
        ckText.alignment = TextAnchor.MiddleCenter;
        ckText.raycastTarget = false;
        var ckRect = checkMarkGO.GetComponent<RectTransform>();
        ckRect.sizeDelta = new Vector2(18f, 18f);
        toggle.graphic = ckText;

        var labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(ruleGO.transform, false);
        var label = labelGO.AddComponent<Text>();
        var lblTxt = SplitPascalCase(rule.Name ?? "");
        if (string.IsNullOrEmpty(lblTxt)) lblTxt = rule.GetType().Name;
        label.text = lblTxt;
        label.font = GetSafeBuiltinFont("Arial.ttf");
        label.fontSize = 14;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleLeft;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 10;
        label.resizeTextMaxSize = 14;
        label.raycastTarget = false;
        var labelLE = labelGO.AddComponent<LayoutElement>();
        labelLE.flexibleWidth = 1f;

        toggle.isOn = enabled;
        string ruleTypeName = rule.GetType().Name;
        toggle.onValueChanged.AddListener((val) =>
        {
            _registry.SetEnabled(ruleTypeName, val);
            if (toggle.graphic != null) toggle.graphic.gameObject.SetActive(val);
        });
        rowButton.onClick.AddListener(() => { toggle.isOn = !toggle.isOn; });
        if (toggle.graphic != null) toggle.graphic.gameObject.SetActive(enabled);
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
