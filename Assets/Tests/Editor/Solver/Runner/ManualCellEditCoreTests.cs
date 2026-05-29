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
