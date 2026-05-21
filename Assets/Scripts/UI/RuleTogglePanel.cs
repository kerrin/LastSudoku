using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
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
        // If multiple RuleTogglePanel instances exist, prefer the one inside a SidePanel.
        var allPanels = FindObjectsByType<RuleTogglePanel>();

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

        // Create an inner root under the provided parent so we can control layout
        GameObject panelRootGO = new GameObject("RuleTogglePanelRoot", typeof(RectTransform));
        panelRootGO.transform.SetParent(this.transform, false);
        RectTransform panelRootRT = panelRootGO.GetComponent<RectTransform>();
        panelRootRT.anchorMin = new Vector2(0f, 1f);
        // Keep the root from stretching horizontally so preferred widths are respected
        panelRootRT.anchorMax = new Vector2(0f, 1f);
        panelRootRT.pivot = new Vector2(0f, 1f);
        panelRootRT.anchoredPosition = new Vector2(Padding, -Padding);
        float CalculatedHeight = 26f + 4f + (28f + 2f) * _registry.GetRulesWithStatus().Count + 4f; // header + spacing + (row height + spacing) * num rules + padding
        float rectHeight = Mathf.Min(MaxHeight, CalculatedHeight);
        float rectWidth = MaxWidth;
        panelRootRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rectHeight);
        panelRootRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, rectWidth);
        // Add a semi-transparent background and a subtle border so the toggle panel
        // visually separates from the side panel contents.
        var panelImg = panelRootGO.GetComponent<Image>();
        if (panelImg == null) panelImg = panelRootGO.AddComponent<Image>();
        panelImg.color = new Color(0f, 0f, 0f, 0.5f);
        panelImg.raycastTarget = false;
        var outline = panelRootGO.GetComponent<Outline>();
        if (outline == null) outline = panelRootGO.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.06f);
        outline.effectDistance = new Vector2(1f, -1f);
        // Make this root a fixed-size block inside the RulesArea so other panels can share space
        var rootLE = panelRootGO.GetComponent<LayoutElement>();
        if (rootLE == null) rootLE = panelRootGO.AddComponent<LayoutElement>();
        rootLE.preferredHeight = Mathf.Min(MaxHeight, 220f);
        // Respect the computed width as the preferred width so parent layout honors it
        rootLE.preferredWidth = rectWidth;
        rootLE.flexibleWidth = 0f;
        var rootLayout = panelRootGO.GetComponent<VerticalLayoutGroup>();
        if (rootLayout == null) rootLayout = panelRootGO.AddComponent<VerticalLayoutGroup>();
        rootLayout.childControlHeight = true;
        // Do NOT let the VerticalLayoutGroup control or force-expand child widths;
        // we want this panel to keep a fixed width inside the RulesArea.
        rootLayout.childControlWidth = false;
        rootLayout.childForceExpandHeight = false;
        rootLayout.childForceExpandWidth = false;
        rootLayout.spacing = 4;
        rootLayout.padding = new RectOffset(4, 4, 4, 4);

        // Header
        var headerGO = new GameObject("Header", typeof(RectTransform));
        headerGO.transform.SetParent(panelRootGO.transform, false);
        var headerText = headerGO.AddComponent<Text>();
        headerText.text = "Rules";
        headerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        headerText.fontSize = 16;
        headerText.color = Color.white;
        headerText.alignment = TextAnchor.MiddleCenter;
        var headerLayout = headerGO.AddComponent<LayoutElement>();
        headerLayout.preferredHeight = 26f;

        // RuleToggles
        GameObject RuleTogglesGO = new GameObject("RuleToggles", typeof(RectTransform));
        RuleTogglesGO.transform.SetParent(panelRootGO.transform, false);
        var RuleTogglesLayout = RuleTogglesGO.AddComponent<VerticalLayoutGroup>();
        RuleTogglesLayout.childForceExpandHeight = false;
        RuleTogglesLayout.childControlHeight = true;
        RuleTogglesLayout.childControlWidth = true;
        RuleTogglesLayout.spacing = 2f;
        RuleTogglesLayout.padding = new RectOffset(0, 0, 0, 0);
        var csf = RuleTogglesGO.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        // Let the RuleToggles expand to take remaining space inside the panel root
        var RuleTogglesLE = RuleTogglesGO.AddComponent<LayoutElement>();
        // Do not force RuleToggles to expand to fill the parent; let it size to its children
        RuleTogglesLE.flexibleHeight = 0f;

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
        foreach (var entry in rules)
        {
            CreateRuleToggle(RuleTogglesGO.transform, entry.rule, entry.enabled);
            created++;
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
        bgImg.color = new Color(1f, 1f, 1f, 0.06f);
        toggle.targetGraphic = bgImg;
        // Toggle should not be directly clickable; the row Button handles clicks.
        toggle.interactable = false;
        bgImg.raycastTarget = false;

        var checkMarkGO = new GameObject("Checkmark", typeof(RectTransform));
        checkMarkGO.transform.SetParent(toggleGO.transform, false);
        var ckText = checkMarkGO.AddComponent<Text>();
        ckText.text = "✓";
        ckText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
        label.text = rule.Name;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 14;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleLeft;
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
