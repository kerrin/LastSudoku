using System;
using System.Collections.Generic;
using Sudoku.Models;
using Sudoku.Solver.Rules;

namespace Sudoku.Solver.Unsolver
{
    /**
     * Simplified Right Angle unsolve strategy:
     * 1) Find a 2x2 fully-valued group inside one box.
     * 2) Try removing one value and check if Right Angle restores it with no other changes.
     * 3) Otherwise allow one reinstate (same digit) in the deduction row/column derived
     *    from that 2x2 pair, then re-check Right Angle.
     */
    public class RightAngleUnsolveHandler : IUnsolveHandler, IPuzzleGenerationDebugTraceAware
    {
        private readonly RightAngleRule _rightAngleRule = new RightAngleRule();
        private IPuzzleGenerationDebugTracer _debugTracer;
        private Board _solvedBoard;

        public string RuleName => nameof(RightAngleRule);

        public void SetSolvedBoard(Board solvedBoard)
        {
            _solvedBoard = solvedBoard;
        }

        public void SetDebugTracer(IPuzzleGenerationDebugTracer debugTracer)
        {
            _debugTracer = debugTracer;
        }

        public UnsolveResult TryUnsolve(Board board, Random random)
        {
            var opportunities = BuildOpportunities(board);
            _debugTracer?.RecordSnapshot(
                board,
                "Right Angle discovery",
                $"Found {opportunities.Count} 2x2 opportunity targets.",
                RuleName,
                PuzzleGenerationDebugEventKind.InternalStep,
                depth: 1);

            if (opportunities.Count == 0)
            {
                _debugTracer?.RecordSnapshot(
                    board,
                    "Right Angle no candidates",
                    "No eligible 2x2 in-box targets were found.",
                    RuleName,
                    PuzzleGenerationDebugEventKind.InternalStep,
                    depth: 1);
                return UnsolveResult.NoApplicableMove;
            }

            Shuffle(opportunities, random);
            foreach (var opportunity in opportunities)
            {
                _debugTracer?.RecordSnapshot(
                    board,
                    "Right Angle evaluate target",
                    BuildOpportunityLabel(opportunity),
                    RuleName,
                    PuzzleGenerationDebugEventKind.InternalStep,
                    depth: 1,
                    usedCells: new List<UsedCell>
                    {
                        new UsedCell { Row = opportunity.TargetRow, Column = opportunity.TargetColumn, HighlightTag = "Target" }
                    });

                if (TryApplyDirect(board, opportunity))
                {
                    return UnsolveResult.Success;
                }

                if (TryApplyWithSingleReinstate(board, opportunity))
                {
                    return UnsolveResult.Success;
                }

                _debugTracer?.RecordSnapshot(
                    board,
                    "Right Angle opportunity failed",
                    "Direct removal and all single-helper reinstates failed for this target.",
                    RuleName,
                    PuzzleGenerationDebugEventKind.InternalStep,
                    depth: 1,
                    usedCells: new List<UsedCell>
                    {
                        new UsedCell { Row = opportunity.TargetRow, Column = opportunity.TargetColumn, HighlightTag = "Failure" }
                    });
            }

            _debugTracer?.RecordSnapshot(
                board,
                "Right Angle exhausted",
                "All discovered opportunities were evaluated, none produced a valid Right Angle unsolve.",
                RuleName,
                PuzzleGenerationDebugEventKind.InternalStep,
                depth: 1);
            return UnsolveResult.NoApplicableMove;
        }

        public List<Cell> BuildCandidateList(Board board)
        {
            var result = new List<Cell>();
            var opportunities = BuildOpportunities(board);

            foreach (var opportunity in opportunities)
            {
                var trial = PuzzleGenerator.CloneBoard(board);
                var target = trial.Cells[opportunity.TargetRow, opportunity.TargetColumn];
                target.Value = null;
                target.IsGiven = false;
                RecomputeCandidates(trial);

                if (TryGetTargetRightAngleResult(trial, opportunity.TargetRow, opportunity.TargetColumn, opportunity.TargetValue, out _)
                    || CanSucceedWithOneReinstate(trial, opportunity))
                {
                    result.Add(opportunity.TargetCell);
                }
            }

            return result;
        }

