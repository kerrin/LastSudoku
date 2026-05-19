using NUnit.Framework;
using Sudoku.Models;
using Sudoku.Solver.Rules;

namespace Sudoku.Tests.Editor
{
    public class YWingRuleTests
    {
        [Test]
        public void YWing_Rectangle_PlacesDigitInFourthCorner()
        {
            var board = TestHelpers.CreateEmptyBoard();
            var registry = new RuleRegistry();
            registry.Register(new YWingRule());

            // Clear all candidates to shape exact pairs
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    board.Cells[r, c].Candidates.Clear();

            // candidate pairs:
            // (1,2)..|...|..(2,3)
            //   .  ..|...|..  .
            //   .  ..|...|..  .
            // -----------
            //   .  ..|...|..  .
            //   .  ..|...|..  .
            //   .  ..|...|..  .
            // -----------
            //   .  ..|...|..  .
            //   .  ..|...|..  .
            // (1,3)..|...|..  .<-value will be 3

            // Rectangle at rows 0-1, cols 0-1
            // Top-left: {1,2}
            board.Cells[0, 0].Candidates.Add(1);
            board.Cells[0, 0].Candidates.Add(2);
            // Top-right: {2,3}
            board.Cells[0, 8].Candidates.Add(2);
            board.Cells[0, 8].Candidates.Add(3);
            // Bottom-left: {1,3}
            board.Cells[8, 0].Candidates.Add(1);
            board.Cells[8, 0].Candidates.Add(3);
            // Bottom-right: allow {1,2,3,4}
            board.Cells[8, 8].Candidates.Add(1);
            board.Cells[8, 8].Candidates.Add(2);
            board.Cells[8, 8].Candidates.Add(3);
            board.Cells[8, 8].Candidates.Add(4);
            // other candidates to not be altered
            // same cells
            board.Cells[0, 0].Candidates.Add(4);
            board.Cells[0, 8].Candidates.Add(4);
            board.Cells[8, 0].Candidates.Add(4);
            // different cells
            board.Cells[0, 1].Candidates.Add(5);
            board.Cells[1, 0].Candidates.Add(5);

            var (rule, result) = registry.ApplyNext(board);
            Assert.IsNotNull(rule);
            Assert.IsTrue(result.Apply);
            // YWing pattern should place digit 3 into (8,8)
            Assert.AreEqual(3, board.Cells[8, 8].Value);
            Assert.IsEmpty(board.Cells[8, 8].Candidates);
            // check other candidates in the same cell are unaffected
            Assert.IsTrue(board.Cells[0, 0].Candidates.Contains(4));
            Assert.IsTrue(board.Cells[0, 8].Candidates.Contains(4));
            Assert.IsTrue(board.Cells[8, 0].Candidates.Contains(4));
            // check candidates in different cells are unaffected
            Assert.IsTrue(board.Cells[0, 1].Candidates.Contains(5));
            Assert.IsTrue(board.Cells[1, 0].Candidates.Contains(5));
        }

        [Test]
        public void YWing_Rectangle_NotValid_DigitInAllThree()
        {
            var board = TestHelpers.CreateEmptyBoard();
            var registry = new RuleRegistry();
            registry.Register(new YWingRule());

            // Clear all candidates to shape exact pairs
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    board.Cells[r, c].Candidates.Clear();

            // candidate pairs:
            // (1,2,3)..|...|..(2,3)
            //    .   ..|...|..  .
            //    .   ..|...|..  .
            // -----------
            //    .   ..|...|..  .
            //    .   ..|...|..  .
            //    .   ..|...|..  .
            // -----------
            //    .   ..|...|..  .
            //    .   ..|...|..  .
            //  (1,3) ..|...|..  .<-value will be 3

            // Rectangle at rows 0-1, cols 0-1
            // Top-left: {1,2,3}
            board.Cells[0, 0].Candidates.Add(1);
            board.Cells[0, 0].Candidates.Add(2);
            board.Cells[0, 0].Candidates.Add(3);
            // Top-right: {2,3}
            board.Cells[0, 8].Candidates.Add(2);
            board.Cells[0, 8].Candidates.Add(3);
            // Bottom-left: {1,3}
            board.Cells[8, 0].Candidates.Add(1);
            board.Cells[8, 0].Candidates.Add(3);
            // Bottom-right: allow {1,2,3,4}
            board.Cells[8, 8].Candidates.Add(1);
            board.Cells[8, 8].Candidates.Add(2);
            board.Cells[8, 8].Candidates.Add(3);
            board.Cells[8, 8].Candidates.Add(4);

            // 3 appears in all 3, so not a valid y-wing pattern
            var (rule, result) = registry.ApplyNext(board);
            // solver should not detect a Y-Wing when the same digit appears in all three pivot cells
            Assert.IsNull(rule);
        }

        [Test]
        public void YWing_Rectangle_NotValid_PairsNotUnique()
        {
            var board = TestHelpers.CreateEmptyBoard();
            var registry = new RuleRegistry();
            registry.Register(new YWingRule());

            // Clear all candidates to shape exact pairs
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    board.Cells[r, c].Candidates.Clear();

            // candidate pairs:
            // (1,2)..|...|..(2,3)
            //    .   ..|...|..  .
            //    .   ..|...|..  .
            // -----------
            //    .   ..|...|..  .
            //    .   ..|...|..  .
            //    .   ..|...|..  .
            // -----------
            //    .   ..|...|..  .
            //    .   ..|...|..  .
            //  (1,2) ..|...|..  .<-value will be 3

            // Rectangle at rows 0-1, cols 0-1
            // Top-left: {1,2}
            board.Cells[0, 0].Candidates.Add(1);
            board.Cells[0, 0].Candidates.Add(2);
            // Top-right: {2,3}
            board.Cells[0, 8].Candidates.Add(2);
            board.Cells[0, 8].Candidates.Add(3);
            // Bottom-left: {1,2}
            board.Cells[8, 0].Candidates.Add(1);
            board.Cells[8, 0].Candidates.Add(2);
            // Bottom-right: allow {1,2,3,4}
            board.Cells[8, 8].Candidates.Add(1);
            board.Cells[8, 8].Candidates.Add(2);
            board.Cells[8, 8].Candidates.Add(3);
            board.Cells[8, 8].Candidates.Add(4);

            // 3 appears in all 3, so not a valid y-wing pattern
            var (rule, result) = registry.ApplyNext(board);
            // solver should not detect a Y-Wing when the same digit appears in all three pivot cells
            Assert.IsNull(rule);
        }
    }
}
