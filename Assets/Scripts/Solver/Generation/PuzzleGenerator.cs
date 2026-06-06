using System;
using System.Collections.Generic;
using System.Linq;
using Sudoku.Models;
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
     *   <item>Random passes: shuffle all handlers and apply each in turn; repeat until a
     *         full pass produces no successful unsolve.</item>
     *   <item>Finalize: mark remaining valued cells as givens, recompute candidates for
     *         empty cells, clear the change-log.</item>
     *   <item>Validate uniqueness via backtracking; retry up to
     *         <see cref="DefaultMaxRetries"/> times on failure.</item>
     * </list>
     */
    public class PuzzleGenerator
    {
        public const int DefaultMaxRetries = 5;
        public const int DefaultMaxIterations = 500;

        private readonly int _maxRetries;
        private readonly int _maxIterations;

        public PuzzleGenerator(int maxRetries = DefaultMaxRetries, int maxIterations = DefaultMaxIterations)
        {
            _maxRetries = maxRetries;
            _maxIterations = maxIterations;
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

                // Warm-up: drain Naked Single unsolve first to maximise removals.
                var nakedHandler = handlers.OfType<NakedSingleUnsolveHandler>().FirstOrDefault();
                if (nakedHandler != null)
                    while (nakedHandler.TryUnsolve(board, random) == UnsolveResult.Success) { }

                // Random passes until no handler makes progress in a full sweep.
                bool anyProgress = true;
                int iterations = 0;
                while (anyProgress && iterations < _maxIterations)
                {
                    anyProgress = false;
                    Shuffle(handlers, random);
                    foreach (var handler in handlers)
                    {
                        if (handler.TryUnsolve(board, random) == UnsolveResult.Success)
                            anyProgress = true;
                    }
                    iterations++;
                }

                FinalizeBoard(board);

                if (HasUniqueSolution(board))
                    return board;
            }

            throw new InvalidOperationException(
                $"Failed to generate a uniquely solvable puzzle after {_maxRetries} attempts.");
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
