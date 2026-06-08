using System;
using System.Collections.Generic;
using Sudoku.Models;
using Sudoku.Solver.Rules;

namespace Sudoku.Solver.Unsolver
{
    /**
     * Unsolve handler for the Right Angle rule.
     *
     * The handler prefers removals that are specifically attributable to
     * <see cref="RightAngleRule"/>, but it can fall back to removals that are also
     * recoverable by easier value-placement rules when no RightAngle-exclusive option
     * exists.
     */
    public class RightAngleUnsolveHandler : IUnsolveHandler
    {
        private readonly RightAngleRule _rightAngleRule = new RightAngleRule();
        private readonly NakedSingleUnsolveHandler _nakedSingleUnsolve = new NakedSingleUnsolveHandler();
        private readonly HiddenSingleUnsolveHandler _hiddenSingleUnsolve = new HiddenSingleUnsolveHandler();
        private readonly TargetSolvabilityCoordinator _targetSolvabilityCoordinator;
        private Board _solvedBoard;

        public string RuleName => nameof(RightAngleRule);

        public RightAngleUnsolveHandler()
        {
            _targetSolvabilityCoordinator = new TargetSolvabilityCoordinator(
                new ITargetSolvabilityBlocker[]
                {
                    _nakedSingleUnsolve,
                    _hiddenSingleUnsolve,
                });
        }

        /**
         * Provide solved-board value context so this handler can optionally reinstate
         * supporting values when a pure removal cannot create a Right Angle deduction.
         *
         * @param solvedBoard Solved value map for this generation attempt.
         */
        public void SetSolvedBoard(Board solvedBoard)
        {
            _solvedBoard = solvedBoard;
        }

        /**
         * Attempt to remove one value whose restoration is specifically attributable to
         * the Right Angle rule.
         *
         * @param board The working board to modify when a valid removal is found.
         * @param random Random source used to choose among valid removals.
         * @returns <see cref="UnsolveResult.Success"/> when a value is removed; otherwise
         *          <see cref="UnsolveResult.NoApplicableMove"/>.
         */
        public UnsolveResult TryUnsolve(Board board, System.Random random)
        {
            var candidates = BuildMutationCandidates(board);
            if (candidates.Count == 0)
            {
                return UnsolveResult.NoApplicableMove;
            }

            Shuffle(candidates, random);
            Board fallbackBoard = null;

            foreach (var chosen in candidates)
            {
                int targetRow = chosen.TargetCell.Row;
                int targetColumn = chosen.TargetCell.Column;
                int targetValue = chosen.TargetCell.Value.Value;

                var trial = PuzzleGenerator.CloneBoard(board);
                var trialTarget = trial.Cells[targetRow, targetColumn];

                foreach (var helper in chosen.HelperPlacements)
                {
                    var helperCell = trial.Cells[helper.Row, helper.Column];
                    helperCell.Value = helper.Value;
                    helperCell.IsGiven = false;
                }

                trialTarget.Value = null;
                trialTarget.IsGiven = false;

                var contextualRemovals = BuildContextualRemovals(trial, chosen, targetValue);
                foreach (var removal in contextualRemovals)
                {
                    var removalCell = trial.Cells[removal.Row, removal.Column];
                    if (removalCell.IsGiven || !removalCell.Value.HasValue)
                    {
                        continue;
                    }

                    removalCell.Value = null;
                    removalCell.IsGiven = false;
                }

                RecomputeCandidates(trial);
                bool hardened = _targetSolvabilityCoordinator.TryMakeTargetNotSolvableByOtherRules(
                    trial,
                    trialTarget,
                    targetValue,
                    RuleName,
                    random,
                    hardeningBoard => TryGetTargetRightAngleResult(
                        hardeningBoard,
                        targetRow,
                        targetColumn,
                        targetValue,
                        out _));

                RecomputeCandidates(trial);
                if (!TryGetTargetRightAngleResult(trial, targetRow, targetColumn, targetValue, out _))
                {
                    continue;
                }

                if (!hardened)
                {
                    if (fallbackBoard == null)
                    {
                        fallbackBoard = trial;
                    }

                    continue;
                }

                ApplyBoardState(board, trial);
                return UnsolveResult.Success;
            }

            if (fallbackBoard != null)
            {
                ApplyBoardState(board, fallbackBoard);
                return UnsolveResult.Success;
            }

            return UnsolveResult.NoApplicableMove;
        }

