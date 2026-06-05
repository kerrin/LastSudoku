using System.Collections.Generic;
using Sudoku.Solver;
using Sudoku.Solver.Rules;

namespace Sudoku.Models
{
    /**
     * Stores solve-analysis metadata for a saved puzzle.
     */
    [System.Serializable]
    public class SavedPuzzleAnalysis
    {
        /** Combined solvability+difficulty label (e.g. Easy/Medium/Hard/Unsolvable). */
        public string DifficultyLabel;

        /** Ordered unique rule type names used during solving. */
        public List<string> RulesUsed = new List<string>();

        /** Number of rule applications in the solver run. */
        public int SolveSteps;
    }

    /**
     * Computes solve-analysis metadata for encoded puzzle codes.
     */
    public static class SavedPuzzleAnalysisGenerator
    {
        /**
         * Analyze a puzzle code with the current deterministic rule engine.
         *
         * @param code Puzzle code to decode and analyze.
         * @returns Analysis summary with solvability, difficulty, and rules used.
         */
        public static SavedPuzzleAnalysis AnalyzeFromCode(string code)
        {
            var analysis = new SavedPuzzleAnalysis
            {
                DifficultyLabel = "Unsolvable",
                SolveSteps = 0
            };

            if (string.IsNullOrWhiteSpace(code))
            {
                analysis.DifficultyLabel = "Unknown";
                return analysis;
            }

            var board = PuzzleCodeGenerator.DecodeBoardFromCode(code);
            if (board == null)
            {
                analysis.DifficultyLabel = "Unknown";
                return analysis;
            }

            // Always evaluate with all registered rules enabled.
            var registry = new RuleRegistry();
            registry.RegisterMinimal();
            registry.RegisterMedium();
            registry.RegisterAdvanced();
            var engine = new SolverEngine(registry);

            bool solved = engine.Solve(board, out var steps);
            analysis.SolveSteps = steps != null ? steps.Count : 0;

            Difficulty hardest = Difficulty.Easy;
            bool hasRule = false;

            if (steps != null)
            {
                for (int i = 0; i < steps.Count; i++)
                {
                    var rule = steps[i].rule;
                    if (rule == null)
                    {
                        continue;
                    }

                    string ruleName = rule.GetType().Name;
                    if (!analysis.RulesUsed.Contains(ruleName))
                    {
                        analysis.RulesUsed.Add(ruleName);
                    }

                    if (!hasRule || rule.Difficulty > hardest)
                    {
                        hardest = rule.Difficulty;
                        hasRule = true;
                    }
                }
            }

            if (solved)
            {
                analysis.DifficultyLabel = hasRule ? hardest.ToString() : "Easy";
            }
            else
            {
                analysis.DifficultyLabel = "Unsolvable";
            }

            return analysis;
        }
    }
}
