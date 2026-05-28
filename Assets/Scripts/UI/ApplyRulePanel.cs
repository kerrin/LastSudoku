using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Sudoku.Solver;
using Sudoku.Solver.Rules;
using Sudoku.Models;
using Sudoku.Scripts.UI;

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

        // Use an existing designer-created child named "ApplyRules" when present
        GameObject ruleListRootGO = null;
        var existing = transform.Find("ApplyRules");
        if (existing != null)
        {
            ruleListRootGO = existing.gameObject;
        }
        else
        {
            ruleListRootGO = new GameObject("ApplyRules", typeof(RectTransform));
            ruleListRootGO.transform.SetParent(transform, false);
        }

        var rt = ruleListRootGO.GetComponent<RectTransform>();
        if (rt == null) rt = ruleListRootGO.AddComponent<RectTransform>();
        // Stretch the panel root to match the parent so it can take the
        // available width of the SidePanel. Constrain height via the
        // optional LayoutElement below rather than a fixed sizeDelta.
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // Ensure a LayoutElement so the parent VerticalLayoutGroup can size the
        // header + rules area correctly. Prefer flexibleHeight so it expands.
        var panelLE = ruleListRootGO.GetComponent<LayoutElement>();
        if (panelLE == null) panelLE = ruleListRootGO.AddComponent<LayoutElement>();
        panelLE.preferredHeight = MaxHeight;
        panelLE.flexibleHeight = 1f;

        var bg = ruleListRootGO.GetComponent<Image>();
        if (bg == null) bg = ruleListRootGO.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.45f);

        // Reuse designer-time hierarchy when present: ScrollArea (or Scroll) -> Viewport (or ViewPort) -> Content
        // Try exact known names first, then fall back to a recursive search to handle naming/case differences.
        Transform scrollTrans = ruleListRootGO.transform.Find("ScrollArea");
        GameObject scrollGO;
        ScrollRect scroll;
        RectTransform scrollRT;
        scrollGO = scrollTrans.gameObject;
        scroll = scrollGO.GetComponent<ScrollRect>();
        scrollRT = scrollGO.GetComponent<RectTransform>();
        
        scrollRT.anchorMin = Vector2.zero;
        scrollRT.anchorMax = Vector2.one;
        scrollRT.offsetMin = new Vector2(6, 6);
        scrollRT.offsetMax = new Vector2(-6, -6);

        // Viewport (accept "Viewport" or "ViewPort", and search recursively)
        Transform vpTrans = scrollGO.transform.Find("ViewPort");
        GameObject viewportGO;
        RectTransform vpRT;
        viewportGO = vpTrans.gameObject;
        vpRT = viewportGO.GetComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero;
        vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = Vector2.zero;
        vpRT.offsetMax = Vector2.zero;
        var vpImg = viewportGO.GetComponent<Image>();
        if (vpImg == null) vpImg = viewportGO.AddComponent<Image>();
        vpImg.color = new Color(0f, 0f, 0f, 0f);
        // The viewport should not block raycasts to the rule rows/buttons;
        // leave it non-raycastable so pointer events reach the child rows.
        vpImg.raycastTarget = false;
        // RectMask2D clips children to the viewport rect without requiring a visible Image.
        if (viewportGO.GetComponent<RectMask2D>() == null)
            viewportGO.AddComponent<RectMask2D>();

        // Content (prefer existing descendant named "Content")
        Transform contentTrans = null;
        // Try direct path first with common names
        contentTrans = ruleListRootGO.transform.Find("ScrollArea/ViewPort/Content");        
        GameObject contentGO;
        RectTransform contentRT;
        contentGO = contentTrans.gameObject;
        contentRT = contentGO.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 1);
        contentRT.anchorMax = new Vector2(1, 1);
        contentRT.pivot = new Vector2(0.5f, 1);
        contentRT.anchoredPosition = Vector2.zero;
        var vlg = contentGO.GetComponent<VerticalLayoutGroup>();
        vlg.childControlHeight = true;
        vlg.childControlWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.spacing = 4;

        var csf = contentGO.GetComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.content           = contentRT;
        scroll.viewport          = vpRT;
        scroll.horizontal        = false;
        scroll.scrollSensitivity = 30f;

        // Vertical scrollbar — anchored to the right edge of the scroll container.
        var apScrollbarGO = new GameObject("VerticalScrollbar",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Scrollbar));
        apScrollbarGO.transform.SetParent(scrollGO.transform, false);
        var apScrollbarRT = apScrollbarGO.GetComponent<RectTransform>();
        apScrollbarRT.anchorMin        = new Vector2(1f, 0f);
        apScrollbarRT.anchorMax        = new Vector2(1f, 1f);
        apScrollbarRT.pivot            = new Vector2(1f, 0.5f);
        apScrollbarRT.sizeDelta        = new Vector2(12f, 0f);
        apScrollbarRT.anchoredPosition = Vector2.zero;
        apScrollbarGO.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f, 0.8f);

        var apSlidingAreaGO = new GameObject("SlidingArea", typeof(RectTransform));
        apSlidingAreaGO.transform.SetParent(apScrollbarGO.transform, false);
        var apSlidingAreaRT = apSlidingAreaGO.GetComponent<RectTransform>();
        apSlidingAreaRT.anchorMin = Vector2.zero;
        apSlidingAreaRT.anchorMax = Vector2.one;
        apSlidingAreaRT.offsetMin = new Vector2(2f, 6f);
        apSlidingAreaRT.offsetMax = new Vector2(-2f, -6f);

        var apHandleGO = new GameObject("Handle",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        apHandleGO.transform.SetParent(apSlidingAreaGO.transform, false);
        var apHandleRT = apHandleGO.GetComponent<RectTransform>();
        apHandleRT.anchorMin = Vector2.zero;
        apHandleRT.anchorMax = Vector2.one;
        apHandleRT.offsetMin = Vector2.zero;
        apHandleRT.offsetMax = Vector2.zero;
        var apHandleImg = apHandleGO.GetComponent<Image>();
        apHandleImg.color = new Color(0.6f, 0.6f, 0.6f, 1f);

        var apScrollbarComp = apScrollbarGO.GetComponent<Scrollbar>();
        apScrollbarComp.handleRect    = apHandleRT;
        apScrollbarComp.direction     = Scrollbar.Direction.BottomToTop;
        apScrollbarComp.targetGraphic = apHandleImg;

        scroll.verticalScrollbar           = apScrollbarComp;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;

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

        // If running, destroy children then rebuild next frame to ensure
        // Unity finishes its internal layout + event update cycle so hitboxes
        // don't become stale. In edit-mode we can rebuild synchronously.
        if (Application.isPlaying)
        {
            for (int i = _contentRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(_contentRoot.GetChild(i).gameObject);
            }
            StartCoroutine(BuildListAsync());
        }
        else
        {
            // editor/editor-time synchronous rebuild
            for (int i = _contentRoot.childCount - 1; i >= 0; i--) DestroyImmediate(_contentRoot.GetChild(i).gameObject);
            BuildListInternal();
        }
    }

    private System.Collections.IEnumerator BuildListAsync()
    {
        // allow destruction to complete and the UI system to settle
        yield return null;
        BuildListInternal();
    }

    private void BuildListInternal()
    {
        // Always add a dedicated Validate Board row so users can explicitly
        // validate the current board and highlight conflicts. This should
        // appear regardless of whether candidates have been initialised.
        CreateValidateRow(_contentRoot);

        // If candidates have not yet been initialised, show only the Initialise
        // Candidates entry and hide other rules until it has been run.
        if (!Runner.CandidatesInitialised)
        {
            CreateInitialiseCandidatesRow(_contentRoot);
            return;
        }

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

        // Force an immediate layout rebuild so the ScrollRect and EventSystem
        // see the final child positions and sizes before any pointer events.
        var contentRect = _contentRoot.GetComponent<RectTransform>();
        if (contentRect != null) LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
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
        le.preferredHeight = 36f;

        var btnImg = ruleGO.AddComponent<Image>();
        btnImg.color = new Color(1f, 1f, 1f, 0.02f);
        var button = ruleGO.AddComponent<Button>();

        // Label
        var labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(ruleGO.transform, false);
        var label = labelGO.AddComponent<Text>();
        label.text = rule.Name;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 16;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleLeft;
        var lblRT = labelGO.GetComponent<RectTransform>();
        // Place the label to fill the row vertically.
        lblRT.anchorMin = new Vector2(0f, 0f);
        lblRT.anchorMax = new Vector2(1f, 1f);
        lblRT.offsetMin = new Vector2(8f, 2f);
        lblRT.offsetMax = new Vector2(-8f, -2f);
        var labelLE = labelGO.AddComponent<LayoutElement>();
        labelLE.flexibleWidth = 1f;

        // click runs the rule. Suppress preview requests during the apply so
        // the preview doesn't reappear while the rule is enacting and causing
        // visual-state inconsistencies.
        button.onClick.AddListener(() => { StartCoroutine(ApplyRuleCoroutine(rule)); });

        // add hover preview via EventTrigger
        var trigger = ruleGO.AddComponent<EventTrigger>();
        var entryEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        entryEnter.callback.AddListener((data) => { Runner.PreviewRule(rule); });
        trigger.triggers.Add(entryEnter);
        var entryExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        entryExit.callback.AddListener((data) => { Runner.ClearPreview(); });
        trigger.triggers.Add(entryExit);

        // Description/tooltips removed: list shows only the rule name.
    }

    private void CreateInitialiseCandidatesRow(Transform parent)
    {
        var ruleGO = new GameObject("InitialiseCandidates_Row", typeof(RectTransform));
        ruleGO.transform.SetParent(parent, false);
        var rt = ruleGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        var le = ruleGO.AddComponent<LayoutElement>();
        le.preferredHeight = 36f;

        var btnImg = ruleGO.AddComponent<Image>();
        btnImg.color = new Color(1f, 1f, 1f, 0.02f);
        var button = ruleGO.AddComponent<Button>();

        // Label
        var labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(ruleGO.transform, false);
        var label = labelGO.AddComponent<Text>();
        label.text = "Initialise Candidates";
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 16;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleLeft;
        var lblRT = labelGO.GetComponent<RectTransform>();
        lblRT.anchorMin = new Vector2(0f, 0f);
        lblRT.anchorMax = new Vector2(1f, 1f);
        lblRT.offsetMin = new Vector2(8f, 2f);
        lblRT.offsetMax = new Vector2(-8f, -2f);
        var labelLE = labelGO.AddComponent<LayoutElement>();
        labelLE.flexibleWidth = 1f;

        button.onClick.AddListener(() => {
            if (Runner == null) return;
            Runner.InitialiseCandidates();
            BuildList();
        });

        // add hover preview via EventTrigger to highlight cells that would be
        // initialised (empty cells). Exit clears the preview.
        var trigger = ruleGO.AddComponent<EventTrigger>();
        var entryEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        entryEnter.callback.AddListener((data) => { Runner.PreviewInitialiseCandidates(); });
        trigger.triggers.Add(entryEnter);
        var entryExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        entryExit.callback.AddListener((data) => { Runner.ClearPreview(); });
        trigger.triggers.Add(entryExit);
    }

    private void CreateValidateRow(Transform parent)
    {
        var ruleGO = new GameObject("ValidateBoard_Row", typeof(RectTransform));
        ruleGO.transform.SetParent(parent, false);
        var rt = ruleGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        var le = ruleGO.AddComponent<LayoutElement>();
        le.preferredHeight = 36f;

        var btnImg = ruleGO.AddComponent<Image>();
        btnImg.color = new Color(1f, 1f, 1f, 0.02f);
        var button = ruleGO.AddComponent<Button>();

        // Label
        var labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(ruleGO.transform, false);
        var label = labelGO.AddComponent<Text>();
        label.text = "Validate Board";
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 16;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleLeft;
        var lblRT = labelGO.GetComponent<RectTransform>();
        lblRT.anchorMin = new Vector2(0f, 0f);
        lblRT.anchorMax = new Vector2(1f, 1f);
        lblRT.offsetMin = new Vector2(8f, 2f);
        lblRT.offsetMax = new Vector2(-8f, -2f);
        var labelLE = labelGO.AddComponent<LayoutElement>();
        labelLE.flexibleWidth = 1f;

        // click runs validation and colours the row accordingly. Rebuild list
        // afterwards so highlights reset when the board changes or other rules
        // are applied (the list is rebuilt in those cases).
        button.onClick.AddListener(() => {
            if (Runner == null) return;
            bool ok = Runner.ValidateBoard();
            if (ok)
            {
                btnImg.color = new Color(0f, 0.45f, 0f, 1f); // dark green
            }
            else
            {
                btnImg.color = new Color(0.6f, 0f, 0f, 1f); // red
            }
        });

        // No hover preview for validate row — it only acts on click.
    }

    private System.Collections.IEnumerator ApplyRuleCoroutine(ISudokuRule rule)
    {
        if (Runner != null) Runner.SuppressPreviewRequests = true;
        Runner.ClearPreview();
        // wait one frame to allow UI/visualiser to pick up the cleared preview
        yield return null;
        Runner.RunRule(rule);
        if (Runner != null) Runner.SuppressPreviewRequests = false;
        ChangeLogRuntimeControls.RefreshButtonStates();
        BuildList();
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

    /**
     * Public wrapper to force the panel to rebuild its rule list and
     * update its cached board reference. Useful for external callers
     * (e.g. ChangeLog UI) that alter the board state without swapping
     * the Runner.CurrentBoard reference.
     */
    public void RefreshList()
    {
        BuildList();
        _lastBoard = Runner != null ? Runner.CurrentBoard : null;
    }
}
