using System.Collections.Generic;
using UnityEngine;
using Sudoku.Models;
using Sudoku.Solver.Rules;

namespace Sudoku.Solver
{
    /// <summary>
    /// Temporary Simple Unity runner component to load a 9x9 puzzle from inspector rows
    /// and execute the registered rule engine. Use the context menu to run steps.
    /// Just used for testing.
    /// </summary>
    public class SolverRunner : MonoBehaviour
    {
        [Tooltip("Provide 9 strings each with 9 characters (digits 1-9 or . for empty)")]
        public string[] PuzzleRows = new string[9];

        public RuleRegistry Registry;
        public SolverEngine Engine;

        private Board _board;

        /// <summary>
        /// Expose the currently loaded board (may be null until loaded).
        /// </summary>
        public Board CurrentBoard => _board;

        /// <summary>
        /// Last rule that was applied via the runner (null when none).
        /// </summary>
        public Sudoku.Solver.Rules.ISudokuRule LastAppliedRule { get; private set; }

        /// <summary>
        /// Result of the last rule application (null when none or not applied).
        /// </summary>
        public Sudoku.Solver.Rules.RuleResult LastRuleResult { get; private set; }

        private void Awake()
        {
            EnsureEngine();
        }

        private void EnsureEngine()
        {
            if (Registry == null)
            {
                Registry = new RuleRegistry();
                Registry.RegisterDefaults();
            }
            if (Engine == null) Engine = new SolverEngine(Registry);
        }

        /// <summary>
        /// Parse the `PuzzleRows` into a standard 9x9 Board. Logs errors if format invalid.
        /// </summary>
        [ContextMenu("Load Board From Rows")]
        public void LoadBoardFromRows()
        {
            if (PuzzleRows == null || PuzzleRows.Length != 9)
            {
                Debug.LogError("PuzzleRows must contain exactly 9 strings for a 9x9 puzzle.");
                return;
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

        [ContextMenu("Run Next Rule Step")]
        public void RunNextStep()
        {
            if (_board == null) LoadBoardFromRows();
            if (_board == null) return;
            EnsureEngine();
            var (rule, result) = Registry.ApplyNext(_board);
            LastAppliedRule = rule;
            LastRuleResult = result;
            if (rule == null || result == null || !result.Applied)
            {
                Debug.Log("No applicable rule found.");
                return;
            }
            Debug.Log($"Applied '{rule.Name}': {result.Description}\n{BoardToString(_board)}");
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
                var last = steps[steps.Count - 1];
                LastAppliedRule = last.rule;
                LastRuleResult = last.result;
            }
            else
            {
                LastAppliedRule = null;
                LastRuleResult = null;
            }
            Debug.Log($"Solver finished. Solved={solved}. Steps={steps.Count}\n{BoardToString(_board)}");
            foreach (var s in steps)
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
                    var cell = _board.Cells[r, c];
                    if (!cell.Value.HasValue)
                    {
                        cell.Candidates.Clear();
                        for (int v = 1; v <= _board.Size; v++) cell.Candidates.Add(v);
                    }
                }
            Debug.Log("Candidates reset.");
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
