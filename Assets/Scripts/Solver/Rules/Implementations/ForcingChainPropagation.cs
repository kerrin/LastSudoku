using System.Collections.Generic;
using System.Linq;
using Sudoku.Models;
using Board = Sudoku.Models.Board;
using ChainModel = Sudoku.Solver.Rules.ForcingChainLinkModel;

namespace Sudoku.Solver.Rules
{
    internal static class ForcingChainPropagation
    {
        private const int MaxAssignmentsPerBranch = 4000;

        internal static PropagationState PropagateFromAssumption(
            Board board,
            ChainModel model,
            int seedCandidate,
            bool assumeTrue,
            bool enableUnitCompletion)
        {
            var state = new PropagationState();
            TryEnqueueAssignment(board, model, state, seedCandidate, assumeTrue, "Seed assumption");

            while (state.Pending.Count > 0 && !state.HasContradiction)
            {
                if (state.AssignmentOrder.Count > MaxAssignmentsPerBranch)
                {
                    state.HasContradiction = true;
                    state.ContradictionReason = "Chain bound exceeded.";
                    break;
                }

                var (candidate, valueTrue, sourceCandidate, sourceLinkKind) = state.Pending.Dequeue();
                if (!ApplyAssignment(board, model, state, candidate, valueTrue, sourceCandidate, sourceLinkKind))
                {
                    continue;
                }

                // Strong-link propagation:
                // false on one endpoint forces true on the other, true forces false.
                if (model.StrongLinksByKey.TryGetValue(candidate, out var links))
                {
                    for (int i = 0; i < links.Count; i++)
                    {
                        TryEnqueueAssignment(
                            board,
                            model,
                            state,
                            links[i],
                            !valueTrue,
                            "Strong link",
                            sourceCandidate: candidate,
                            sourceLinkKind: DirectionalLinkKind.Strong);
                    }
                }

                // Weak links: true candidate excludes all conflicting candidates.
                if (valueTrue && model.WeakLinksByKey.TryGetValue(candidate, out var weakLinks))
                {
                    // Only true candidates propagate weak links; false candidates do not.
                    for (int i = 0; i < weakLinks.Count; i++)
                    {
                        TryEnqueueAssignment(
                            board,
                            model,
                            state,
                            weakLinks[i],
                            false,
                            "Weak link",
                            sourceCandidate: candidate,
                            sourceLinkKind: DirectionalLinkKind.Weak,
                            isPreviewLink: ShouldExposeWeakConflictAsLink(board, candidate, weakLinks[i]) && !model.IsStrongLink(candidate, weakLinks[i]));
                    }
                }

                // Local cell completion is safe and useful for forcing chains:
                // if all but one candidates in a cell are false, the last one is true.
                ResolveCellGroup(board, model, state, ForcingChainLinkModel.GetRow(board, candidate), ForcingChainLinkModel.GetColumn(board, candidate));

                if (enableUnitCompletion)
                {
                    // Unit completion is useful for contradiction discovery but can
                    // over-constrain sparse synthetic boards used for common-conclusion examples.
                    ResolveUnitGroups(
                        board,
                        model,
                        state,
                        ForcingChainLinkModel.GetRow(board, candidate),
                        ForcingChainLinkModel.GetColumn(board, candidate),
                        ForcingChainLinkModel.GetDigit(board, candidate));
                }
            }

            return state;
        }

        private static void TryEnqueueAssignment(
            Board board,
            ChainModel model,
            PropagationState state,
            int candidate,
            bool valueTrue,
            string reason,
            int? sourceCandidate = null,
            DirectionalLinkKind? sourceLinkKind = null,
            bool isPreviewLink = true)
        {
            if (state.HasContradiction)
            {
                return;
            }

            if (!model.CandidateIndexByKey.ContainsKey(candidate))
            {
                return;
            }

            if (valueTrue)
            {
                if (state.FalseCandidates.Contains(candidate))
                {
                    state.HasContradiction = true;
                    state.ContradictionCandidate = candidate;
                    state.ContradictionSourceCandidate = sourceCandidate;
                    state.ContradictionSourceLinkKind = sourceLinkKind;
                    state.ContradictionReason = $"Candidate {FormatCandidate(board, candidate)} became both true and false ({reason}).";
                    return;
                }

                if (state.TrueCandidates.Contains(candidate))
                {
                    RecordInferenceCauseIfLinked(state, candidate, sourceCandidate, sourceLinkKind, isPreviewLink);
                    return;
                }
            }
            else
            {
                if (state.TrueCandidates.Contains(candidate))
                {
                    state.HasContradiction = true;
                    state.ContradictionCandidate = candidate;
                    state.ContradictionSourceCandidate = sourceCandidate;
                    state.ContradictionSourceLinkKind = sourceLinkKind;
                    state.ContradictionReason = $"Candidate {FormatCandidate(board, candidate)} became both true and false ({reason}).";
                    return;
                }

                if (state.FalseCandidates.Contains(candidate))
                {
                    RecordInferenceCauseIfLinked(state, candidate, sourceCandidate, sourceLinkKind, isPreviewLink);
                    return;
                }
            }

            if (!state.Pending.Any(pending => pending.key == candidate && pending.valueTrue == valueTrue))
            {
                state.Pending.Enqueue((candidate, valueTrue, sourceCandidate, sourceLinkKind));

                RecordInferenceCauseIfLinked(state, candidate, sourceCandidate, sourceLinkKind, isPreviewLink);
            }
        }

