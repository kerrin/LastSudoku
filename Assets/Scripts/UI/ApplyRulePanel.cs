using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Sudoku.Solver;
using Sudoku.Solver.Rules;
using Sudoku.Models;

/**
 * UI panel that lists all currently enabled rules that can be applied to
 * the current board. Hovering a row previews the rule; clicking runs it.
 */
public class ApplyRulePanel : MonoBehaviour
{
    public SolverRunner Runner;

    [Tooltip("Optional: width of the panel in pixels")]
    public float PanelWidth = 300f;

    [Tooltip("Optional: maximum height before the panel becomes scrollable")]
    public float MaxHeight = 420f;

    private RuleRegistry _registry;
    private Transform _contentRoot;
    private Board _lastBoard;

    private System.Collections.IEnumerator Start()
    {
        if (Runner == null) Runner = FindAnyObjectByType<SolverRunner>();
        if (Runner == null)
        {
            Debug.LogWarning("ApplyRulePanel: No SolverRunner found in scene.");
            yield break;
        }
        
        Runner.EnsureEngine();
        _registry = Runner.Registry;
        if (_registry == null)
        {
            Debug.LogWarning("ApplyRulePanel: Runner has no RuleRegistry.");
            yield break;
        }

        // Create a simple vertical panel under this GameObject to host rule rows
        var ruleListRootGO = new GameObject("RuleListRoot", typeof(RectTransform));
        ruleListRootGO.transform.SetParent(transform, false);
        var rt = ruleListRootGO.GetComponent<RectTransform>();
        // Stretch the panel root to match the parent so it can take the
        // available width of the SidePanel. Constrain height via the
        // optional LayoutElement below rather than a fixed sizeDelta.
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        // Add a LayoutElement so callers can limit height if desired
        var panelLE = ruleListRootGO.AddComponent<LayoutElement>();
        panelLE.preferredHeight = MaxHeight;
        panelLE.flexibleHeight = 1f;

        var bg = ruleListRootGO.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.45f);

        var scrollGO = new GameObject("Scroll", typeof(RectTransform));
        scrollGO.transform.SetParent(ruleListRootGO.transform, false);
        var scroll = scrollGO.AddComponent<ScrollRect>();
        var scrollRT = scrollGO.GetComponent<RectTransform>();
        scrollRT.anchorMin = Vector2.zero;
        scrollRT.anchorMax = Vector2.one;
        scrollRT.offsetMin = new Vector2(6, 6);
        scrollRT.offsetMax = new Vector2(-6, -6);

        var viewportGO = new GameObject("Viewport", typeof(RectTransform));
        viewportGO.transform.SetParent(scrollGO.transform, false);
        var vpRT = viewportGO.GetComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero;
        vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = Vector2.zero;
        vpRT.offsetMax = Vector2.zero;
        var vpImg = viewportGO.AddComponent<Image>();
        vpImg.color = new Color(0f, 0f, 0f, 0f);
        vpImg.raycastTarget = true;