        private bool TryApplyDirect(Board board, Opportunity opportunity)
        {
            var trial = PuzzleGenerator.CloneBoard(board);
            var target = trial.Cells[opportunity.TargetRow, opportunity.TargetColumn];
            target.Value = null;
            target.IsGiven = false;
            RecomputeCandidates(trial);

            if (!TryGetTargetRightAngleResult(
                trial,
                opportunity.TargetRow,
                opportunity.TargetColumn,
                opportunity.TargetValue,
                out _,
                out string directFailureReason))
            {
                _debugTracer?.RecordSnapshot(
                    trial,
                    "Right Angle direct failed",
                    $"Removing target alone did not produce a Right Angle restoration for this target. {directFailureReason}",
                    RuleName,
                    PuzzleGenerationDebugEventKind.InternalStep,
                    depth: 2,
                    usedCells: new List<UsedCell>
                    {
                        new UsedCell { Row = opportunity.TargetRow, Column = opportunity.TargetColumn, HighlightTag = "Failure" }
                    });
                return false;
            }

            _debugTracer?.RecordTransition(
                board,
                trial,
                "Right Angle target removal",
                $"Removed r{opportunity.TargetRow}c{opportunity.TargetColumn}={opportunity.TargetValue}; Right Angle can restore it with no helper reinstates.",
                RuleName,
                PuzzleGenerationDebugEventKind.InternalStep,
                depth: 1,
                usedCells: new List<UsedCell>
                {
                    new UsedCell { Row = opportunity.TargetRow, Column = opportunity.TargetColumn, HighlightTag = "Target" }
                });

            ApplyBoardState(board, trial);
            return true;
        }

        private bool TryApplyWithSingleReinstate(Board board, Opportunity opportunity)
        {
            var removedTrial = PuzzleGenerator.CloneBoard(board);
            var target = removedTrial.Cells[opportunity.TargetRow, opportunity.TargetColumn];
            target.Value = null;
            target.IsGiven = false;
            RecomputeCandidates(removedTrial);

            if (!TryGetReinstateCandidate(
                removedTrial,
                opportunity,
                out var reinstatement,
                out string helperReason))
            {
                _debugTracer?.RecordSnapshot(
                    removedTrial,
                    "Right Angle helper search failed",
                    helperReason,
                    RuleName,
                    PuzzleGenerationDebugEventKind.InternalStep,
                    depth: 2,
                    usedCells: new List<UsedCell>
                    {
                        new UsedCell { Row = opportunity.TargetRow, Column = opportunity.TargetColumn, HighlightTag = "Failure" }
                    });
                return false;
            }

            var withHelper = PuzzleGenerator.CloneBoard(removedTrial);
            var helperCell = withHelper.Cells[reinstatement.Row, reinstatement.Column];
            helperCell.Value = opportunity.TargetValue;
            helperCell.IsGiven = false;
            RecomputeCandidates(withHelper);

            if (!TryGetTargetRightAngleResult(
                withHelper,
                opportunity.TargetRow,
                opportunity.TargetColumn,
                opportunity.TargetValue,
                out _,
                out string helperFailureReason))
            {
                _debugTracer?.RecordSnapshot(
                    withHelper,
                    "Right Angle helper candidate failed",
                    $"Tried helper r{reinstatement.Row}c{reinstatement.Column}={opportunity.TargetValue}, but Right Angle still did not restore the target. {helperFailureReason}",
                    RuleName,
                    PuzzleGenerationDebugEventKind.InternalStep,
                    depth: 2,
                    usedCells: new List<UsedCell>
                    {
                        new UsedCell { Row = opportunity.TargetRow, Column = opportunity.TargetColumn, HighlightTag = "Target" },
                        new UsedCell { Row = reinstatement.Row, Column = reinstatement.Column, HighlightTag = "Failure" }
                    });
                return false;
            }

            _debugTracer?.RecordTransition(
                board,
                withHelper,
                "Right Angle helper reinstate",
                $"Removed r{opportunity.TargetRow}c{opportunity.TargetColumn}={opportunity.TargetValue} and reinstated r{reinstatement.Row}c{reinstatement.Column}={opportunity.TargetValue} so Right Angle restores the target.",
                RuleName,
                PuzzleGenerationDebugEventKind.InternalStep,
                depth: 1,
                usedCells: new List<UsedCell>
                {
                    new UsedCell { Row = opportunity.TargetRow, Column = opportunity.TargetColumn, HighlightTag = "Target" },
                    new UsedCell { Row = reinstatement.Row, Column = reinstatement.Column, HighlightTag = "Deduction" }
                });

            ApplyBoardState(board, withHelper);
            return true;
        }