        private List<CellAddress> BuildContextualRemovals(
            Board board,
            RightAngleMutationCandidate chosen,
            int targetValue)
        {
            var trial = PuzzleGenerator.CloneBoard(board);
            int targetRow = chosen.TargetCell.Row;
            int targetColumn = chosen.TargetCell.Column;

            foreach (var helper in chosen.HelperPlacements)
            {
                var helperCell = trial.Cells[helper.Row, helper.Column];
                helperCell.Value = helper.Value;
                helperCell.IsGiven = false;
            }

            var trialTarget = trial.Cells[targetRow, targetColumn];
            trialTarget.Value = null;
            trialTarget.IsGiven = false;
            RecomputeCandidates(trial);

            if (!TryGetTargetRightAngleResult(trial, targetRow, targetColumn, targetValue, out var rightAngleResult))
            {
                return new List<CellAddress>();
            }

            var protectedKeys = new HashSet<int>
            {
                ToCellKey(board.Size, targetRow, targetColumn)
            };
            foreach (var helper in chosen.HelperPlacements)
            {
                protectedKeys.Add(ToCellKey(board.Size, helper.Row, helper.Column));
            }

            foreach (var used in rightAngleResult.UsedCells)
            {
                protectedKeys.Add(ToCellKey(board.Size, used.Row, used.Column));
            }

            var contextualRemovals = new List<CellAddress>();

            bool progressed;
            do
            {
                progressed = false;

                RecomputeCandidates(trial);
                var nakedCandidates = _nakedSingleUnsolve.BuildCandidateList(trial);

                var removableKeys = new HashSet<int>();
                foreach (var nakedCandidate in nakedCandidates)
                {
                    removableKeys.Add(ToCellKey(board.Size, nakedCandidate.Row, nakedCandidate.Column));
                }

                var scopedCandidates = CollectContextualCandidates(
                    trial,
                    chosen,
                    rightAngleResult,
                    protectedKeys);

                foreach (var scopedCandidate in scopedCandidates)
                {
                    int key = ToCellKey(board.Size, scopedCandidate.Row, scopedCandidate.Column);
                    if (!removableKeys.Contains(key))
                    {
                        continue;
                    }

                    int previousValue = scopedCandidate.Value.Value;
                    scopedCandidate.Value = null;
                    scopedCandidate.IsGiven = false;

                    RecomputeCandidates(trial);
                    if (TryGetTargetRightAngleResult(trial, targetRow, targetColumn, targetValue, out _))
                    {
                        protectedKeys.Add(key);
                        contextualRemovals.Add(new CellAddress(scopedCandidate.Row, scopedCandidate.Column));
                        progressed = true;
                        break;
                    }

                    scopedCandidate.Value = previousValue;
                    scopedCandidate.IsGiven = false;
                }
            }
            while (progressed);

            return contextualRemovals;
        }

        private List<Cell> CollectContextualCandidates(
            Board board,
            RightAngleMutationCandidate chosen,
            RuleResult rightAngleResult,
            HashSet<int> protectedKeys)
        {
            int boardSize = board.Size;
            int targetKey = ToCellKey(boardSize, chosen.TargetCell.Row, chosen.TargetCell.Column);

            var ordered = new List<Cell>();
            var seen = new HashSet<int>();

            // Priority 1: cells seen by the target cell.
            foreach (var peer in board.GetPeers(board.Cells[chosen.TargetCell.Row, chosen.TargetCell.Column]))
            {
                TryAddContextCandidate(peer, boardSize, protectedKeys, seen, ordered);
            }

            // Priority 2: non-deduction cells in deduction rows/columns/boxes.
            var deductionRows = new HashSet<int>();
            var deductionColumns = new HashSet<int>();
            var deductionBoxes = new HashSet<int>();
            foreach (var used in rightAngleResult.UsedCells)
            {
                deductionRows.Add(used.Row);
                deductionColumns.Add(used.Column);
                deductionBoxes.Add(board.Cells[used.Row, used.Column].Box);
            }

            foreach (int row in deductionRows)
            {
                foreach (var rowCell in board.GetRow(row))
                {
                    TryAddContextCandidate(rowCell, boardSize, protectedKeys, seen, ordered);
                }
            }

            foreach (int column in deductionColumns)
            {
                foreach (var columnCell in board.GetColumn(column))
                {
                    TryAddContextCandidate(columnCell, boardSize, protectedKeys, seen, ordered);
                }
            }

            foreach (int box in deductionBoxes)
            {
                foreach (var boxCell in board.GetBox(box))
                {
                    TryAddContextCandidate(boxCell, boardSize, protectedKeys, seen, ordered);
                }
            }

            // Always protect the target even if it appears in scans.
            seen.Remove(targetKey);
            return ordered;
        }

