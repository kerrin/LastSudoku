using System.Collections.Generic;
using UnityEngine;
using Sudoku.Models;
using Sudoku.Solver.Rules;

namespace Sudoku.Solver
{
    /**
     * Temporary Simple Unity runner component to load a 9x9 puzzle from inspector rows
     * and execute the registered rule engine. Use the context menu to run steps.
     * Just used for testing.
     */
    public class SolverRunner : MonoBehaviour
    {
        [Tooltip("Provide 9 strings each with 9 characters (digits 1-9 or . for empty)")]
        public string[] PuzzleRows = new string[9];

        public RuleRegistry Registry;
        public SolverEngine Engine;

        private Board _board;

        /**
         * Expose the currently loaded board (may be null until loaded).
         */
        public Board CurrentBoard => _board;

        /**
         * Last rule that was applied via the runner (null when none).
         */
        public ISudokuRule LastAppliedRule { get; private set; }

        /**
         * Result of the last rule application (null when none or not applied).
         */
        public RuleResult LastRuleResult { get; private set; }

        /**
            * Result of a hovered/previewed rule. This is not enacted on the board;
            * it is used by UI visualizers to show what a rule would change.
            */
        public RuleResult PreviewRuleResult { get; private set; }

        private void Awake()
        {
            EnsureEngine();
            // Auto-load the puzzle rows when the scene starts so UI visualizers
            // that depend on `CurrentBoard` (e.g. `BoardVisualizer`) will render
            // without requiring manual context-menu actions in the Editor.
            LoadBoardFromRows();
        }

        // Made public so external UI components can ensure the runner has
        // initialized its RuleRegistry and SolverEngine.
        public void EnsureEngine()
        {
            if (Registry == null)
            {
                Registry = new RuleRegistry();
                Registry.RegisterMinimal();
                Registry.RegisterMedium();
                Registry.RegisterAdvanced();
            }
            if (Engine == null) Engine = new SolverEngine(Registry);
        }

        /**
         * Parse the `PuzzleRows` into a standard 9x9 Board. Logs errors if format invalid.
         */
        [ContextMenu("Load Board From Rows")]
        public void LoadBoardFromRows()
        {
            if (PuzzleRows == null || PuzzleRows.Length != 9)
            {
                Debug.LogError("PuzzleRows must contain exactly 9 strings for a 9x9 puzzle.");
                return;
            }

            // Clear other SolverRunner instances in the scene to avoid conflicting models
            var runners = Object.FindObjectsByType<SolverRunner>(FindObjectsSortMode.None);
            foreach (var r in runners)
            {
                if (r == this) continue;
#if UNITY_EDITOR
                DestroyImmediate(r.gameObject);
#else
                Destroy(r.gameObject);
#endif
            }

            var board = new Board(9, 3, 3);
            for (int r = 0; r < 9; r++)
            {
                var rowStr = PuzzleRows[r] ?? string.Empty;
                if (rowStr.Length < 9)
                {
                    Debug.LogError($"Row {r} must be at least 9 characters.");
                    return;
                }
                for (int c = 0; c < 9; c++)
                {
                    char ch = rowStr[c];
                    int? val = null;
                    bool isGiven = false;
                    if (ch >= '1' && ch <= '9') { 
                        val = ch - '0'; 
                        isGiven = true; 
                    }
                    var cell = new Cell(r, c, val, isGiven);
                    board.Cells[r, c] = cell;
                }
            }
            _board = board;
            Debug.Log("Board loaded from PuzzleRows:\n" + BoardToString(_board));
        }

        [ContextMenu("Initialise Candidates")]
        public void InitialiseCandidates()
        {
            if (_board == null) LoadBoardFromRows();
            if (_board == null) return;
            EnsureEngine();
            // Step over each cell and initialize candidates for empty cells
            // checking the peers for number elimination.
            for (int r = 0; r < _board.Size; r++)
                for (int c = 0; c < _board.Size; c++)
                {
                    Cell cell = _board.Cells[r, c];
                    if (cell.Value.HasValue) continue; // skip filled cells
                    cell.Candidates.Clear();
                    for (int v = 1; v <= _board.Size; v++) cell.Candidates.Add(v);
                    // Eliminate candidates based on peers' values
                    var peers = _board.GetPeers(cell);
                    foreach (var peer in peers)
                    {
                        if (peer.Value.HasValue)
                        {
                            cell.Candidates.Remove(peer.Value.Value);
                        }
                    }
                }
        }

        [ContextMenu("Run Next Rule Step")]
        public void RunNextStep()
        {
            if (_board == null) LoadBoardFromRows();
            if (_board == null) return;
            EnsureEngine();
            (ISudokuRule rule, RuleResult result) = Registry.ApplyNext(_board);
            LastAppliedRule = rule;
            LastRuleResult = result;
            if (rule == null || result == null || !result.Apply)
            {
                Debug.Log("No applicable rule found.");
                return;
            }
            Debug.Log($"Applied '{rule.Name}': {result.Description}\n{BoardToString(_board)}");
        }