        private static void RecordInferenceCauseIfLinked(
            PropagationState state,
            int candidate,
            int? sourceCandidate,
            DirectionalLinkKind? sourceLinkKind,
            bool isPreviewLink)
        {
            if (sourceCandidate.HasValue && sourceLinkKind.HasValue)
            {
                RecordInferenceCause(state, candidate, sourceCandidate.Value, sourceLinkKind.Value, isPreviewLink);
            }
        }

        private static void RecordInferenceCause(PropagationState state, int candidate, int fromCandidate, DirectionalLinkKind linkKind, bool isPreviewLink)
        {
            if (state == null)
            {
                return;
            }

            if (!state.InferenceCausesByCandidate.TryGetValue(candidate, out var causes))
            {
                causes = new List<InferenceCause>();
                state.InferenceCausesByCandidate[candidate] = causes;
            }

            bool alreadyStored = causes.Exists(c => c.FromCandidate == fromCandidate && c.LinkKind == linkKind && c.IsPreviewLink == isPreviewLink);
            if (alreadyStored)
            {
                return;
            }

            causes.Add(new InferenceCause
            {
                FromCandidate = fromCandidate,
                LinkKind = linkKind,
                IsPreviewLink = isPreviewLink,
            });
        }

        private static bool ApplyAssignment(
            Board board,
            ChainModel model,
            PropagationState state,
            int candidate,
            bool valueTrue,
            int? sourceCandidate,
            DirectionalLinkKind? sourceLinkKind)
        {
            if (valueTrue)
            {
                if (state.TrueCandidates.Contains(candidate))
                {
                    return false;
                }

                if (state.FalseCandidates.Contains(candidate))
                {
                    state.HasContradiction = true;
                    state.ContradictionCandidate = candidate;
                    state.ContradictionSourceCandidate = sourceCandidate;
                    state.ContradictionSourceLinkKind = sourceLinkKind;
                    state.ContradictionReason = $"Candidate {FormatCandidate(board, candidate)} became both true and false.";
                    return false;
                }

                state.TrueCandidates.Add(candidate);
            }
            else
            {
                if (state.FalseCandidates.Contains(candidate))
                {
                    return false;
                }

                if (state.TrueCandidates.Contains(candidate))
                {
                    state.HasContradiction = true;
                    state.ContradictionCandidate = candidate;
                    state.ContradictionSourceCandidate = sourceCandidate;
                    state.ContradictionSourceLinkKind = sourceLinkKind;
                    state.ContradictionReason = $"Candidate {FormatCandidate(board, candidate)} became both true and false.";
                    return false;
                }

                state.FalseCandidates.Add(candidate);
            }

            state.AssignmentOrder.Add(candidate);
            return true;
        }

        private static void ResolveCellGroup(Board board, ChainModel model, PropagationState state, int row, int column)
        {
            int cellIndex = row * board.Size + column;
            if (!model.CellGroupsByIndex.TryGetValue(cellIndex, out var group) || group.Count == 0)
            {
                return;
            }

            int trueCount = 0;
            int unknownCount = 0;
            int unknownCandidate = 0;
            int firstTrueCandidate = 0;
            int lastFalseCandidate = 0;

            for (int i = 0; i < group.Count; i++)
            {
                int key = group[i];
                if (state.TrueCandidates.Contains(key))
                {
                    trueCount++;
                    if (trueCount == 1)
                    {
                        firstTrueCandidate = key;
                    }
                    else if (trueCount == 2)
                    {
                        state.ContradictionCandidate = key;
                        state.ContradictionSourceCandidate = firstTrueCandidate;
                        state.ContradictionSourceLinkKind = DirectionalLinkKind.Weak;
                    }
                    continue;
                }

                if (!state.FalseCandidates.Contains(key))
                {
                    unknownCount++;
                    unknownCandidate = key;
                }
                else
                {
                    lastFalseCandidate = key;
                }
            }

            if (trueCount > 1)
            {
                // Too many trues in the cell, so the branch is impossible.
                state.HasContradiction = true;
                state.ContradictionGroupCandidates = group
                    .Where(state.TrueCandidates.Contains)
                    .ToList();
                state.ContradictionReason = $"Cell r{row + 1}c{column + 1} has multiple true forced digits.";
                return;
            }

            if (trueCount == 0 && unknownCount == 0)
            {
                // Resulted in no possible candidates for the cell, so the branch is impossible.
                state.HasContradiction = true;
                state.ContradictionCandidate = lastFalseCandidate;
                state.ContradictionGroupCandidates = group
                    .Where(state.FalseCandidates.Contains)
                    .ToList();
                state.ContradictionReason = $"Cell r{row + 1}c{column + 1} has no valid candidates.";
                return;
            }

            if (trueCount == 0 && unknownCount == 1)
            {
                // Only one candidate left in the cell, so it must be true.
                TryEnqueueAssignment(board, model, state, unknownCandidate, true, "Cell completion");
            }
        }

