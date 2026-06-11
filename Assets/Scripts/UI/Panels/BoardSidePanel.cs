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
    // Cache of recent layout-affecting values to avoid unnecessary per-frame layout work
    private int _lastScreenWidth = -1;
    private int _lastScreenHeight = -1;
    private Vector2 _lastBoardOffset = new Vector2(float.MinValue, float.MinValue);
    private int _lastComputedCellSize = -1;
    private int _lastBoardSize = -1;
    private bool _preserveSceneLayout = true;
    private RectTransform _colourClearPanelRect;

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
            if (!_preserveSceneLayout) UpdatePanelGeometry();
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

        // Only update geometry when something that affects placement changes
        bool shouldUpdate = false;

        int sw = Screen.width;
        int sh = Screen.height;
        if (sw != _lastScreenWidth || sh != _lastScreenHeight)
        {
            shouldUpdate = true;
            _lastScreenWidth = sw;
            _lastScreenHeight = sh;
        }

        if (BoardVisualizer != null)
        {
            var bv = BoardVisualizer;
            int computedCell = bv.GetComputedCellSize();
            int boardSize = (bv.Runner != null && bv.Runner.CurrentBoard != null) ? bv.Runner.CurrentBoard.Size : -1;
            if (computedCell != _lastComputedCellSize || boardSize != _lastBoardSize || bv.Offset != _lastBoardOffset)
            {
                shouldUpdate = true;
                _lastComputedCellSize = computedCell;
                _lastBoardSize = boardSize;
                _lastBoardOffset = bv.Offset;
            }
        }

        if (shouldUpdate && !_preserveSceneLayout) UpdatePanelGeometry();
        UpdateColourClearPanelLayout();
    }

    private void EnsurePanel()
    {
        if (_panelRect != null) return;

        _preserveSceneLayout = true;

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

        UpdateColourClearPanelLayout();
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
        if (_panelRect == null || _preserveSceneLayout) return;
        float left = Padding;
        float top = Padding;
        float bottom = Padding;
        float right = Padding;

        // Make sure we have a valid canvas reference for pixel-accurate layout.
        if (TargetCanvas == null)
            TargetCanvas = FindAnyObjectByType<Canvas>();

        // Use screen dimensions for geometry because the board is drawn with IMGUI
        // coordinates (Screen space). This keeps both systems aligned.
        // Prefer canvas pixel rect when available (handles CanvasScaler)
        float canvasWidth = TargetCanvas != null ? TargetCanvas.pixelRect.width : Screen.width;
        float canvasHeight = TargetCanvas != null ? TargetCanvas.pixelRect.height : Screen.height;

        // Determine board rectangle in screen space
        if (BoardVisualizer != null && BoardVisualizer.Runner != null && BoardVisualizer.Runner.CurrentBoard != null)
        {
            int gridSize = BoardVisualizer.Runner.CurrentBoard.Size;
            int cell = BoardVisualizer.GetComputedCellSize();
            float boardLeft = BoardVisualizer.Offset.x;
            float boardTop = BoardVisualizer.Offset.y;
            float boardWidth = gridSize * cell;
            float boardHeight = gridSize * cell;

            // If the board, when placed at BoardVisualizer.Offset.x, would overflow
            // the screen (or Offset.x looks like a default small margin while the
            // board is actually centered), prefer a centered placement computed
            // from the screen width so the panel starts at the board's visible
            // right edge.
            float centeredLeft = Mathf.Round((Screen.width - boardWidth) / 2f);
            bool offsetLooksInvalid = float.IsNaN(boardLeft) || boardLeft < 0f || boardLeft + boardWidth + Padding > Screen.width;
            if (offsetLooksInvalid)
            {
                boardLeft = centeredLeft;
            }

            // The panel's left boundary should be just after the board's right edge
            left = Mathf.Round(boardLeft + boardWidth) + Padding;

            // Ensure minimum width is respected
            float availableWidth = canvasWidth - left - Padding;
            if (availableWidth < MinWidth)
            {
                // Move left so min width fits
                left = Mathf.Max(Padding, canvasWidth - MinWidth - Padding);
            }
        }
        else
        {
            // If we don't have board geometry yet, place the panel at the right edge
            left = Mathf.Max(Padding, canvasWidth - MinWidth - Padding);
        }

        // Prefer setting horizontal anchors using normalized canvas coordinates
        // so the panel reliably occupies the remaining right-side space even
        // when UI scaling or CanvasScaler is active.
        // Compute pixel rectangle for the panel: left offset and width in pixels
        float leftPixel = left;
        float widthPixels = Mathf.Max(MinWidth, canvasWidth - leftPixel - right);
        float heightPixels = Mathf.Max(0f, canvasHeight - top - bottom);

        // Anchor to top-left and set explicit size; position by anchoredPosition so we can align with IMGUI board top
        _panelRect.anchorMin = new Vector2(0f, 1f);
        _panelRect.anchorMax = new Vector2(0f, 1f);
        _panelRect.pivot = new Vector2(0.5f, 1f);
        _panelRect.sizeDelta = new Vector2(widthPixels, heightPixels);

        // If we have a BoardVisualizer, align the panel top with the board's top (IMGUI uses top-left screen origin).
        if (BoardVisualizer != null)
        {
            // BoardVisualizer.Offset is in IMGUI coordinates (top-left origin).
            Vector2 boardGuiOffset = BoardVisualizer.Offset; // x,y in IMGUI (top-left)
            // For a RectTransform anchored at (0,1) with pivot (0.5,1), the anchoredPosition
            // x should be leftPixel and y should be negative boardGuiOffset.y (to move down).
            _panelRect.anchoredPosition = new Vector2(leftPixel, -boardGuiOffset.y);

            Vector3[] corners = new Vector3[4];
            _panelRect.GetWorldCorners(corners);
            float topWorldY = corners[1].y; // top-left corner
        }
        else
        {
            // Fallback: simple anchored position using top inset
            float panelTopOffset = top;
            _panelRect.anchoredPosition = new Vector2(leftPixel, -panelTopOffset);
        }
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
}
}