        /**
         * Prepare a preview of what the given rule would change on the current board.
         * The preview is non-destructive and stored in `PreviewRuleResult` for UI use.
         */
        public void PreviewRule(ISudokuRule rule)
        {
            if (_board == null) LoadBoardFromRows();
            if (_board == null) { PreviewRuleResult = null; return; }
            EnsureEngine();
            if (rule == null) { PreviewRuleResult = null; return; }
            try
            {
                var res = rule.CalculateChanges(_board);
                PreviewRuleResult = (res != null && res.Apply) ? res : new RuleResult { Apply = false };
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"PreviewRule threw for {rule.GetType().Name}: {ex.Message}");
                PreviewRuleResult = new RuleResult { Apply = false, Description = "Preview error" };
            }
        }

        /** Clear any previewed rule result. */
        public void ClearPreview()
        {
            PreviewRuleResult = null;
        }

        /**
         * Execute a specific rule against the current board (if applicable and enabled).
         */
        public void RunRule(ISudokuRule rule)
        {
            if (_board == null) LoadBoardFromRows();
            if (_board == null) return;
            EnsureEngine();
            if (rule == null) return;
            if (!Registry.IsEnabled(rule))
            {
                Debug.LogWarning($"RunRule: rule {rule.GetType().Name} is disabled.");
                return;
            }
            RuleResult res = null;
            try
            {
                res = rule.CalculateChanges(_board);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"RunRule: CalculateChanges threw for {rule.GetType().Name}: {ex.Message}");
                return;
            }
            if (res == null || !res.Apply)
            {
                Debug.Log("RunRule: rule had no effect.");
                return;
            }
            // Enact all recorded changes
            res.EnactAll(_board);
            LastAppliedRule = rule;
            LastRuleResult = res;
            PreviewRuleResult = null;
            Debug.Log($"Applied '{rule.Name}': {res.Description}\n{BoardToString(_board)}");
        }

        [ContextMenu("Run Solve")]
        public void RunSolve()
        {
            if (_board == null) LoadBoardFromRows();
            if (_board == null) return;
            EnsureEngine();
            var solved = Engine.Solve(_board, out List<(ISudokuRule rule, RuleResult result)> steps);
            // store last applied step if any
            if (steps != null && steps.Count > 0)
            {
                (ISudokuRule rule, RuleResult result) last = steps[steps.Count - 1];
                LastAppliedRule = last.rule;
                LastRuleResult = last.result;
            }
            else
            {
                LastAppliedRule = null;
                LastRuleResult = null;
            }
            Debug.Log($"Solver finished. Solved={solved}. Steps={steps.Count}\n{BoardToString(_board)}");
            foreach ((ISudokuRule rule, RuleResult result) s in steps)
            {
                Debug.Log($"{s.rule.Name}: {s.result.Description}");
            }
        }

        [ContextMenu("Reset Candidates for Empty Cells")]
        public void ResetCandidates()
        {
            if (_board == null) LoadBoardFromRows();
            if (_board == null) return;
            for (int r = 0; r < _board.Size; r++)
                for (int c = 0; c < _board.Size; c++)
                {
                    Cell cell = _board.Cells[r, c];
                    if (!cell.Value.HasValue)
                    {
                        cell.Candidates.Clear();
                        for (int v = 1; v <= _board.Size; v++) cell.Candidates.Add(v);
                    }
                }
            Debug.Log("Candidates reset.");
        }

        [ContextMenu("Validate Board")]
        public bool ValidateBoard()
        {
            if (_board == null) LoadBoardFromRows();
            if (_board == null)
            {
                Debug.LogError("No board loaded to validate.");
                return false;
            }

            bool valid = _board.IsValid();
            var msg = valid ? "Board is valid." : "Board is INVALID: duplicate found in a unit.";
            Debug.Log(msg + "\n" + BoardToString(_board));

            // Store a simple RuleResult-like message for UI inspection
            LastAppliedRule = null;
            LastRuleResult = new RuleResult { Apply = false, Description = msg };

            return valid;
        }

        private string BoardToString(Board board)
        {
            var lines = new List<string>();
            for (int r = 0; r < board.Size; r++)
            {
                var chars = new char[board.Size];
                for (int c = 0; c < board.Size; c++)
                {
                    var v = board.Cells[r, c].Value;
                    chars[c] = v.HasValue ? (char)('0' + v.Value) : '.';
                }
                lines.Add(new string(chars));
            }
            return string.Join("\n", lines);
        }
    }
}
