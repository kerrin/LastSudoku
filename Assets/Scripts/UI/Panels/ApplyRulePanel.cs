using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Sudoku.Solver;
using Sudoku.Solver.Rules;
using Sudoku.Models;

namespace Sudoku.UI.Panels
{

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
    private Coroutine _pendingBuildListCoroutine;
    private int _buildListRequestVersion;
    private int _lastChangeLogIndex = -1;
    private bool _lastCandidatesInitialised;
    private bool _lastCanReinitialiseCandidatesRemoveAny;
    

    private void OnEnable()
    {
        if (!Application.isPlaying) return;

        if (Runner == null) Runner = FindAnyObjectByType<SolverRunner>();
        if (Runner != null && _registry == null)
        {
            Runner.EnsureEngine();
            _registry = Runner.Registry;
        }

        if (ResolveUiReferences())
        {
            BuildList();
            _lastBoard = Runner != null ? Runner.CurrentBoard : null;
            CacheRefreshState();
        }
    }

    private System.Collections.IEnumerator Start()
    {
        if (Runner == null) Runner = FindAnyObjectByType<SolverRunner>();
        if (Runner == null)
        {
            Debug.LogWarning("ApplyRulePanel: No SolverRunner found in scene.");
            if (ResolveUiReferences())
            {
                CreateInfoRow(_contentRoot, "Waiting for solver...");
            }
            yield break;
        }
        
        Runner.EnsureEngine();
        _registry = Runner.Registry;
        if (_registry == null)
        {
            Debug.LogWarning("ApplyRulePanel: Runner has no RuleRegistry.");
            if (ResolveUiReferences())
            {
                CreateInfoRow(_contentRoot, "Rule registry unavailable.");
            }
            yield break;
        }

        if (!ResolveUiReferences())
        {
            yield break;
        }

        // initial build
        BuildList();

        // watch for board changes
        _lastBoard = Runner.CurrentBoard;
        CacheRefreshState();

        yield break;
    }

    private void Update()
    {
        if (Runner == null) return;

        bool needsRefresh = Runner.CurrentBoard != _lastBoard || HasRefreshStateChanged();
        if (needsRefresh)
        {
            BuildList();
            _lastBoard = Runner.CurrentBoard;
            CacheRefreshState();
        }
    }

    private void BuildList()
    {
        if (_contentRoot == null)
        {
            if (!ResolveUiReferences()) return;
        }
        if (_contentRoot == null || _registry == null || Runner == null) return;

        // If running, destroy children then rebuild next frame to ensure
        // Unity finishes its internal layout + event update cycle so hitboxes
        // don't become stale. In edit-mode we can rebuild synchronously.
        if (Application.isPlaying)
        {
            _buildListRequestVersion++;

            for (int i = _contentRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(_contentRoot.GetChild(i).gameObject);
            }

            if (_pendingBuildListCoroutine != null)
            {
                StopCoroutine(_pendingBuildListCoroutine);
                _pendingBuildListCoroutine = null;
            }

            _pendingBuildListCoroutine = StartCoroutine(BuildListAsync(_buildListRequestVersion));
        }
        else
        {
            // editor/editor-time synchronous rebuild
            for (int i = _contentRoot.childCount - 1; i >= 0; i--) DestroyImmediate(_contentRoot.GetChild(i).gameObject);
            BuildListInternal();
        }
    }

    private System.Collections.IEnumerator BuildListAsync(int requestVersion)
    {
        // allow destruction to complete and the UI system to settle
        yield return null;

        if (requestVersion != _buildListRequestVersion)
        {
            yield break;
        }

        BuildListInternal();

        if (requestVersion == _buildListRequestVersion)
        {
            _pendingBuildListCoroutine = null;
        }
    }

