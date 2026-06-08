using System;
using System.Collections.Generic;
using Sudoku.Models;
using Sudoku.Solver.Rules;

namespace Sudoku.Solver.Unsolver
{
    public enum PuzzleGenerationDebugEventKind
    {
        SessionStart = 0,
        RuleAboutToRun = 1,
        RuleApplied = 2,
        InternalStep = 3,
        Finalize = 4,
        Info = 5,
    }

    public interface IPuzzleGenerationDebugTracer
    {
        void RecordSnapshot(
            Board board,
            string title,
            string description,
            string ruleName,
            PuzzleGenerationDebugEventKind kind,
            int depth,
            List<UsedCell> usedCells = null);

        void RecordTransition(
            Board before,
            Board after,
            string title,
            string description,
            string ruleName,
            PuzzleGenerationDebugEventKind kind,
            int depth,
            List<UsedCell> usedCells = null);

        PuzzleGenerationDebugSession BuildSession();
    }

    [Serializable]
    public sealed class PuzzleGenerationDebugEvent
    {
        public int Index;
        public int Depth;
        public string RuleName;
        public string Title;
        public string Description;
        public PuzzleGenerationDebugEventKind Kind;
        public Board Snapshot;
        public RuleResult HighlightResult;
    }

    [Serializable]
    public sealed class PuzzleGenerationDebugSession
    {
        public readonly List<PuzzleGenerationDebugEvent> Events = new List<PuzzleGenerationDebugEvent>();

        public int FindNextIndex(int currentIndex)
        {
            int next = currentIndex + 1;
            return next < Events.Count ? next : -1;
        }

        public int FindStepIntoIndex(int currentIndex)
        {
            int next = FindNextIndex(currentIndex);
            if (next < 0)
            {
                return -1;
            }

            int currentDepth = currentIndex >= 0 && currentIndex < Events.Count
                ? Events[currentIndex].Depth
                : -1;

            for (int i = next; i < Events.Count; i++)
            {
                if (Events[i].Depth > currentDepth)
                {
                    return i;
                }

                if (Events[i].Depth <= currentDepth)
                {
                    break;
                }
            }

            return next;
        }

