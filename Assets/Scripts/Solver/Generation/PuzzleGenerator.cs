using System;
using System.Collections.Generic;
using System.Linq;
using Sudoku.Models;
using Sudoku.Solver;
using Sudoku.Solver.Rules;

namespace Sudoku.Solver.Unsolver
{
    /**
     * Generates a Sudoku puzzle solvable using only the specified enabled rules.
     *
     * Algorithm:
     * <list type="number">
     *   <item>Clone the provided fully-solved board.</item>
     *   <item>Warm-up: repeatedly apply the Naked Single unsolve handler until it can
     *         make no further moves.</item>
        *   <item>Non-Naked phase: shuffle and apply only non-Naked handlers in passes,
        *         targeting at least <see cref="DefaultMinimumOtherRulePasses"/> passes when
        *         progress continues, or stopping early when no move is available.</item>
     *   <item>Finalize: mark remaining valued cells as givens, recompute candidates for
     *         empty cells, clear the change-log.</item>
     *   <item>Validate uniqueness via backtracking; retry up to
     *         <see cref="DefaultMaxRetries"/> times on failure.</item>
     * </list>
     */
    public class PuzzleGenerator
    {
        public const int DefaultMaxRetries = 30;
        public const int DefaultMaxIterations = 500;
        public const int DefaultMinimumOtherRulePasses = 10;

        private readonly int _maxRetries;
        private readonly int _maxIterations;
        private readonly int _minimumOtherRulePasses;
        private readonly bool _requireNonNakedContribution;

        /** Ordered successful unsolve rule sequence for the last successful generation attempt. */
        public IReadOnlyList<string> LastGenerationRuleSequence { get; private set; } = Array.Empty<string>();

        /** Grouped summary for <see cref="LastGenerationRuleSequence"/> (e.g. "45 x Naked Single | 3 x Hidden Single"). */
        public string LastGenerationRuleUsageSummary { get; private set; } = string.Empty;

        public PuzzleGenerator(
            int maxRetries = DefaultMaxRetries,
            int maxIterations = DefaultMaxIterations,
            int minimumOtherRulePasses = DefaultMinimumOtherRulePasses,
            bool requireNonNakedContribution = false)
        {
            _maxRetries = maxRetries;
            _maxIterations = maxIterations;
            _minimumOtherRulePasses = Math.Max(0, minimumOtherRulePasses);
            _requireNonNakedContribution = requireNonNakedContribution;
        }

        /**
         * Generate a puzzle from the supplied solved board using only the enabled rules.
         *
         * @param solvedBoard  A fully solved, valid Sudoku board (not modified).
         * @param enabledRules Rules to use for unsolver handlers; only enabled rules contribute.
         * @param random       Optional random source (a new instance is created if null).
         * @returns A new board with some values removed, ready for solving.
         * @throws InvalidOperationException when uniqueness cannot be achieved within retries.
         */
        public Board Generate(Board solvedBoard, IEnumerable<ISudokuRule> enabledRules, Random random = null)
        {
            random ??= new Random();
            var rulesList = enabledRules.ToList();

            for (int attempt = 0; attempt < _maxRetries; attempt++)
            {
                var board = CloneBoard(solvedBoard);
                var handlers = rulesList.Select(UnsolveHandlerRegistry.GetHandler).ToList();
                bool hasAnyNonNakedValueHandler = handlers.Any(handler =>
                    handler is not NakedSingleUnsolveHandler
                    && handler is not CandidateOnlyUnsolveHandler);
                var appliedRuleSequence = new List<string>(128);
                bool usedAnyNonNakedRule = false;

                // Phase 1: drain Naked Single unsolve first to maximise removals.
                var nakedHandler = handlers.OfType<NakedSingleUnsolveHandler>().FirstOrDefault();
                if (nakedHandler != null)
                {
                    while (nakedHandler.TryUnsolve(board, random) == UnsolveResult.Success)
                    {
                        appliedRuleSequence.Add(NormalizeRuleDisplayName(nakedHandler.RuleName));
                    }
                }

                // Phase 2: try only non-Naked handlers in repeated shuffled passes.
                // Keep going while progress exists; target at least N passes when possible.
                var otherHandlers = handlers
                    .Where(handler => handler is not NakedSingleUnsolveHandler)
                    .ToList();

                bool anyProgress = true;
                int iterations = 0;
                while (iterations < _maxIterations
                    && (iterations < _minimumOtherRulePasses || anyProgress))
                {
                    anyProgress = false;
                    if (otherHandlers.Count == 0)
                    {
                        break;
                    }

                    Shuffle(otherHandlers, random);
                    foreach (var handler in otherHandlers)
                    {
                        if (handler.TryUnsolve(board, random) == UnsolveResult.Success)
                        {
                            anyProgress = true;
                            usedAnyNonNakedRule = true;
                            appliedRuleSequence.Add(NormalizeRuleDisplayName(handler.RuleName));
                        }
                    }
                    iterations++;

                    // Early exit when no remaining non-Naked moves are possible.
                    if (!anyProgress)
                    {
                        break;
                    }
                }

                if (_requireNonNakedContribution
                    && hasAnyNonNakedValueHandler
                    && !usedAnyNonNakedRule)
                {
                    continue;
                }

                FinalizeBoard(board);

                // Reject trivially easy boards when harder rules are available.
                if (_requireNonNakedContribution
                    && hasAnyNonNakedValueHandler
                    && IsSolvableByNakedSingleOnly(board))
                {
                    continue;
                }

                if (HasUniqueSolution(board))
                {
                    LastGenerationRuleSequence = appliedRuleSequence;
                    LastGenerationRuleUsageSummary = BuildConsecutiveRuleSummary(appliedRuleSequence);
                    return board;
                }
            }

            throw new InvalidOperationException(
                $"Failed to generate a uniquely solvable puzzle after {_maxRetries} attempts.");
        }