        var contentGO = new GameObject("Content", typeof(RectTransform));
        contentGO.transform.SetParent(viewportGO.transform, false);
        var contentRT = contentGO.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 1);
        contentRT.anchorMax = new Vector2(1, 1);
        contentRT.pivot = new Vector2(0.5f, 1);
        contentRT.anchoredPosition = Vector2.zero;
        var vlg = contentGO.AddComponent<VerticalLayoutGroup>();
        vlg.childControlHeight = true;
        vlg.childControlWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.spacing = 4;

        var csf = contentGO.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.content = contentRT;
        scroll.viewport = vpRT;

        _contentRoot = contentGO.transform;

        // initial build
        BuildList();

        // watch for board changes
        _lastBoard = Runner.CurrentBoard;

        yield break;
    }

    private void Update()
    {
        if (Runner == null) return;
        if (Runner.CurrentBoard != _lastBoard)
        {
            BuildList();
            _lastBoard = Runner.CurrentBoard;
        }
    }

    private void BuildList()
    {
        if (_contentRoot == null || _registry == null || Runner == null) return;
        // clear
        for (int i = _contentRoot.childCount - 1; i >= 0; i--) DestroyImmediate(_contentRoot.GetChild(i).gameObject);

        var rules = _registry.GetRulesWithStatus();
        int created = 0;
        foreach (var e in rules)
        {
            var rule = e.rule;
            bool enabled = e.enabled;
            // skip disabled rules
            if (!enabled) continue;
            // quick applicability check
            bool can = false;
            try { can = rule.CanApply(Runner.CurrentBoard); } catch { can = false; }
            // calculate a preview as well to detect finer-grained applicability
            RuleResult preview = null;
            try { preview = rule.CalculateChanges(Runner.CurrentBoard); } catch { preview = null; }
            bool applies = (preview != null && preview.Apply) || can;
            if (!applies) continue;

            CreateRuleRow(_contentRoot, rule, preview);
            created++;
        }
        // Optionally add a header summary
        Debug.Log($"ApplyRulePanel: built list with {created} applicable rule(s)");
    }

    private void CreateRuleRow(Transform parent, ISudokuRule rule, RuleResult preview)
    {
        var ruleGO = new GameObject(rule.GetType().Name + "_Row", typeof(RectTransform));
        ruleGO.transform.SetParent(parent, false);
        var rt = ruleGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        // Let the VerticalLayoutGroup control horizontal sizing; provide a preferred height.
        // If we have a description preview, increase the preferred height so the
        // description fits inside the same row instead of spilling underneath.
        var le = ruleGO.AddComponent<LayoutElement>();
        bool hasDesc = (preview != null && !string.IsNullOrEmpty(preview.Description));
        le.preferredHeight = hasDesc ? 48f : 28f;

        var btnImg = ruleGO.AddComponent<Image>();
        btnImg.color = new Color(1f, 1f, 1f, 0.02f);
        var button = ruleGO.AddComponent<Button>();

        // Label
        var labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(ruleGO.transform, false);
        var label = labelGO.AddComponent<Text>();
        label.text = rule.Name + " (" + rule.GetType().Name + ")";
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 14;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleLeft;
        var lblRT = labelGO.GetComponent<RectTransform>();
        // Place the label in the top half of the row when a description exists,
        // otherwise fill the row vertically.
        if (hasDesc)
        {
            lblRT.anchorMin = new Vector2(0f, 0.5f);
            lblRT.anchorMax = new Vector2(1f, 1f);
            lblRT.offsetMin = new Vector2(8f, 6f);
            lblRT.offsetMax = new Vector2(-8f, -6f);
        }
        else
        {
            lblRT.anchorMin = new Vector2(0f, 0f);
            lblRT.anchorMax = new Vector2(1f, 1f);
            lblRT.offsetMin = new Vector2(8f, 2f);
            lblRT.offsetMax = new Vector2(-8f, -2f);
        }
        var labelLE = labelGO.AddComponent<LayoutElement>();
        labelLE.flexibleWidth = 1f;

        // click runs the rule
        button.onClick.AddListener(() => { Runner.RunRule(rule); BuildList(); });

        // add hover preview via EventTrigger
        var trigger = ruleGO.AddComponent<EventTrigger>();
        var entryEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        entryEnter.callback.AddListener((data) => { Runner.PreviewRule(rule); });
        trigger.triggers.Add(entryEnter);
        var entryExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        entryExit.callback.AddListener((data) => { Runner.ClearPreview(); });
        trigger.triggers.Add(entryExit);

        // optionally show a tooltip/description when available
        if (hasDesc)
        {
            var descGO = new GameObject("Desc", typeof(RectTransform));
            descGO.transform.SetParent(ruleGO.transform, false);
            var desc = descGO.AddComponent<Text>();
            desc.text = preview.Description;
            desc.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            desc.fontSize = 10;
            desc.color = new Color(0.9f, 0.9f, 0.9f, 0.8f);
            desc.alignment = TextAnchor.UpperLeft;
            var dRT = descGO.GetComponent<RectTransform>();
            // Place description in the bottom half of the row so it remains
            // visually attached to its rule and does not flow outside the list.
            dRT.anchorMin = new Vector2(0f, 0f);
            dRT.anchorMax = new Vector2(1f, 0.5f);
            dRT.offsetMin = new Vector2(8f, 4f);
            dRT.offsetMax = new Vector2(-8f, -4f);
        }
    }
}
