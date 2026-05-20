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
            // different cells
            board.Cells[0, 1].Candidates.Add(4);
            board.Cells[1, 0].Candidates.Add(4);

            var (rule, result) = registry.ApplyNext(board);
            Assert.IsNotNull(rule);
            Assert.IsTrue(result.Apply);
            // YWing pattern should place digit 3 into (8,8)
            Assert.AreEqual(3, board.Cells[8, 8].Value);
            Assert.IsEmpty(board.Cells[8, 8].Candidates);
            // check candidates in different cells are unaffected
            Assert.IsTrue(board.Cells[0, 1].Candidates.Contains(4));
            Assert.IsTrue(board.Cells[1, 0].Candidates.Contains(4));
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
            //  (1,3) ..|...|..  .

            // Rectangle at rows 0-1, cols 0-1
            // Top-left: {1,2,3}
            board.Cells[0, 0].Candidates.Add(1);
            board.Cells[0, 0].Candidates.Add(2);
            board.Cells[0, 0].Candidates.Add(3); // This is why this should not be a valid y-wing pattern
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
            //  (1,2) ..|...|..(2,3)
            //    .   ..|...|..  .
            //    .   ..|...|..  .
            // -----------
            //    .   ..|...|..  .
            //    .   ..|...|..  .
            //    .   ..|...|..  .
            // -----------
            //    .   ..|...|..  .
            //    .   ..|...|..  .
            //  (1,2) ..|...|..  .

            // Rectangle at rows 0-1, cols 0-1
            // Top-left: {1,2}
            board.Cells[0, 0].Candidates.Add(1);
            board.Cells[0, 0].Candidates.Add(2);
            // Top-right: {2,3}
            board.Cells[0, 8].Candidates.Add(2);
            board.Cells[0, 8].Candidates.Add(3);
            // Bottom-left: {1,2}, same as 0,0 so not a valid y-wing pattern
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

        [Test]
        public void YWing_Rectangle_NotValid_Example1()
        {
            var board = TestHelpers.CreateEmptyBoard();
            var registry = new RuleRegistry();
            registry.Register(new YWingRule());

            // Clear all candidates to shape exact pairs
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    board.Cells[r, c].Candidates.Clear();

            // candidate pairs:
            // ...|(1,4,7)..|(1,2,4,9)..
            // ...|...|...
            // ...|...|...
            // -----------
            // ...|(2,5,6,7)..|(4,5,6,8,9)..
            // ...|...|...
            // ...|...|...
            // -----------
            // ...|...|...
            // ...|...|...
            // ...|...|...

            // Top-left
            board.Cells[0, 3].Candidates.Add(1);
            board.Cells[0, 3].Candidates.Add(4);
            board.Cells[0, 3].Candidates.Add(7);
            // Top-right
            board.Cells[0, 6].Candidates.Add(1);
            board.Cells[0, 6].Candidates.Add(2);
            board.Cells[0, 6].Candidates.Add(4);
            board.Cells[0, 6].Candidates.Add(9);
            // Bottom-left
            board.Cells[3, 3].Candidates.Add(2);
            board.Cells[3, 3].Candidates.Add(5);
            board.Cells[3, 3].Candidates.Add(6);
            board.Cells[3, 3].Candidates.Add(7);
            // Bottom-right
            board.Cells[3, 6].Candidates.Add(4);
            board.Cells[3, 6].Candidates.Add(5);
            board.Cells[3, 6].Candidates.Add(6);
            board.Cells[3, 6].Candidates.Add(8);
            board.Cells[3, 6].Candidates.Add(9);

            // 3 appears in all 3, so not a valid y-wing pattern
            var (rule, result) = registry.ApplyNext(board);
            // solver should not detect a Y-Wing when the same digit appears in all three pivot cells
            Assert.IsNull(rule);
        }

        [Test]
        public void YWing_Rectangle_NotValid_Example2()
        {
            var board = TestHelpers.CreateEmptyBoard();
            var registry = new RuleRegistry();
            registry.Register(new YWingRule());

            // Clear all candidates to shape exact pairs
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    board.Cells[r, c].Candidates.Clear();

            // candidate pairs:
            // .(1479).|(147)..|...
            // ...|...|...
            // ...|...|...
            // -----------
            // ...|...|...
            // .(569).|(1256)..|...
            // ...|...|...
            // -----------
            // ...|...|...
            // ...|...|...
            // ...|...|...

            // Top-left
            board.Cells[0, 1].Candidates.Add(1);
            board.Cells[0, 1].Candidates.Add(4);
            board.Cells[0, 1].Candidates.Add(7);
            board.Cells[0, 1].Candidates.Add(9);
            // Top-right
            board.Cells[0, 3].Candidates.Add(1);
            board.Cells[0, 3].Candidates.Add(4);
            board.Cells[0, 3].Candidates.Add(7);
            // Bottom-left
            board.Cells[4, 1].Candidates.Add(5);
            board.Cells[4, 1].Candidates.Add(6);
            board.Cells[4, 1].Candidates.Add(9);
            // Bottom-right
            board.Cells[4, 3].Candidates.Add(1);
            board.Cells[4, 3].Candidates.Add(2);
            board.Cells[4, 3].Candidates.Add(5);
            board.Cells[4, 3].Candidates.Add(6);

            // 3 appears in all 3, so not a valid y-wing pattern
            var (rule, result) = registry.ApplyNext(board);
            // solver should not detect a Y-Wing when the same digit appears in all three pivot cells
            Assert.IsNull(rule);
        }
    }
}