        /**
         * Build a compact grouped summary from an ordered rule sequence.
         * Consecutive identical rules are collapsed (e.g. "45 x Naked Single").
         *
         * @param orderedRuleSequence Ordered rule names as they were applied.
         * @returns Grouped summary string, or empty when sequence is empty.
         */
        public static string BuildConsecutiveRuleSummary(IReadOnlyList<string> orderedRuleSequence)
        {
            if (orderedRuleSequence == null || orderedRuleSequence.Count == 0)
            {
                return string.Empty;
            }

            var groups = new List<string>();
            string current = orderedRuleSequence[0] ?? string.Empty;
            int count = 1;

            for (int i = 1; i < orderedRuleSequence.Count; i++)
            {
                string name = orderedRuleSequence[i] ?? string.Empty;
                if (string.Equals(name, current, StringComparison.Ordinal))
                {
                    count++;
                    continue;
                }

                groups.Add($"{count} x {current}");
                current = name;
                count = 1;
            }

            groups.Add($"{count} x {current}");
            return string.Join(" | ", groups);
        }

        // ── Board helpers ──────────────────────────────────────────────────────────

        /**
         * Produce a deep clone of <paramref name="source"/> (cells and their candidates are
         * copied; the change-log is not copied — the clone starts fresh).
         */
        public static Board CloneBoard(Board source)
        {
            var clone = new Board(source.Size, source.BoxWidth, source.BoxHeight);
            for (int r = 0; r < source.Size; r++)
                for (int c = 0; c < source.Size; c++)
                    clone.Cells[r, c] = source.Cells[r, c].Clone();
            return clone;
        }

        /**
         * Prepare the board for play:
         * <list type="bullet">
         *   <item>Mark cells that still have a value as <c>IsGiven = true</c>.</item>
         *   <item>Compute candidates for empty cells from peer set-values.</item>
         *   <item>Clear the change-log so the player starts from a clean history.</item>
         * </list>
         */
        public static void FinalizeBoard(Board board)
        {
            // First pass: clear candidates and set IsGiven.
            for (int r = 0; r < board.Size; r++)
            {
                for (int c = 0; c < board.Size; c++)
                {
                    var cell = board.Cells[r, c];
                    cell.Candidates.Clear();
                    if (cell.Value.HasValue)
                    {
                        cell.IsGiven = true;
                    }
                    else
                    {
                        cell.IsGiven = false;
                        for (int v = 1; v <= board.Size; v++) cell.Candidates.Add(v);
                    }
                }
            }

            // Second pass: eliminate candidates blocked by peer values.
            for (int r = 0; r < board.Size; r++)
            {
                for (int c = 0; c < board.Size; c++)
                {
                    var cell = board.Cells[r, c];
                    if (cell.Value.HasValue) continue;
                    foreach (var peer in board.GetPeers(cell))
                        if (peer.Value.HasValue) cell.Candidates.Remove(peer.Value.Value);
                }
            }

            board.ChangeLog?.Clear();
            board.ChangeLogIndex = 0;
            board.NextChangeGroupId = 1;
        }

        /**
         * Returns true when the board has exactly one solution (verified by backtracking
         * up to 2 solutions — stops as soon as a second is found).
         */
        public static bool HasUniqueSolution(Board board)
        {
            var testBoard = CloneBoard(board);
            return CountSolutions(testBoard, 2) == 1;
        }

        // ── Backtracking solution counter ──────────────────────────────────────────

        private static int CountSolutions(Board board, int maxCount)
        {
            // Find the first empty cell (row-major order).
            Cell empty = null;
            for (int r = 0; r < board.Size && empty == null; r++)
                for (int c = 0; c < board.Size && empty == null; c++)
                    if (!board.Cells[r, c].Value.HasValue) empty = board.Cells[r, c];

            if (empty == null) return 1; // All cells filled → one complete solution.

            int count = 0;
            for (int v = 1; v <= board.Size; v++)
            {
                if (!IsValidPlacement(board, empty, v)) continue;
                empty.Value = v;
                count += CountSolutions(board, maxCount);
                empty.Value = null;
                if (count >= maxCount) return count; // Early-exit once limit reached.
            }
            return count;
        }

        private static bool IsValidPlacement(Board board, Cell cell, int value)
        {
            foreach (var peer in board.GetPeers(cell))
                if (peer.Value == value) return false;
            return true;
        }

        private static bool IsSolvableByNakedSingleOnly(Board board)
        {
            var clone = CloneBoard(board);
            var registry = new RuleRegistry();
            registry.Register(new NakedSingleRule());
            var engine = new SolverEngine(registry);
            return engine.Solve(clone, out _);
        }

        private static string NormalizeRuleDisplayName(string rawRuleName)
        {
            if (string.IsNullOrWhiteSpace(rawRuleName))
            {
                return "Unknown Rule";
            }

            string trimmed = rawRuleName.EndsWith("Rule", StringComparison.Ordinal)
                ? rawRuleName.Substring(0, rawRuleName.Length - 4)
                : rawRuleName;

            var chars = new List<char>(trimmed.Length + 8);
            for (int i = 0; i < trimmed.Length; i++)
            {
                char ch = trimmed[i];
                if (i > 0 && char.IsUpper(ch) && !char.IsWhiteSpace(trimmed[i - 1]))
                {
                    chars.Add(' ');
                }
                chars.Add(ch);
            }

            return new string(chars.ToArray());
        }

        // ── Utility ────────────────────────────────────────────────────────────────

        private static void Shuffle<T>(List<T> list, Random random)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
