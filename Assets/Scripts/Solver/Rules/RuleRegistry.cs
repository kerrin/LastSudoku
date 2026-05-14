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
            Register(new NakedSingleRule());
            Register(new HiddenSingleRule());
            Register(new LastCellInUnitRule());
            Register(new MissingSingleRule());
            Register(new EmptyRectangleRule());
            Register(new SkyscraperRule());
            Register(new StrongLinkIntersectionRule());
            // Register the Right Angle rule (forced placements based on 3-corner pattern)
            Register(new RightAngleRule());
        }

        /**
         * Apply the first applicable rule and return the pair (rule, result).
         * If no rule applies, (null, RuleResult{Applied=false}) is returned.
         */
        public (ISudokuRule rule, RuleResult result) UpdateCandidates(Board board)
        {
            // Give each rule a chance to update the candidate sets before
            // attempting to apply any rule that may place values.
            foreach (ISudokuRule r in _rules)
            {
                try
                {
                    if (r.UpdateCandidates(board)) Debug.Log($"Rule {r.GetType().Name} updated candidates.");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"UpdateCandidates threw for {r.GetType().Name}: {ex.Message}");
                }
            }
            return (null, new RuleResult { Applied = false });
        }
        
        /**
         * Apply the first applicable rule and return the pair (rule, result).
         * If no rule applies, (null, RuleResult{Applied=false}) is returned.
         */
        public (ISudokuRule rule, RuleResult result) ApplyNext(Board board)
        {
            foreach (ISudokuRule rule in _rules)
            {
                if (rule.CanApply(board))
                {
                    Debug.Log($"Applying rule: {rule.GetType().Name}");
                    RuleResult res = rule.Apply(board);
                    if (res != null && res.Applied) return (rule, res);
                } else {
                    Debug.Log($"Rule {rule.GetType().Name} cannot apply.");
                }
            }
            return (null, new RuleResult { Applied = false });
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
                if (rule == null || !result.Applied) break;
                results.Add((rule, result));
            }
            return results;
        }
    }
}
