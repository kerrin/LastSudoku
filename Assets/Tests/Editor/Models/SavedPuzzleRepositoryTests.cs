using System;
using System.IO;
using NUnit.Framework;
using Sudoku.Models;

namespace Sudoku.Tests.Editor.Models
{
    /**
     * Unit tests for SavedPuzzleRepository.
     * Uses a temporary file path to avoid touching Application.persistentDataPath.
     */
    [TestFixture]
    public class SavedPuzzleRepositoryTests
    {
        private string _tempFile;

        [SetUp]
        public void SetUp()
        {
            // Redirect repository to a temp file so tests are isolated.
            _tempFile = Path.Combine(Path.GetTempPath(), $"test_saved_puzzles_{Guid.NewGuid():N}.json");
            SavedPuzzleRepository.OverrideFilePath = _tempFile;
        }

        [TearDown]
        public void TearDown()
        {
            SavedPuzzleRepository.OverrideFilePath = null;

            if (File.Exists(_tempFile))
            {
                File.Delete(_tempFile);
            }
        }

        // ------------------------------------------------------------------ //
        //  LoadAll
        // ------------------------------------------------------------------ //

        [Test]
        public void LoadAll_WhenFileDoesNotExist_ReturnsEmptyList()
        {
            var puzzles = SavedPuzzleRepository.LoadAll();

            Assert.IsNotNull(puzzles);
            Assert.AreEqual(0, puzzles.Count);
        }

        [Test]
        public void LoadAll_WhenFileIsEmpty_ReturnsEmptyList()
        {
            File.WriteAllText(_tempFile, string.Empty);

            var puzzles = SavedPuzzleRepository.LoadAll();

            Assert.IsNotNull(puzzles);
            Assert.AreEqual(0, puzzles.Count);
        }

        [Test]
        public void LoadAll_WhenFileIsCorrupt_ReturnsEmptyList()
        {
            File.WriteAllText(_tempFile, "{ not valid json {{{{");

            var puzzles = SavedPuzzleRepository.LoadAll();

            Assert.IsNotNull(puzzles);
            Assert.AreEqual(0, puzzles.Count);
        }

        // ------------------------------------------------------------------ //
        //  Add
        // ------------------------------------------------------------------ //

        [Test]
        public void Add_NullEntry_DoesNotThrowAndListRemainsEmpty()
        {
            SavedPuzzleRepository.Add(null);

            Assert.AreEqual(0, SavedPuzzleRepository.Count());
        }

        [Test]
        public void Add_SingleEntry_PersistsToFile()
        {
            var puzzle = new SavedPuzzle("My Puzzle", "D0000ABC");

            SavedPuzzleRepository.Add(puzzle);

            var loaded = SavedPuzzleRepository.LoadAll();
            Assert.AreEqual(1, loaded.Count);
            Assert.AreEqual("My Puzzle", loaded[0].Name);
            Assert.AreEqual("D0000ABC", loaded[0].Code);
        }

        [Test]
        public void Add_MultipleEntries_PreservesOrder()
        {
            SavedPuzzleRepository.Add(new SavedPuzzle("First", "code1"));
            SavedPuzzleRepository.Add(new SavedPuzzle("Second", "code2"));
            SavedPuzzleRepository.Add(new SavedPuzzle("Third", "code3"));

            var loaded = SavedPuzzleRepository.LoadAll();

            Assert.AreEqual(3, loaded.Count);
            Assert.AreEqual("First", loaded[0].Name);
            Assert.AreEqual("Second", loaded[1].Name);
            Assert.AreEqual("Third", loaded[2].Name);
        }

        // ------------------------------------------------------------------ //
        //  Delete
        // ------------------------------------------------------------------ //

        [Test]
        public void Delete_ByValidId_RemovesEntry()
        {
            var puzzle = new SavedPuzzle("ToDelete", "code");
            SavedPuzzleRepository.Add(puzzle);

            SavedPuzzleRepository.Delete(puzzle.Id);

            Assert.AreEqual(0, SavedPuzzleRepository.Count());
        }

        [Test]
        public void Delete_UnknownId_LeavesListUnchanged()
        {
            SavedPuzzleRepository.Add(new SavedPuzzle("Keep Me", "code"));

            SavedPuzzleRepository.Delete("nonexistent-id");

            Assert.AreEqual(1, SavedPuzzleRepository.Count());
        }

        [Test]
        public void Delete_NullId_LeavesListUnchanged()
        {
            SavedPuzzleRepository.Add(new SavedPuzzle("Keep Me", "code"));

            SavedPuzzleRepository.Delete(null);

            Assert.AreEqual(1, SavedPuzzleRepository.Count());
        }

        [Test]
        public void Delete_MiddleEntry_PreservesOtherEntries()
        {
            var a = new SavedPuzzle("A", "codeA");
            var b = new SavedPuzzle("B", "codeB");
            var c = new SavedPuzzle("C", "codeC");
            SavedPuzzleRepository.Add(a);
            SavedPuzzleRepository.Add(b);
            SavedPuzzleRepository.Add(c);

            SavedPuzzleRepository.Delete(b.Id);

            var loaded = SavedPuzzleRepository.LoadAll();
            Assert.AreEqual(2, loaded.Count);
            Assert.AreEqual("A", loaded[0].Name);
            Assert.AreEqual("C", loaded[1].Name);
        }

