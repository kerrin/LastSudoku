using System;
using System.Collections.Generic;
using Sudoku.Models;
using UnityEngine;

namespace Sudoku.Solver.Rules
{
    /**
     * Holds a set of available rules and provides methods to apply them
     * to a <see cref="Board"/> in sequence.
     */
    public class RuleRegistry
    {
        private readonly List<ISudokuRule> _rules = new List<ISudokuRule>();
        // Names of rules that are disabled (by type name). Using names keeps
        // the registry lightweight and avoids requiring rule implementations
        // to change.
        private readonly HashSet<string> _disabledRuleNames = new HashSet<string>();

        /**
         * Read-only view of registered rules in insertion order.
         */
        public IReadOnlyList<ISudokuRule> Rules => _rules.AsReadOnly();

        /**
         * Register a rule so it will be considered when running the solver.
         */
        public void Register(ISudokuRule rule)
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));
            _rules.Add(rule);
        }

        /** Return whether the given rule is enabled (not disabled). */
        public bool IsEnabled(ISudokuRule rule)
        {
            if (rule == null) return false;
            return !_disabledRuleNames.Contains(rule.GetType().Name);
        }

        /** Enable or disable a rule by its type name. */
        public void SetEnabled(string ruleTypeName, bool enabled)
        {
            if (string.IsNullOrEmpty(ruleTypeName)) return;
            if (enabled) _disabledRuleNames.Remove(ruleTypeName);
            else _disabledRuleNames.Add(ruleTypeName);
        }

        /** Get a snapshot of rules with their enabled state. */
        public List<(ISudokuRule rule, bool enabled)> GetRulesWithStatus()
        {
            var list = new List<(ISudokuRule, bool)>();
            foreach (var r in _rules) list.Add((r, IsEnabled(r)));
            return list;
        }

        /**
         * Helper that registers a sensible minimal set of rules.
         */
        public void RegisterDefaults()
        {
            RegisterMinimal();
            RegisterMedium();
            RegisterAdvanced();
        }

        /**
         * Register the minimal set of rules required for basic solving.
         * Tests expect this to register 4 rules.
         */
        public void RegisterMinimal()
        {
            Register(new NakedSingleRule());
            Register(new HiddenSingleRule());
            // The following rules were removed because they duplicate existing logic:
            // LastCellInUnitRule and MissingSingleRule
        }

        /**
         * Register medium-difficulty candidate elimination rules.
         * Tests expect this to register 3 rules.
         */
        public void RegisterMedium()
        {
            Register(new BoxLineRule());
            Register(new SkyscraperRule());
            Register(new RightAngleRule());
        }

        /**
         * Register advanced rules. Currently none are enabled.
         */
        public void RegisterAdvanced()
        {
            Register(new XWingRule());
            Register(new YWingRule());
        }

        /**
         * Apply the first applicable rule and return the pair (rule, result).
         * If no rule applies, (null, RuleResult{Applied=false}) is returned.
         */
        public (ISudokuRule rule, RuleResult result) ApplyNext(Board board)
        {
            return ApplyNext(board, true);
        }

        /**
         * Apply the first applicable rule and return the pair (rule, result).
         * If no rule applies, (null, RuleResult{Applied=false}) is returned.
         *
         * The optional `enactAll` flag determines whether the returned
         * `RuleResult` will be enacted fully (value assignments + candidate
         * removals) or only candidate removals.
         */
        public (ISudokuRule rule, RuleResult result) ApplyNext(Board board, bool enactAll)
        {
            // Iterate rules in order and apply the first that reports changes.
            foreach (ISudokuRule r in _rules)
            {
                // Skip disabled rules
                if (!IsEnabled(r))
                {
                    continue;
                }
                try
                {
                    if (!r.CanApply(board) && enactAll)
                    {
                        continue;
                    }

                    RuleResult res = r.CalculateChanges(board);
                    if (res != null && res.Apply)
                    {
                        // Populate OldValue for each change from the current board state
                        foreach (var ch in res.Changes)
                        {
                            try
                            {
                                var cell = board.Cells[ch.Row, ch.Column];
                                ch.OldValue = cell?.Value;
                            }
                            catch { /* ignore invalid indices */ }
                        }

                        if (enactAll)
                        {
                            res.EnactAll(board);
                        }
                        else
                        {
                            res.EnactCandidates(board);
                        }

                        // Append a deep copy of each recorded change to the board's in-memory change log
                        try
                        {
                            if (board.ChangeLog == null) board.ChangeLog = new System.Collections.Generic.List<CellChange>();

                            // If the user previously undid some actions and then a new action occurs,
                            // clear any redo-history beyond the current ChangeLogIndex so the log
                            // reflects a linear history.
                            if (board.ChangeLogIndex < board.ChangeLog.Count)
                            {
                                board.ChangeLog.RemoveRange(board.ChangeLogIndex, board.ChangeLog.Count - board.ChangeLogIndex);
                            }

                            // Assign a new group id for this atomic application of the rule
                            int gid = board.NextChangeGroupId;
                            board.NextChangeGroupId++;

                            foreach (var ch in res.Changes)
                            {
                                var copy = new CellChange
                                {
                                    Row = ch.Row,
                                    Column = ch.Column,
                                    OldValue = ch.OldValue,
                                    NewValue = ch.NewValue,
                                    ClearValue = ch.ClearValue,
                                    ForceSetValue = ch.ForceSetValue,
                                    RemovedCandidates = ch.RemovedCandidates != null ? new System.Collections.Generic.List<int>(ch.RemovedCandidates) : new System.Collections.Generic.List<int>(),
                                    AddedCandidates = ch.AddedCandidates != null ? new System.Collections.Generic.List<int>(ch.AddedCandidates) : new System.Collections.Generic.List<int>(),
                                    GroupId = gid,
                                    SourceRuleName = r.GetType().Name,
                                    SourceRuleDescription = res.Description
                                };
                                board.ChangeLog.Add(copy);
                            }

                            // Log appended changes for runtime diagnostics
                            try
                            {
                                Debug.Log($"RuleRegistry: appended {res.Changes.Count} changes as group {gid}; board.hash={board.GetHashCode()} ChangeLogCount={board.ChangeLog.Count}");
                            }
                            catch { }

                            // Move the ChangeLogIndex to the end - next redo would be after the last appended change
                            board.ChangeLogIndex = board.ChangeLog.Count;
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogWarning($"Failed to append changes to board.ChangeLog: {ex.Message}");
                        }
                        // After enacting changes, validate board consistency. If invalid,
                        // log an error and annotate the returned RuleResult so callers
                        // (e.g. SolverRunner) can display the failure in the UI.
                        try
                        {
                            bool valid = board.IsValid();
                            if (!valid)
                            {
                                string boardStr = FormatBoard(board);
                                string err = $"Board became INVALID after applying {r.GetType().Name}.";
                                Debug.LogError(err + "\n" + boardStr);
                                if (string.IsNullOrEmpty(res.Description)) res.Description = err;
                                else res.Description = res.Description + " -- " + err;
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"Board validation threw: {ex.Message}");
                        }
                        return (r, res);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"ApplyNext threw for {r.GetType().Name}: {ex.Message}");
                }
            }

            return (null, new RuleResult { Apply = false });
        }

        // Helper to produce a compact string representation of the board for logging.
        private static string FormatBoard(Board board)
        {
            var sb = new System.Text.StringBuilder();
            for (int r = 0; r < board.Size; r++)
            {
                for (int c = 0; c < board.Size; c++)
                {
                    var v = board.Cells[r, c].Value;
                    sb.Append(v.HasValue ? (char)('0' + v.Value) : '.');
                }
                if (r < board.Size - 1) sb.AppendLine();
            }
            return sb.ToString();
        }

        /**
         * Repeatedly apply rules until none apply or the maximum iteration
         * count is reached. Returns the ordered list of applied (rule,result) pairs.
         */
        public List<(ISudokuRule rule, RuleResult result)> ApplyAll(Board board, int maxIterations = 1000)
        {
            var results = new List<(ISudokuRule, RuleResult)>();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            // Safety: if ApplyAll runs for too long, bail out to avoid hanging the editor/tests.
            // This protects against pathological rule configurations or extremely large iteration counts.
            const int MaxMilliseconds = 30000; // 30s
            for (int i = 0; i < maxIterations; i++)
            {
                if (sw.ElapsedMilliseconds > MaxMilliseconds)
                {
                    Debug.LogWarning($"RuleRegistry.ApplyAll: timeout after {sw.ElapsedMilliseconds}ms and {i} iterations");
                    break;
                }
                (ISudokuRule rule, RuleResult result) = ApplyNext(board);
                if (rule == null || !result.Apply) break;
                results.Add((rule, result));
            }
            return results;
        }
    }
}
