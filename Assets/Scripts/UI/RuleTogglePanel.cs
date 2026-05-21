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

    private void Start()
    {
        // Initialize asynchronously so host scripts (like BoardSidePanel)
        // have a chance to reparent or attach first. This avoids creating a
        // duplicate floating panel that would immediately be removed.
        StartCoroutine(InitializeCoroutine());
    }

    private System.Collections.IEnumerator InitializeCoroutine()
    {
        // If multiple RuleTogglePanel instances exist, prefer the one inside a SidePanel.
        var allPanels = FindObjectsByType<RuleTogglePanel>();
        if (allPanels.Length > 1)
        {
            bool IsUnderRuleTogglePanel(Transform t)
            {
                var cur = t;
                while (cur != null)
                {
                    if (cur.name == "RuleTogglePanel") return true;
                    cur = cur.parent;
                }
                return false;
            }

            bool thisUnder = IsUnderRuleTogglePanel(transform);
            foreach (var p in allPanels)
            {
                if (p == this) continue;
                bool otherUnder = IsUnderRuleTogglePanel(p.transform);
                if (otherUnder && !thisUnder)
                {
                    Debug.Log("RuleTogglePanel: Found existing RuleTogglePanel-hosted panel; removing duplicate.");
                    Destroy(gameObject);
                    yield break;
                }
                if (thisUnder && !otherUnder)
                {
                    Debug.Log("RuleTogglePanel: Found duplicate panel outside RuleTogglePanel; removing the other instance.");
                    Destroy(p.gameObject);
                }
            }
        }

        if (Runner == null) Runner = FindAnyObjectByType<SolverRunner>();
        Debug.Log($"RuleTogglePanel.Start: Runner assigned={(Runner!=null)}");
        if (Runner == null)
        {
            Debug.LogWarning("RuleTogglePanel: No SolverRunner found in scene.");
            yield break;
        }

        Runner.EnsureEngine();
        _registry = Runner.Registry;
        Debug.Log($"RuleTogglePanel.Start: Registry assigned={( _registry != null)}");
        if (_registry == null)
        {
            Debug.LogWarning("RuleTogglePanel: Runner did not provide a RuleRegistry.");
            yield break;
        }

        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            canvas = CreateDefaultCanvas();

        // Give one frame for other components (BoardSidePanel) to reparent us
        // into a proper RulesArea host before we decide to create a floating panel.
        yield return null;

        // If this component is already placed under a RulesArea-like container (parented),
        // populate the UI inside that parent. Otherwise create a standalone floating panel.
        Transform parentContainer = null;
        if (transform.parent != null && transform.parent.GetComponentInParent<Canvas>() != null && transform.parent != canvas.transform)
        {
            parentContainer = transform.parent;
            Debug.Log($"RuleTogglePanel: Using parent container '{parentContainer.name}' for UI placement.");
        }
        else
        {
            // Don't create a floating panel here — defer to BoardSidePanel which
            // will create a hosted RuleTogglePanel when appropriate. This avoids
            // transient creation+destruction of floating UI.
            Debug.Log("RuleTogglePanel: No host found after delay; deferring floating panel creation.");
            yield break;
        }
        // Use an existing permanent root if present in the scene (created in editor),
        // otherwise create a new one dynamically. This keeps the root GameObject
        // permanent like other panels while preserving runtime population behavior.
        GameObject panelRootGO = null;
        RectTransform panelRootRT = null;
        // Prefer a scene-placed permanent root if present (created in editor).
        var existingSceneGO = GameObject.Find("RuleTogglePanelRoot");
        if (existingSceneGO != null)
        {
            panelRootGO = existingSceneGO;
            panelRootRT = panelRootGO.GetComponent<RectTransform>();
            if (panelRootRT == null) panelRootRT = panelRootGO.AddComponent<RectTransform>();
            // Ensure the permanent root becomes a child of this RuleTogglePanel
            if (panelRootGO.transform.parent != this.transform)
            {
                panelRootGO.transform.SetParent(this.transform, false);
                Debug.Log("RuleTogglePanel: Moved existing RuleTogglePanelRoot to be child of RuleTogglePanel.");
            }
            // Clear any runtime children to ensure a consistent rebuild
            for (int i = panelRootGO.transform.childCount - 1; i >= 0; i--)
            {
                Destroy(panelRootGO.transform.GetChild(i).gameObject);
            }
            // Remove any other RuleTogglePanelRoot instances in the scene to avoid duplicates
            foreach (var t in Object.FindObjectsByType<Transform>())
            {
                if (t.name == "RuleTogglePanelRoot" && t.gameObject != panelRootGO)
                {
                    Debug.Log($"RuleTogglePanel: Removing duplicate RuleTogglePanelRoot found at '{t.gameObject.transform.parent?.name}'");
                    Destroy(t.gameObject);
                }
            }
        }
        else
        {
            // Fallback: look for a root under the parent container (legacy)
            var existingUnderParent = parentContainer.Find("RuleTogglePanelRoot");
            if (existingUnderParent != null)
            {
                panelRootGO = existingUnderParent.gameObject;
                panelRootRT = panelRootGO.GetComponent<RectTransform>();
                if (panelRootRT == null) panelRootRT = panelRootGO.AddComponent<RectTransform>();
                Debug.Log("RuleTogglePanel: Found existing RuleTogglePanelRoot under parent; using it.");
                // Move the existing root to be a child of this RuleTogglePanel so
                // it is not a sibling under the side panel.
                if (panelRootGO.transform.parent != this.transform)
                {
                    panelRootGO.transform.SetParent(this.transform, false);
                    Debug.Log("RuleTogglePanel: Moved existing RuleTogglePanelRoot to be child of RuleTogglePanel.");
                }
                for (int i = panelRootGO.transform.childCount - 1; i >= 0; i--)
                {
                    Destroy(panelRootGO.transform.GetChild(i).gameObject);
                }
                    // Also remove any other duplicates to prevent another one from staying under SidePanel
                    foreach (var t in Object.FindObjectsByType<Transform>())
                    {
                        if (t.name == "RuleTogglePanelRoot" && t.gameObject != panelRootGO)
                        {
                            Debug.Log($"RuleTogglePanel: Removing duplicate RuleTogglePanelRoot found at '{t.gameObject.transform.parent?.name}'");
                            Destroy(t.gameObject);
                        }
                    }
            }
            else
            {
                // Create an inner root under the provided parent so we can control layout
                panelRootGO = new GameObject("RuleTogglePanelRoot", typeof(RectTransform));
                panelRootGO.transform.SetParent(parentContainer, false);
                panelRootRT = panelRootGO.GetComponent<RectTransform>();
            }
        }
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
        Debug.Log($"RuleTogglePanelRoot: RuleTogglePanelRoot created. parent={parentContainer.name} rectWidth={rectWidth} rectSize=({panelRootRT.rect.width}x{panelRootRT.rect.height}) MaxWidth={MaxWidth}");
        // Add a semi-transparent background and a subtle border so the toggle panel
        // visually separates from the side panel contents.
        var panelImg = panelRootGO.GetComponent<UnityEngine.UI.Image>();
        if (panelImg == null) panelImg = panelRootGO.AddComponent<UnityEngine.UI.Image>();
        panelImg.color = new Color(0f, 0f, 0f, 0.5f);
        panelImg.raycastTarget = false;
        var outline = panelRootGO.GetComponent<UnityEngine.UI.Outline>();
        if (outline == null) outline = panelRootGO.AddComponent<UnityEngine.UI.Outline>();
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
        Debug.Log($"RuleTogglePanel: Found {rules.Count} registered rules.");
        int created = 0;
        foreach (var entry in rules)
        {
            CreateRuleToggle(RuleTogglesGO.transform, entry.rule, entry.enabled);
            created++;
        }
        Debug.Log($"RuleTogglePanel: Created {created} toggle(s).");
        Debug.Log($"RuleTogglePanel: Final RuleTogglePanelRoot size=({panelRootRT.rect.width}x{panelRootRT.rect.height}), parent='{panelRootGO.transform.parent?.name}'");

        // Another frame to allow parent layout groups to run, then re-assert our fixed width
        yield return null;
        panelRootRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, rectWidth);
        rootLE.preferredWidth = rectWidth;
        rootLE.minWidth = rectWidth;
        // Ensure the RuleTogglePanelRoot is parented under this RuleTogglePanel GameObject
        if (panelRootGO.transform.parent != this.transform)
        {
            panelRootGO.transform.SetParent(this.transform, false);
            panelRootGO.transform.SetAsFirstSibling();
            Debug.Log("RuleTogglePanel: Reparented RuleTogglePanelRoot to RuleTogglePanel at end of init.");
        }

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
                    Debug.Log($"RuleTogglePanel: Positioned root relative to SidePanel top-left at {SidePanelOffset} -> anchored={desired}");
                }
            }
        }

        Debug.Log($"RuleTogglePanel: Reapplied fixed width={rectWidth}, final size=({panelRootRT.rect.width}x{panelRootRT.rect.height}), parent='{panelRootGO.transform.parent?.name}'");
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