        // ------------------------------------------------------------------ //
        //  MoveUp
        // ------------------------------------------------------------------ //

        [Test]
        public void MoveUp_FirstEntry_DoesNotChangeOrder()
        {
            var a = new SavedPuzzle("A", "cA");
            var b = new SavedPuzzle("B", "cB");
            SavedPuzzleRepository.Add(a);
            SavedPuzzleRepository.Add(b);

            SavedPuzzleRepository.MoveUp(a.Id);

            var loaded = SavedPuzzleRepository.LoadAll();
            Assert.AreEqual("A", loaded[0].Name);
            Assert.AreEqual("B", loaded[1].Name);
        }

        [Test]
        public void MoveUp_SecondEntry_SwapsWithFirst()
        {
            var a = new SavedPuzzle("A", "cA");
            var b = new SavedPuzzle("B", "cB");
            SavedPuzzleRepository.Add(a);
            SavedPuzzleRepository.Add(b);

            SavedPuzzleRepository.MoveUp(b.Id);

            var loaded = SavedPuzzleRepository.LoadAll();
            Assert.AreEqual("B", loaded[0].Name);
            Assert.AreEqual("A", loaded[1].Name);
        }

        [Test]
        public void MoveUp_UnknownId_LeavesListUnchanged()
        {
            SavedPuzzleRepository.Add(new SavedPuzzle("A", "cA"));

            SavedPuzzleRepository.MoveUp("does-not-exist");

            Assert.AreEqual(1, SavedPuzzleRepository.Count());
        }

        // ------------------------------------------------------------------ //
        //  MoveDown
        // ------------------------------------------------------------------ //

        [Test]
        public void MoveDown_LastEntry_DoesNotChangeOrder()
        {
            var a = new SavedPuzzle("A", "cA");
            var b = new SavedPuzzle("B", "cB");
            SavedPuzzleRepository.Add(a);
            SavedPuzzleRepository.Add(b);

            SavedPuzzleRepository.MoveDown(b.Id);

            var loaded = SavedPuzzleRepository.LoadAll();
            Assert.AreEqual("A", loaded[0].Name);
            Assert.AreEqual("B", loaded[1].Name);
        }

        [Test]
        public void MoveDown_FirstEntry_SwapsWithSecond()
        {
            var a = new SavedPuzzle("A", "cA");
            var b = new SavedPuzzle("B", "cB");
            SavedPuzzleRepository.Add(a);
            SavedPuzzleRepository.Add(b);

            SavedPuzzleRepository.MoveDown(a.Id);

            var loaded = SavedPuzzleRepository.LoadAll();
            Assert.AreEqual("B", loaded[0].Name);
            Assert.AreEqual("A", loaded[1].Name);
        }

        // ------------------------------------------------------------------ //
        //  SaveAll / Count
        // ------------------------------------------------------------------ //

        [Test]
        public void SaveAll_NullList_WritesEmptyList()
        {
            SavedPuzzleRepository.Add(new SavedPuzzle("Existing", "code"));

            SavedPuzzleRepository.SaveAll(null);

            Assert.AreEqual(0, SavedPuzzleRepository.Count());
        }

        [Test]
        public void Count_ReflectsNumberOfSavedPuzzles()
        {
            Assert.AreEqual(0, SavedPuzzleRepository.Count());

            SavedPuzzleRepository.Add(new SavedPuzzle("A", "cA"));
            Assert.AreEqual(1, SavedPuzzleRepository.Count());

            SavedPuzzleRepository.Add(new SavedPuzzle("B", "cB"));
            Assert.AreEqual(2, SavedPuzzleRepository.Count());
        }

        // ------------------------------------------------------------------ //
        //  SavedPuzzle model
        // ------------------------------------------------------------------ //

        [Test]
        public void SavedPuzzle_Constructor_AssignsUniqueIds()
        {
            var a = new SavedPuzzle("A", "cA");
            var b = new SavedPuzzle("B", "cB");

            Assert.AreNotEqual(a.Id, b.Id);
        }

        [Test]
        public void SavedPuzzle_SavedAt_ReturnsUtcTime()
        {
            var before = DateTime.UtcNow;
            var puzzle = new SavedPuzzle("Name", "code");
            var after = DateTime.UtcNow;

            Assert.That(puzzle.SavedAt, Is.GreaterThanOrEqualTo(before));
            Assert.That(puzzle.SavedAt, Is.LessThanOrEqualTo(after));
        }

        [Test]
        public void SavedPuzzle_NullNameAndCode_StoredAsEmptyStrings()
        {
            var puzzle = new SavedPuzzle(null, null);

            Assert.AreEqual(string.Empty, puzzle.Name);
            Assert.AreEqual(string.Empty, puzzle.Code);
        }
    }
}
