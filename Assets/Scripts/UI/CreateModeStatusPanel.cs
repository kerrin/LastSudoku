using UnityEngine;
using UnityEngine.UI;
using Sudoku.Solver;
using Sudoku.Models;
using Sudoku.Solver.Rules;

/**
 * Status panel displayed only during Create Puzzle mode. Shows board validity,
 * possibility, and solve analysis messages.
 */
[ExecuteAlways]
public class CreateModeStatusPanel : MonoBehaviour
{
    public SolverRunner Runner;

    private Text _titleText;
    private Text _statusText;
    private Text _possibilityText;
    private Text _solveStatusText;
    private Image _background;
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
        // Runs both on first enable and when re-activated after being hidden.
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
        if (_statusText == null || _background == null)
        {
            BuildUi();
            if (_statusText == null || _background == null) return;
        }

        if (Runner == null) Runner = Object.FindAnyObjectByType<SolverRunner>();
        if (Runner == null || Runner.CurrentBoard == null)
        {
            _titleText.text = "Board Status";
            _statusText.text = "No active board.";
            _possibilityText.text = string.Empty;
            _solveStatusText.text = string.Empty;
            _background.color = new Color(0.25f, 0.25f, 0.25f, 0.9f);
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
        _titleText.text = "Board Status";
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
        _background.color = Runner.LastBoardStateIsPossible
            ? new Color(0.13f, 0.38f, 0.16f, 0.9f)
            : new Color(0.45f, 0.16f, 0.16f, 0.9f);

        // Update text colors
        _possibilityText.color = Runner.LastBoardStateIsPossible ? new Color(0.9f, 1f, 0.9f, 1f) : new Color(1f, 0.85f, 0.85f, 1f);
        _solveStatusText.color = Runner.LastCreationSolveFoundSolution ? new Color(0.95f, 1f, 0.92f, 1f) : new Color(1f, 0.93f, 0.82f, 1f);
    }

    /**
     * Build the panel UI structure.
     */
    private void BuildUi()
    {
        var rt = GetComponent<RectTransform>();
        if (rt == null) rt = gameObject.AddComponent<RectTransform>();

        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        _background = GetComponent<Image>();
        if (_background == null) _background = gameObject.AddComponent<Image>();
        _background.color = new Color(0.25f, 0.25f, 0.25f, 0.9f);

        var existingLayout = GetComponent<VerticalLayoutGroup>();
        if (existingLayout == null) existingLayout = gameObject.AddComponent<VerticalLayoutGroup>();
        existingLayout.childControlWidth = true;
        existingLayout.childControlHeight = false;
        existingLayout.childForceExpandWidth = true;
        existingLayout.childForceExpandHeight = false;
        existingLayout.spacing = 2f;
        existingLayout.padding = new RectOffset(8, 8, 8, 8);

        _titleText = EnsureTextChild("Title", 18, FontStyle.Bold, TextAnchor.UpperLeft);
        _statusText = EnsureTextChild("Status", 15, FontStyle.Normal, TextAnchor.UpperLeft);
        _possibilityText = EnsureTextChild("Possibility", 14, FontStyle.Normal, TextAnchor.UpperLeft);
        _solveStatusText = EnsureTextChild("SolveStatus", 13, FontStyle.Normal, TextAnchor.UpperLeft);

        // Configure text wrapping
        _statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _statusText.verticalOverflow = VerticalWrapMode.Overflow;
        _possibilityText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _possibilityText.verticalOverflow = VerticalWrapMode.Overflow;
        _solveStatusText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _solveStatusText.verticalOverflow = VerticalWrapMode.Overflow;

        // Set preferred heights for layout elements
        var titleLE = _titleText.GetComponent<LayoutElement>();
        if (titleLE == null) titleLE = _titleText.gameObject.AddComponent<LayoutElement>();
        titleLE.preferredHeight = 24f;

        var statusLE = _statusText.GetComponent<LayoutElement>();
        if (statusLE == null) statusLE = _statusText.gameObject.AddComponent<LayoutElement>();
        statusLE.preferredHeight = 34f;

        var possibilityLE = _possibilityText.GetComponent<LayoutElement>();
        if (possibilityLE == null) possibilityLE = _possibilityText.gameObject.AddComponent<LayoutElement>();
        possibilityLE.preferredHeight = 30f;

        var solveStatusLE = _solveStatusText.GetComponent<LayoutElement>();
        if (solveStatusLE == null) solveStatusLE = _solveStatusText.gameObject.AddComponent<LayoutElement>();
        solveStatusLE.preferredHeight = 42f;
    }

    /**
     * Ensure a named text child exists with proper formatting.
     * 
     * @param childName Name of the child GameObject.
     * @param fontSize Font size in pixels.
     * @param fontStyle Font style (Bold, Normal, Italic, etc.).
     * @param anchor Text anchor position.
     * @returns The Text component.
     */
    private Text EnsureTextChild(string childName, int fontSize, FontStyle fontStyle, TextAnchor anchor)
    {
        var child = transform.Find(childName);
        Text text;
        if (child == null)
        {
            var textGO = new GameObject(childName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textGO.transform.SetParent(transform, false);
            text = textGO.GetComponent<Text>();
        }
        else
        {
            text = child.GetComponent<Text>();
            if (text == null) text = child.gameObject.AddComponent<Text>();
        }

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        text.font = font;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = anchor;
        text.color = Color.white;

        return text;
    }
}
