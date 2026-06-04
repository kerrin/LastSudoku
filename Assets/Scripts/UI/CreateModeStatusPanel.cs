using UnityEngine;
using UnityEngine.UI;
using Sudoku.Solver;
using Sudoku.Models;
using Sudoku.Solver.Rules;

/**
 * Status panel displayed only during Create Puzzle mode. Shows board validity,
 * possibility, and solve analysis messages.
 */
public class CreateModeStatusPanel : MonoBehaviour
{
    public SolverRunner Runner;

    private Text _headerText;
    private Text _statusText;
    private Text _possibilityText;
    private Text _solveStatusText;
    private Image _background;
    private RectTransform _contentRoot;
    private BoardStateSnapshot _lastSnapshot;

    private struct BoardStateSnapshot
    {
        public int FilledCount;
        public int ChangeLogIndex;
        public bool IsValid;
        public bool IsPossible;
        public int ValidationMessageHash;
        public int SolveStatusHash;
    }

    private void OnEnable()
    {
        if (!Application.isPlaying) return;

        if (Runner == null) Runner = Object.FindAnyObjectByType<SolverRunner>();
        BuildUi();
        RefreshStatus(force: true);
    }

    private void Start()
    {
        // OnEnable already ran; nothing extra needed.
    }

    private void Update()
    {
        if (!Application.isPlaying) return;

        if (Runner == null)
        {
            Runner = Object.FindAnyObjectByType<SolverRunner>();
        }

        // Visibility is controlled by BoardSidePanel.SyncApplyRulePanelVisibility.
        // Only refresh content here; do not call SetActive.
        if (Runner == null || !Runner.IsPuzzleCreationMode) return;

        RefreshStatus(force: false);
    }

    /**
     * Refresh the status display based on current board state.
     * 
     * @param force If true, update regardless of whether state changed.
     */
    public void RefreshStatus(bool force = false)
    {
        if (_headerText == null || _statusText == null || _possibilityText == null || _solveStatusText == null)
        {
            BuildUi();
            if (_headerText == null || _statusText == null || _possibilityText == null || _solveStatusText == null) return;
        }

        if (Runner == null) Runner = Object.FindAnyObjectByType<SolverRunner>();
        if (Runner == null || Runner.CurrentBoard == null)
        {
            _headerText.text = "Board Status";
            _statusText.text = "No active board.";
            _possibilityText.text = string.Empty;
            _solveStatusText.text = string.Empty;
            if (_background != null) _background.color = new Color(0.25f, 0.25f, 0.25f, 0.9f);
            return;
        }

        var board = Runner.CurrentBoard;
        int filledCount = 0;
        for (int r = 0; r < board.Size; r++)
        {
            for (int c = 0; c < board.Size; c++)
            {
                if (board.Cells[r, c] != null && board.Cells[r, c].Value.HasValue)
                {
                    filledCount++;
                }
            }
        }

        bool isValid = board.IsValid();
        var snapshot = new BoardStateSnapshot
        {
            FilledCount = filledCount,
            ChangeLogIndex = board.ChangeLogIndex,
            IsValid = isValid,
            IsPossible = Runner.LastBoardStateIsPossible,
            ValidationMessageHash = (Runner.LastBoardStateValidationMessage ?? string.Empty).GetHashCode(),
            SolveStatusHash = (Runner.LastCreationSolveStatusMessage ?? string.Empty).GetHashCode(),
        };

        // Skip update if nothing changed
        if (!force && snapshot.FilledCount == _lastSnapshot.FilledCount &&
            snapshot.ChangeLogIndex == _lastSnapshot.ChangeLogIndex &&
            snapshot.IsValid == _lastSnapshot.IsValid &&
            snapshot.IsPossible == _lastSnapshot.IsPossible &&
            snapshot.ValidationMessageHash == _lastSnapshot.ValidationMessageHash &&
            snapshot.SolveStatusHash == _lastSnapshot.SolveStatusHash)
        {
            return;
        }

        _lastSnapshot = snapshot;

        // Update title and status
        _headerText.text = "Board Status";
        _statusText.text = isValid
            ? $"Valid board. Filled cells: {filledCount}/{board.Size * board.Size}"
            : $"Invalid board. Check row/column/box duplicates. Filled cells: {filledCount}/{board.Size * board.Size}";

        // Update possibility message
        _possibilityText.text = Runner.LastBoardStateValidationMessage;

        // Update solve status
        string rulesUsed = Runner.LastCreationSolveRuleNames != null && Runner.LastCreationSolveRuleNames.Count > 0
            ? string.Join(", ", Runner.LastCreationSolveRuleNames)
            : "none";
        _solveStatusText.text = $"{Runner.LastCreationSolveStatusMessage} Rules used: {rulesUsed}";

        // Update background color based on possibility
        if (_background != null)
        {
            _background.color = Runner.LastBoardStateIsPossible
                ? new Color(0.13f, 0.38f, 0.16f, 0.9f)
                : new Color(0.45f, 0.16f, 0.16f, 0.9f);
        }

        // Update text colors
        _possibilityText.color = Runner.LastBoardStateIsPossible ? new Color(0.9f, 1f, 0.9f, 1f) : new Color(1f, 0.85f, 0.85f, 1f);
        _solveStatusText.color = Runner.LastCreationSolveFoundSolution ? new Color(0.95f, 1f, 0.92f, 1f) : new Color(1f, 0.93f, 0.82f, 1f);
    }

