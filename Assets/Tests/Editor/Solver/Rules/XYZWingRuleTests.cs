using NUnit.Framework;
using Sudoku.Solver.Rules;

namespace Sudoku.Tests.Editor
{
    public class XYZWingRuleTests
    {
        [Test]
        public void XYZWing_Canonical_RemovesCandidateFromCommonPeer()
        {
            var board = TestHelpers.CreateEmptyBoard();
            var rule = new XYZWingRule();

            // Clear all candidates to shape an explicit canonical XYZ-Wing pattern.
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    board.Cells[r, c].Candidates.Clear();

            // Pivot {1,2,3}
            board.Cells[4, 4].Candidates.Add(1);
            board.Cells[4, 4].Candidates.Add(2);
            board.Cells[4, 4].Candidates.Add(3);

            // Wing A {1,3}, sees pivot by row
            board.Cells[4, 1].Candidates.Add(1);
            board.Cells[4, 1].Candidates.Add(3);

            // Wing B {2,3}, sees pivot by row
            board.Cells[4, 7].Candidates.Add(2);
            board.Cells[4, 7].Candidates.Add(3);

            // Target sees both wings and the pivot, so it should lose candidate 3.
            board.Cells[4, 5].Candidates.Add(3);
            board.Cells[4, 5].Candidates.Add(9);

            // This cell sees only one wing, so it must be untouched.
            board.Cells[3, 1].Candidates.Add(3);

            var result = rule.CalculateChanges(board);
            Assert.IsTrue(result.Apply);
            result.EnactCandidates(board);

            Assert.IsFalse(board.Cells[4, 5].Candidates.Contains(3));
            Assert.IsTrue(board.Cells[4, 5].Candidates.Contains(9));
            Assert.IsTrue(board.Cells[3, 1].Candidates.Contains(3));
        }

        [Test]
        public void XYZWing_Canonical_DoesNotTrigger_WhenPivotIsNotTriValue()
        {
            var board = TestHelpers.CreateEmptyBoard();
            var rule = new XYZWingRule();

            // Clear all candidates to shape an invalid pattern.
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    board.Cells[r, c].Candidates.Clear();

            // Pivot is not tri-value: {1,2}
            board.Cells[4, 4].Candidates.Add(1);
            board.Cells[4, 4].Candidates.Add(2);

            board.Cells[4, 1].Candidates.Add(1);
            board.Cells[4, 1].Candidates.Add(3);
            board.Cells[4, 7].Candidates.Add(2);
            board.Cells[4, 7].Candidates.Add(3);
            board.Cells[4, 5].Candidates.Add(3);

            var result = rule.CalculateChanges(board);
            Assert.IsFalse(result.Apply);
            Assert.AreEqual(0, result.Changes.Count);
        }

        [Test]
        public void XYZWing_Canonical_DoesNotTrigger_WithoutCommonEliminationCell()
        {
            var board = TestHelpers.CreateEmptyBoard();
            var rule = new XYZWingRule();

            // Clear all candidates to shape a canonical XYZ-Wing with no removable c.
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    board.Cells[r, c].Candidates.Clear();

            board.Cells[4, 4].Candidates.Add(1);
            board.Cells[4, 4].Candidates.Add(2);
            board.Cells[4, 4].Candidates.Add(3);
            board.Cells[4, 1].Candidates.Add(1);
            board.Cells[4, 1].Candidates.Add(3);
            board.Cells[4, 7].Candidates.Add(2);
            board.Cells[4, 7].Candidates.Add(3);

            var result = rule.CalculateChanges(board);
            Assert.IsFalse(result.Apply);
            Assert.AreEqual(0, result.Changes.Count);
        }

        [Test]
        public void XYZWing_Canonical_DoesNotTrigger_WhenWingsDoNotShareCommonPeer()
        {
            var board = TestHelpers.CreateEmptyBoard();
            var rule = new XYZWingRule();

            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    board.Cells[r, c].Candidates.Clear();

            // Pivot {1,2,3}
            board.Cells[4, 4].Candidates.Add(1);
            board.Cells[4, 4].Candidates.Add(2);
            board.Cells[4, 4].Candidates.Add(3);

            // Wing A {1,3}
            board.Cells[4, 1].Candidates.Add(1);
            board.Cells[4, 1].Candidates.Add(3);

            // Wing B {2,3}
            board.Cells[1, 4].Candidates.Add(2);
            board.Cells[1, 4].Candidates.Add(3);

            // This cell sees both wings, but not the pivot, so the rule should not apply.
            board.Cells[1, 1].Candidates.Add(3);

            var result = rule.CalculateChanges(board);
            Assert.IsFalse(result.Apply);
            Assert.AreEqual(0, result.Changes.Count);
        }

        [Test]
        public void XYZWing_Canonical_DoesNotTrigger_WhenOneWingDoesNotSeePivot()
        {
            var board = TestHelpers.CreateEmptyBoard();
            var rule = new XYZWingRule();

            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    board.Cells[r, c].Candidates.Clear();

            // Pivot {1,2,3}
            board.Cells[4, 4].Candidates.Add(1);
            board.Cells[4, 4].Candidates.Add(2);
            board.Cells[4, 4].Candidates.Add(3);

            // Wing A sees pivot by row
            board.Cells[4, 1].Candidates.Add(1);
            board.Cells[4, 1].Candidates.Add(3);

            // Wing B does not see pivot, so the canonical pattern should not form.
            board.Cells[0, 4].Candidates.Add(2);
            board.Cells[0, 4].Candidates.Add(3);

            board.Cells[1, 1].Candidates.Add(3);

            var result = rule.CalculateChanges(board);
            Assert.IsFalse(result.Apply);
            Assert.AreEqual(0, result.Changes.Count);
        }
    }
}
