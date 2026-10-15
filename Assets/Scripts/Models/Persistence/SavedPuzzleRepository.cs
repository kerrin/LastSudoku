using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using UnityEngine;

namespace Sudoku.Models
{
    /**
     * Manages persistence of the saved puzzle list.
     * Puzzles are serialized as XML. On Windows builds and in the editor, the
     * file is stored under My Documents/My Games/Last Sudoku/SavedPuzzles.xml.
     * All mutating operations immediately flush to disk.
     */
    public static class SavedPuzzleRepository
    {
        private const string FileName = "SavedPuzzles.xml";
        private static readonly XmlSerializer PuzzleListSerializer = new XmlSerializer(typeof(PuzzleListData));

        /**
         * Optional path override used by unit tests to avoid touching
         * Application.persistentDataPath during test runs.
         */
        public static string OverrideFilePath { get; set; } = null;

        private static string FilePath =>
            OverrideFilePath ?? GetDefaultFilePath();

        private static string GetDefaultFilePath()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(documentsPath, "My Games", "Last Sudoku", FileName);
#else
            return Path.Combine(Application.persistentDataPath, FileName);
#endif
        }

        // XmlSerializer requires a concrete root type, so we wrap the list.
        [Serializable]
        [XmlRoot("SavedPuzzleList")]
        public class PuzzleListData
        {
            [XmlArray("Puzzles")]
            [XmlArrayItem("Puzzle")]
            public List<SavedPuzzle> Puzzles = new List<SavedPuzzle>();
        }

        /**
         * Load all saved puzzles from disk in their stored order.
         * Returns an empty list when no file exists or the file is corrupt.
         *
         * @returns All saved puzzle entries.
         */
        public static List<SavedPuzzle> LoadAll()
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    return new List<SavedPuzzle>();
                }

                string xml = File.ReadAllText(FilePath);
                if (string.IsNullOrWhiteSpace(xml))
                {
                    return new List<SavedPuzzle>();
                }

                using var stream = File.OpenRead(FilePath);
                var data = PuzzleListSerializer.Deserialize(stream) as PuzzleListData;
                return data?.Puzzles ?? new List<SavedPuzzle>();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"SavedPuzzleRepository: Failed to load puzzles. {ex.Message}");
                return new List<SavedPuzzle>();
            }
        }

        /**
         * Derive the default display name for a saved puzzle at the provided slot index.
         *
         * @param index One-based slot index in the saved list.
         * @returns The default name for the entry.
         */
        public static string GetDefaultName(int index)
        {
            int safeIndex = Math.Max(index, 1);
            return $"Puzzle {safeIndex}";
        }

        /**
         * Normalize a raw user-entered name into a non-empty list-friendly display label.
         *
         * @param rawName Raw value entered by the user.
         * @param fallbackIndex One-based index used if the value is blank.
         * @returns A displayable name.
         */
        public static string SanitizeDisplayName(string rawName, int fallbackIndex)
        {
            if (string.IsNullOrWhiteSpace(rawName))
            {
                return GetDefaultName(fallbackIndex);
            }

            string trimmed = rawName.Trim();
            return string.IsNullOrEmpty(trimmed)
                ? GetDefaultName(fallbackIndex)
                : trimmed;
        }

        /**
         * Update an existing puzzle's display name and persist the change.
         *
         * @param id Unique puzzle identifier.
         * @param newName New display name to store.
         * @returns True when a matching puzzle was renamed; otherwise false.
         */
        public static bool UpdateName(string id, string newName)
        {
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }

            var puzzles = LoadAll();
            bool changed = false;

            for (int i = 0; i < puzzles.Count; i++)
            {
                var puzzle = puzzles[i];
                if (puzzle == null || puzzle.Id != id)
                {
                    continue;
                }

                puzzle.Name = string.IsNullOrWhiteSpace(newName) ? string.Empty : newName.Trim();
                changed = true;
                break;
            }

            if (changed)
            {
                SaveAll(puzzles);
            }

            return changed;
        }

        /**
         * Append a new puzzle entry to the end of the saved list and flush to disk.
         *
         * @param puzzle The puzzle entry to add. Null entries are ignored.
         */
        public static void Add(SavedPuzzle puzzle)
        {
            if (puzzle == null)
            {
                return;
            }

            var puzzles = LoadAll();
            puzzles.Add(puzzle);
            SaveAll(puzzles);
        }

        /**
         * Remove the saved puzzle with the given unique ID and flush to disk.
         *
         * @param id The unique ID of the puzzle to delete.
         */
        public static void Delete(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return;
            }

            var puzzles = LoadAll();
            int removed = puzzles.RemoveAll(p => p?.Id == id);

            if (removed > 0)
            {
                SaveAll(puzzles);
            }
        }

        /**
         * Move the puzzle with the given ID one position earlier in the list.
         * Does nothing when the puzzle is already first or is not found.
         *
         * @param id The unique ID of the puzzle to move up.
         */
        public static void MoveUp(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return;
            }

            var puzzles = LoadAll();
            int index = puzzles.FindIndex(p => p?.Id == id);
            if (index <= 0)
            {
                return;
            }

            (puzzles[index - 1], puzzles[index]) = (puzzles[index], puzzles[index - 1]);
            SaveAll(puzzles);
        }

        /**
         * Move the puzzle with the given ID one position later in the list.
         * Does nothing when the puzzle is already last or is not found.
         *
         * @param id The unique ID of the puzzle to move down.
         */
        public static void MoveDown(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return;
            }

            var puzzles = LoadAll();
            int index = puzzles.FindIndex(p => p?.Id == id);
            if (index < 0 || index >= puzzles.Count - 1)
            {
                return;
            }

            (puzzles[index], puzzles[index + 1]) = (puzzles[index + 1], puzzles[index]);
            SaveAll(puzzles);
        }

        /**
         * Overwrite the entire saved puzzle list with the provided collection and flush to disk.
         *
         * @param puzzles The replacement puzzle list. A null value is treated as empty.
         */
        public static void SaveAll(List<SavedPuzzle> puzzles)
        {
            try
            {
                var data = new PuzzleListData
                {
                    Puzzles = puzzles ?? new List<SavedPuzzle>()
                };

                string directory = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using var stream = File.Create(FilePath);
                PuzzleListSerializer.Serialize(stream, data);
            }
            catch (Exception ex)
            {
                Debug.LogError($"SavedPuzzleRepository: Failed to save puzzles. {ex.Message}");
            }
        }

        /**
         * Return the number of saved puzzle entries without loading full data.
         *
         * @returns The count of saved puzzles.
         */
        public static int Count()
        {
            return LoadAll().Count;
        }
    }
}
