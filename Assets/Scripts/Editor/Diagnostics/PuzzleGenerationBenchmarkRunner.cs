using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sudoku.Models;
using Sudoku.Solver.Rules;
using Sudoku.Solver.Unsolver;
using UnityEditor;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Sudoku.Editor.Diagnostics
{
    /**
     * Utility for profiling Sudoku puzzle generation timings in the Unity Editor.
     */
    public static class PuzzleGenerationBenchmarkRunner
    {
        private const int DefaultIterations = 30;
        private const int DefaultSeed = 123456;

        /**
         * Run the benchmark from the Unity menu and write results to the Console.
         *
         * @param None.
         * @returns Nothing.
         */
        [MenuItem("Sudoku/Diagnostics/Benchmark Puzzle Generation")]
        public static void RunBenchmarkFromMenu()
        {
            string report = RunBenchmark(DefaultIterations, DefaultSeed);
            Debug.Log(report);
        }

        /**
         * Measure generation timings for non-symmetric and 180-degree symmetric clue layouts.
         *
         * @param iterations Number of generation attempts per mode.
         * @param seed Deterministic seed for repeatable benchmark runs.
         * @returns A formatted benchmark report.
         */
        public static string RunBenchmark(int iterations, int seed)
        {
            int safeIterations = Math.Max(1, iterations);
            var rules = BuildBenchmarkRules();

            var noneStats = MeasureMode(safeIterations, seed, rules, PuzzleClueSymmetryMode.None);
            var rotationalStats = MeasureMode(safeIterations, seed, rules, PuzzleClueSymmetryMode.Rotational180);

            var report = new StringBuilder(512);
            report.AppendLine("[PuzzleGenerationBenchmark]");
            report.AppendLine($"Iterations per mode: {safeIterations}");
            report.AppendLine($"Seed base: {seed}");
            report.AppendLine($"Rules: {string.Join(", ", rules.Select(rule => rule.Name))}");
            report.AppendLine();
            report.AppendLine(FormatStats("No symmetry", noneStats));
            report.AppendLine(FormatStats("Rotational 180", rotationalStats));

            if (noneStats.SuccessCount > 0 && rotationalStats.SuccessCount > 0)
            {
                double deltaMs = rotationalStats.AverageMilliseconds - noneStats.AverageMilliseconds;
                double deltaPercent = noneStats.AverageMilliseconds <= 0.0
                    ? 0.0
                    : (deltaMs / noneStats.AverageMilliseconds) * 100.0;
                report.AppendLine($"Delta (rotational - none): {deltaMs:F2} ms ({deltaPercent:F2}%).");
            }

            return report.ToString();
        }

        /**
         * Build the rule set used by benchmark runs.
         *
         * @param None.
         * @returns A stable list of value-placement rules.
         */
        private static List<ISudokuRule> BuildBenchmarkRules()
        {
            return new List<ISudokuRule>
            {
                new NakedSingleRule(),
                new HiddenSingleRule(),
                new RightAngleRule(),
            };
        }

        /**
         * Execute a benchmark pass for one symmetry mode.
         *
         * @param iterations Number of generation attempts.
         * @param seed Base seed for deterministic randomization.
         * @param rules Rules used by the generator.
         * @param symmetryMode Clue symmetry mode for this pass.
         * @returns Aggregate timing and success metrics.
         */
        private static BenchmarkStats MeasureMode(
            int iterations,
            int seed,
            IReadOnlyList<ISudokuRule> rules,
            PuzzleClueSymmetryMode symmetryMode)
        {
            var timings = new List<double>(iterations);
            int failures = 0;

            for (int i = 0; i < iterations; i++)
            {
                var random = new System.Random(seed + i);
                var solved = RandomSolvedBoardGenerator.GenerateRandomSolvedBoard(random);
                var generator = new PuzzleGenerator(
                    maxRetries: 50,
                    requireNonNakedContribution: true,
                    clueSymmetryMode: symmetryMode);

                var stopwatch = Stopwatch.StartNew();
                try
                {
                    _ = generator.Generate(solved, rules, random);
                    stopwatch.Stop();
                    timings.Add(stopwatch.Elapsed.TotalMilliseconds);
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    failures++;
                    Debug.LogWarning($"Benchmark generation failed for mode {symmetryMode} at iteration {i}: {ex.Message}");
                }
            }

            return BenchmarkStats.FromTimings(timings, failures);
        }

        /**
         * Format a benchmark stats row.
         *
         * @param label Display label for the measured mode.
         * @param stats Aggregate statistics to format.
         * @returns A concise single-line summary.
         */
        private static string FormatStats(string label, BenchmarkStats stats)
        {
            return $"{label}: success={stats.SuccessCount}, failures={stats.FailureCount}, avg={stats.AverageMilliseconds:F2} ms, p95={stats.P95Milliseconds:F2} ms, min={stats.MinMilliseconds:F2} ms, max={stats.MaxMilliseconds:F2} ms";
        }

        /**
         * Immutable aggregate benchmark statistics.
         */
        private readonly struct BenchmarkStats
        {
            public int SuccessCount { get; }
            public int FailureCount { get; }
            public double AverageMilliseconds { get; }
            public double P95Milliseconds { get; }
            public double MinMilliseconds { get; }
            public double MaxMilliseconds { get; }

            private BenchmarkStats(
                int successCount,
                int failureCount,
                double averageMilliseconds,
                double p95Milliseconds,
                double minMilliseconds,
                double maxMilliseconds)
            {
                SuccessCount = successCount;
                FailureCount = failureCount;
                AverageMilliseconds = averageMilliseconds;
                P95Milliseconds = p95Milliseconds;
                MinMilliseconds = minMilliseconds;
                MaxMilliseconds = maxMilliseconds;
            }

            /**
             * Compute aggregate statistics from raw timing values.
             *
             * @param timings Successful generation timings in milliseconds.
             * @param failureCount Number of failed generation attempts.
             * @returns A populated stats record.
             */
            public static BenchmarkStats FromTimings(List<double> timings, int failureCount)
            {
                if (timings == null || timings.Count == 0)
                {
                    return new BenchmarkStats(0, failureCount, 0.0, 0.0, 0.0, 0.0);
                }

                timings.Sort();
                double total = 0.0;
                for (int i = 0; i < timings.Count; i++)
                {
                    total += timings[i];
                }

                double average = total / timings.Count;
                int p95Index = Math.Clamp((int)Math.Ceiling(timings.Count * 0.95) - 1, 0, timings.Count - 1);
                return new BenchmarkStats(
                    timings.Count,
                    failureCount,
                    average,
                    timings[p95Index],
                    timings[0],
                    timings[timings.Count - 1]);
            }
        }
    }
}
