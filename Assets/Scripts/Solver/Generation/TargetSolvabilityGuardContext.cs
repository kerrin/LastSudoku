using System;
using System.Collections.Generic;
using Sudoku.Models;
using Sudoku.Solver.Rules;

namespace Sudoku.Solver.Unsolver
{
    /**
     * Tracks active cross-rule blocking calls to prevent recursive cycles.
     */
    public sealed class TargetSolvabilityGuardContext
    {
        private readonly int _maxDepth;
        private readonly Stack<string> _stack = new Stack<string>();
        private readonly HashSet<string> _active = new HashSet<string>(StringComparer.Ordinal);

        /**
         * Optional viability probe provided by the anchor rule (e.g. Right Angle).
         * When set, blockers should only keep mutations that preserve anchor solvability.
         */
        public Func<Board, bool> AnchorSolveStillPossible { get; set; }

        /**
         * Optional debug callback used to trace blocker decisions.
         */
        public Action<string, string, bool, List<UsedCell>, Board> TraceSnapshotStep { get; set; }

        /**
         * Optional debug callback used to trace blocker board mutations.
         */
        public Action<string, string, bool, List<UsedCell>, Board, Board> TraceTransitionStep { get; set; }

        public void Trace(string message, bool failed = false, List<UsedCell> usedCells = null)
        {
            TraceSnapshotStep?.Invoke(
                failed ? "Force unsolve failed" : "Force unsolve step",
                message,
                failed,
                usedCells,
                null);
        }

        public void TraceSnapshot(
            string title,
            string message,
            Board snapshot,
            bool failed = false,
            List<UsedCell> usedCells = null)
        {
            TraceSnapshotStep?.Invoke(title, message, failed, usedCells, snapshot);
        }

        public void TraceTransition(
            string title,
            string message,
            Board before,
            Board after,
            bool failed = false,
            List<UsedCell> usedCells = null)
        {
            TraceTransitionStep?.Invoke(title, message, failed, usedCells, before, after);
        }

        public TargetSolvabilityGuardContext(int maxDepth = 16)
        {
            _maxDepth = Math.Max(1, maxDepth);
        }

        public bool TryEnter(string ruleName, int row, int column, int value)
        {
            if (_stack.Count >= _maxDepth)
            {
                return false;
            }

            string key = BuildKey(ruleName, row, column, value);
            if (!_active.Add(key))
            {
                return false;
            }

            _stack.Push(key);
            return true;
        }

        public void Exit(string ruleName, int row, int column, int value)
        {
            string key = BuildKey(ruleName, row, column, value);
            if (_stack.Count == 0)
            {
                return;
            }

            string top = _stack.Pop();
            _active.Remove(top);

            // Defensive recovery in case of mismatch; do not throw inside generation loop.
            if (!string.Equals(top, key, StringComparison.Ordinal))
            {
                _active.Remove(key);
            }
        }

        private static string BuildKey(string ruleName, int row, int column, int value)
        {
            string safeRuleName = string.IsNullOrWhiteSpace(ruleName) ? "UnknownRule" : ruleName;
            return safeRuleName + "|" + row + "|" + column + "|" + value;
        }
    }
}