        private static void TryAddContextCandidate(
            Cell candidate,
            int boardSize,
            HashSet<int> protectedKeys,
            HashSet<int> seen,
            List<Cell> ordered)
        {
            if (!candidate.Value.HasValue || candidate.IsGiven)
            {
                return;
            }

            int key = ToCellKey(boardSize, candidate.Row, candidate.Column);
            if (protectedKeys.Contains(key) || !seen.Add(key))
            {
                return;
            }

            ordered.Add(candidate);
        }

        private bool TryGetTargetRightAngleResult(
            Board board,
            int targetRow,
            int targetColumn,
            int targetValue,
            out RuleResult result)
        {
            result = _rightAngleRule.CalculateChanges(board);
            if (result == null || !result.Apply)
            {
                return false;
            }

            foreach (var change in result.Changes)
            {
                if (change.Row == targetRow
                    && change.Column == targetColumn
                    && change.NewValue == targetValue)
                {
                    return true;
                }
            }

            result = null;
            return false;
        }

        private static int ToCellKey(int boardSize, int row, int column)
        {
            return row * boardSize + column;
        }

        private static void ApplyBoardState(Board destination, Board source)
        {
            for (int r = 0; r < destination.Size; r++)
            {
                for (int c = 0; c < destination.Size; c++)
                {
                    var target = destination.Cells[r, c];
                    var from = source.Cells[r, c];

                    target.Value = from.Value;
                    target.IsGiven = from.IsGiven;
                    target.Candidates.Clear();
                    foreach (var candidate in from.Candidates)
                    {
                        target.Candidates.Add(candidate);
                    }
                }
            }
        }

