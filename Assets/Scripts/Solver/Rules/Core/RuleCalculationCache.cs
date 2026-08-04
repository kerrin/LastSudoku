using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Sudoku.Models;

namespace Sudoku.Solver.Rules
{
    /**
     * Shared per-rule cache for CalculateChanges results.
     *
     * Cache entries are keyed by rule instance and board state hash so we only
     * recalculate when the board state has changed.
     */
    public static class RuleCalculationCache
    {
        private sealed class CacheEntry
        {
            public int StateHash;
            public RuleResult Result;
        }

        private static readonly ConditionalWeakTable<ISudokuRule, CacheEntry> CacheByRule = new ConditionalWeakTable<ISudokuRule, CacheEntry>();

        /**
         * Return a cached result when available, otherwise calculate and cache it.
         *
         * @param rule Rule instance owning the cached entry.
         * @param board Board to evaluate.
         * @param calculator Callback that performs the expensive rule evaluation.
         * @returns A cloned RuleResult that callers can safely mutate.
         */
        public static RuleResult GetOrCalculate(ISudokuRule rule, Board board, Func<RuleResult> calculator)
        {
            if (rule == null)
            {
                return calculator?.Invoke() ?? new RuleResult { Apply = false };
            }

            if (board == null)
            {
                return calculator?.Invoke() ?? new RuleResult { Apply = false };
            }

            int stateHash = board.RecalculateStateHash();

            if (CacheByRule.TryGetValue(rule, out var existing) && existing != null && existing.StateHash == stateHash && existing.Result != null)
            {
                return CloneResult(existing.Result);
            }

            var computed = calculator?.Invoke() ?? new RuleResult { Apply = false };
            var stored = CloneResult(computed);

            CacheByRule.Remove(rule);
            CacheByRule.Add(rule, new CacheEntry
            {
                StateHash = stateHash,
                Result = stored
            });

            return CloneResult(stored);
        }

        /**
         * Clear cache for a specific rule instance.
         *
         * @param rule Rule instance whose cache should be removed.
         */
        public static void Clear(ISudokuRule rule)
        {
            if (rule == null) return;
            CacheByRule.Remove(rule);
        }

        private static RuleResult CloneResult(RuleResult source)
        {
            var clone = new RuleResult
            {
                Apply = source?.Apply ?? false,
                Description = source?.Description
            };

            if (source == null)
            {
                return clone;
            }

            if (source.Changes != null)
            {
                foreach (var change in source.Changes)
                {
                    if (change == null) continue;

                    clone.Changes.Add(new CellChange
                    {
                        Row = change.Row,
                        Column = change.Column,
                        OldValue = change.OldValue,
                        NewValue = change.NewValue,
                        ClearValue = change.ClearValue,
                        ForceSetValue = change.ForceSetValue,
                        ValueOnlySet = change.ValueOnlySet,
                        RemovedCandidates = change.RemovedCandidates != null ? new List<int>(change.RemovedCandidates) : new List<int>(),
                        AddedCandidates = change.AddedCandidates != null ? new List<int>(change.AddedCandidates) : new List<int>(),
                        GroupId = change.GroupId,
                        SourceRuleName = change.SourceRuleName,
                        SourceRuleDescription = change.SourceRuleDescription
                    });
                }
            }

            if (source.UsedCells != null)
            {
                foreach (var used in source.UsedCells)
                {
                    if (used == null) continue;

                    clone.UsedCells.Add(new UsedCell
                    {
                        Row = used.Row,
                        Column = used.Column,
                        Candidate = used.Candidate,
                        HighlightTag = used.HighlightTag
                    });
                }
            }

            return clone;
        }
    }
}