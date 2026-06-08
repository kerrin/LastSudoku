using UnityEngine;
using UnityEngine.UI;
using Sudoku.Solver;
using Sudoku.Solver.Rules;
using System.Text;

namespace Sudoku.UI.Panels
{

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
        public int ChangeLogIndex;
        public bool IsValid;
        public bool IsPossible;
        public bool IsSolvedWithSelectedRules;
        public bool IsSolvedWithAnyRules;
        public int ValidationMessageHash;
        public int SolveStatusHash;
        public int RulesUsedHash;
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
        bool isValid = board.IsValid();
        string rulesUsed = Runner.LastCreationSolveRuleNames != null && Runner.LastCreationSolveRuleNames.Count > 0
            ? string.Join("|", Runner.LastCreationSolveRuleNames)
            : "none";
        var snapshot = new BoardStateSnapshot
        {
            ChangeLogIndex = board.ChangeLogIndex,
            IsValid = isValid,
            IsPossible = Runner.LastBoardStateIsPossible,
            IsSolvedWithSelectedRules = Runner.LastCreationSolveFoundWithSelectedRules,
            IsSolvedWithAnyRules = Runner.LastCreationSolveFoundSolution,
            ValidationMessageHash = (Runner.LastBoardStateValidationMessage ?? string.Empty).GetHashCode(),
            SolveStatusHash = (Runner.LastCreationSolveStatusMessage ?? string.Empty).GetHashCode(),
            RulesUsedHash = rulesUsed.GetHashCode(),
            IsCreationMode = Runner.IsPuzzleCreationMode
        };

        if (!force && snapshot.ChangeLogIndex == _lastSnapshot.ChangeLogIndex &&
            snapshot.IsValid == _lastSnapshot.IsValid &&
            snapshot.IsPossible == _lastSnapshot.IsPossible &&
            snapshot.IsSolvedWithSelectedRules == _lastSnapshot.IsSolvedWithSelectedRules &&
            snapshot.IsSolvedWithAnyRules == _lastSnapshot.IsSolvedWithAnyRules &&
            snapshot.ValidationMessageHash == _lastSnapshot.ValidationMessageHash &&
            snapshot.SolveStatusHash == _lastSnapshot.SolveStatusHash &&
            snapshot.RulesUsedHash == _lastSnapshot.RulesUsedHash &&
            snapshot.IsCreationMode == _lastSnapshot.IsCreationMode)
        {
            return;
        }

        _lastSnapshot = snapshot;
        _titleText.text = "Board Status";
        _statusText.text = isValid
            ? "Valid board."
            : "Invalid board. Check row/column/box duplicates.";
        _possibilityText.text = Runner.LastBoardStateValidationMessage;

        var red = new Color(1f, 0.42f, 0.42f, 1f);
        var amber = new Color(1f, 0.78f, 0.38f, 1f);
        var green = new Color(0.62f, 1f, 0.62f, 1f);

        if (Runner.IsPuzzleCreationMode)
        {
            _solveStatusText.supportRichText = true;
            _solveStatusText.color = Color.white;
            _solveStatusText.text = BuildSolveStatusText(red, amber, green);
        }
        else
        {
            _solveStatusText.text = string.Empty;
        }

        _background.color = new Color(0.08f, 0.15f, 0.28f, 0.9f);

        _statusText.color = isValid ? green : red;
        _possibilityText.color = Runner.LastBoardStateIsPossible ? green : red;
    }

    /**
     * Build solve-status details with independent colors for selected-rules status,
     * all-rules status, and the used-rules vertical list.
     *
     * @param red Color used for invalid or unsolved states.
     * @param amber Color used for solvable-only-with-all-rules states.
     * @param green Color used for solvable-with-selected-rules states.
     * @returns Rich-text string for the solve status block.
     */
    private string BuildSolveStatusText(Color red, Color amber, Color green)
    {
        if (Runner == null)
        {
            return string.Empty;
        }

        string selectedText = Runner.LastCreationSolveFoundWithSelectedRules
            ? "Selected rules: Solution found."
            : "Selected rules: No complete solution found.";

        Color selectedColor = Runner.LastCreationSolveFoundWithSelectedRules
            ? green
            : (Runner.LastCreationSolveFoundSolution ? amber : red);

        string allRulesText = Runner.LastCreationSolveFoundSolution
            ? "All rules: Solution found."
            : "All rules: No complete solution found.";

        Color allRulesColor = Runner.LastCreationSolveFoundSolution ? green : red;

        var sb = new StringBuilder();
        sb.AppendLine(Colorize(selectedText, selectedColor));
        sb.AppendLine(Colorize(allRulesText, allRulesColor));
        sb.AppendLine();
        string rulesUsedLabel = Runner.LastCreationSolveFoundWithSelectedRules
            ? "Selected Solve Rules Used (Analysis):"
            : (Runner.LastCreationSolveFoundSolution ? "All Solve Rules Used (Analysis):" : "Selected Solve Rules Used (Analysis):");
        sb.AppendLine(Colorize(rulesUsedLabel, allRulesColor));

        if (Runner.LastCreationSolveRuleNames != null && Runner.LastCreationSolveRuleNames.Count > 0)
        {
            for (int i = 0; i < Runner.LastCreationSolveRuleNames.Count; i++)
            {
                sb.AppendLine(Colorize($"- {Runner.LastCreationSolveRuleNames[i]}", allRulesColor));
            }
        }
        else
        {
            sb.AppendLine(Colorize("- none", allRulesColor));
        }

        return sb.ToString().TrimEnd();
    }

    /**
     * Wrap text in a Unity rich-text color tag.
     *
     * @param text Content to colorize.
     * @param color Color value.
     * @returns Rich-text color-tagged string.
     */
    private static string Colorize(string text, Color color)
    {
        return $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{text}</color>";
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
}