    /**
     * Build the panel UI structure.
     */
    private void BuildUi()
    {
        bool createMissing = Application.isPlaying;

        _background = GetComponent<Image>();
        if (_background == null && createMissing)
        {
            _background = gameObject.AddComponent<Image>();
        }

        var statusRoot = transform.Find("CreateModeStatus") ?? transform;

        _headerText = FindOrCreateTextChild(statusRoot, "Header", false, 18, FontStyle.Bold);
        if (_headerText == null)
        {
            _headerText = FindOrCreateTextChild(transform, "Header", false, 18, FontStyle.Bold);
        }
        if (_headerText == null)
        {
            _headerText = FindOrCreateTextChild(statusRoot, "Header", createMissing, 18, FontStyle.Bold);
        }

        var scrollArea = FindOrCreateRectTransformChild(statusRoot, "ScrollArea", createMissing);
        var viewport = scrollArea != null
            ? FindOrCreateRectTransformChild(scrollArea, "ViewPort", createMissing) ?? FindOrCreateRectTransformChild(scrollArea, "Viewport", createMissing)
            : null;
        _contentRoot = viewport != null ? FindOrCreateRectTransformChild(viewport, "Content", createMissing) : null;

        _statusText = _contentRoot != null ? FindOrCreateTextChild(_contentRoot, "Status", createMissing, 15, FontStyle.Normal) : null;
        _possibilityText = _contentRoot != null ? FindOrCreateTextChild(_contentRoot, "Possibility", createMissing, 14, FontStyle.Normal) : null;
        _solveStatusText = _contentRoot != null ? FindOrCreateTextChild(_contentRoot, "SolveStatus", createMissing, 13, FontStyle.Normal) : null;

        if (scrollArea != null)
        {
            var scrollRect = scrollArea.GetComponent<ScrollRect>();
            if (scrollRect == null && createMissing) scrollRect = scrollArea.gameObject.AddComponent<ScrollRect>();
            if (scrollRect != null)
            {
                scrollRect.horizontal = false;
                scrollRect.vertical = true;
                scrollRect.scrollSensitivity = 30f;
                if (_contentRoot != null) scrollRect.content = _contentRoot;
                if (viewport != null) scrollRect.viewport = viewport;
            }
        }

        if (viewport != null)
        {
            var viewportImage = viewport.GetComponent<Image>();
            if (viewportImage == null && createMissing) viewportImage = viewport.gameObject.AddComponent<Image>();
            if (viewportImage != null) viewportImage.raycastTarget = false;
            if (viewport.GetComponent<RectMask2D>() == null && createMissing)
            {
                viewport.gameObject.AddComponent<RectMask2D>();
            }
        }
    }

    /**
     * Find a named RectTransform child, optionally creating it in play mode.
     *
     * @param parent Parent transform.
     * @param childName Child object name.
     * @param createMissing Whether to create child when absent.
     * @returns Child RectTransform or null.
     */
    private RectTransform FindOrCreateRectTransformChild(Transform parent, string childName, bool createMissing)
    {
        if (parent == null || string.IsNullOrEmpty(childName)) return null;

        var child = parent.Find(childName);
        if (child == null)
        {
            if (!createMissing) return null;

            var go = new GameObject(childName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        var rt = child.GetComponent<RectTransform>();
        if (rt == null && createMissing) rt = child.gameObject.AddComponent<RectTransform>();
        return rt;
    }

    /**
     * Find a named Text child, optionally creating it in play mode.
     *
     * @param parent Parent transform.
     * @param childName Child object name.
     * @param createMissing Whether to create child when absent.
     * @param defaultFontSize Font size used only when creating a new text object.
     * @param defaultStyle Font style used only when creating a new text object.
     * @returns Text component or null.
     */
    private Text FindOrCreateTextChild(Transform parent, string childName, bool createMissing, int defaultFontSize, FontStyle defaultStyle)
    {
        if (parent == null || string.IsNullOrEmpty(childName)) return null;

        var child = parent.Find(childName);
        if (child == null)
        {
            if (!createMissing) return null;

            var go = new GameObject(childName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var createdText = go.GetComponent<Text>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            createdText.font = font;
            createdText.fontSize = defaultFontSize;
            createdText.fontStyle = defaultStyle;
            createdText.alignment = TextAnchor.UpperLeft;
            createdText.horizontalOverflow = HorizontalWrapMode.Wrap;
            createdText.verticalOverflow = VerticalWrapMode.Truncate;
            return createdText;
        }

        var text = child.GetComponent<Text>();
        if (text == null && createMissing) text = child.gameObject.AddComponent<Text>();
        return text;
    }

}
