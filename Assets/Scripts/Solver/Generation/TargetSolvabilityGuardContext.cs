using System;
using System.Collections.Generic;
using Sudoku.Models;

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