        public int FindNextDifferentTopLevelRuleIndex(int currentIndex)
        {
            string currentRule = GetCurrentTopLevelRuleName(currentIndex);
            for (int i = currentIndex + 1; i < Events.Count; i++)
            {
                var entry = Events[i];
                if (entry.Depth != 0 || entry.Kind != PuzzleGenerationDebugEventKind.RuleAboutToRun)
                {
                    continue;
                }

                if (!string.Equals(entry.RuleName, currentRule, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        public string GetCurrentTopLevelRuleName(int currentIndex)
        {
            if (currentIndex < 0)
            {
                return string.Empty;
            }

            for (int i = Math.Min(currentIndex, Events.Count - 1); i >= 0; i--)
            {
                var entry = Events[i];
                if (entry.Depth != 0)
                {
                    continue;
                }

                if (entry.Kind == PuzzleGenerationDebugEventKind.RuleAboutToRun
                    || entry.Kind == PuzzleGenerationDebugEventKind.RuleApplied)
                {
                    return entry.RuleName ?? string.Empty;
                }
            }

            return string.Empty;
        }
    }

    public sealed class PuzzleGenerationDebugTracer : IPuzzleGenerationDebugTracer
    {
        private readonly PuzzleGenerationDebugSession _session = new PuzzleGenerationDebugSession();

        public void RecordSnapshot(
            Board board,
            string title,
            string description,
            string ruleName,
            PuzzleGenerationDebugEventKind kind,
            int depth,
            List<UsedCell> usedCells = null)
        {
            if (board == null)
            {
                return;
            }

            _session.Events.Add(new PuzzleGenerationDebugEvent
            {
                Index = _session.Events.Count,
                Depth = Math.Max(0, depth),
                RuleName = ruleName ?? string.Empty,
                Title = title ?? string.Empty,
                Description = description ?? string.Empty,
                Kind = kind,
                Snapshot = PuzzleGenerator.CloneBoard(board),
                HighlightResult = BuildHighlightOnlyResult(description, usedCells),
            });
        }

        public void RecordTransition(
            Board before,
            Board after,
            string title,
            string description,
            string ruleName,
            PuzzleGenerationDebugEventKind kind,
            int depth,
            List<UsedCell> usedCells = null)
        {
            if (after == null)
            {
                return;
            }

            var diff = BuildDiff(before, after, description, ruleName, usedCells);
            _session.Events.Add(new PuzzleGenerationDebugEvent
            {
                Index = _session.Events.Count,
                Depth = Math.Max(0, depth),
                RuleName = ruleName ?? string.Empty,
                Title = title ?? string.Empty,
                Description = description ?? string.Empty,
                Kind = kind,
                Snapshot = PuzzleGenerator.CloneBoard(after),
                HighlightResult = diff,
            });
        }

        public PuzzleGenerationDebugSession BuildSession()
        {
            return _session;
        }

        private static RuleResult BuildHighlightOnlyResult(string description, List<UsedCell> usedCells)
        {
            var result = new RuleResult
            {
                Apply = false,
                Description = description ?? string.Empty,
            };

            AppendUsedCells(result, usedCells);
            return result;
        }

        private static RuleResult BuildDiff(
            Board before,
            Board after,
            string description,
            string ruleName,
            List<UsedCell> usedCells)
        {
            var result = new RuleResult
            {
                Apply = true,
                Description = description ?? string.Empty,
            };

            if (after == null || after.Cells == null)
            {
                result.Apply = false;
                AppendUsedCells(result, usedCells);
                return result;
            }

            for (int row = 0; row < after.Size; row++)
            {
                for (int column = 0; column < after.Size; column++)
                {
                    var afterCell = after.Cells[row, column];
                    var beforeCell = before != null && before.Cells != null
                        ? before.Cells[row, column]
                        : null;

                    int? beforeValue = beforeCell != null ? beforeCell.Value : null;
                    int? afterValue = afterCell != null ? afterCell.Value : null;
                    var removedCandidates = new List<int>();
                    var addedCandidates = new List<int>();

                    if (beforeCell != null && beforeCell.Candidates != null)
                    {
                        foreach (int candidate in beforeCell.Candidates)
                        {
                            if (afterCell == null || afterCell.Candidates == null || !afterCell.Candidates.Contains(candidate))
                            {
                                removedCandidates.Add(candidate);
                            }
                        }
                    }

                    if (afterCell != null && afterCell.Candidates != null)
                    {
                        foreach (int candidate in afterCell.Candidates)
                        {
                            if (beforeCell == null || beforeCell.Candidates == null || !beforeCell.Candidates.Contains(candidate))
                            {
                                addedCandidates.Add(candidate);
                            }
                        }
                    }

                    if (beforeValue == afterValue
                        && removedCandidates.Count == 0
                        && addedCandidates.Count == 0)
                    {
                        continue;
                    }

                    var change = new CellChange
                    {
                        Row = row,
                        Column = column,
                        OldValue = beforeValue,
                        NewValue = afterValue != beforeValue ? afterValue : null,
                        ClearValue = beforeValue.HasValue && !afterValue.HasValue,
                        RemovedCandidates = removedCandidates,
                        AddedCandidates = addedCandidates,
                        SourceRuleName = ruleName ?? string.Empty,
                        SourceRuleDescription = description ?? string.Empty,
                    };
                    result.Changes.Add(change);
                }
            }

            AppendUsedCells(result, usedCells);
            if (result.Changes.Count == 0)
            {
                result.Apply = false;
            }

            return result;
        }

        private static void AppendUsedCells(RuleResult result, List<UsedCell> usedCells)
        {
            if (result == null || usedCells == null)
            {
                return;
            }

            for (int i = 0; i < usedCells.Count; i++)
            {
                var used = usedCells[i];
                if (used == null)
                {
                    continue;
                }

                result.UsedCells.Add(new UsedCell
                {
                    Row = used.Row,
                    Column = used.Column,
                    Candidate = used.Candidate,
                    HighlightTag = used.HighlightTag,
                });
            }
        }
    }
}
