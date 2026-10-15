using UnityEngine;
using UnityEngine.UI;
using Sudoku.Solver;
using Sudoku.UI.Config;
using Sudoku.Models;

namespace Sudoku.UI.Panels
{

/**
 * Creates and maintains a UI panel that fills the canvas space to the right
 * of the rendered board (IMGUI board rendered by `BoardVisualizer`). The panel
 * will stretch full canvas height, start at the board's right edge plus a
 * padding, and respect a minimum width.
 */
[ExecuteAlways]
public class BoardSidePanel : MonoBehaviour
{
    public Canvas TargetCanvas;
    public Sudoku.Solver.BoardVisualizer BoardVisualizer;

    [Tooltip("Minimum width in pixels for the side panel")]
    public float MinWidth = 220f;

    [Tooltip("Padding in pixels between board and panel and edges")]
    public float Padding = 8f;

    [Tooltip("Background color for the panel")]
    public Color Background = new Color(0f, 0f, 0f, 0.6f);

    [Tooltip("Minimum width for each side-panel column")]
    public float PanelSlotMinWidth = 220f;

    [Tooltip("Preferred width for each side-panel column")]
    public float PanelSlotPreferredWidth = 260f;

    private RectTransform _panelRect;
    private Image _panelImage;
    public RectTransform RulesArea;
    private int _lastScreenWidth = -1;
    private int _lastScreenHeight = -1;
    private Rect _lastCanvasPixelRect;
    private float _lastCanvasScaleFactor = -1f;
    private Vector2 _lastBoardOffset = new Vector2(float.MinValue, float.MinValue);
    private int _lastComputedCellSize = -1;
    private int _lastBoardSize = -1;
    private RectTransform _colourClearPanelRect;
    private RectTransform _directionalLinkPanelRect;
    private const float DirectionalLinkPanelWidth = 48f;

    private void Awake()
    {
        if (TargetCanvas == null) TargetCanvas = Object.FindAnyObjectByType<Canvas>();
        if (BoardVisualizer == null) BoardVisualizer = Object.FindAnyObjectByType<Sudoku.Solver.BoardVisualizer>();

        if (!Application.isPlaying)
        {
            CacheExistingPanelReferences();
            return;
        }

        // Defer creating the side panel until Start so other scene objects
        // (notably the BoardVisualizer / SolverRunner) have a chance to
        // initialize in their Awake methods. Start() will wait for the board.
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            CacheExistingPanelReferences();
            return;
        }
        // Only initialize here if the board is already available; otherwise
        // Start() will perform initialization when ready.
        if (BoardVisualizer != null && BoardVisualizer.Runner != null && BoardVisualizer.Runner.CurrentBoard != null)
        {
            EnsurePanel();
            SyncApplyRulePanelVisibility();
            UpdatePanelGeometry();
        }
    }

    private void Start()
    {
        if (!Application.isPlaying) return;
        // Ensure references are present
        if (TargetCanvas == null) TargetCanvas = Object.FindAnyObjectByType<Canvas>();
        if (BoardVisualizer == null) BoardVisualizer = Object.FindAnyObjectByType<Sudoku.Solver.BoardVisualizer>();

        // Wire up the panel and panels immediately so SyncApplyRulePanelVisibility() works
        // from the very first Update frame. Geometry update is deferred until the board loads.
        EnsurePanel();
        SyncApplyRulePanelVisibility();
        StartCoroutine(WaitForBoardAndInit());
    }

    private SolverRunner GetRunner()
    {
        if (BoardVisualizer == null) BoardVisualizer = Object.FindAnyObjectByType<Sudoku.Solver.BoardVisualizer>();
        if (BoardVisualizer != null && BoardVisualizer.Runner != null) return BoardVisualizer.Runner;
        return Object.FindAnyObjectByType<SolverRunner>();
    }

    private System.Collections.IEnumerator WaitForBoardAndInit()
    {
        // Wait until a board is present (or timeout after a few seconds)
        float start = Time.realtimeSinceStartup;
        float timeout = 5f;
        while (Application.isPlaying && (BoardVisualizer == null || BoardVisualizer.Runner == null || BoardVisualizer.Runner.CurrentBoard == null))
        {
            if (Time.realtimeSinceStartup - start > timeout) break;
            yield return null;
            if (BoardVisualizer == null) BoardVisualizer = Object.FindAnyObjectByType<Sudoku.Solver.BoardVisualizer>();
        }

        EnsurePanel();
        SyncApplyRulePanelVisibility();
        UpdateColourClearPanelLayout();
        UpdateDirectionalLinkModePanelLayout();
        UpdatePanelGeometry();
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            // In designer mode do not mutate hierarchy/components. Only cache refs.
            if (_panelRect == null) CacheExistingPanelReferences();
            return;
        }

