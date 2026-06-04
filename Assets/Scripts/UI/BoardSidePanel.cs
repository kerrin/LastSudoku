using UnityEngine;
using UnityEngine.UI;
using Sudoku.Solver;

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
    private int _lastInteractionMode = -1;
    private bool _preserveSceneLayout = true;

    private void Awake()
    {
        if (TargetCanvas == null) TargetCanvas = Object.FindAnyObjectByType<Canvas>();
        if (BoardVisualizer == null) BoardVisualizer = Object.FindAnyObjectByType<Sudoku.Solver.BoardVisualizer>();

        if (!Application.isPlaying)
        {
            EnsurePanel();
            EnsureCreateModeStatusPanelAttached();
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
            EnsurePanel();
            EnsureCreateModeStatusPanelAttached();
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
        UpdatePanelGeometry();
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            // Only re-ensure panel structure; do NOT force-reset visibility every frame
            // so the user can manually preview either state in the designer.
            if (_panelRect == null) EnsurePanel();
            EnsureCreateModeStatusPanelAttached();
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
    }

    private void EnsurePanel()
    {
        if (_panelRect != null) return;

        // If a TargetCanvas is already set, prefer reusing any existing SidePanel
        // on that canvas to avoid creating duplicates. Do this regardless of
        // the canvas' render mode.
        if (TargetCanvas != null)
        {
            var existing = TargetCanvas.transform.Find("SidePanel");
            if (existing != null)
            {
                _preserveSceneLayout = true;
                _panelRect = existing.GetComponent<RectTransform>();
                _panelImage = existing.GetComponent<Image>();
                var ra = existing.Find("RulesArea");
                if (ra == null) ra = EnsureRulesAreaExists(existing.gameObject);
                if (ra != null)
                {
                    RulesArea = ra.GetComponent<RectTransform>();
                    EnsureRulesAreaLayout(ra.gameObject);
                }
                EnsureRuleTogglePanelAttached();
                EnsureCreateModeStatusPanelAttached();
                return;
            }
        }

        // Prefer an existing ScreenSpaceOverlay canvas. If TargetCanvas is unset
        // or set to a non-overlay mode (WorldSpace), try to find an overlay canvas
        // in the scene. Otherwise create a default overlay canvas.
        if (TargetCanvas == null || TargetCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            Canvas found = null;
            foreach (var c in Object.FindObjectsByType<Canvas>())
            {
                if (c.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    found = c;
                    break;
                }
            }
            if (found != null)
            {
                TargetCanvas = found;
            }
            else
            {
                // Create a simple default canvas if none exists (screen-space overlay)
                var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                TargetCanvas = canvasGO.GetComponent<Canvas>();
                TargetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }
        }

        // After locating/creating the TargetCanvas, check once more for an
        // existing SidePanel that may already be present on it.
        if (TargetCanvas != null)
        {
            var existing2 = TargetCanvas.transform.Find("SidePanel");
            if (existing2 != null)
            {
                _preserveSceneLayout = true;
                _panelRect = existing2.GetComponent<RectTransform>();
                _panelImage = existing2.GetComponent<Image>();
                var ra = existing2.Find("RulesArea");
                if (ra == null) ra = EnsureRulesAreaExists(existing2.gameObject);
                if (ra != null)
                {
                    RulesArea = ra.GetComponent<RectTransform>();
                    EnsureRulesAreaLayout(ra.gameObject);
                }
                EnsureRuleTogglePanelAttached();
                EnsureCreateModeStatusPanelAttached();
                return;
            }
        }

        var panelGO = new GameObject("SidePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        _preserveSceneLayout = false;
        panelGO.transform.SetParent(TargetCanvas.transform, false);
        _panelRect = panelGO.GetComponent<RectTransform>();
        _panelImage = panelGO.GetComponent<Image>();
        _panelImage.color = Background;
        // Don't let the background image intercept raycasts; children should handle input.
        _panelImage.raycastTarget = false;

        // Stretch full canvas and use offsets to place it to the right of the board
        _panelRect.anchorMin = Vector2.zero;
        _panelRect.anchorMax = Vector2.one;
        // Use a centered pivot so offsets apply symmetrically and anchoredPosition behaves predictably
        _panelRect.pivot = new Vector2(0.5f, 0.5f);
        _panelRect.anchoredPosition = Vector2.zero;

        // Create a dedicated area for rules and other UI inside the panel.
        var rulesGO = new GameObject("RulesArea", typeof(RectTransform));
        rulesGO.transform.SetParent(panelGO.transform, false);
        RulesArea = rulesGO.GetComponent<RectTransform>();
        RulesArea.anchorMin = new Vector2(0f, 0f);
        RulesArea.anchorMax = new Vector2(1f, 1f);
        RulesArea.pivot = new Vector2(0.5f, 0.5f);
        // Apply padding via offsets so children won't sit flush against edges
        RulesArea.offsetMin = new Vector2(Padding, Padding);
        RulesArea.offsetMax = new Vector2(-Padding, -Padding);
        // Use a HorizontalLayoutGroup so toggle and apply panels sit side-by-side as equal columns.
        EnsureRulesAreaLayout(rulesGO);

        // Ensure a RuleTogglePanel is attached inside the RulesArea so runtime
        // toggles appear in the side panel automatically.
        EnsureRuleTogglePanelAttached();
        EnsureCreateModeStatusPanelAttached();
        // Update geometry immediately and again next frame to ensure proper placement
        if (!_preserveSceneLayout)
        {
            UpdatePanelGeometry();
            StartCoroutine(PositionNextFrame());
        }
    }

    private System.Collections.IEnumerator PositionNextFrame()
    {
        yield return null;
        UpdatePanelGeometry();
    }

    /**
     * Ensures the RulesArea uses a HorizontalLayoutGroup for the 2-column layout,
     * upgrading from any legacy VerticalLayoutGroup in the scene.
     *
     * @param rulesGO The RulesArea game object to configure.
     */
    private void EnsureRulesAreaLayout(GameObject rulesGO)
    {
        // Replace any pre-existing VLG with HLG for side-by-side column layout.
        // Use DestroyImmediate so the VLG is gone before AddComponent<HLG> runs
        // (Destroy is deferred and having both on the same object causes layout conflicts).
        var vlg = rulesGO.GetComponent<VerticalLayoutGroup>();
        if (vlg != null) DestroyImmediate(vlg);
        var hlg = rulesGO.GetComponent<HorizontalLayoutGroup>();
        if (hlg == null) hlg = rulesGO.AddComponent<HorizontalLayoutGroup>();
        hlg.childControlHeight     = true;
        hlg.childForceExpandHeight = true;
        hlg.childControlWidth      = true;
        hlg.childForceExpandWidth  = true;
        hlg.spacing                = 8f;
        hlg.padding                = new RectOffset(0, 0, 0, 0);
        // Note: do NOT touch RulesArea.offsetMin/Max here — callers manage the RT.
    }

    /**
     * Creates a RulesArea container inside the given SidePanel and re-parents
     * any existing RuleTogglePanel and ApplyRulePanel direct children into it.
     * Called when the scene has the panels as flat children of SidePanel (legacy).
     *
     * @param sidePanelGO The SidePanel game object.
     * @returns The newly created RulesArea Transform.
     */
    private Transform EnsureRulesAreaExists(GameObject sidePanelGO)
    {
        var rulesGO = new GameObject("RulesArea", typeof(RectTransform));
        rulesGO.transform.SetParent(sidePanelGO.transform, false);
        // Insert before ChangeLogControls so the rule panels appear first.
        rulesGO.transform.SetSiblingIndex(0);
        var rulesRT = rulesGO.GetComponent<RectTransform>();
        // Top-anchored, full-width strip with a screen-proportion height.
        rulesRT.anchorMin        = new Vector2(0f, 1f);
        rulesRT.anchorMax        = new Vector2(1f, 1f);
        rulesRT.pivot            = new Vector2(0.5f, 1f);
        rulesRT.anchoredPosition = new Vector2(0f, -Padding);
        float h = Mathf.Min(400f, Mathf.Max(80f, (float)Screen.height * 0.4f));
        rulesRT.sizeDelta        = new Vector2(-Padding * 2f, h);
        RulesArea = rulesRT;

        // Re-parent the toggle panel and apply rules panel
        var toggleT = sidePanelGO.transform.Find("RuleTogglePanel");
        if (toggleT != null) toggleT.SetParent(rulesGO.transform, false);
        var applyT = sidePanelGO.transform.Find("ApplyRulePanel");
        if (applyT != null) applyT.SetParent(rulesGO.transform, false);

        return rulesGO.transform;
    }

    private void EnsureRuleTogglePanelAttached()
    {
        if (_panelRect == null) return;
        if (RulesArea == null) return;

        // If a RuleTogglePanel already exists anywhere in the scene, reparent
        // the first instance under the SidePanel so we don't create duplicates.
        // Include inactive objects so a panel that was hidden by editor preview is still found.
        var all = Object.FindObjectsByType<RuleTogglePanel>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (all != null && all.Length > 0)
        {
            RuleTogglePanel chosen = null;
            foreach (var p in all)
            {
                var cur = p.transform;
                while (cur != null)
                {
                    if (cur.name == "SidePanel")
                    {
                        chosen = p;
                        break;
                    }
                    cur = cur.parent;
                }
                if (chosen != null) break;
            }
            if (chosen == null) chosen = all[0];

            if (chosen.transform.parent != RulesArea)
                chosen.transform.SetParent(RulesArea, false);

            ConfigurePanelSlot(chosen.gameObject);

            // Tell the toggle panel not to impose its own fixed width.
            var rtpComp = chosen.GetComponent<RuleTogglePanel>();
            if (rtpComp != null) rtpComp.FlexFillParent = true;

            if (BoardVisualizer != null && BoardVisualizer.Runner != null)
            {
                chosen.Runner = BoardVisualizer.Runner;
            }
            else
            {
                chosen.Runner = Object.FindAnyObjectByType<Sudoku.Solver.SolverRunner>();
            }

            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == chosen) continue;
                Destroy(all[i].gameObject);
            }

            // Ensure the ApplyRulePanel is present.
            EnsureRuleListPanelAttached();
            EnsureCreateModeStatusPanelAttached();
            return;
        }

        // No existing instance found: create a host object as a direct child of the SidePanel
        var hostGO = new GameObject("RuleTogglePanelHost", typeof(RectTransform));
        hostGO.transform.SetParent(RulesArea, false);
        var rt = hostGO.GetComponent<RectTransform>();
        // Host should not stretch horizontally; the toggle panel will have a preferred width
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(Padding, -Padding);
        ConfigurePanelSlot(hostGO);

        var panel = hostGO.AddComponent<RuleTogglePanel>();
        panel.FlexFillParent = true;

        if (BoardVisualizer != null && BoardVisualizer.Runner != null)
        {
            panel.Runner = BoardVisualizer.Runner;
        }
        else
        {
            panel.Runner = Object.FindAnyObjectByType<Sudoku.Solver.SolverRunner>();
        }

        // Ensure the ApplyRulePanel is present.
        EnsureRuleListPanelAttached();
        // Ensure the CreateModeStatusPanel is present for Create mode.
        EnsureCreateModeStatusPanelAttached();
    }

    private void EnsureRuleListPanelAttached()
    {
        if (RulesArea == null) return;

        // Include inactive objects — the panel may have been hidden by editor preview.
        var all = Object.FindObjectsByType<ApplyRulePanel>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (all != null && all.Length > 0)
        {
            ApplyRulePanel chosen = null;
            foreach (var p in all)
            {
                var cur = p.transform;
                while (cur != null)
                {
                    if (cur.name == "SidePanel")
                    {
                        chosen = p;
                        break;
                    }
                    cur = cur.parent;
                }
                if (chosen != null) break;
            }
            if (chosen == null) chosen = all[0];

            if (chosen.transform.parent != RulesArea)
                chosen.transform.SetParent(RulesArea, false);
            ConfigurePanelSlot(chosen.gameObject);

            if (BoardVisualizer != null && BoardVisualizer.Runner != null)
            {
                chosen.Runner = BoardVisualizer.Runner;
            }
            else
            {
                chosen.Runner = Object.FindAnyObjectByType<SolverRunner>();
            }

            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == chosen) continue;
                Destroy(all[i].gameObject);
            }

            return;
        }

        var hostGO = new GameObject("RuleListPanelHost", typeof(RectTransform));
        hostGO.transform.SetParent(RulesArea, false);
        var rt = hostGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        ConfigurePanelSlot(hostGO);

        var panel = hostGO.AddComponent<ApplyRulePanel>();
        if (BoardVisualizer != null && BoardVisualizer.Runner != null)
        {
            panel.Runner = BoardVisualizer.Runner;
        }
        else
        {
            panel.Runner = Object.FindAnyObjectByType<SolverRunner>();
        }
    }

    private void EnsureCreateModeStatusPanelAttached()
    {
        if (RulesArea == null) return;

        // Find or create CreateModeStatusPanel in the RulesArea so it can replace
        // ApplyRulePanel as the right-hand column in create mode.
        var existing = RulesArea.GetComponentInChildren<CreateModeStatusPanel>(true);
        if (existing != null)
        {
            if (existing.transform.parent != RulesArea)
            {
                existing.transform.SetParent(RulesArea, false);
            }

            ConfigurePanelSlot(existing.gameObject);

            // Wire Runner if needed
            if (existing.Runner == null)
            {
                existing.Runner = GetRunner();
            }
            return;
        }

        // Create a hosted panel inside RulesArea.
        var hostGO = new GameObject("CreateModeStatusPanelHost", typeof(RectTransform));
        hostGO.transform.SetParent(RulesArea, false);
        var rt = hostGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        ConfigurePanelSlot(hostGO);

        var panel = hostGO.AddComponent<CreateModeStatusPanel>();
        panel.Runner = GetRunner();
    }

    private void ConfigurePanelSlot(GameObject panelGO)
    {
        if (panelGO == null) return;

        var rect = panelGO.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.localScale = Vector3.one;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        var layout = panelGO.GetComponent<LayoutElement>();
        if (layout == null) layout = panelGO.AddComponent<LayoutElement>();
        layout.ignoreLayout = false;
        layout.minWidth = PanelSlotMinWidth;
        layout.preferredWidth = PanelSlotPreferredWidth;
        layout.flexibleWidth = 1f;
        layout.minHeight = 0f;
        layout.preferredHeight = 0f;
        layout.flexibleHeight = 1f;

        var canvasGroup = panelGO.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
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

        if (_panelRect == null) return;

        var applyPanels = _panelRect.GetComponentsInChildren<ApplyRulePanel>(true);
        for (int i = 0; i < applyPanels.Length; i++)
        {
            var p = applyPanels[i];
            if (p != null)
            {
                ConfigurePanelSlot(p.gameObject);
                p.gameObject.SetActive(!isCreation);
            }
        }

        var statusPanels = _panelRect.GetComponentsInChildren<CreateModeStatusPanel>(true);
        for (int i = 0; i < statusPanels.Length; i++)
        {
            var p = statusPanels[i];
            if (p != null)
            {
                p.Runner = runner;
                ConfigurePanelSlot(p.gameObject);
                p.gameObject.SetActive(isCreation);
                if (isCreation)
                {
                    p.RefreshStatus(force: true);
                }
            }
        }
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
}
