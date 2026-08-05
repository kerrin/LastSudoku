using NUnit.Framework;
using Sudoku.Solver.Rules;

namespace Sudoku.Tests.Editor
{
    public class WWingRuleTests
    {
        [Test]
        public void WWing_Canonical_RemovesOtherCandidateFromCommonPeer()
        {
            var board = TestHelpers.CreateEmptyBoard();
            var rule = new WWingRule();

            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    board.Cells[r, c].Candidates.Clear();

            // Matching bi-value pincers {1,2} that do not see each other.
            board.Cells[0, 0].Candidates.Add(1);
            board.Cells[0, 0].Candidates.Add(2);
            board.Cells[3, 3].Candidates.Add(1);
            board.Cells[3, 3].Candidates.Add(2);

            // Strong link for digit 1 in row 1: exactly two positions.
            board.Cells[1, 0].Candidates.Add(1);
            board.Cells[1, 0].Candidates.Add(7);
            board.Cells[1, 3].Candidates.Add(1);
            board.Cells[1, 3].Candidates.Add(8);

            // Target sees both pincers and should lose candidate 2.
            board.Cells[0, 3].Candidates.Add(2);
            board.Cells[0, 3].Candidates.Add(9);

            // Sees only one pincer, so must remain unchanged.
            board.Cells[0, 4].Candidates.Add(2);

            var result = rule.CalculateChanges(board);
            Assert.IsTrue(result.Apply);

            result.EnactCandidates(board);
            Assert.IsFalse(board.Cells[0, 3].Candidates.Contains(2));
            Assert.IsTrue(board.Cells[0, 3].Candidates.Contains(9));
            Assert.IsTrue(board.Cells[0, 4].Candidates.Contains(2));
        }

        [Test]
        public void WWing_DoesNotTrigger_WhenPincersSeeEachOther()
        {
            var board = TestHelpers.CreateEmptyBoard();
            var rule = new WWingRule();

            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    board.Cells[r, c].Candidates.Clear();

            // Pincers are in the same row, so W-Wing precondition fails.
            board.Cells[0, 0].Candidates.Add(1);
            board.Cells[0, 0].Candidates.Add(2);
            board.Cells[0, 6].Candidates.Add(1);
            board.Cells[0, 6].Candidates.Add(2);

            // Strong link exists, but should not matter because pincers see each other.
            board.Cells[1, 0].Candidates.Add(1);
            board.Cells[1, 0].Candidates.Add(7);
            board.Cells[1, 6].Candidates.Add(1);
            board.Cells[1, 6].Candidates.Add(8);

            board.Cells[0, 3].Candidates.Add(2);

            var result = rule.CalculateChanges(board);
            Assert.IsFalse(result.Apply);
            Assert.AreEqual(0, result.Changes.Count);
        }

        [Test]
        public void WWing_DoesNotTrigger_WithoutStrongLink()
        {
            var board = TestHelpers.CreateEmptyBoard();
            var rule = new WWingRule();

            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    board.Cells[r, c].Candidates.Clear();

            // Matching bi-value pincers {1,2} that do not see each other.
            board.Cells[0, 0].Candidates.Add(1);
            board.Cells[0, 0].Candidates.Add(2);
            board.Cells[3, 3].Candidates.Add(1);
            board.Cells[3, 3].Candidates.Add(2);

            // Three candidate-1 cells in row 1, so no strong link on digit 1 there.
            board.Cells[1, 0].Candidates.Add(1);
            board.Cells[1, 0].Candidates.Add(7);
            board.Cells[1, 3].Candidates.Add(1);
            board.Cells[1, 3].Candidates.Add(8);
            board.Cells[1, 8].Candidates.Add(1);
            board.Cells[1, 8].Candidates.Add(5);

            board.Cells[0, 3].Candidates.Add(2);

            var result = rule.CalculateChanges(board);
            Assert.IsFalse(result.Apply);
            Assert.AreEqual(0, result.Changes.Count);
        }
    }
}