        private static string BuildOpportunityLabel(Opportunity opportunity)
        {
            return $"Target r{opportunity.TargetRow}c{opportunity.TargetColumn}={opportunity.TargetValue}; deduction row r{opportunity.DeductionRow}, column c{opportunity.DeductionColumn}.";
        }

        private bool CanSucceedWithOneReinstate(Board removedTrial, Opportunity opportunity)
        {
            return TryGetReinstateCandidate(
                removedTrial,
                opportunity,
                out var reinstatement,
                out _)
                && TryRightAngleWithReinstate(removedTrial, opportunity, reinstatement, out _);
        }

        private List<Opportunity> BuildOpportunities(Board board)
        {
            var opportunities = new List<Opportunity>();

            for (int row = 0; row < board.Size - 1; row++)
            {
                for (int column = 0; column < board.Size - 1; column++)
                {
                    var a = board.Cells[row, column];
                    var b = board.Cells[row, column + 1];
                    var c = board.Cells[row + 1, column];
                    var d = board.Cells[row + 1, column + 1];

                    if (!a.Value.HasValue || !b.Value.HasValue || !c.Value.HasValue || !d.Value.HasValue)
                    {
                        continue;
                    }

                    if (a.Box != b.Box || a.Box != c.Box || a.Box != d.Box)
                    {
                        continue;
                    }

                    int deductionRow = -1;
                    int deductionColumn = -1;
                    foreach (var boxCell in board.GetBox(a.Box))
                    {
                        if (deductionRow < 0 && boxCell.Row != row && boxCell.Row != row + 1)
                        {
                            deductionRow = boxCell.Row;
                        }

                        if (deductionColumn < 0 && boxCell.Column != column && boxCell.Column != column + 1)
                        {
                            deductionColumn = boxCell.Column;
                        }

                        if (deductionRow >= 0 && deductionColumn >= 0)
                        {
                            break;
                        }
                    }

                    if (deductionRow < 0 || deductionColumn < 0)
                    {
                        continue;
                    }

                    TryAddOpportunity(opportunities, a, deductionRow, deductionColumn);
                    TryAddOpportunity(opportunities, b, deductionRow, deductionColumn);
                    TryAddOpportunity(opportunities, c, deductionRow, deductionColumn);
                    TryAddOpportunity(opportunities, d, deductionRow, deductionColumn);
                }
            }

            return opportunities;
        }

        private static void TryAddOpportunity(
            List<Opportunity> opportunities,
            Cell target,
            int deductionRow,
            int deductionColumn)
        {
            if (target.IsGiven || !target.Value.HasValue)
            {
                return;
            }

            opportunities.Add(new Opportunity(target, target.Value.Value, deductionRow, deductionColumn));
        }

