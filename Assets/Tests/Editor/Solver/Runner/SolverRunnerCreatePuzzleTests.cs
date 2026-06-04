using NUnit.Framework;
using Sudoku.Solver;
using UnityEngine;

namespace Sudoku.Tests.Editor
{
    public class SolverRunnerCreatePuzzleTests
    {
        [Test]
        public void ExecuteManualSetValue_InPuzzleCreationMode_RejectsValueOutsideCellCandidates()
        {
            var root = new GameObject("SolverRunnerCreatePuzzleTests_RejectsNonCandidate");
            try
            {
                var runner = root.AddComponent<SolverRunner>();
                runner.CreateBlankBoard();

                var cell = runner.CurrentBoard.Cells[0, 0];
                cell.Candidates.Remove(5);

                var execution = runner.ExecuteManualSetValue(0, 0, 5);

                Assert.IsNotNull(execution);
                Assert.IsFalse(execution.Applied);
                StringAssert.Contains("not a valid candidate", execution.Description);
                Assert.IsNull(cell.Value);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ExecuteManualSetValue_InPuzzleCreationMode_DoesNotMutateOtherCellValues()
        {
            var root = new GameObject("SolverRunnerCreatePuzzleTests_DoesNotMutateOtherValues");
            try
            {
                var runner = root.AddComponent<SolverRunner>();
                runner.CreateBlankBoard();

                var untouchedCell = runner.CurrentBoard.Cells[0, 1];
                Assert.IsNull(untouchedCell.Value);

                var execution = runner.ExecuteManualSetValue(0, 0, 1);

                Assert.IsNotNull(execution);
                Assert.IsTrue(execution.Applied);
                Assert.AreEqual(1, runner.CurrentBoard.Cells[0, 0].Value);
                Assert.IsNull(untouchedCell.Value, "Candidate syncing must not assign values to other cells.");
                Assert.IsNotNull(untouchedCell.Candidates);
                Assert.Greater(untouchedCell.Candidates.Count, 0);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ValidateCurrentBoardState_NoCandidates_UsesRowColumnWords_AndTracksConflictCell()
        {
            var root = new GameObject("SolverRunnerCreatePuzzleTests_NoCandidatesMessage");
            try
            {
                var runner = root.AddComponent<SolverRunner>();
                runner.CreateBlankBoard();

                var cell = runner.CurrentBoard.Cells[0, 0];
                cell.Candidates.Clear();

                runner.ValidateCurrentBoardState();

                Assert.IsFalse(runner.LastBoardStateIsPossible);
                StringAssert.Contains("row 1, column 1", runner.LastBoardStateValidationMessage);
                StringAssert.Contains("no possible candidates", runner.LastBoardStateValidationMessage);
                Assert.IsNotNull(runner.LastBoardStateConflictCells);
                Assert.AreEqual(1, runner.LastBoardStateConflictCells.Count);
                Assert.AreEqual(0, runner.LastBoardStateConflictCells[0].Row);
                Assert.AreEqual(0, runner.LastBoardStateConflictCells[0].Column);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