        // Enforce ApplyRulePanel visibility: hidden in Create mode, visible in Puzzle mode
        if (_panelRect == null) EnsurePanel();
        SyncApplyRulePanelVisibility();

        bool shouldUpdateGeometry = false;
        int screenWidth = Screen.width;
        int screenHeight = Screen.height;
        if (screenWidth != _lastScreenWidth || screenHeight != _lastScreenHeight)
        {
            _lastScreenWidth = screenWidth;
            _lastScreenHeight = screenHeight;
            shouldUpdateGeometry = true;
        }

        Rect canvasPixelRect = TargetCanvas != null
            ? TargetCanvas.pixelRect
            : new Rect(0f, 0f, screenWidth, screenHeight);
        float canvasScaleFactor = TargetCanvas != null ? TargetCanvas.scaleFactor : 1f;
        if (canvasPixelRect != _lastCanvasPixelRect ||
            !Mathf.Approximately(canvasScaleFactor, _lastCanvasScaleFactor))
        {
            _lastCanvasPixelRect = canvasPixelRect;
            _lastCanvasScaleFactor = canvasScaleFactor;
            shouldUpdateGeometry = true;
        }

        if (BoardVisualizer != null)
        {
            int computedCellSize = BoardVisualizer.GetComputedCellSize();
            int boardSize = BoardVisualizer.Runner != null && BoardVisualizer.Runner.CurrentBoard != null
                ? BoardVisualizer.Runner.CurrentBoard.Size
                : -1;
            if (computedCellSize != _lastComputedCellSize ||
                boardSize != _lastBoardSize ||
                BoardVisualizer.Offset != _lastBoardOffset)
            {
                _lastComputedCellSize = computedCellSize;
                _lastBoardSize = boardSize;
                _lastBoardOffset = BoardVisualizer.Offset;
                shouldUpdateGeometry = true;
            }
        }