        private bool TryGetReinstateCandidate(
            Board board,
            Opportunity opportunity,
            out CellAddress candidate,
            out string reason)
        {
            candidate = default;
            reason = string.Empty;

            bool hasFallback = false;
            CellAddress fallback = default;
            var seen = new HashSet<int>();

            foreach (var cell in board.GetRow(opportunity.DeductionRow))
            {
                if (TryCaptureReinstateCandidate(board, cell, opportunity.TargetValue, seen, out var found, out _))
                {
                    if (_solvedBoard != null && _solvedBoard.Cells[found.Row, found.Column].Value == opportunity.TargetValue)
                    {
                        candidate = found;
                        return true;
                    }

                    if (!hasFallback)
                    {
                        fallback = found;
                        hasFallback = true;
                    }
                }
            }

            foreach (var cell in board.GetColumn(opportunity.DeductionColumn))
            {
                if (TryCaptureReinstateCandidate(board, cell, opportunity.TargetValue, seen, out var found, out _))
                {
                    if (_solvedBoard != null && _solvedBoard.Cells[found.Row, found.Column].Value == opportunity.TargetValue)
                    {
                        candidate = found;
                        return true;
                    }

                    if (!hasFallback)
                    {
                        fallback = found;
                        hasFallback = true;
                    }
                }
            }

            if (hasFallback)
            {
                candidate = fallback;
                return true;
            }

            reason = "No valid helper cell was available in the deduction row or column.";
            return false;
        }

        private static bool TryCaptureReinstateCandidate(
            Board board,
            Cell cell,
            int value,
            HashSet<int> seen,
            out CellAddress found,
            out string reason)
        {
            found = default;
            reason = string.Empty;

            if (cell.Value.HasValue)
            {
                reason = "occupied/given";
                return false;
            }

            if (cell.Candidates == null || cell.Candidates.Count <= 1)
            {
                reason = "singleton";
                return false;
            }

            if (!cell.Candidates.Contains(value))
            {
                reason = "missing candidate";
                return false;
            }

            if (!IsPlacementValid(board, cell, value))
            {
                reason = "invalid placement";
                return false;
            }

            int key = ToCellKey(board.Size, cell.Row, cell.Column);
            if (!seen.Add(key))
            {
                reason = "duplicate";
                return false;
            }

            found = new CellAddress(cell.Row, cell.Column);
            return true;
        }

        private bool TryRightAngleWithReinstate(Board removedTrial, Opportunity opportunity, CellAddress reinstatement, out string failureReason)
        {
            var withHelper = PuzzleGenerator.CloneBoard(removedTrial);
            var helperCell = withHelper.Cells[reinstatement.Row, reinstatement.Column];
            helperCell.Value = opportunity.TargetValue;
            helperCell.IsGiven = false;
            RecomputeCandidates(withHelper);

            if (TryGetTargetRightAngleResult(withHelper, opportunity.TargetRow, opportunity.TargetColumn, opportunity.TargetValue, out _, out failureReason))
            {
                return true;
            }

            return false;
        }

        private bool TryGetTargetRightAngleResult(
            Board board,
            int targetRow,
            int targetColumn,
            int targetValue,
            out RuleResult result)
        {
            return TryGetTargetRightAngleResult(
                board,
                targetRow,
                targetColumn,
                targetValue,
                out result,
                out _);
        }

        private bool TryGetTargetRightAngleResult(
            Board board,
            int targetRow,
            int targetColumn,
            int targetValue,
            out RuleResult result,
            out string failureReason)
        {
            failureReason = string.Empty;
            result = _rightAngleRule.CalculateChanges(board);
            if (result != null && result.Apply)
            {
                foreach (var change in result.Changes)
                {
                    if (change.Row == targetRow
                        && change.Column == targetColumn
                        && change.NewValue == targetValue)
                    {
                        return true;
                    }
                }

                if (result.Changes != null && result.Changes.Count > 0)
                {
                    var first = result.Changes[0];
                    failureReason = $"Rule returned a different target first: r{first.Row}c{first.Column}={first.NewValue}.";
                }
            }

            // RightAngleRule returns only the first found elimination in scan order.
            // A target can still be valid even when another elimination is returned first.
            if (CanRightAnglePlaceAtTarget(board, targetRow, targetColumn, targetValue, out string specificFailure))
            {
                result = new RuleResult
                {
                    Apply = true,
                    Description = $"Right-angle can place {targetValue} at r{targetRow}c{targetColumn}.",
                };
                result.Changes.Add(new CellChange
                {
                    Row = targetRow,
                    Column = targetColumn,
                    NewValue = targetValue,
                });
                return true;
            }

            if (!string.IsNullOrWhiteSpace(specificFailure))
            {
                failureReason = string.IsNullOrWhiteSpace(failureReason)
                    ? specificFailure
                    : failureReason + " " + specificFailure;
            }

            result = null;
            return false;
        }

