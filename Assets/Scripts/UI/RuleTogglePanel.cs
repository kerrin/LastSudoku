using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Sudoku.Solver;
using Sudoku.Solver.Rules;

/// <summary>
/// Runtime UI panel that creates a toggle for each registered rule so the
/// user can enable/disable rules without modifying code. Attach this to a
/// GameObject in the scene; it will create a Canvas if none exists.
/// </summary>
public class RuleTogglePanel : MonoBehaviour
{
    public SolverRunner Runner;

    [Tooltip("Optional: width of the panel in pixels")]
    public float PanelWidth = 260f;

    [Tooltip("Optional: maximum height before the panel becomes scrollable")]
    public float MaxHeight = 400f;

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
        var allPanels = Object.FindObjectsByType<RuleTogglePanel>();
        if (allPanels.Length > 1)
        {
            bool IsUnderSidePanel(Transform t)
            {
                var cur = t;
                while (cur != null)
                {
                    if (cur.name == "SidePanel") return true;
                    cur = cur.parent;
                }
                return false;
            }

            bool thisUnder = IsUnderSidePanel(transform);
            foreach (var p in allPanels)
            {
                if (p == this) continue;
                bool otherUnder = IsUnderSidePanel(p.transform);
                if (otherUnder && !thisUnder)
                {
                    Debug.Log("RuleTogglePanel: Found existing SidePanel-hosted panel; removing duplicate.");
                    Destroy(gameObject);
                    yield break;
                }
                if (thisUnder && !otherUnder)
                {
                    Debug.Log("RuleTogglePanel: Found duplicate panel outside SidePanel; removing the other instance.");
                    Destroy(p.gameObject);
                }
            }
        }

        if (Runner == null) Runner = Object.FindAnyObjectByType<SolverRunner>();
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

        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
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
        // Create an inner root under the provided parent so we can control layout
        var panelRoot = new GameObject("PanelRoot", typeof(RectTransform));
        panelRoot.transform.SetParent(parentContainer, false);
        var panelRootRT = panelRoot.GetComponent<RectTransform>();
        panelRootRT.anchorMin = Vector2.zero;
        panelRootRT.anchorMax = Vector2.one;
        panelRootRT.offsetMin = Vector2.zero;
        panelRootRT.offsetMax = Vector2.zero;
        var rootLayout = panelRoot.AddComponent<VerticalLayoutGroup>();
        rootLayout.childControlHeight = true;
        rootLayout.childControlWidth = true;
        rootLayout.childForceExpandHeight = false;
        rootLayout.childForceExpandWidth = true;
        rootLayout.spacing = 4;
        rootLayout.padding = new RectOffset(4, 4, 4, 4);

        // Header
        var headerGO = new GameObject("Header", typeof(RectTransform));
        headerGO.transform.SetParent(panelRoot.transform, false);
        var headerText = headerGO.AddComponent<Text>();
        headerText.text = "Rules";
        headerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        headerText.fontSize = 16;
        headerText.color = Color.white;
        headerText.alignment = TextAnchor.MiddleCenter;
        var headerLayout = headerGO.AddComponent<LayoutElement>();
        headerLayout.preferredHeight = 26f;

        // Content
        GameObject contentGO = new GameObject("Content", typeof(RectTransform));
        contentGO.transform.SetParent(panelRoot.transform, false);
        var contentLayout = contentGO.AddComponent<VerticalLayoutGroup>();
        contentLayout.childForceExpandHeight = false;
        contentLayout.childControlHeight = true;
        contentLayout.childControlWidth = true;
        contentLayout.spacing = 2f;
        contentLayout.padding = new RectOffset(0, 0, 0, 0);
        var csf = contentGO.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        // Let the content expand to take remaining space inside the panel root
        var contentLE = contentGO.AddComponent<LayoutElement>();
        contentLE.flexibleHeight = 1f;

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
            CreateRuleToggle(contentGO.transform, entry.rule, entry.enabled);
            created++;
        }
        Debug.Log($"RuleTogglePanel: Created {created} toggle(s).");
    }

    private void CreateRuleToggle(Transform parent, ISudokuRule rule, bool enabled)
    {
        var go = new GameObject(rule.GetType().Name + "_Toggle", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 28f;

        var h = go.AddComponent<HorizontalLayoutGroup>();
        h.childForceExpandHeight = false;
        h.childForceExpandWidth = false;
        h.spacing = 6f;

        var toggleGO = new GameObject("Toggle", typeof(RectTransform));
        toggleGO.transform.SetParent(go.transform, false);
        var toggle = toggleGO.AddComponent<Toggle>();
        var bgImg = toggleGO.AddComponent<Image>();
        // Prefer a small fixed size so layout doesn't stretch this element vertically
        var toggleRect = toggleGO.GetComponent<RectTransform>();
        toggleRect.sizeDelta = new Vector2(26f, 26f);
        bgImg.color = new Color(1f, 1f, 1f, 0.06f);
        toggle.targetGraphic = bgImg;

        var ck = new GameObject("Checkmark", typeof(RectTransform));
        ck.transform.SetParent(toggleGO.transform, false);
        var ckText = ck.AddComponent<Text>();
        ckText.text = "✓";
        ckText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        ckText.fontSize = 14;
        ckText.color = Color.white;
        ckText.alignment = TextAnchor.MiddleCenter;
        var ckRect = ck.GetComponent<RectTransform>();
        ckRect.sizeDelta = new Vector2(18f, 18f);
        // Use the Text as the toggle's graphic so the checkmark shows/hides with isOn
        toggle.graphic = ckText;

        var labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(go.transform, false);
        var label = labelGO.AddComponent<Text>();
        label.text = rule.Name;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 14;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleLeft;
        var labelLE = labelGO.AddComponent<LayoutElement>();
        labelLE.flexibleWidth = 1f;

        toggle.isOn = enabled;
        string ruleTypeName = rule.GetType().Name;
        toggle.onValueChanged.AddListener((val) =>
        {
            _registry.SetEnabled(ruleTypeName, val);
            // Ensure the text graphic visibility matches state (Toggle may not auto-disable Text)
            if (toggle.graphic != null) toggle.graphic.enabled = val;
            Debug.Log($"Rule '{ruleTypeName}' enabled={val}");
        });
        // Initialize graphic visibility
        if (toggle.graphic != null) toggle.graphic.enabled = enabled;
        Debug.Log($"RuleTogglePanel: Added toggle for '{ruleTypeName}' (initially {(enabled?"ON":"OFF")}).");
    }

    private Canvas CreateDefaultCanvas()
    {
        var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        if (Object.FindAnyObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        return canvas;
    }
}