        private static void ResolveUnitGroups(Board board, ChainModel model, PropagationState state, int row, int column, int digit)
        {
            int box = board.Cells[row, column].Box;

            ResolveUnitGroup(board, model, state, ForcingChainLinkModel.MakeUnitDigitGroupIndex(board, ForcingChainUnitType.Row, row, digit), $"Row {row + 1}");
            ResolveUnitGroup(board, model, state, ForcingChainLinkModel.MakeUnitDigitGroupIndex(board, ForcingChainUnitType.Column, column, digit), $"Column {column + 1}");
            ResolveUnitGroup(board, model, state, ForcingChainLinkModel.MakeUnitDigitGroupIndex(board, ForcingChainUnitType.Box, box, digit), $"Box {box + 1}");
        }

        private static void ResolveUnitGroup(Board board, ChainModel model, PropagationState state, int groupIndex, string groupLabel)
        {
            if (!model.UnitDigitGroupsByIndex.TryGetValue(groupIndex, out var group) || group.Count == 0)
            {
                return;
            }

            // A singleton unit-digit group is already a hidden single before either
            // branch is assumed. Leave it to the single-candidate rules; treating
            // its rejection as a chain contradiction creates false positives when
            // candidate sets are incomplete.
            if (group.Count == 1)
            {
                return;
            }

            int trueCount = 0;
            int unknownCount = 0;
            int falseCount = 0;
            int unknownCandidate = 0;
            int firstTrueCandidate = 0;
            int lastFalseCandidate = 0;

            for (int i = 0; i < group.Count; i++)
            {
                int key = group[i];
                if (state.TrueCandidates.Contains(key))
                {
                    trueCount++;
                    if (trueCount == 1)
                    {
                        firstTrueCandidate = key;
                    }
                    else if (trueCount == 2)
                    {
                        state.ContradictionCandidate = key;
                        state.ContradictionSourceCandidate = firstTrueCandidate;
                        state.ContradictionSourceLinkKind = DirectionalLinkKind.Weak;
                    }
                    continue;
                }

                if (!state.FalseCandidates.Contains(key))
                {
                    unknownCount++;
                    unknownCandidate = key;
                }
                else
                {
                    falseCount++;
                    lastFalseCandidate = key;
                }
            }

            if (trueCount > 1)
            {
                state.HasContradiction = true;
                state.ContradictionGroupCandidates = group
                    .Where(state.TrueCandidates.Contains)
                    .ToList();
                state.ContradictionReason = $"{groupLabel} has multiple forced placements for one digit.";
                return;
            }

            if (trueCount == 0 && unknownCount == 0)
            {
                state.HasContradiction = true;
                state.ContradictionCandidate = lastFalseCandidate;
                state.ContradictionGroupCandidates = group
                    .Where(state.FalseCandidates.Contains)
                    .ToList();
                state.ContradictionReason = $"{groupLabel} has no location left for a digit.";
                return;
            }

            // Restrict hidden-single forcing to groups reduced by branch propagation.
            // This avoids globally-singleton unit groups on sparse synthetic candidate
            // boards from dominating forcing-chain test scenarios.
            if (trueCount == 0 && unknownCount == 1 && falseCount > 0)
            {
                TryEnqueueAssignment(board, model, state, unknownCandidate, true, "Unit completion");
            }
        }

        private static bool ShouldExposeWeakConflictAsLink(Board board, int fromCandidate, int toCandidate)
        {
            if (ForcingChainLinkModel.GetDigit(board, fromCandidate) != ForcingChainLinkModel.GetDigit(board, toCandidate))
            {
                // Only show links between the same digits
                return false;
            }

            int fromRow = ForcingChainLinkModel.GetRow(board, fromCandidate);
            int fromColumn = ForcingChainLinkModel.GetColumn(board, fromCandidate);
            int toRow = ForcingChainLinkModel.GetRow(board, toCandidate);
            int toColumn = ForcingChainLinkModel.GetColumn(board, toCandidate);

            if (fromRow == toRow && fromColumn == toColumn)
            {
                return false;
            }

            var fromCell = board.Cells[fromRow, fromColumn];
            var toCell = board.Cells[toRow, toColumn];
            return fromCell != null && toCell != null && board.GetPeers(fromCell).Contains(toCell);
        }
        private static string FormatCandidate(Board board, int candidate)
        {
            return $"r{ForcingChainLinkModel.GetRow(board, candidate) + 1}c{ForcingChainLinkModel.GetColumn(board, candidate) + 1}={ForcingChainLinkModel.GetDigit(board, candidate)}";
        }
    }
}
