using UnityEngine;
using UnityEngine.UI;
using Sudoku.Solver;
using Sudoku.Models;
using Sudoku.Solver.Rules;
using System.Text;

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
        public int ChangeLogIndex;
        public bool IsValid;
        public bool IsPossible;
        public bool IsSolvedWithSelectedRules;
        public bool IsSolvedWithAnyRules;
        public int ValidationMessageHash;
        public int SolveStatusHash;
        public int RulesUsedHash;
    }

    private void OnEnable()
    {
        if (!Application.isPlaying) return;

        if (Runner == null) Runner = FindAnyObjectByType<SolverRunner>();
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
            Runner = FindAnyObjectByType<SolverRunner>();
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

        if (Runner == null) Runner = FindAnyObjectByType<SolverRunner>();
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
        };

        // Skip update if nothing changed
        if (!force && snapshot.ChangeLogIndex == _lastSnapshot.ChangeLogIndex &&
            snapshot.IsValid == _lastSnapshot.IsValid &&
            snapshot.IsPossible == _lastSnapshot.IsPossible &&
            snapshot.IsSolvedWithSelectedRules == _lastSnapshot.IsSolvedWithSelectedRules &&
            snapshot.IsSolvedWithAnyRules == _lastSnapshot.IsSolvedWithAnyRules &&
            snapshot.ValidationMessageHash == _lastSnapshot.ValidationMessageHash &&
            snapshot.SolveStatusHash == _lastSnapshot.SolveStatusHash &&
            snapshot.RulesUsedHash == _lastSnapshot.RulesUsedHash)
        {
            return;
        }

        _lastSnapshot = snapshot;

        // Update title and status
        _headerText.text = "Board Status";
        _statusText.text = isValid
            ? "Valid board."
            : "Invalid board. Check row/column/box duplicates.";

        // Update possibility message
        _possibilityText.text = Runner.LastBoardStateValidationMessage;

        // Keep panel background stable; apply semantics through per-message colors.
        if (_background != null)
        {
            _background.color = new Color(0.08f, 0.15f, 0.28f, 0.9f);
        }

        var red = new Color(1f, 0.42f, 0.42f, 1f);
        var amber = new Color(1f, 0.78f, 0.38f, 1f);
        var green = new Color(0.62f, 1f, 0.62f, 1f);

        _statusText.color = isValid ? green : red;
        _possibilityText.color = Runner.LastBoardStateIsPossible ? green : red;
        _solveStatusText.supportRichText = true;
        _solveStatusText.color = Color.white;
        _solveStatusText.text = BuildSolveStatusText(red, amber, green);
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
            ? "Selected Rules Used:"
            : (Runner.LastCreationSolveFoundSolution ? "All Rules Used:" : "Selected Rules Used:");
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
