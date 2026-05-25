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
            Register(new LastCellInUnitRule());
            Register(new MissingSingleRule());
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
                    Debug.Log($"Skipping disabled rule: {r.GetType().Name}");
                    continue;
                }
                try
                {
                    if (!r.CanApply(board) && enactAll)
                    {
                        Debug.Log($"Rule {r.GetType().Name} cannot apply.");
                        continue;
                    }

                    Debug.Log($"Applying rule: {r.GetType().Name}");
                    RuleResult res = r.CalculateChanges(board);
                    if (res != null && res.Apply)
                    {
                        if (enactAll)
                        {
                            res.EnactAll(board);
                        }
                        else
                        {
                            res.EnactCandidates(board);
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
