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
        _background = GetComponent<Image>();
        if (_background == null)
        {
            Debug.LogWarning("CreateModeStatusPanel: Expected Image on panel root.");
        }

        var statusRoot = transform.Find("CreateModeStatus") ?? transform;

        _headerText = FindTextChild(statusRoot, "Header");
        if (_headerText == null)
        {
            _headerText = FindTextChild(transform, "Header");
        }

        var scrollArea = FindRectTransformChild(statusRoot, "ScrollArea");
        var viewport = scrollArea != null
            ? FindRectTransformChild(scrollArea, "ViewPort") ?? FindRectTransformChild(scrollArea, "Viewport")
            : null;
        _contentRoot = viewport != null ? FindRectTransformChild(viewport, "Content") : null;

        _statusText = _contentRoot != null ? FindTextChild(_contentRoot, "Status") : null;
        _possibilityText = _contentRoot != null ? FindTextChild(_contentRoot, "Possibility") : null;
        _solveStatusText = _contentRoot != null ? FindTextChild(_contentRoot, "SolveStatus") : null;

        if (scrollArea != null)
        {
            var scrollRect = scrollArea.GetComponent<ScrollRect>();
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
            if (viewportImage != null) viewportImage.raycastTarget = false;
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
    private RectTransform FindRectTransformChild(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrEmpty(childName)) return null;

        var child = parent.Find(childName);
        return child != null ? child.GetComponent<RectTransform>() : null;
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
    private Text FindTextChild(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrEmpty(childName)) return null;

        var child = parent.Find(childName);
        return child != null ? child.GetComponent<Text>() : null;
    }

}