    private void BuildListInternal()
    {
        if (_contentRoot == null) return;

        // Always add a dedicated Validate Board row so users can explicitly
        // validate the current board and highlight conflicts. This should
        // appear regardless of whether candidates have been initialised.
        CreateValidateRow(_contentRoot);

        var usedRuleNames = new System.Collections.Generic.HashSet<string>();

        bool canReinitialiseCandidates = Runner.CanReinitialiseCandidatesRemoveAny();
        if (canReinitialiseCandidates)
        {
            CreateReinitialiseCandidatesRow(_contentRoot);
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

            bool usedInAnalysis = usedRuleNames.Contains(rule.GetType().Name);
            CreateRuleRow(_contentRoot, rule, preview, usedInAnalysis);
            created++;
        }

        // Ensure the panel is never empty in Solve mode.
        if (created == 0 && !canReinitialiseCandidates)
        {
            CreateInfoRow(_contentRoot, "No applicable rules right now.");
        }

        // Force an immediate layout rebuild so the ScrollRect and EventSystem
        // see the final child positions and sizes before any pointer events.
        var contentRect = _contentRoot.GetComponent<RectTransform>();
        if (contentRect != null) LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
    }

    /**
     * Resolve ApplyRules hierarchy and ensure required UI components exist.
     * This keeps the panel resilient when designer wiring is partially missing.
     *
     * @returns True when Content root was resolved.
     */
    private bool ResolveUiReferences()
    {
        var ruleListRoot = transform.Find("ApplyRules") ?? transform;

        Transform scrollTrans = ruleListRoot.Find("ScrollArea") ?? FindChildRecursive(ruleListRoot, "ScrollArea");
        if (scrollTrans == null)
        {
            Debug.LogWarning("ApplyRulePanel: ScrollArea not found; cannot build rule list.");
            return false;
        }

        var scrollGO = scrollTrans.gameObject;
        var scroll = scrollGO.GetComponent<ScrollRect>();
        if (scroll == null) scroll = scrollGO.AddComponent<ScrollRect>();
        var scrollRT = scrollGO.GetComponent<RectTransform>();
        if (scrollRT != null)
        {
            scrollRT.anchorMin = Vector2.zero;
            scrollRT.anchorMax = Vector2.one;
            scrollRT.offsetMin = new Vector2(6f, 6f);
            scrollRT.offsetMax = new Vector2(-6f, -6f);
        }

        Transform vpTrans = scrollGO.transform.Find("ViewPort") ?? scrollGO.transform.Find("Viewport") ?? FindChildRecursive(scrollGO.transform, "ViewPort") ?? FindChildRecursive(scrollGO.transform, "Viewport");
        if (vpTrans == null)
        {
            Debug.LogWarning("ApplyRulePanel: ViewPort/Viewport not found; cannot build rule list.");
            return false;
        }

        var viewportGO = vpTrans.gameObject;
        var vpRT = viewportGO.GetComponent<RectTransform>();
        if (vpRT == null)
        {
            Debug.LogWarning("ApplyRulePanel: Viewport is missing RectTransform.");
            return false;
        }
        vpRT.anchorMin = Vector2.zero;
        vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = Vector2.zero;
        vpRT.offsetMax = Vector2.zero;

        var vpImg = viewportGO.GetComponent<Image>();
        if (vpImg == null) vpImg = viewportGO.AddComponent<Image>();
        vpImg.raycastTarget = false;
        if (viewportGO.GetComponent<RectMask2D>() == null) viewportGO.AddComponent<RectMask2D>();

        Transform contentTrans = vpTrans.Find("Content") ?? FindChildRecursive(vpTrans, "Content");
        if (contentTrans == null)
        {
            Debug.LogWarning("ApplyRulePanel: Content not found under viewport; cannot build rule list.");
            return false;
        }

        var contentGO = contentTrans.gameObject;
        var contentRT = contentGO.GetComponent<RectTransform>();
        if (contentRT == null)
        {
            Debug.LogWarning("ApplyRulePanel: Content is missing RectTransform.");
            return false;
        }
        contentRT.anchorMin = new Vector2(0f, 1f);
        contentRT.anchorMax = new Vector2(1f, 1f);
        contentRT.pivot = new Vector2(0.5f, 1f);
        contentRT.anchoredPosition = Vector2.zero;
        contentRT.offsetMin = Vector2.zero;
        contentRT.offsetMax = Vector2.zero;

        var vlg = contentGO.GetComponent<VerticalLayoutGroup>();
        if (vlg == null) vlg = contentGO.AddComponent<VerticalLayoutGroup>();
        vlg.childControlHeight = true;
        vlg.childControlWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.spacing = 4f;
        vlg.padding = new RectOffset(0, 0, 0, 0);

        var csf = contentGO.GetComponent<ContentSizeFitter>();
        if (csf == null) csf = contentGO.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.content = contentRT;
        scroll.viewport = vpRT;
        scroll.horizontal = false;
        scroll.scrollSensitivity = 30f;

        var apScrollbarComp = scrollGO.transform.Find("VerticalScrollbar")?.GetComponent<Scrollbar>();
        if (apScrollbarComp != null)
        {
            scroll.verticalScrollbar = apScrollbarComp;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        }

        _contentRoot = contentGO.transform;
        return true;
    }