        private static bool CanRightAnglePlaceAtTarget(
            Board board,
            int targetRow,
            int targetColumn,
            int targetValue,
            out string failureReason)
        {
            failureReason = string.Empty;
            var target = board.Cells[targetRow, targetColumn];
            if (target == null || target.Value.HasValue)
            {
                failureReason = "Target cell is not empty after removal.";
                return false;
            }

            int boxesPerRow = board.Size / board.BoxWidth;
            int boxCount = (board.Size / board.BoxWidth) * (board.Size / board.BoxHeight);

            int testedQuads = 0;
            int failedSingleEmpty = 0;
            int failedNotTargetEmpty = 0;
            int failedCornerContainsTarget = 0;
            int failedBoxContainsTarget = 0;
            int failedOutsideRowSupport = 0;
            int failedOutsideColumnSupport = 0;

            for (int box = 0; box < boxCount; box++)
            {
                int startBoxRow = (box / boxesPerRow) * board.BoxHeight;
                int startBoxColumn = (box % boxesPerRow) * board.BoxWidth;
                var boxCells = board.GetBox(box);

                for (int r0 = startBoxRow; r0 <= startBoxRow + board.BoxHeight - 2; r0++)
                {
                    for (int c0 = startBoxColumn; c0 <= startBoxColumn + board.BoxWidth - 2; c0++)
                    {
                        testedQuads++;
                        var a = board.Cells[r0, c0];
                        var b = board.Cells[r0, c0 + 1];
                        var c = board.Cells[r0 + 1, c0];
                        var d = board.Cells[r0 + 1, c0 + 1];
                        var quad = new[] { a, b, c, d };

                        int placedCount = 0;
                        var placedValues = new HashSet<int>();
                        Cell empty = null;

                        foreach (var cell in quad)
                        {
                            // During unsolve we may temporarily carry singleton-candidate cells.
                            // Treat those as effective placements for corner occupancy only.
                            if (UnsolveValueSemantics.TryGetEffectiveValue(cell, out int effectiveValue))
                            {
                                placedCount++;
                                placedValues.Add(effectiveValue);
                            }
                            else
                            {
                                empty = cell;
                            }
                        }

                        if (placedCount != 3 || empty == null)
                        {
                            failedSingleEmpty++;
                            continue;
                        }

                        if (empty.Row != targetRow || empty.Column != targetColumn)
                        {
                            failedNotTargetEmpty++;
                            continue;
                        }

                        if (placedValues.Contains(targetValue))
                        {
                            failedCornerContainsTarget++;
                            continue;
                        }

                        bool boxHasTarget = false;
                        foreach (var cell in boxCells)
                        {
                            if (cell.Row == targetRow && cell.Column == targetColumn)
                            {
                                continue;
                            }

                            if (cell.Value.HasValue && cell.Value.Value == targetValue)
                            {
                                boxHasTarget = true;
                                break;
                            }
                        }
                        if (boxHasTarget)
                        {
                            failedBoxContainsTarget++;
                            continue;
                        }

                        int rowInBox = -1;
                        for (int rrInBox = startBoxRow; rrInBox < startBoxRow + board.BoxHeight; rrInBox++)
                        {
                            if (rrInBox != r0 && rrInBox != r0 + 1)
                            {
                                rowInBox = rrInBox;
                                break;
                            }
                        }

                        int columnInBox = -1;
                        for (int ccInBox = startBoxColumn; ccInBox < startBoxColumn + board.BoxWidth; ccInBox++)
                        {
                            if (ccInBox != c0 && ccInBox != c0 + 1)
                            {
                                columnInBox = ccInBox;
                                break;
                            }
                        }

                        if (rowInBox < 0 || columnInBox < 0)
                        {
                            continue;
                        }

                        bool rowHasOutside = false;
                        for (int cc = 0; cc < board.Size; cc++)
                        {
                            if (cc >= startBoxColumn && cc < startBoxColumn + board.BoxWidth)
                            {
                                continue;
                            }

                            if (UnsolveValueSemantics.CellRepresentsValue(board.Cells[rowInBox, cc], targetValue))
                            {
                                rowHasOutside = true;
                                break;
                            }
                        }
                        if (!rowHasOutside)
                        {
                            failedOutsideRowSupport++;
                            continue;
                        }

                        bool columnHasOutside = false;
                        for (int rr = 0; rr < board.Size; rr++)
                        {
                            if (rr >= startBoxRow && rr < startBoxRow + board.BoxHeight)
                            {
                                continue;
                            }

                            if (UnsolveValueSemantics.CellRepresentsValue(board.Cells[rr, columnInBox], targetValue))
                            {
                                columnHasOutside = true;
                                break;
                            }
                        }
                        if (!columnHasOutside)
                        {
                            failedOutsideColumnSupport++;
                            continue;
                        }

                        return true;
                    }
                }
            }

            failureReason =
                $"No right-angle match for target after scanning all quads. quads={testedQuads}; " +
                $"not-single-empty={failedSingleEmpty}; " +
                $"empty-not-target={failedNotTargetEmpty}; " +
                $"corner-had-target={failedCornerContainsTarget}; " +
                $"box-had-target={failedBoxContainsTarget}; " +
                $"missing-row-support={failedOutsideRowSupport}; " +
                $"missing-column-support={failedOutsideColumnSupport}.";

            return false;
        }

