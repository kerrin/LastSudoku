using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Creates and maintains a UI panel that fills the canvas space to the right
/// of the rendered board (IMGUI board rendered by `BoardVisualizer`). The panel
/// will stretch full canvas height, start at the board's right edge plus a
/// padding, and respect a minimum width.
/// </summary>
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

    private void Awake()
    {
        if (TargetCanvas == null) TargetCanvas = Object.FindAnyObjectByType<Canvas>();
        if (BoardVisualizer == null) BoardVisualizer = Object.FindAnyObjectByType<Sudoku.Solver.BoardVisualizer>();
        EnsurePanel();
        UpdatePanelGeometry();
    }

    private void OnEnable()
    {
        EnsurePanel();
        UpdatePanelGeometry();
    }

    private void Update()
    {
        // Update geometry each frame so the panel follows screen / board changes.
        UpdatePanelGeometry();
    }

    private void EnsurePanel()
    {
        if (_panelRect != null) return;
        if (TargetCanvas == null)
        {
            // Create a simple default canvas if none exists (screen-space overlay)
            var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            TargetCanvas = canvasGO.GetComponent<Canvas>();
            TargetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        var panelGO = new GameObject("SidePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelGO.transform.SetParent(TargetCanvas.transform, false);
        _panelRect = panelGO.GetComponent<RectTransform>();
        _panelImage = panelGO.GetComponent<Image>();
        _panelImage.color = Background;

        // Stretch full canvas and use offsets to place it to the right of the board
        _panelRect.anchorMin = Vector2.zero;
        _panelRect.anchorMax = Vector2.one;
        _panelRect.pivot = new Vector2(0f, 1f);

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
    }

    private void UpdatePanelGeometry()
    {
        if (_panelRect == null) return;
        float left = Padding;
        float top = Padding;
        float bottom = Padding;
        float right = Padding;

        // Determine board rectangle in screen space
        if (BoardVisualizer != null && BoardVisualizer.Runner != null && BoardVisualizer.Runner.CurrentBoard != null)
        {
            int gridSize = BoardVisualizer.Runner.CurrentBoard.Size;
            int cell = BoardVisualizer.GetComputedCellSize();
            float boardLeft = BoardVisualizer.Offset.x;
            float boardTop = BoardVisualizer.Offset.y;
            float boardWidth = gridSize * cell;
            float boardHeight = gridSize * cell;

            // The panel's left boundary should be just after the board's right edge
            left = Mathf.Round(boardLeft + boardWidth) + Padding;

            // Ensure minimum width is respected
            float availableWidth = Screen.width - left - Padding;
            if (availableWidth < MinWidth)
            {
                // Move left so min width fits
                left = Mathf.Max(Padding, Screen.width - MinWidth - Padding);
            }
        }

        // For a stretched rect transform: offsetMin = (left, bottom), offsetMax = (-right, -top)
        _panelRect.offsetMin = new Vector2(left, bottom);
        _panelRect.offsetMax = new Vector2(-right, -top);
    }
}
