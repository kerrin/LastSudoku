using System.Linq;
using NUnit.Framework;
using Sudoku.Solver;
using Sudoku.Solver.Rules;

namespace Sudoku.Tests.Editor
{
    public class ManualCellEditCoreTests
    {
        [Test]
        public void ApplySetValue_RecordsSingleAtomicGroup_WithPeerCleanupAndUndoRedo()
        {
            var board = TestHelpers.CreateEmptyBoard();
            var target = board.Cells[0, 0];

            Assert.IsTrue(board.Cells[0, 1].Candidates.Contains(5));
            Assert.IsTrue(board.Cells[1, 0].Candidates.Contains(5));

            var execution = ManualCellEditCore.ApplySetValue(board, target.Row, target.Column, 5);

            Assert.IsTrue(execution.Applied);
            Assert.AreEqual(5, target.Value);
            Assert.IsEmpty(target.Candidates);
            Assert.IsFalse(board.Cells[0, 1].Candidates.Contains(5));
            Assert.IsFalse(board.Cells[1, 0].Candidates.Contains(5));

            Assert.IsNotNull(board.ChangeLog);
            Assert.Greater(board.ChangeLog.Count, 1, "SetValue should include peer-candidate cleanup records in the same group.");
            Assert.AreEqual(board.ChangeLog.Count, board.ChangeLogIndex);

            var groupIds = board.ChangeLog.Select(ch => ch.GroupId).Distinct().ToList();
            Assert.AreEqual(1, groupIds.Count);

            var valueEntry = board.ChangeLog.SingleOrDefault(ch => ch.Row == 0 && ch.Column == 0 && ch.NewValue == 5);
            Assert.IsNotNull(valueEntry);
            Assert.AreEqual("ManualSetValue", valueEntry.SourceRuleName);

            Assert.IsTrue(board.UndoLast());
            Assert.IsNull(target.Value);
            Assert.IsTrue(board.Cells[0, 1].Candidates.Contains(5));
            Assert.IsTrue(board.Cells[1, 0].Candidates.Contains(5));

            Assert.IsTrue(board.RedoNext());
            Assert.AreEqual(5, target.Value);
            Assert.IsFalse(board.Cells[0, 1].Candidates.Contains(5));
            Assert.IsFalse(board.Cells[1, 0].Candidates.Contains(5));
        }

        [Test]
        public void ApplySetValue_WithPeerConflict_StillSetsValueAndClearsCandidates()
        {
            var board = TestHelpers.CreateEmptyBoard();
            board.Cells[0, 1].Value = 5;
            board.Cells[0, 1].Candidates.Clear();
            var target = board.Cells[0, 0];

            var execution = ManualCellEditCore.ApplySetValue(board, target.Row, target.Column, 5);

            Assert.IsTrue(execution.Applied);
            Assert.AreEqual(5, target.Value);
            Assert.IsEmpty(target.Candidates);
        }

        [Test]
        public void ApplySetValue_WhenCellAlreadyHasDifferentValue_ReplacesValue()
        {
            var board = TestHelpers.CreateEmptyBoard();
            var target = board.Cells[0, 0];
            board.SetValue(target, 4);

            Assert.IsFalse(board.Cells[0, 1].Candidates.Contains(4));
            Assert.IsTrue(board.Cells[0, 1].Candidates.Contains(6));

            var execution = ManualCellEditCore.ApplySetValue(board, target.Row, target.Column, 6);

            Assert.IsTrue(execution.Applied);
            Assert.AreEqual(6, target.Value);
            Assert.IsEmpty(target.Candidates);
            Assert.IsTrue(board.Cells[0, 1].Candidates.Contains(4), "Old value should be restored to eligible peers.");
            Assert.IsFalse(board.Cells[0, 1].Candidates.Contains(6), "New value should be removed from peers.");
        }

        [Test]
        public void ApplyAddCandidate_RecordsChangeAndUndoRedo()
        {
            var board = TestHelpers.CreateEmptyBoard();
            var cell = board.Cells[2, 2];
            cell.Candidates.Remove(7);

            var execution = ManualCellEditCore.ApplyAddCandidate(board, 2, 2, 7);

            Assert.IsTrue(execution.Applied);
            Assert.IsTrue(cell.Candidates.Contains(7));
            Assert.AreEqual(1, board.ChangeLog.Count);

            var change = board.ChangeLog[0];
            Assert.IsTrue(change.AddedCandidates.Contains(7));
            Assert.IsEmpty(change.RemovedCandidates);

            Assert.IsTrue(board.UndoLast());
            Assert.IsFalse(cell.Candidates.Contains(7));

            Assert.IsTrue(board.RedoNext());
            Assert.IsTrue(cell.Candidates.Contains(7));
        }