        if (shouldUpdateGeometry) UpdatePanelGeometry();
        UpdateColourClearPanelLayout();
        UpdateDirectionalLinkModePanelLayout();
    }

    private void EnsurePanel()
    {
        if (_panelRect != null) return;

        if (TargetCanvas == null) TargetCanvas = Object.FindAnyObjectByType<Canvas>();
        if (TargetCanvas == null)
        {
            Debug.LogWarning("BoardSidePanel: No Canvas found. Runtime side panel wiring skipped.");
            return;
        }

        var sidePanel = TargetCanvas.transform.Find("SidePanel");
        if (sidePanel == null)
        {
            Debug.LogWarning("BoardSidePanel: Expected designer object 'SidePanel' was not found.");
            return;
        }

        _panelRect = sidePanel.GetComponent<RectTransform>();
        _panelImage = sidePanel.GetComponent<Image>();

        var rulesAreaTransform = sidePanel.Find("RulesArea");
        if (rulesAreaTransform == null)
        {
            Debug.LogWarning("BoardSidePanel: Expected designer object 'SidePanel/RulesArea' was not found.");
            return;
        }

        RulesArea = rulesAreaTransform.GetComponent<RectTransform>();
        EnsureRuleTogglePanelAttached();
        EnsureRuleListPanelAttached();
        EnsureCreateModeStatusPanelAttached();
        EnsureColourClearPanelAttached();
        EnsureDirectionalLinkModePanelAttached();
    }

    /**
     * Cache existing scene references in edit mode without creating or moving objects.
     */
    private void CacheExistingPanelReferences()
    {
        if (_panelRect != null) return;

        if (TargetCanvas == null) TargetCanvas = Object.FindAnyObjectByType<Canvas>();
        if (TargetCanvas == null) return;

        var sidePanel = TargetCanvas.transform.Find("SidePanel");
        if (sidePanel == null) return;

        _panelRect = sidePanel.GetComponent<RectTransform>();
        _panelImage = sidePanel.GetComponent<Image>();

        var rules = sidePanel.Find("RulesArea");
        if (rules != null)
        {
            RulesArea = rules.GetComponent<RectTransform>();
        }
    }

    private void EnsureRuleTogglePanelAttached()
    {
        if (RulesArea == null) return;

        var panel = RulesArea.GetComponentInChildren<RuleTogglePanel>(true);
        if (panel == null)
        {
            Debug.LogWarning("BoardSidePanel: Expected designer object with RuleTogglePanel was not found under RulesArea.");
            return;
        }

        panel.FlexFillParent = true;
        panel.Runner = GetRunner();
    }

    private void EnsureRuleListPanelAttached()
    {
        if (RulesArea == null) return;

        var panel = RulesArea.GetComponentInChildren<ApplyRulePanel>(true);
        if (panel == null)
        {
            Debug.LogWarning("BoardSidePanel: Expected designer object with ApplyRulePanel was not found under RulesArea.");
            return;
        }

        panel.Runner = GetRunner();
    }

    private void EnsureCreateModeStatusPanelAttached()
    {
        if (RulesArea == null) return;

        var panel = RulesArea.GetComponentInChildren<CreateModeStatusPanel>(true);
        if (panel == null)
        {
            Debug.LogWarning("BoardSidePanel: Expected designer object with CreateModeStatusPanel was not found under RulesArea.");
            return;
        }

        panel.Runner = GetRunner();
    }

    private void ConfigurePanelSlot(GameObject panelGO)
    {
        // Designer-mode values own layout and anchors for static panel slots.
        // Intentionally no-op to avoid runtime overrides.
    }

    /**
     * Every frame, enforce ApplyRulePanel visibility: hide in Create mode, show in Puzzle mode.
     * This ensures the Apply Rules panel is never visible when in Create mode.
     */
    private void SyncApplyRulePanelVisibility()
    {
        if (!Application.isPlaying) return;
        var runner = GetRunner();
        if (runner == null) return;

        bool isCreation = runner.IsPuzzleCreationMode;
        bool hideApplyRules = AssistanceSettings.HideApplyRules;

        if (_panelRect == null) return;

        var applyPanels = _panelRect.GetComponentsInChildren<ApplyRulePanel>(true);
        for (int i = 0; i < applyPanels.Length; i++)
        {
            var p = applyPanels[i];
            if (p != null)
            {
                p.gameObject.SetActive(!isCreation && !hideApplyRules);
            }
        }

        // Keep Toggle Rules visible in create mode, but when in solve mode and
        // "Hide Apply Rules" is enabled, hide Toggle Rules as well.
        bool hideToggleRules = !isCreation && hideApplyRules;
        var togglePanels = _panelRect.GetComponentsInChildren<RuleTogglePanel>(true);
        for (int i = 0; i < togglePanels.Length; i++)
        {
            var p = togglePanels[i];
            if (p != null)
            {
                p.gameObject.SetActive(!hideToggleRules);
            }
        }

        var statusPanels = _panelRect.GetComponentsInChildren<CreateModeStatusPanel>(true);
        for (int i = 0; i < statusPanels.Length; i++)
        {
            var p = statusPanels[i];
            if (p != null)
            {
                p.Runner = runner;
                p.gameObject.SetActive(isCreation);
                if (isCreation)
                {
                    p.RefreshStatus(force: true);
                }
            }
        }

        // ColourClearPanel: visible only in solve mode when at least one colour is enabled.
        var colourPanels = _panelRect.GetComponentsInChildren<ColourClearPanel>(true);
        for (int i = 0; i < colourPanels.Length; i++)
        {
            var p = colourPanels[i];
            if (p == null) continue;
            bool showColourPanel = !isCreation && ColourSettings.AnyEnabled;
            if (p.gameObject.activeSelf != showColourPanel)
            {
                p.gameObject.SetActive(showColourPanel);
            }
        }

        var directionalPanels = _panelRect.GetComponentsInChildren<DirectionalLinkModePanel>(true);
        for (int i = 0; i < directionalPanels.Length; i++)
        {
            var p = directionalPanels[i];
            if (p == null) continue;
            p.Runner = runner;
            p.BoardVisualizer = BoardVisualizer;
            bool showDirectionalPanel = !isCreation;
            if (p.gameObject.activeSelf != showDirectionalPanel)
            {
                p.gameObject.SetActive(showDirectionalPanel);
            }
        }

        UpdateColourClearPanelLayout();
        UpdateDirectionalLinkModePanelLayout();
    }

    /**
     * Force an immediate refresh of side-panel visibility using the current runner mode.
     * Safe to call from other runtime controllers right after they change puzzle state.
     */
    public void RefreshPanelVisibilityForCurrentMode()
    {
        if (!Application.isPlaying) return;

        if (_panelRect == null)
        {
            EnsurePanel();
        }

        SyncApplyRulePanelVisibility();
    }

    private void UpdatePanelGeometry()
    {
        if (_panelRect == null) return;

        if (TargetCanvas == null)
        {
            TargetCanvas = FindAnyObjectByType<Canvas>();
        }

        if (TargetCanvas == null || !(TargetCanvas.transform is RectTransform canvasRect))
        {
            return;
        }

        var canvasBounds = canvasRect.rect;
        float leftAnchor = canvasBounds.width > 0f
            ? Mathf.Clamp01((canvasBounds.width - MinWidth - Padding) / canvasBounds.width)
            : 1f;
        float topOffset = Padding;
        if (BoardVisualizer != null && BoardVisualizer.TryGetBoardRect(out var boardRect))
        {
            // IMGUI uses a top-left origin; convert the board's right/top screen point
            // into the canvas' local coordinates before deriving normalized anchors.
            var boardRightTop = new Vector2(
                boardRect.xMax + Padding,
                Screen.height - boardRect.yMin);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect,
                    boardRightTop,
                    TargetCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : TargetCanvas.worldCamera,
                    out var localPoint))
            {
                if (canvasBounds.width > 0f)
                {
                    leftAnchor = Mathf.Clamp01((localPoint.x - canvasBounds.xMin) / canvasBounds.width);
                }

                topOffset = Mathf.Max(0f, canvasBounds.yMax - localPoint.y);
            }
        }

        // Stretch from the board's right edge to the canvas right edge. Anchors
        // make the remaining width follow the canvas instead of storing a pixel width.
        _panelRect.anchorMin = new Vector2(leftAnchor, 0f);
        _panelRect.anchorMax = Vector2.one;
        _panelRect.pivot = new Vector2(0f, 1f);
        _panelRect.offsetMin = new Vector2(0f, Padding);
        _panelRect.offsetMax = new Vector2(-Padding, -topOffset);
    }
    /**
     * Create and wire up the ColourClearPanel if it does not already exist.
     * The panel is placed at the bottom of the SidePanel with content-size fitting.
     */
    private void EnsureColourClearPanelAttached()
    {
        if (_panelRect == null || RulesArea == null) return;

        var existing = _panelRect.GetComponentInChildren<ColourClearPanel>(true);
        if (existing != null)
        {
            existing.Runner = GetRunner();
            var existingRect = existing.transform as RectTransform;
            if (existingRect != null)
            {
                existingRect.SetParent(_panelRect, false);
                _colourClearPanelRect = existingRect;
            }
            EnsureColourClearPanelLayoutComponents(_colourClearPanelRect);
            UpdateColourClearPanelLayout();
            return;
        }

        var panelGO = new GameObject("ColourClearPanel", typeof(RectTransform));
        panelGO.transform.SetParent(_panelRect, false);

        var rt = panelGO.GetComponent<RectTransform>();
        // Stretch to side-panel width and sit under RulesArea.
        rt.anchorMin        = new Vector2(0f, 1f);
        rt.anchorMax        = new Vector2(1f, 1f);
        rt.pivot            = new Vector2(0.5f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta        = new Vector2(0f, 0f);

        EnsureColourClearPanelLayoutComponents(rt);

        var panel = panelGO.AddComponent<ColourClearPanel>();
        panel.Runner = GetRunner();
        _colourClearPanelRect = rt;
        UpdateColourClearPanelLayout();
        panelGO.SetActive(false); // SyncApplyRulePanelVisibility controls visibility.
    }

    private void EnsureColourClearPanelLayoutComponents(RectTransform colourPanelRect)
    {
        if (colourPanelRect == null)
        {
            return;
        }

        var csf = colourPanelRect.GetComponent<UnityEngine.UI.ContentSizeFitter>();
        if (csf == null)
        {
            csf = colourPanelRect.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
        }
        csf.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
    }

    private void UpdateColourClearPanelLayout()
    {
        if (_panelRect == null || RulesArea == null)
        {
            return;
        }

        if (_colourClearPanelRect == null)
        {
            var panel = _panelRect.GetComponentInChildren<ColourClearPanel>(true);
            _colourClearPanelRect = panel != null ? panel.transform as RectTransform : null;
            if (_colourClearPanelRect == null)
            {
                return;
            }
        }

        if (_colourClearPanelRect.parent != _panelRect)
        {
            _colourClearPanelRect.SetParent(_panelRect, false);
        }

        _colourClearPanelRect.anchorMin = new Vector2(0f, 1f);
        _colourClearPanelRect.anchorMax = new Vector2(1f, 1f);
        _colourClearPanelRect.pivot = new Vector2(0.5f, 1f);

        float yOffset = RulesArea.anchoredPosition.y - RulesArea.rect.height - 6f;
        _colourClearPanelRect.anchoredPosition = new Vector2(0f, yOffset);
        _colourClearPanelRect.sizeDelta = new Vector2(0f, _colourClearPanelRect.sizeDelta.y);

        int desiredIndex = Mathf.Min(RulesArea.GetSiblingIndex() + 1, _panelRect.childCount - 1);
        if (_colourClearPanelRect.GetSiblingIndex() != desiredIndex)
        {
            _colourClearPanelRect.SetSiblingIndex(desiredIndex);
        }
    }

    /**
     * Create and wire up the DirectionalLinkModePanel if it does not already exist.
     */
    private void EnsureDirectionalLinkModePanelAttached()
    {
        if (_panelRect == null || RulesArea == null)
        {
            return;
        }

        var existing = _panelRect.GetComponentInChildren<DirectionalLinkModePanel>(true);
        if (existing != null)
        {
            existing.Runner = GetRunner();
            existing.BoardVisualizer = BoardVisualizer;
            var existingRect = existing.transform as RectTransform;
            if (existingRect != null)
            {
                existingRect.SetParent(_panelRect, false);
                _directionalLinkPanelRect = existingRect;
            }

            EnsureDirectionalLinkModePanelLayoutComponents(_directionalLinkPanelRect);
            UpdateDirectionalLinkModePanelLayout();
            return;
        }

        var panelGO = new GameObject("DirectionalLinkModePanel", typeof(RectTransform));
        panelGO.transform.SetParent(_panelRect, false);

        var rt = panelGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(DirectionalLinkPanelWidth, 0f);

        EnsureDirectionalLinkModePanelLayoutComponents(rt);

        var panel = panelGO.AddComponent<DirectionalLinkModePanel>();
        panel.Runner = GetRunner();
        panel.BoardVisualizer = BoardVisualizer;
        _directionalLinkPanelRect = rt;
        UpdateDirectionalLinkModePanelLayout();
        panelGO.SetActive(false);
    }

    private void EnsureDirectionalLinkModePanelLayoutComponents(RectTransform directionalPanelRect)
    {
        if (directionalPanelRect == null)
        {
            return;
        }

        var csf = directionalPanelRect.GetComponent<UnityEngine.UI.ContentSizeFitter>();
        if (csf == null)
        {
            csf = directionalPanelRect.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
        }

        csf.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
    }

    private void UpdateDirectionalLinkModePanelLayout()
    {
        if (_panelRect == null || RulesArea == null)
        {
            return;
        }

        if (_directionalLinkPanelRect == null)
        {
            var panel = _panelRect.GetComponentInChildren<DirectionalLinkModePanel>(true);
            _directionalLinkPanelRect = panel != null ? panel.transform as RectTransform : null;
            if (_directionalLinkPanelRect == null)
            {
                return;
            }
        }

        if (_directionalLinkPanelRect.parent != _panelRect)
        {
            _directionalLinkPanelRect.SetParent(_panelRect, false);
        }

        _directionalLinkPanelRect.anchorMin = new Vector2(0f, 1f);
        _directionalLinkPanelRect.anchorMax = new Vector2(0f, 1f);
        _directionalLinkPanelRect.pivot = new Vector2(0f, 1f);

        float anchorFromRules = RulesArea.anchoredPosition.y - RulesArea.rect.height - 6f;
        float anchorFromColour = float.MinValue;
        if (_colourClearPanelRect != null)
        {
            anchorFromColour = _colourClearPanelRect.anchoredPosition.y - _colourClearPanelRect.rect.height - 6f;
        }

        float yOffset = Mathf.Min(anchorFromRules, anchorFromColour == float.MinValue ? anchorFromRules : anchorFromColour);
        _directionalLinkPanelRect.anchoredPosition = new Vector2(0f, yOffset);
        _directionalLinkPanelRect.sizeDelta = new Vector2(DirectionalLinkPanelWidth, _directionalLinkPanelRect.sizeDelta.y);

        int desiredIndex = _panelRect.childCount - 1;

        if (_directionalLinkPanelRect.GetSiblingIndex() != desiredIndex)
        {
            _directionalLinkPanelRect.SetSiblingIndex(desiredIndex);
        }
    }
}
}
