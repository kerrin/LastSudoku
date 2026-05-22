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
    private System.Collections.IEnumerator Start()
    {
        if (Runner == null) Runner = FindAnyObjectByType<SolverRunner>();
        if (Runner == null)
        {
            Debug.LogWarning("RuleTogglePanel: No SolverRunner found in scene.");
            yield break;
        }

        Runner.EnsureEngine();
        _registry = Runner.Registry;
        if (_registry == null)
        {
            Debug.LogWarning("RuleTogglePanel: Runner did not provide a RuleRegistry.");
            yield break;
        }

        // Use the GameObject this component is attached to as the panel root
        GameObject panelRootGO = this.gameObject;
        panelRootGO.name = "RuleTogglePanelRoot";
        RectTransform panelRootRT = panelRootGO.GetComponent<RectTransform>();
        if (panelRootRT == null) panelRootRT = panelRootGO.AddComponent<RectTransform>();
        panelRootRT.anchorMin = new Vector2(0f, 1f);
        panelRootRT.anchorMax = new Vector2(0f, 1f);
        panelRootRT.pivot = new Vector2(0f, 1f);
        panelRootRT.anchoredPosition = new Vector2(Padding, -Padding);
        float CalculatedHeight = 26f + 4f + (28f + 2f) * _registry.GetRulesWithStatus().Count + 4f;
        float rectHeight = Mathf.Min(MaxHeight, CalculatedHeight);
        float rectWidth = MaxWidth;
        panelRootRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rectHeight);
        panelRootRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, rectWidth);

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

        // Header (reuse if already present in-scene)
        Transform headerTrans = panelRootGO.transform.Find("Header");
        GameObject headerGO;
        Text headerText;
        if (headerTrans != null)
        {
            headerGO = headerTrans.gameObject;
            headerText = headerGO.GetComponent<Text>();
            var headerTmp = headerGO.GetComponent<TextMeshProUGUI>();
            if (headerTmp != null)
            {
                headerTmp.text = "Rules";
                headerTmp.alignment = TMPro.TextAlignmentOptions.Center;
                headerTmp.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
                headerTmp.enableAutoSizing = false;
                headerTmp.fontSize = 16;
                headerTmp.color = Color.white;
            }
            else
            {
                if (headerText == null) headerText = headerGO.AddComponent<Text>();
                headerText.text = "Rules";
                headerText.font = GetSafeBuiltinFont("Arial.ttf");
                headerText.fontSize = 16;
                headerText.color = Color.white;
                headerText.alignment = TextAnchor.MiddleCenter;
                headerText.horizontalOverflow = HorizontalWrapMode.Overflow;
                headerText.verticalOverflow = VerticalWrapMode.Truncate;
            }
        }
        else
        {
            headerGO = new GameObject("Header", typeof(RectTransform));
            headerGO.transform.SetParent(panelRootGO.transform, false);
            headerText = headerGO.AddComponent<Text>();
            headerText.text = "Rules";
            headerText.font = GetSafeBuiltinFont("Arial.ttf");
            headerText.fontSize = 16;
            headerText.color = Color.white;
            headerText.alignment = TextAnchor.MiddleCenter;
            headerText.horizontalOverflow = HorizontalWrapMode.Overflow;
            headerText.verticalOverflow = VerticalWrapMode.Truncate;
        }
        var headerLayout = headerGO.GetComponent<LayoutElement>();
        if (headerLayout == null) headerLayout = headerGO.AddComponent<LayoutElement>();
        headerLayout.preferredHeight = 26f;
        headerLayout.flexibleWidth = 1f;

        // RuleToggles wrapper (reuse if already present in-scene)
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

        // Ensure the wrapper expands to fill remaining vertical space
        var RuleTogglesLE = RuleTogglesGO.GetComponent<LayoutElement>();
        if (RuleTogglesLE == null) RuleTogglesLE = RuleTogglesGO.AddComponent<LayoutElement>();
        RuleTogglesLE.flexibleHeight = 1f;

        // Prefer designer-provided Content if present; do not create or mutate ScrollRect/Viewport visuals at runtime.
        Transform contentTransform = RuleTogglesGO.transform.Find("ScrollArea/Viewport/Content");
        RectTransform contentRT = contentTransform != null ? contentTransform.GetComponent<RectTransform>() : null;
        Transform togglesParent = contentTransform != null ? contentTransform : RuleTogglesGO.transform;

        // Move any legacy children (designer-placed toggles) into the scroll content so they are visible at the top
        if (contentTransform != null)
        {
            var legacy = new System.Collections.Generic.List<Transform>();
            for (int i = RuleTogglesGO.transform.childCount - 1; i >= 0; --i)
            {
                var c = RuleTogglesGO.transform.GetChild(i);
                if (c.name == "ScrollArea") continue;
                legacy.Add(c);
            }
            for (int i = legacy.Count - 1; i >= 0; --i)
            {
                var c = legacy[i];
                if (c.name == "PlaceholderToggle") continue;
                c.SetParent(contentTransform, false);
            }
        }

        var rules = new List<(ISudokuRule rule, bool enabled)>();
        try
        {
            rules = _registry.GetRulesWithStatus();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"RuleTogglePanel: GetRulesWithStatus threw: {ex.Message}");
        }
        int created = 0;
        var expectedNames = new System.Collections.Generic.HashSet<string>();
        foreach (var entry in rules)
        {
            string ruleTypeName = entry.rule.GetType().Name;
            string toggleName = ruleTypeName + "_Toggle";
            expectedNames.Add(toggleName);

            // If a static toggle exists in the scene (designer-placed), reuse it instead of creating a new one
            // Search in the new content location first (if present), then fall back to legacy location.
            var existing = togglesParent.Find(toggleName);
            if (existing == null) existing = RuleTogglesGO.transform.Find(toggleName);
            if (existing != null)
            {
                // Find toggle and label children
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
                    Debug.Log($"RuleTogglePanel: Set label (Text) for '{ruleTypeName}' => '{label.text}'");
                }
                else if (tmpLabel != null) {
                    var ttxt = SplitPascalCase(entry.rule.Name ?? "");
                    if (string.IsNullOrEmpty(ttxt)) ttxt = ruleTypeName;
                    tmpLabel.text = ttxt;
                    tmpLabel.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
                    tmpLabel.enableAutoSizing = true;
                    tmpLabel.fontSizeMin = 10;
                    tmpLabel.fontSizeMax = 14;
                    Debug.Log($"RuleTogglePanel: Set label (TMP) for '{ruleTypeName}' => '{tmpLabel.text}'");
                }

                if (toggle != null)
                {
                    // Clear prior listeners to avoid duplicate handlers
                    toggle.onValueChanged.RemoveAllListeners();
                    toggle.isOn = entry.enabled;
                    string capturedName = ruleTypeName;
                    toggle.onValueChanged.AddListener((val) =>
                    {
                        _registry.SetEnabled(capturedName, val);
                        if (toggle.graphic != null) toggle.graphic.gameObject.SetActive(val);
                        Debug.Log($"Rule '{capturedName}' enabled={val}");
                    });
                    // Hook up row button if present to flip the toggle
                    var rowButton = existing.GetComponent<Button>();
                    if (rowButton != null)
                    {
                        rowButton.onClick.RemoveAllListeners();
                        rowButton.onClick.AddListener(() => { toggle.isOn = !toggle.isOn; });
                    }
                    // Initialize graphic visibility
                    if (toggle.graphic != null) toggle.graphic.gameObject.SetActive(entry.enabled);
                }
                else
                {
                    // If no Toggle component exists, create the runtime one to match CreateRuleToggle
                    CreateRuleToggle(togglesParent, entry.rule, entry.enabled);
                }
            }
            else
            {
                CreateRuleToggle(togglesParent, entry.rule, entry.enabled);
            }
            created++;
        }

        // Remove any extra toggles that are present in the content but no longer correspond to registered rules
        var removalParent = togglesParent;
        for (int i = removalParent.childCount - 1; i >= 0; --i)
        {
            var child = removalParent.GetChild(i);
            if (child.name == "PlaceholderToggle") continue;
            if (!expectedNames.Contains(child.name))
            {
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
                Debug.Log($"RuleTogglePanel: Removed extra toggle '{child.name}' from scene (not in registry).");
            }
        }

        // Another frame to allow parent layout groups to run, then re-assert our fixed width
        yield return null;
        panelRootRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, rectWidth);
        rootLE.preferredWidth = rectWidth;
        rootLE.minWidth = rectWidth;

        // If we're hosted somewhere under SidePanel, let the SidePanel top-left be (0,0)
        if (SidePanelOffset != Vector2.zero)
        {
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
                    // Unity corners: 0=bottom-left, 1=top-left, 2=top-right, 3=bottom-right
                    Vector3 topLeftWorld = corners[1];
                    // Convert world top-left into parent's local coordinates
                    Vector2 topLeftLocal;
                    Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, topLeftWorld);
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRT, screenPoint, null, out topLeftLocal);
                    // SidePanelOffset: x = right positive, y = down positive
                    Vector2 desired = topLeftLocal + new Vector2(SidePanelOffset.x, -SidePanelOffset.y);
                    panelRootRT.anchoredPosition = desired;
                }
            }
        }
    }

    private void CreateRuleToggle(Transform parent, ISudokuRule rule, bool enabled)
    {
        var ruleGO = new GameObject(rule.GetType().Name + "_Toggle", typeof(RectTransform));
        ruleGO.transform.SetParent(parent, false);

        var le = ruleGO.AddComponent<LayoutElement>();
        le.preferredHeight = 28f;

        var h = ruleGO.AddComponent<HorizontalLayoutGroup>();
        h.childForceExpandHeight = false;
        // Let the layout control child widths so the label can be given the
        // remaining horizontal space and properly truncate/resize instead of wrapping.
        h.childControlWidth = true;
        h.childForceExpandWidth = false;
        h.spacing = 6f;
        // Make the whole row clickable: add an invisible background Image and Button
        var rowBg = ruleGO.AddComponent<Image>();
        rowBg.color = new Color(0f, 0f, 0f, 0f);
        var rowButton = ruleGO.AddComponent<Button>();
        rowButton.targetGraphic = rowBg;

        var toggleGO = new GameObject("Toggle", typeof(RectTransform));
        toggleGO.transform.SetParent(ruleGO.transform, false);
        var toggle = toggleGO.AddComponent<Toggle>();
        var bgImg = toggleGO.AddComponent<Image>();
        // Prefer a small fixed size so layout doesn't stretch this element vertically
        var toggleRect = toggleGO.GetComponent<RectTransform>();
        toggleRect.sizeDelta = new Vector2(26f, 26f);
        // Provide a LayoutElement so HorizontalLayoutGroup can allocate a fixed
        // width for the toggle when childControlWidth == true.
        var toggleLE = toggleGO.AddComponent<LayoutElement>();
        toggleLE.preferredWidth = 26f;
        toggleLE.preferredHeight = 26f;
        bgImg.color = new Color(1f, 1f, 1f, 0.06f);
        toggle.targetGraphic = bgImg;
        // Toggle should not be directly clickable; the row Button handles clicks.
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
        // Don't let the checkmark or label block pointer events
        ckText.raycastTarget = false;
        var ckRect = checkMarkGO.GetComponent<RectTransform>();
        ckRect.sizeDelta = new Vector2(18f, 18f);
        // Use the Text as the toggle's graphic so the checkmark shows/hides with isOn
        toggle.graphic = ckText;

        var labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(ruleGO.transform, false);
        var label = labelGO.AddComponent<Text>();
        var lblTxt = SplitPascalCase(rule.Name ?? "");
        if (string.IsNullOrEmpty(lblTxt)) lblTxt = rule.GetType().Name;
        label.text = lblTxt;
        Debug.Log($"RuleTogglePanel: Created label for '{rule.GetType().Name}' => '{label.text}'");
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
            Debug.Log($"Rule '{ruleTypeName}' enabled={val}");
        });
        // Row click flips the toggle state; onValueChanged handles registry update and visuals.
        rowButton.onClick.AddListener(() => { toggle.isOn = !toggle.isOn; });
        // Initialize graphic visibility
        if (toggle.graphic != null) toggle.graphic.gameObject.SetActive(enabled);
        Debug.Log($"RuleTogglePanel: Added toggle for '{ruleTypeName}' (initially {(enabled?"ON":"OFF")}).");

    }

    private string SplitPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        // Insert spaces before capital letters, but avoid splitting acronyms badly.
        // This heuristic yields: HiddenSingles -> Hidden Singles, XYZRule -> XYZ Rule
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
