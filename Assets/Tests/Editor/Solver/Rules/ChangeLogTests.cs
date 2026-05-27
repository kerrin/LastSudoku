using NUnit.Framework;
using Sudoku.Models;
using Sudoku.Solver.Rules;

namespace Sudoku.Tests.Editor
{
    public class ChangeLogTests
    {
        /**
         * Applying a rule via RuleRegistry should populate Board.ChangeLog with the enacted changes,
         * including the placed value entry with OldValue recorded.
         */
        [Test]
        public void ApplyNext_RecordsChangesInBoardChangeLog()
        {
            var board = TestHelpers.CreateEmptyBoard();

            // Setup row 1 so only column 5 can contain digit 3
            for (int c = 0; c < 9; c++)
            {
                var cell = board.Cells[1, c];
                cell.Candidates.Clear();
                if (c == 5) { cell.Candidates.UnionWith(new[] { 3 }); }
                else { cell.Candidates.UnionWith(new[] { 1, 2 }); }
            }

            var registry = new RuleRegistry();
            registry.RegisterMinimal();

            var applied = registry.ApplyNext(board);
            var res = applied.result;

            Assert.IsNotNull(res);
            Assert.IsTrue(res.Apply);

            // The board's change log should have at least one entry for the placed value
            Assert.IsNotNull(board.ChangeLog);
            Assert.IsNotEmpty(board.ChangeLog);

            var placed = board.ChangeLog.Find(ch => ch.NewValue.HasValue && ch.NewValue.Value == 3 && ch.Row == 1 && ch.Column == 5);
            Assert.IsNotNull(placed, "Expected a change record for the placed value at (1,5)");
            Assert.IsNull(placed.OldValue, "OldValue should be null for an initially empty cell");
        }

        [Test]
        public void UndoRedo_RestoresBoardState()
        {
            var board = TestHelpers.CreateEmptyBoard();

            // Setup row 1 so only column 5 can contain digit 3
            for (int c = 0; c < 9; c++)
            {
                var cell = board.Cells[1, c];
                cell.Candidates.Clear();
                if (c == 5) { cell.Candidates.UnionWith(new[] { 3 }); }
                else { cell.Candidates.UnionWith(new[] { 1, 2 }); }
            }

            var registry = new RuleRegistry();
            registry.RegisterMinimal();

            var applied = registry.ApplyNext(board);
            var res = applied.result;
            Assert.IsTrue(res.Apply);

            // Ensure the placed value exists
            Assert.AreEqual(3, board.Cells[1, 5].Value);

            // Undo the last change(s) until the placed value is reverted
            while (board.ChangeLogIndex > 0)
            {
                board.UndoLast();
            }

            Assert.IsNull(board.Cells[1,5].Value);
            // The deduced cell should have candidate 3 restored
            Assert.IsTrue(board.Cells[1,5].Candidates.Contains(3));

            // Redo all changes
            board.RedoSteps(board.ChangeLog.Count);
            Assert.AreEqual(3, board.Cells[1,5].Value);
        }

        [Test]
        public void NewActionAfterUndo_ClearsRedoHistory()
        {
            var board = TestHelpers.CreateEmptyBoard();

            var registry = new RuleRegistry();
            registry.RegisterMinimal();

            // First hidden single at (1,5) for digit 3
            for (int c = 0; c < 9; c++)
            {
                var cell = board.Cells[1, c];
                cell.Candidates.Clear();
                if (c == 5) cell.Candidates.UnionWith(new[] { 3 }); else cell.Candidates.UnionWith(new[] { 1, 2 });
            }
            var first = registry.ApplyNext(board);
            Assert.IsTrue(first.result.Apply);
            Assert.AreEqual(3, board.Cells[1,5].Value);

            // Undo the placement
            Assert.IsTrue(board.UndoLast());
            Assert.IsNull(board.Cells[1,5].Value);

            // Now create a different hidden single at (0,1) for digit 4 and apply it
            for (int c = 0; c < 9; c++)
            {
                var cell = board.Cells[0, c];
                cell.Candidates.Clear();
                if (c == 1) cell.Candidates.UnionWith(new[] { 4 }); else cell.Candidates.UnionWith(new[] { 1, 2 });
            }
            var second = registry.ApplyNext(board);
            Assert.IsTrue(second.result.Apply);
            Assert.AreEqual(4, board.Cells[0,1].Value);

            // The original change (placing 3 at 1,5) should no longer exist in the change log
            var oldEntry = board.ChangeLog.Find(ch => ch.Row == 1 && ch.Column == 5 && ch.NewValue == 3);
            Assert.IsNull(oldEntry, "Redo-history beyond the current index should be cleared when a new action is performed");

            // There should be nothing to redo now
            Assert.IsFalse(board.RedoNext());
        }
    }
}
