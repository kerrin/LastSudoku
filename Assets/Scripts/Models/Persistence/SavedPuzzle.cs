using System;
using System.Collections.Generic;

namespace Sudoku.Models
{
    /**
     * Represents a single saved puzzle entry in the user's saved puzzle list.
     * Each entry stores a compact code, a display name, and a save timestamp.
     */
    [Serializable]
    public class SavedPuzzle
    {
        /** Unique identifier for this entry (GUID without hyphens). */
        public string Id;

        /** User-visible display name shown in the saved puzzles list. */
        public string Name;

        /** Encoded puzzle code representing the board's given values. */
        public string Code;

        /** UTC DateTime stored as Ticks for stable JSON serialization. */
        public long SavedAtTicks;

        /** Combined solvability+difficulty label (e.g. Easy/Medium/Hard/Unsolvable). */
        public string DifficultyLabel;

        /** Ordered unique rule names used while solving. */
        public List<string> RulesUsed = new List<string>();

        /** Total number of rule applications used in the solve attempt. */
        public int SolveSteps;

        /**
         * Construct a saved puzzle entry with the given name and code.
         * Assigns a new unique ID and records the current UTC time.
         *
         * @param name Display name for the puzzle.
         * @param code Encoded puzzle code representing the board state.
         */
        public SavedPuzzle(string name, string code)
        {
            Id = Guid.NewGuid().ToString("N");
            Name = name ?? string.Empty;
            Code = code ?? string.Empty;
            SavedAtTicks = DateTime.UtcNow.Ticks;
            DifficultyLabel = "Unknown";
            SolveSteps = 0;
        }

        /** Parameterless constructor required for XML deserialization. */
        public SavedPuzzle() { }

        /** The UTC DateTime when this puzzle entry was created. */
        public DateTime SavedAt => new DateTime(SavedAtTicks, DateTimeKind.Utc);

        /**
         * Copy solve-analysis details into this entry.
         *
         * @param analysis Analysis result to store.
         */
        public void ApplyAnalysis(SavedPuzzleAnalysis analysis)
        {
            if (analysis == null)
            {
                return;
            }

            DifficultyLabel = string.IsNullOrWhiteSpace(analysis.DifficultyLabel) ? "Unknown" : analysis.DifficultyLabel;
            SolveSteps = analysis.SolveSteps;

            RulesUsed.Clear();
            if (analysis.RulesUsed == null)
            {
                return;
            }

            for (int i = 0; i < analysis.RulesUsed.Count; i++)
            {
                string ruleName = analysis.RulesUsed[i];
                if (!string.IsNullOrWhiteSpace(ruleName))
                {
                    RulesUsed.Add(ruleName);
                }
            }
        }
    }
}