        private static void Shuffle<T>(List<T> list, System.Random random)
        {
            if (random == null)
            {
                return;
            }

            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /**
         * Collect all non-given cells whose removal creates a Right Angle placement.
         * RightAngle-exclusive candidates are preferred; candidates that also satisfy
         * easier rules are returned only when no preferred option exists.
         *
         * @param board The board to inspect.
         * @returns A list of original board cells that can be removed safely.
         */
        public List<Cell> BuildCandidateList(Board board)
        {
            var preferred = new List<Cell>();
            var fallback = new List<Cell>();
            var assistedPreferred = new List<Cell>();
            var assistedFallback = new List<Cell>();

            foreach (var candidate in BuildMutationCandidates(board))
            {
                if (candidate.HelperPlacements.Count == 0)
                {
                    if (candidate.AlsoSolvedByEasierRule)
                    {
                        fallback.Add(candidate.TargetCell);
                    }
                    else
                    {
                        preferred.Add(candidate.TargetCell);
                    }
                }
                else
                {
                    if (candidate.AlsoSolvedByEasierRule)
                    {
                        assistedFallback.Add(candidate.TargetCell);
                    }
                    else
                    {
                        assistedPreferred.Add(candidate.TargetCell);
                    }
                }
            }

            if (preferred.Count > 0)
            {
                return preferred;
            }

            if (fallback.Count > 0)
            {
                return fallback;
            }

            if (assistedPreferred.Count > 0)
            {
                return assistedPreferred;
            }

            return assistedFallback;
        }

        private List<RightAngleMutationCandidate> BuildMutationCandidates(Board board)
        {
            var preferred = new List<RightAngleMutationCandidate>();
            var fallback = new List<RightAngleMutationCandidate>();

            for (int r = 0; r < board.Size; r++)
            {
                for (int c = 0; c < board.Size; c++)
                {
                    var cell = board.Cells[r, c];
                    if (!cell.Value.HasValue || cell.IsGiven)
                    {
                        continue;
                    }

                    if (!TryClassifyRightAngleOpportunity(board, cell, out bool alsoSolvedByEasierRule))
                    {
                        continue;
                    }

                    if (alsoSolvedByEasierRule)
                    {
                        fallback.Add(new RightAngleMutationCandidate(cell, alsoSolvedByEasierRule));
                    }
                    else
                    {
                        preferred.Add(new RightAngleMutationCandidate(cell, alsoSolvedByEasierRule));
                    }
                }
            }

            if (preferred.Count > 0)
            {
                return preferred;
            }

            if (fallback.Count > 0)
            {
                return fallback;
            }

            // Assisted search is significantly more expensive than direct classification.
            // Only run it when no direct removal candidate exists.
            var assistedPreferred = new List<RightAngleMutationCandidate>();
            var assistedFallback = new List<RightAngleMutationCandidate>();
            for (int r = 0; r < board.Size; r++)
            {
                for (int c = 0; c < board.Size; c++)
                {
                    var cell = board.Cells[r, c];
                    if (!cell.Value.HasValue || cell.IsGiven)
                    {
                        continue;
                    }

                    if (!TryBuildAssistedOpportunity(board, cell, out var helperPlacements, out bool assistedAlsoSolvedByEasierRule))
                    {
                        continue;
                    }

                    var assisted = new RightAngleMutationCandidate(cell, assistedAlsoSolvedByEasierRule);
                    assisted.HelperPlacements.AddRange(helperPlacements);
                    if (assistedAlsoSolvedByEasierRule)
                    {
                        assistedFallback.Add(assisted);
                    }
                    else
                    {
                        assistedPreferred.Add(assisted);
                    }
                }
            }

            if (assistedPreferred.Count > 0)
            {
                return assistedPreferred;
            }

            return assistedFallback;
        }

        private bool TryBuildAssistedOpportunity(
            Board board,
            Cell sourceCell,
            out List<HelperPlacement> helperPlacements,
            out bool alsoSolvedByEasierRule)
        {
            helperPlacements = new List<HelperPlacement>();
            alsoSolvedByEasierRule = false;

            if (_solvedBoard == null)
            {
                return false;
            }

            var trialBoard = PuzzleGenerator.CloneBoard(board);
            var trialCell = trialBoard.Cells[sourceCell.Row, sourceCell.Column];
            int targetValue = trialCell.Value.Value;

            trialCell.Value = null;
            trialCell.IsGiven = false;

            var potentialHelpers = CollectPotentialHelpers(trialBoard, sourceCell);
            if (potentialHelpers.Count == 0)
            {
                return false;
            }

            // Performance guard: only evaluate single-helper reinstatements.
            // This covers the common case where one removed support value blocked Right Angle.
            for (int i = 0; i < potentialHelpers.Count; i++)
            {
                var one = new List<HelperPlacement> { potentialHelpers[i] };
                if (EvaluateAssistedCombo(trialBoard, trialCell, targetValue, one, out alsoSolvedByEasierRule))
                {
                    helperPlacements = one;
                    return true;
                }
            }

            return false;
        }

        private bool EvaluateAssistedCombo(
            Board baseTrialBoard,
            Cell sourceCell,
            int targetValue,
            List<HelperPlacement> helpers,
            out bool alsoSolvedByEasierRule)
        {
            alsoSolvedByEasierRule = false;
            var trial = PuzzleGenerator.CloneBoard(baseTrialBoard);

            foreach (var helper in helpers)
            {
                var helperCell = trial.Cells[helper.Row, helper.Column];
                if (helperCell.Value.HasValue)
                {
                    return false;
                }

                if (!IsPlacementValid(trial, helperCell, helper.Value))
                {
                    return false;
                }

                helperCell.Value = helper.Value;
                helperCell.IsGiven = false;
            }

            var trialSource = trial.Cells[sourceCell.Row, sourceCell.Column];
            RecomputeCandidates(trial);

            bool isNakedSingle = IsNakedSingle(trial, trialSource, targetValue);
            bool isHiddenSingle = IsHiddenSingle(trial, trialSource, targetValue);
            alsoSolvedByEasierRule = isNakedSingle || isHiddenSingle;

            var rightAngleResult = _rightAngleRule.CalculateChanges(trial);
            if (rightAngleResult == null || !rightAngleResult.Apply)
            {
                alsoSolvedByEasierRule = false;
                return false;
            }

            foreach (var change in rightAngleResult.Changes)
            {
                if (change.NewValue == targetValue
                    && change.Row == trialSource.Row
                    && change.Column == trialSource.Column)
                {
                    return true;
                }
            }

            alsoSolvedByEasierRule = false;
            return false;
        }

        private List<HelperPlacement> CollectPotentialHelpers(Board trialBoard, Cell sourceCell)
        {
            var helpers = new List<(HelperPlacement placement, int priority)>();

            for (int r = 0; r < trialBoard.Size; r++)
            {
                for (int c = 0; c < trialBoard.Size; c++)
                {
                    var trialCell = trialBoard.Cells[r, c];
                    if (trialCell.Value.HasValue || trialCell.IsGiven)
                    {
                        continue;
                    }

                    var solvedCell = _solvedBoard.Cells[r, c];
                    if (solvedCell == null || !solvedCell.Value.HasValue)
                    {
                        continue;
                    }

                    int solvedValue = solvedCell.Value.Value;
                    if (!IsPlacementValid(trialBoard, trialCell, solvedValue))
                    {
                        continue;
                    }

                    int priority = GetHelperPriority(sourceCell, trialCell);
                    helpers.Add((new HelperPlacement(r, c, solvedValue), priority));
                }
            }

            helpers.Sort((a, b) => a.priority.CompareTo(b.priority));

            const int maxHelperPool = 6;
            var result = new List<HelperPlacement>(Math.Min(maxHelperPool, helpers.Count));
            for (int i = 0; i < helpers.Count && i < maxHelperPool; i++)
            {
                result.Add(helpers[i].placement);
            }

            return result;
        }

        private static int GetHelperPriority(Cell sourceCell, Cell helperCell)
        {
            if (helperCell.Box == sourceCell.Box)
            {
                return 0;
            }

            if (helperCell.Row == sourceCell.Row || helperCell.Column == sourceCell.Column)
            {
                return 1;
            }

            return 2;
        }

        private static bool IsPlacementValid(Board board, Cell target, int value)
        {
            foreach (var peer in board.GetPeers(target))
            {
                if (peer.Value.HasValue && peer.Value.Value == value)
                {
                    return false;
                }
            }

            return true;
        }

        /**
         * Test a single cell by removing it on a clone, rebuilding candidates, and then
         * checking whether Right Angle restores exactly that value in that cell.
         *
         * @param board The source board.
         * @param sourceCell The valued cell to test.
         * @param alsoSolvedByEasierRule Set when the removed value is also recoverable by
         *        Naked Single or Hidden Single.
         * @returns True when the removed value is restored by Right Angle.
         */
        private bool TryClassifyRightAngleOpportunity(Board board, Cell sourceCell, out bool alsoSolvedByEasierRule)
        {
            var trialBoard = PuzzleGenerator.CloneBoard(board);
            var trialCell = trialBoard.Cells[sourceCell.Row, sourceCell.Column];
            int value = trialCell.Value.Value;

            trialCell.Value = null;
            trialCell.IsGiven = false;
            RecomputeCandidates(trialBoard);

            bool isNakedSingle = IsNakedSingle(trialBoard, trialCell, value);
            bool isHiddenSingle = IsHiddenSingle(trialBoard, trialCell, value);
            alsoSolvedByEasierRule = isNakedSingle || isHiddenSingle;

            var result = _rightAngleRule.CalculateChanges(trialBoard);
            if (result == null || !result.Apply)
            {
                alsoSolvedByEasierRule = false;
                return false;
            }

            foreach (var change in result.Changes)
            {
                if (change.NewValue == value
                    && change.Row == trialCell.Row
                    && change.Column == trialCell.Column)
                {
                    return true;
                }
            }

            alsoSolvedByEasierRule = false;
            return false;
        }

        /**
         * Recompute candidates from the current set values so rule checks evaluate against
         * the same candidate semantics used by the solver on finalized puzzles.
         *
         * @param board The board whose candidates should be rebuilt.
         */
        private static void RecomputeCandidates(Board board)
        {
            for (int r = 0; r < board.Size; r++)
            {
                for (int c = 0; c < board.Size; c++)
                {
                    var cell = board.Cells[r, c];
                    cell.Candidates.Clear();
                    if (!cell.Value.HasValue)
                    {
                        for (int digit = 1; digit <= board.Size; digit++)
                        {
                            cell.Candidates.Add(digit);
                        }
                    }
                }
            }

            for (int r = 0; r < board.Size; r++)
            {
                for (int c = 0; c < board.Size; c++)
                {
                    var cell = board.Cells[r, c];
                    if (cell.Value.HasValue)
                    {
                        continue;
                    }

                    foreach (var peer in board.GetPeers(cell))
                    {
                        if (peer.Value.HasValue)
                        {
                            cell.Candidates.Remove(peer.Value.Value);
                        }
                    }
                }
            }
        }

        /**
         * Determine whether the tested cell would be immediately placed by Naked Single.
         *
         * @param board The candidate-rebuilt trial board.
         * @param cell The empty cell under test.
         * @param value The original value removed from the cell.
         * @returns True when all other digits are already visible among the cell's peers.
         */
        private static bool IsNakedSingle(Board board, Cell cell, int value)
        {
            var peerValues = new HashSet<int>();
            foreach (var peer in board.GetPeers(cell))
            {
                if (peer.Value.HasValue)
                {
                    peerValues.Add(peer.Value.Value);
                }
            }

            for (int digit = 1; digit <= board.Size; digit++)
            {
                if (digit == value)
                {
                    continue;
                }

                if (!peerValues.Contains(digit))
                {
                    return false;
                }
            }

            return true;
        }

        /**
         * Determine whether the tested cell would already qualify as a Hidden Single.
         *
         * @param board The candidate-rebuilt trial board.
         * @param cell The empty cell under test.
         * @param value The original value removed from the cell.
         * @returns True when the value appears in exactly one candidate position in any
         *          of the cell's row, column, or box.
         */
        private static bool IsHiddenSingle(Board board, Cell cell, int value)
        {
            if (!cell.Candidates.Contains(value))
            {
                return false;
            }

            return CountCandidateOccurrences(board.GetRow(cell.Row), value) == 1
                || CountCandidateOccurrences(board.GetColumn(cell.Column), value) == 1
                || CountCandidateOccurrences(board.GetBox(cell.Box), value) == 1;
        }

        /**
         * Count how many empty cells in a unit still allow the supplied digit.
         *
         * @param unitCells The row, column, or box to inspect.
         * @param value The digit to count.
         * @returns The number of empty cells whose candidates include the digit.
         */
        private static int CountCandidateOccurrences(IEnumerable<Cell> unitCells, int value)
        {
            int count = 0;
            foreach (var unitCell in unitCells)
            {
                if (!unitCell.Value.HasValue && unitCell.Candidates.Contains(value))
                {
                    count++;
                }
            }

            return count;
        }

        private sealed class RightAngleMutationCandidate
        {
            public RightAngleMutationCandidate(Cell targetCell, bool alsoSolvedByEasierRule)
            {
                TargetCell = targetCell;
                AlsoSolvedByEasierRule = alsoSolvedByEasierRule;
            }

            public Cell TargetCell { get; }
            public bool AlsoSolvedByEasierRule { get; }
            public List<HelperPlacement> HelperPlacements { get; } = new List<HelperPlacement>();
        }

        private readonly struct HelperPlacement
        {
            public HelperPlacement(int row, int column, int value)
            {
                Row = row;
                Column = column;
                Value = value;
            }

            public int Row { get; }
            public int Column { get; }
            public int Value { get; }
        }

        private readonly struct CellAddress
        {
            public CellAddress(int row, int column)
            {
                Row = row;
                Column = column;
            }

            public int Row { get; }
            public int Column { get; }
        }
    }
}