        private static bool IsPlacementValid(Board board, Cell target, int value)
        {
            foreach (var peer in board.GetPeers(target))
            {
                if (peer.Value == value)
                {
                    return false;
                }
            }

            return true;
        }

        private static void RecomputeCandidates(Board board)
        {
            for (int row = 0; row < board.Size; row++)
            {
                for (int column = 0; column < board.Size; column++)
                {
                    var cell = board.Cells[row, column];
                    cell.Candidates.Clear();
                    if (!cell.Value.HasValue)
                    {
                        for (int value = 1; value <= board.Size; value++)
                        {
                            cell.Candidates.Add(value);
                        }
                    }
                }
            }

            for (int row = 0; row < board.Size; row++)
            {
                for (int column = 0; column < board.Size; column++)
                {
                    var cell = board.Cells[row, column];
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

        private static void ApplyBoardState(Board destination, Board source)
        {
            for (int row = 0; row < destination.Size; row++)
            {
                for (int column = 0; column < destination.Size; column++)
                {
                    var dst = destination.Cells[row, column];
                    var src = source.Cells[row, column];
                    dst.Value = src.Value;
                    dst.IsGiven = src.IsGiven;
                    dst.Candidates.Clear();
                    foreach (int candidate in src.Candidates)
                    {
                        dst.Candidates.Add(candidate);
                    }
                }
            }
        }

        private static int ToCellKey(int boardSize, int row, int column)
        {
            return row * boardSize + column;
        }

        private static void Shuffle<T>(List<T> list, Random random)
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

        private sealed class Opportunity
        {
            public Opportunity(Cell targetCell, int targetValue, int deductionRow, int deductionColumn)
            {
                TargetCell = targetCell;
                TargetValue = targetValue;
                TargetRow = targetCell.Row;
                TargetColumn = targetCell.Column;
                DeductionRow = deductionRow;
                DeductionColumn = deductionColumn;
            }

            public Cell TargetCell { get; }
            public int TargetValue { get; }
            public int TargetRow { get; }
            public int TargetColumn { get; }
            public int DeductionRow { get; }
            public int DeductionColumn { get; }
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

        private struct ReinstatementDiagnostics
        {
            public int Scanned;
            public int Accepted;
            public int SkippedOccupiedOrGiven;
            public int SkippedMissingCandidateValue;
            public int SkippedSingletonCandidate;
            public int SkippedInvalidPlacement;
            public int SkippedDuplicateAcrossUnits;
        }
    }
}
