using UnityEngine;
using UnityEngine.UI;
using Sudoku.Solver;
using Sudoku.Solver.Rules;

/**
 * Runtime status panel that reports whether the current board is valid.
 * This replaces the old Apply Rules panel in the side panel layout.
 */
public class BoardValidityPanel : MonoBehaviour
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
        public bool IsCreationMode;
    }

    /**
     * Build the panel UI once at startup.
     */
    private void Start()
    {
        if (Runner == null) Runner = Object.FindAnyObjectByType<SolverRunner>();
        BuildUi();
        RefreshStatus(force: true);
    }

    /**
     * Refresh status when board state changes.
     */
    private void Update()
    {
        RefreshStatus(force: false);
    }

    /**
     * Public refresh hook for external callers.
     */
    public void RefreshStatus(bool force = true)
    {
        if (_statusText == null || _background == null)
        {
            BuildUi();
            if (_statusText == null || _background == null) return;
        }

        if (Runner == null) Runner = Object.FindAnyObjectByType<SolverRunner>();
        if (Runner == null || Runner.CurrentBoard == null)
        {
            if (force || _statusText.text != "No active board.")
            {
                _titleText.text = "Board Status";
                _statusText.text = "No active board.";
                _possibilityText.text = string.Empty;
                _solveStatusText.text = string.Empty;
                _background.color = new Color(0.25f, 0.25f, 0.25f, 0.9f);
            }
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
            IsCreationMode = Runner.IsPuzzleCreationMode
        };

        if (!force && snapshot.FilledCount == _lastSnapshot.FilledCount &&
            snapshot.ChangeLogIndex == _lastSnapshot.ChangeLogIndex &&
            snapshot.IsValid == _lastSnapshot.IsValid &&
            snapshot.IsPossible == _lastSnapshot.IsPossible &&
            snapshot.ValidationMessageHash == _lastSnapshot.ValidationMessageHash &&
            snapshot.SolveStatusHash == _lastSnapshot.SolveStatusHash &&
            snapshot.IsCreationMode == _lastSnapshot.IsCreationMode)
        {
            return;
        }

        _lastSnapshot = snapshot;
        _titleText.text = "Board Status";
        _statusText.text = isValid
            ? $"Valid board. Filled cells: {filledCount}/{board.Size * board.Size}"
            : $"Invalid board. Check row/column/box duplicates. Filled cells: {filledCount}/{board.Size * board.Size}";
        _possibilityText.text = Runner.LastBoardStateValidationMessage;

        if (Runner.IsPuzzleCreationMode)
        {
            string rulesUsed = Runner.LastCreationSolveRuleNames != null && Runner.LastCreationSolveRuleNames.Count > 0
                ? string.Join(", ", Runner.LastCreationSolveRuleNames)
                : "none";
            _solveStatusText.text = $"{Runner.LastCreationSolveStatusMessage} Rules used: {rulesUsed}";
        }
        else
        {
            _solveStatusText.text = string.Empty;
        }

        _background.color = Runner.LastBoardStateIsPossible
            ? new Color(0.13f, 0.38f, 0.16f, 0.9f)
            : new Color(0.45f, 0.16f, 0.16f, 0.9f);

        _possibilityText.color = Runner.LastBoardStateIsPossible ? new Color(0.9f, 1f, 0.9f, 1f) : new Color(1f, 0.85f, 0.85f, 1f);
        _solveStatusText.color = Runner.LastCreationSolveFoundSolution ? new Color(0.95f, 1f, 0.92f, 1f) : new Color(1f, 0.93f, 0.82f, 1f);
    }

    /**
     * Build panel hierarchy and styles.
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
        existingLayout.spacing = 6f;
        existingLayout.padding = new RectOffset(12, 12, 12, 12);

        _titleText = EnsureTextChild("Title", 18, FontStyle.Bold, TextAnchor.UpperLeft);
        _statusText = EnsureTextChild("Status", 15, FontStyle.Normal, TextAnchor.UpperLeft);
        _possibilityText = EnsureTextChild("Possibility", 14, FontStyle.Normal, TextAnchor.UpperLeft);
        _solveStatusText = EnsureTextChild("SolveStatus", 13, FontStyle.Normal, TextAnchor.UpperLeft);
        _statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _statusText.verticalOverflow = VerticalWrapMode.Overflow;
        _statusText.resizeTextForBestFit = false;
        _possibilityText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _possibilityText.verticalOverflow = VerticalWrapMode.Overflow;
        _possibilityText.resizeTextForBestFit = false;
        _solveStatusText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _solveStatusText.verticalOverflow = VerticalWrapMode.Overflow;
        _solveStatusText.resizeTextForBestFit = false;

        var titleLE = _titleText.GetComponent<LayoutElement>();
        if (titleLE == null) titleLE = _titleText.gameObject.AddComponent<LayoutElement>();
        titleLE.preferredHeight = 28f;

        var statusLE = _statusText.GetComponent<LayoutElement>();
        if (statusLE == null) statusLE = _statusText.gameObject.AddComponent<LayoutElement>();
        statusLE.preferredHeight = 54f;

        var possibilityLE = _possibilityText.GetComponent<LayoutElement>();
        if (possibilityLE == null) possibilityLE = _possibilityText.gameObject.AddComponent<LayoutElement>();
        possibilityLE.preferredHeight = 52f;

        var solveStatusLE = _solveStatusText.GetComponent<LayoutElement>();
        if (solveStatusLE == null) solveStatusLE = _solveStatusText.gameObject.AddComponent<LayoutElement>();
        solveStatusLE.preferredHeight = 74f;
    }

    /**
     * Ensure a named text child exists.
     *
     * @param childName Child object name.
     * @param fontSize Font size.
     * @param fontStyle Font style.
     * @param anchor Text anchor.
     * @returns The text component.
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
