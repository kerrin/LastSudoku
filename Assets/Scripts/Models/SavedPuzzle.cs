using System;

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
        }

        /** Parameterless constructor required for JSON deserialization. */
        public SavedPuzzle() { }

        /** The UTC DateTime when this puzzle entry was created. */
        public DateTime SavedAt => new DateTime(SavedAtTicks, DateTimeKind.Utc);
    }
}
