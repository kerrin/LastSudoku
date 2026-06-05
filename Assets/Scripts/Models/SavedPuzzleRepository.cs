using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Sudoku.Models
{
    /**
     * Manages persistence of the saved puzzle list.
     * Puzzles are serialized as JSON and stored in the application's persistent
     * data directory. All mutating operations immediately flush to disk.
     */
    public static class SavedPuzzleRepository
    {
        private const string FileName = "saved_puzzles.json";

        /**
         * Optional path override used by unit tests to avoid touching
         * Application.persistentDataPath during test runs.
         */
        public static string OverrideFilePath { get; set; } = null;

        private static string FilePath =>
            OverrideFilePath ?? Path.Combine(Application.persistentDataPath, FileName);

        // Unity's JsonUtility cannot serialize a List<T> at the root level,
        // so we wrap the list in a container class.
        [Serializable]
        private class PuzzleListData
        {
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

                string json = File.ReadAllText(FilePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new List<SavedPuzzle>();
                }

                var data = JsonUtility.FromJson<PuzzleListData>(json);
                return data?.Puzzles ?? new List<SavedPuzzle>();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"SavedPuzzleRepository: Failed to load puzzles. {ex.Message}");
                return new List<SavedPuzzle>();
            }
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

                string json = JsonUtility.ToJson(data, prettyPrint: false);
                File.WriteAllText(FilePath, json);
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
