using NUnit.Framework;
using Sudoku.Solver.Rules;

namespace Sudoku.Tests.Editor
{
    public class YWingRuleTests
    {
        [Test]
        public void YWing_Canonical_RemovesCandidateFromCommonPeer()
        {
            var board = TestHelpers.CreateEmptyBoard();
            var rule = new YWingRule();

            // Clear all candidates to shape an explicit canonical pattern.
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    board.Cells[r, c].Candidates.Clear();

            // candidate pairs:
            // .  .  .|.  .  .|...
            // .(3,9).|.(2,3).|... <-- (3,9) -> 9
            // .  .  .|.  .  .|...
            // -----------
            // .  .  .|.  .  .|...
            // .(1,3).|.(1,2).|...
            // .  .  .|.  .  .|...
            // -----------
            // .  .  .|.  .  .|...
            // .  .  .|.  .  .|...
            // .  .  .|.  .  .|...
            
            // Pivot {4,4}
            board.Cells[4, 4].Candidates.Add(1);
            board.Cells[4, 4].Candidates.Add(2);

            // Pincer A {4,1}, sees pivot by row
            board.Cells[4, 1].Candidates.Add(1);
            board.Cells[4, 1].Candidates.Add(3);

            // Pincer B {1,4}, sees pivot by column
            board.Cells[1, 4].Candidates.Add(2);
            board.Cells[1, 4].Candidates.Add(3);

            // Target sees both pincers and should lose candidate 3.
            board.Cells[1, 1].Candidates.Add(3);
            board.Cells[1, 1].Candidates.Add(9);

            // This cell sees only one pincer, so it must be untouched.
            board.Cells[4, 0].Candidates.Add(3);

            var result = rule.CalculateChanges(board);
            Assert.IsTrue(result.Apply);
            result.EnactCandidates(board);

            Assert.IsFalse(board.Cells[1, 1].Candidates.Contains(3));
            Assert.IsTrue(board.Cells[1, 1].Candidates.Contains(9));
            Assert.IsTrue(board.Cells[4, 0].Candidates.Contains(3));
        }

        [Test]
        public void YWing_Canonical_DoesNotTrigger_WhenPivotIsNotBiValue()
        {
            var board = TestHelpers.CreateEmptyBoard();
            var rule = new YWingRule();

            // Clear all candidates to shape an invalid pattern.
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    board.Cells[r, c].Candidates.Clear();

            // Pivot is not bi-value: {1,2,8}
            board.Cells[4, 4].Candidates.Add(1);
            board.Cells[4, 4].Candidates.Add(2);
            board.Cells[4, 4].Candidates.Add(8);

            board.Cells[4, 1].Candidates.Add(1);
            board.Cells[4, 1].Candidates.Add(3);
            board.Cells[1, 4].Candidates.Add(2);
            board.Cells[1, 4].Candidates.Add(3);
            board.Cells[1, 1].Candidates.Add(3);

            var result = rule.CalculateChanges(board);
            Assert.IsFalse(result.Apply);
            Assert.AreEqual(0, result.Changes.Count);
        }

        [Test]
        public void YWing_Canonical_DoesNotTrigger_WithoutCommonEliminationCell()
        {
            var board = TestHelpers.CreateEmptyBoard();
            var rule = new YWingRule();

            // Clear all candidates to shape a canonical Y-Wing with no removable c.
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    board.Cells[r, c].Candidates.Clear();

            board.Cells[4, 4].Candidates.Add(1);
            board.Cells[4, 4].Candidates.Add(2);
            board.Cells[4, 1].Candidates.Add(1);
            board.Cells[4, 1].Candidates.Add(3);
            board.Cells[1, 4].Candidates.Add(2);
            board.Cells[1, 4].Candidates.Add(3);

            var result = rule.CalculateChanges(board);
            Assert.IsFalse(result.Apply);
            Assert.AreEqual(0, result.Changes.Count);
        }
    }
}
