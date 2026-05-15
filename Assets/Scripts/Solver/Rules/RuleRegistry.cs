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
            Register(new EmptyRectangleRule());
            Register(new SkyscraperRule());
            Register(new RightAngleRule());
        }

        /**
         * Register advanced rules. Currently none are enabled.
         */
        public void RegisterAdvanced()
        {
            // Intentionally empty for now
        }

        /**
         * Apply the first applicable rule and return the pair (rule, result).
         * If no rule applies, (null, RuleResult{Applied=false}) is returned.
         */
        /**
         * Apply the first applicable rule and return the pair (rule, result).
         * If no rule applies, (null, RuleResult{Applied=false}) is returned.
         *
         * The optional `enactAll` flag determines whether the returned
         * `RuleResult` will be enacted fully (value assignments + candidate
         * removals) or only candidate removals.
         */
        public (ISudokuRule rule, RuleResult result) ApplyNext(Board board, bool enactAll = true)
        {
            // Iterate rules in order and apply the first that reports changes.
            foreach (ISudokuRule r in _rules)
            {
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

        /**
         * Repeatedly apply rules until none apply or the maximum iteration
         * count is reached. Returns the ordered list of applied (rule,result) pairs.
         */
        public List<(ISudokuRule rule, RuleResult result)> ApplyAll(Board board, int maxIterations = 1000)
        {
            var results = new List<(ISudokuRule, RuleResult)>();
            for (int i = 0; i < maxIterations; i++)
            {
                (ISudokuRule rule, RuleResult result) = ApplyNext(board);
                if (rule == null || !result.Apply) break;
                results.Add((rule, result));
            }
            return results;
        }
    }
}