        [Test]
        public void ApplyAddCandidate_OnSolvedCell_ClearsValueAndAddsCandidate()
        {
            var board = TestHelpers.CreateEmptyBoard();
            var cell = board.Cells[1, 1];
            board.SetValue(cell, 4);

            var execution = ManualCellEditCore.ApplyAddCandidate(board, 1, 1, 4);

            Assert.IsTrue(execution.Applied);
            Assert.IsNull(cell.Value);
            Assert.IsTrue(cell.Candidates.Contains(4));
            Assert.AreEqual(1, board.ChangeLog.Count);
            Assert.IsTrue(board.ChangeLog[0].ClearValue);
            Assert.IsTrue(board.ChangeLog[0].AddedCandidates.Contains(4));

            Assert.IsTrue(board.UndoLast());
            Assert.AreEqual(4, cell.Value);

            Assert.IsTrue(board.RedoNext());
            Assert.IsNull(cell.Value);
            Assert.IsTrue(cell.Candidates.Contains(4));
        }

        [Test]
        public void ApplyUnitCandidateAction_RemovesAndAddsAcrossRowColumnAndBox()
        {
            var board = TestHelpers.CreateEmptyBoard();
            var anchor = board.Cells[4, 4];

            var removal = ManualCellEditCore.ApplyUnitCandidateAction(board, anchor.Row, anchor.Column, 3, addToUnsolvedCells: false);
            Assert.IsTrue(removal.Applied);
            Assert.IsFalse(board.Cells[4, 0].Candidates.Contains(3));
            Assert.IsFalse(board.Cells[0, 4].Candidates.Contains(3));
            Assert.IsFalse(board.Cells[3, 3].Candidates.Contains(3));

            var add = ManualCellEditCore.ApplyUnitCandidateAction(board, anchor.Row, anchor.Column, 3, addToUnsolvedCells: true);
            Assert.IsTrue(add.Applied);
            Assert.IsTrue(board.Cells[4, 0].Candidates.Contains(3));
            Assert.IsTrue(board.Cells[0, 4].Candidates.Contains(3));
            Assert.IsTrue(board.Cells[3, 3].Candidates.Contains(3));
        }

        [Test]
        public void ApplyUnitCandidateAction_AllowsGivenAnchorCell()
        {
            var board = TestHelpers.CreateEmptyBoard();
            var anchor = board.Cells[4, 4];
            anchor.IsGiven = true;
            anchor.Value = 7;
            anchor.Candidates.Clear();

            var removal = ManualCellEditCore.ApplyUnitCandidateAction(board, anchor.Row, anchor.Column, 3, addToUnsolvedCells: false);

            Assert.IsTrue(removal.Applied);
            Assert.IsFalse(board.Cells[4, 0].Candidates.Contains(3));
        }

        [Test]
        public void ApplyRemoveCandidate_RecordsChangeAndUndoRedo()
        {
            var board = TestHelpers.CreateEmptyBoard();
            var cell = board.Cells[3, 3];

            Assert.IsTrue(cell.Candidates.Contains(4));

            var execution = ManualCellEditCore.ApplyRemoveCandidate(board, 3, 3, 4);

            Assert.IsTrue(execution.Applied);
            Assert.IsFalse(cell.Candidates.Contains(4));
            Assert.AreEqual(1, board.ChangeLog.Count);
            Assert.IsTrue(board.ChangeLog[0].RemovedCandidates.Contains(4));

            Assert.IsTrue(board.UndoLast());
            Assert.IsTrue(cell.Candidates.Contains(4));

            Assert.IsTrue(board.RedoNext());
            Assert.IsFalse(cell.Candidates.Contains(4));
        }

        [Test]
        public void ResolveSmartAction_ReturnsNoOpSkeleton()
        {
            var board = TestHelpers.CreateEmptyBoard();

            var resolution = ManualCellEditCore.ResolveSmartAction(board, 1, 1);

            Assert.IsNotNull(resolution);
            Assert.IsFalse(resolution.HasAction);
            Assert.AreEqual(ManualCellEditOperation.None, resolution.Operation);
            Assert.IsNull(resolution.Digit);
            Assert.IsNotEmpty(resolution.Description);
        }
    }
}
