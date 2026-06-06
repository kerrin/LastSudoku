using NUnit.Framework;
using Sudoku.Solver;
using Sudoku.UI.Config;
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

        [Test]
        public void ExecuteManualSetValue_InPuzzleMode_AutoCandidateDisabled_LeavesPeerCandidatesUnchanged()
        {
            var root = new GameObject("SolverRunnerCreatePuzzleTests_AutoCandidateOff");
            bool original = AssistanceSettings.AutoCandidateOnSetValue;

            try
            {
                AssistanceSettings.AutoCandidateOnSetValue = false;

                var runner = root.AddComponent<SolverRunner>();
                runner.CreateBlankBoard();
                runner.SetInteractionMode(BoardInteractionMode.Puzzle);

                var peer = runner.CurrentBoard.Cells[0, 1];
                Assert.IsTrue(peer.Candidates.Contains(5));

                var execution = runner.ExecuteManualSetValue(0, 0, 5);

                Assert.IsNotNull(execution);
                Assert.IsTrue(execution.Applied);
                Assert.AreEqual(1, execution.RuleResult.Changes.Count, "Value-only set should not emit peer candidate-removal changes.");
                Assert.IsTrue(execution.RuleResult.Changes[0].ValueOnlySet, "Value-only set should be marked to suppress implied peer-removal highlights.");
                Assert.AreEqual(5, runner.CurrentBoard.Cells[0, 0].Value);
                Assert.IsTrue(peer.Candidates.Contains(5), "Peer candidates should remain unchanged when auto-candidate is disabled.");
            }
            finally
            {
                AssistanceSettings.AutoCandidateOnSetValue = original;
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ExecuteManualSetValue_InPuzzleMode_AutoCandidateEnabled_RemovesPeerCandidate()
        {
            var root = new GameObject("SolverRunnerCreatePuzzleTests_AutoCandidateOn");
            bool original = AssistanceSettings.AutoCandidateOnSetValue;

            try
            {
                AssistanceSettings.AutoCandidateOnSetValue = true;

                var runner = root.AddComponent<SolverRunner>();
                runner.CreateBlankBoard();
                runner.SetInteractionMode(BoardInteractionMode.Puzzle);

                var peer = runner.CurrentBoard.Cells[0, 1];
                Assert.IsTrue(peer.Candidates.Contains(5));

                var execution = runner.ExecuteManualSetValue(0, 0, 5);

                Assert.IsNotNull(execution);
                Assert.IsTrue(execution.Applied);
                Assert.AreEqual(5, runner.CurrentBoard.Cells[0, 0].Value);
                Assert.IsFalse(peer.Candidates.Contains(5), "Peer candidates should be cleaned up when auto-candidate is enabled.");
            }
            finally
            {
                AssistanceSettings.AutoCandidateOnSetValue = original;
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ExecuteManualSetValue_InCreationMode_AutoCandidateDisabled_StillClearsPeerCandidates()
        {
            var root = new GameObject("SolverRunnerCreatePuzzleTests_CreateModeAutoCandidateOff");
            bool original = AssistanceSettings.AutoCandidateOnSetValue;

            try
            {
                AssistanceSettings.AutoCandidateOnSetValue = false;

                var runner = root.AddComponent<SolverRunner>();
                runner.CreateBlankBoard();

                var peer = runner.CurrentBoard.Cells[0, 1];
                Assert.IsTrue(peer.Candidates.Contains(5));

                var execution = runner.ExecuteManualSetValue(0, 0, 5);

                Assert.IsNotNull(execution);
                Assert.IsTrue(execution.Applied);
                Assert.AreEqual(5, runner.CurrentBoard.Cells[0, 0].Value);
                Assert.IsFalse(peer.Candidates.Contains(5), "Creation mode candidate sync should be unaffected by the puzzle-mode auto-candidate toggle.");
            }
            finally
            {
                AssistanceSettings.AutoCandidateOnSetValue = original;
                Object.DestroyImmediate(root);
            }
        }
    }
}