    private void CreateRuleRow(Transform parent, ISudokuRule rule, RuleResult preview, bool usedInAnalysis)
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
        btnImg.color = usedInAnalysis
            ? new Color(0.17f, 0.42f, 0.2f, 0.85f)
            : new Color(1f, 1f, 1f, 0.02f);
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

    private void CreateReinitialiseCandidatesRow(Transform parent)
    {
        var ruleGO = new GameObject("ReinitialiseCandidates_Row", typeof(RectTransform));
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
        label.text = "Reinitialise Candidates";
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
        // affected by reinitialisation. Exit clears the preview.
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

    /**
     * Create a non-interactive informational row.
     *
     * @param parent Parent transform where the row should be added.
     * @param message Message to display.
     */
    private void CreateInfoRow(Transform parent, string message)
    {
        if (parent == null) return;

        var rowGO = new GameObject("Info_Row", typeof(RectTransform));
        rowGO.transform.SetParent(parent, false);
        var rt = rowGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);

        var le = rowGO.AddComponent<LayoutElement>();
        le.preferredHeight = 36f;

        var img = rowGO.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.02f);
        img.raycastTarget = false;

        var labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(rowGO.transform, false);
        var label = labelGO.AddComponent<Text>();
        label.text = message;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 14;
        label.color = new Color(0.86f, 0.86f, 0.86f, 1f);
        label.alignment = TextAnchor.MiddleLeft;
        label.raycastTarget = false;

        var lblRT = labelGO.GetComponent<RectTransform>();
        lblRT.anchorMin = new Vector2(0f, 0f);
        lblRT.anchorMax = new Vector2(1f, 1f);
        lblRT.offsetMin = new Vector2(8f, 2f);
        lblRT.offsetMax = new Vector2(-8f, -2f);
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
     * Capture panel refresh-related state to detect in-place board changes.
     */
    private void CacheRefreshState()
    {
        _lastCandidatesInitialised = Runner != null && Runner.CandidatesInitialised;
        _lastCanReinitialiseCandidatesRemoveAny = Runner != null && Runner.CanReinitialiseCandidatesRemoveAny();
        _lastChangeLogIndex = Runner != null && Runner.CurrentBoard != null ? Runner.CurrentBoard.ChangeLogIndex : -1;
    }

    /**
     * Determine whether any non-reference state changed that affects row visibility.
     *
     * @returns True when the panel should be rebuilt.
     */
    private bool HasRefreshStateChanged()
    {
        if (Runner == null)
        {
            return false;
        }

        bool candidatesInitialised = Runner.CandidatesInitialised;
        bool canReinitialiseCandidates = Runner.CanReinitialiseCandidatesRemoveAny();
        int changeLogIndex = Runner.CurrentBoard != null ? Runner.CurrentBoard.ChangeLogIndex : -1;

        return candidatesInitialised != _lastCandidatesInitialised
            || canReinitialiseCandidates != _lastCanReinitialiseCandidatesRemoveAny
            || changeLogIndex != _lastChangeLogIndex;
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
}
