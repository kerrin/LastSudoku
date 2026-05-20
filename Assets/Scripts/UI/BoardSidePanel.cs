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

    private RectTransform _panelRect;
    private Image _panelImage;
    public RectTransform RulesArea;
    // Cache of recent layout-affecting values to avoid unnecessary per-frame layout work
    private int _lastScreenWidth = -1;
    private int _lastScreenHeight = -1;
    private Vector2 _lastBoardOffset = new Vector2(float.MinValue, float.MinValue);
    private int _lastComputedCellSize = -1;
    private int _lastBoardSize = -1;

    private void Awake()
    {
        if (!Application.isPlaying) return;
        if (TargetCanvas == null) TargetCanvas = Object.FindAnyObjectByType<Canvas>();
        if (BoardVisualizer == null) BoardVisualizer = Object.FindAnyObjectByType<Sudoku.Solver.BoardVisualizer>();
        // Defer creating the side panel until Start so other scene objects
        // (notably the BoardVisualizer / SolverRunner) have a chance to
        // initialize in their Awake methods. Start() will wait for the board.
    }

    private void OnEnable()
    {
        if (!Application.isPlaying) return;
        // Only initialize here if the board is already available; otherwise
        // Start() will perform initialization when ready.
        if (BoardVisualizer != null && BoardVisualizer.Runner != null && BoardVisualizer.Runner.CurrentBoard != null)
        {
            EnsurePanel();
            UpdatePanelGeometry();
        }
    }

    private void Start()
    {
        if (!Application.isPlaying) return;
        // Ensure references are present
        if (TargetCanvas == null) TargetCanvas = Object.FindAnyObjectByType<Canvas>();
        if (BoardVisualizer == null) BoardVisualizer = Object.FindAnyObjectByType<Sudoku.Solver.BoardVisualizer>();
        StartCoroutine(WaitForBoardAndInit());
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
        UpdatePanelGeometry();
    }

    private void Update()
    {
        if (!Application.isPlaying) return;
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

        if (shouldUpdate) UpdatePanelGeometry();
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
                _panelRect = existing.GetComponent<RectTransform>();
                _panelImage = existing.GetComponent<Image>();
                var ra = existing.Find("RulesArea");
                if (ra != null) RulesArea = ra.GetComponent<RectTransform>();
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
                _panelRect = existing2.GetComponent<RectTransform>();
                _panelImage = existing2.GetComponent<Image>();
                var ra = existing2.Find("RulesArea");
                if (ra != null) RulesArea = ra.GetComponent<RectTransform>();
                return;
            }
        }

        var panelGO = new GameObject("SidePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
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
        // Use a VerticalLayoutGroup so child panels stack: toggle block on top, list fills remaining space
        var rulesLayout = rulesGO.AddComponent<VerticalLayoutGroup>();
        rulesLayout.childControlHeight = false;
        rulesLayout.childControlWidth = true;
        rulesLayout.childForceExpandHeight = false;
        rulesLayout.childForceExpandWidth = true;
        rulesLayout.spacing = 6f;
        rulesLayout.padding = new RectOffset(0,0,0,0);

        // Ensure a RuleTogglePanel is attached inside the RulesArea so runtime
        // toggles appear in the side panel automatically.
        EnsureRuleTogglePanelAttached();
        // Update geometry immediately and again next frame to ensure proper placement
        UpdatePanelGeometry();
        StartCoroutine(PositionNextFrame());
    }

    private System.Collections.IEnumerator PositionNextFrame()
    {
        yield return null;
        UpdatePanelGeometry();
    }

    private void EnsureRuleTogglePanelAttached()
    {
        if (_panelRect == null) return;
        if (RulesArea == null) return;

        LayoutElement toggleLE = null;

        // If a RuleTogglePanel already exists anywhere in the scene, reparent
        // the first instance under the SidePanel so we don't create duplicates.
        var all = Object.FindObjectsByType<RuleTogglePanel>();
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

            if (chosen.transform.parent != _panelRect)
            {
                chosen.transform.SetParent(_panelRect, false);
                var crt = chosen.GetComponent<RectTransform>();
                if (crt != null)
                {
                    crt.anchorMin = new Vector2(0f, 1f);
                    crt.anchorMax = new Vector2(1f, 1f);
                    crt.pivot = new Vector2(0f, 1f);
                    crt.anchoredPosition = new Vector2(Padding, -Padding);
                    var le = chosen.GetComponent<LayoutElement>();
                    if (le == null) le = chosen.gameObject.AddComponent<LayoutElement>();
                    le.preferredHeight = Mathf.Min(220f,  Mathf.Max(80f,  (float)Screen.height * 0.25f));
                    le.flexibleHeight = 0f;
                    toggleLE = le;
                }
                Debug.Log($"BoardSidePanel: Reparented existing RuleTogglePanel '{chosen.gameObject.name}' into SidePanel top.");
            }

            if (BoardVisualizer != null && BoardVisualizer.Runner != null)
            {
                chosen.Runner = BoardVisualizer.Runner;
                Debug.Log("BoardSidePanel: Wired RuleTogglePanel.Runner from BoardVisualizer.Runner");
            }
            else
            {
                chosen.Runner = Object.FindAnyObjectByType<Sudoku.Solver.SolverRunner>();
                Debug.Log($"BoardSidePanel: Wired RuleTogglePanel.Runner fallback={(chosen.Runner!=null)}");
            }

            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == chosen) continue;
                Debug.Log($"BoardSidePanel: Removing extra RuleTogglePanel instance '{all[i].gameObject.name}'");
                Destroy(all[i].gameObject);
            }

            // Ensure the list is present and then adjust its top inset below the toggle
            EnsureRuleListPanelAttached();
            if (toggleLE != null)
            {
                float topInset = Padding + toggleLE.preferredHeight + 6f;
                RulesArea.offsetMax = new Vector2(RulesArea.offsetMax.x, -topInset);
            }
            return;
        }

        // No existing instance found: create a host object as a direct child of the SidePanel
        var host = new GameObject("RuleTogglePanelHost", typeof(RectTransform));
        host.transform.SetParent(_panelRect, false);
        var rt = host.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(Padding, -Padding);
        var hostLE = host.AddComponent<LayoutElement>();
        hostLE.preferredHeight = Mathf.Min(220f,  Mathf.Max(80f,  (float)Screen.height * 0.25f));
        hostLE.flexibleHeight = 0f;
        toggleLE = hostLE;

        var panel = host.AddComponent<RuleTogglePanel>();
        Debug.Log($"BoardSidePanel: Created RuleTogglePanel host '{host.name}' and attached RuleTogglePanel component at SidePanel top.");

        if (BoardVisualizer != null && BoardVisualizer.Runner != null)
        {
            panel.Runner = BoardVisualizer.Runner;
            Debug.Log("BoardSidePanel: Wired RuleTogglePanel.Runner from BoardVisualizer.Runner");
        }
        else
        {
            panel.Runner = Object.FindAnyObjectByType<Sudoku.Solver.SolverRunner>();
            Debug.Log($"BoardSidePanel: Wired RuleTogglePanel.Runner fallback={(panel.Runner!=null)}");
        }

        // Also ensure a RuleListPanel is present in the RulesArea to list/apply rules
        EnsureRuleListPanelAttached();

        if (toggleLE != null)
        {
            float topInset = Padding + toggleLE.preferredHeight + 6f;
            RulesArea.offsetMax = new Vector2(RulesArea.offsetMax.x, -topInset);
        }
    }

    private void EnsureRuleListPanelAttached()
    {
        if (RulesArea == null) return;

        var all = Object.FindObjectsByType<RuleListPanel>();
        if (all != null && all.Length > 0)
        {
            RuleListPanel chosen = null;
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
                {
                    chosen.transform.SetParent(RulesArea, false);
                    var crt = chosen.GetComponent<RectTransform>();
                    if (crt != null)
                    {
                        // Stretch horizontally and allow VerticalLayoutGroup to size this child
                        crt.anchorMin = new Vector2(0f, 0f);
                        crt.anchorMax = new Vector2(1f, 1f);
                        crt.pivot = new Vector2(0.5f, 0.5f);
                        crt.anchoredPosition = Vector2.zero;
                        // ensure LayoutElement to make it flexible height
                        var le = chosen.GetComponent<LayoutElement>();
                        if (le == null) le = chosen.gameObject.AddComponent<LayoutElement>();
                        le.flexibleHeight = 1f;
                    }
                    Debug.Log($"BoardSidePanel: Reparented existing RuleListPanel '{chosen.gameObject.name}' into RulesArea.");
                }

            // Wire Runner on chosen instance
            if (BoardVisualizer != null && BoardVisualizer.Runner != null)
            {
                chosen.Runner = BoardVisualizer.Runner;
                Debug.Log("BoardSidePanel: Wired RuleListPanel.Runner from BoardVisualizer.Runner");
            }
            else
            {
                chosen.Runner = Object.FindAnyObjectByType<SolverRunner>();
                Debug.Log($"BoardSidePanel: Wired RuleListPanel.Runner fallback={(chosen.Runner!=null)}");
            }

            // Destroy duplicates
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == chosen) continue;
                Debug.Log($"BoardSidePanel: Removing extra RuleListPanel instance '{all[i].gameObject.name}'");
                Destroy(all[i].gameObject);
            }
            return;
        }

        // No existing RuleListPanel: create host and attach
        var host = new GameObject("RuleListPanelHost", typeof(RectTransform));
        host.transform.SetParent(RulesArea, false);
        var rt = host.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        var hostLE = host.AddComponent<LayoutElement>();
        hostLE.flexibleHeight = 1f;

        var panel = host.AddComponent<RuleListPanel>();
        Debug.Log($"BoardSidePanel: Created RuleListPanel host '{host.name}' and attached RuleListPanel component.");

        if (BoardVisualizer != null && BoardVisualizer.Runner != null)
        {
            panel.Runner = BoardVisualizer.Runner;
            Debug.Log("BoardSidePanel: Wired RuleListPanel.Runner from BoardVisualizer.Runner");
        }
        else
        {
            panel.Runner = Object.FindAnyObjectByType<SolverRunner>();
            Debug.Log($"BoardSidePanel: Wired RuleListPanel.Runner fallback={(panel.Runner!=null)}");
        }
    }

    private void UpdatePanelGeometry()
    {
        if (_panelRect == null) return;
        float left = Padding;
        float top = Padding;
        float bottom = Padding;
        float right = Padding;

        // Make sure we have a valid canvas reference for pixel-accurate layout.
        if (TargetCanvas == null)
            TargetCanvas = Object.FindAnyObjectByType<Canvas>();

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
            Debug.Log($"BoardSidePanel: boardLeft={boardLeft} cell={cell} gridSize={gridSize} boardWidth={boardWidth} left={left} availableWidth={availableWidth} canvasWidth={canvasWidth}");
        }
        else
        {
            Debug.Log($"Guessing BoardSidePanel geometry: BoardVisualizer or Runner or Board missing. Defaulting panel left to canvasWidth - MinWidth - Padding.");
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
        _panelRect.pivot = new Vector2(0f, 1f);
        _panelRect.sizeDelta = new Vector2(widthPixels, heightPixels);

        // If we have a BoardVisualizer, align the panel top with the board's top (IMGUI uses top-left screen origin).
        if (BoardVisualizer != null)
        {
            // BoardVisualizer.Offset is in IMGUI coordinates (top-left origin). Convert to screen point (bottom-left origin) expected by ScreenPointToLocalPointInRectangle.
            Vector2 boardGuiOffset = BoardVisualizer.Offset; // x,y in IMGUI (top-left)
            // Position horizontally at the computed panel left (right edge of the board),
            // but vertically align with the board's top (boardGuiOffset.y).
            Vector2 screenPoint = new Vector2(leftPixel, Screen.height - boardGuiOffset.y);

            RectTransform canvasRect = TargetCanvas.transform as RectTransform;
            Camera cam = TargetCanvas.renderMode == RenderMode.ScreenSpaceCamera ? TargetCanvas.worldCamera : null;
            Vector3 worldPoint;
            bool wp = RectTransformUtility.ScreenPointToWorldPointInRectangle(canvasRect, screenPoint, cam, out worldPoint);

            _panelRect.anchorMin = new Vector2(0f, 1f);
            _panelRect.anchorMax = new Vector2(0f, 1f);
            _panelRect.pivot = new Vector2(0f, 1f);
            _panelRect.sizeDelta = new Vector2(widthPixels, heightPixels);
            if (wp)
            {
                // Position the panel's top-left pivot at the computed world point
                _panelRect.position = worldPoint;
            }

            // Debug info
            Vector3[] corners = new Vector3[4];
            _panelRect.GetWorldCorners(corners);
            float topWorldY = corners[1].y; // top-left corner
            Debug.Log($"BoardSidePanel: panel pixels: canvas=({canvasWidth}x{canvasHeight}) left={leftPixel} width={widthPixels} top(screen)={screenPoint.y} worldOk={wp} world={worldPoint} height={heightPixels} worldPos={_panelRect.position} topWorldY={topWorldY}");
        }
        else
        {
            // Fallback: simple anchored position using top inset
            float panelTopOffset = top;
            _panelRect.anchoredPosition = new Vector2(leftPixel, -panelTopOffset);
        }
    }
}
