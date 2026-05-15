using NUnit.Framework;
using Sudoku.Solver.Rules;

namespace Sudoku.Tests.Editor
{
    /**
     * Tests for <see cref="LastCellInUnitRule"/>, which assigns the remaining missing digit when all
     * but one cell in a unit are filled.
     */
    public class LastCellInUnitRuleTests
    {
        /**
         * Populate row 2 with values 1..8, leaving the last cell empty. The rule should place 9 in the last cell
         * and remove 9 from peers' candidate sets.
         */
        [Test]
        public void LastCellInUnitRule_PlacesMissingDigit_WhenOneEmpty_Row()
        {
            var board = TestHelpers.CreateEmptyBoard();
            // prepare row 2 with values 1..8 placed, leaving one empty cell
            for (int c = 0; c < 8; c++)
            {
                board.Cells[2, c].Value = c + 1; // values 1..8
                board.Cells[2, c].Candidates.Clear();
            }
            // last cell at (2,8) remains empty and initially has full candidates
            var rule = new LastCellInUnitRule();
            Assert.IsTrue(rule.CanApply(board));

            var res = rule.Apply(board);
            Assert.IsTrue(res.Applied);
            // missing digit should be 9
            Assert.AreEqual(9, board.Cells[2, 8].Value);
            foreach (var peer in board.GetPeers(board.Cells[2, 8])) Assert.IsFalse(peer.Candidates.Contains(9));
        }

        /**
         * Populate row 2 with values 1..7, leaving the last 2 cells empty. The rule should do nothing.
         */
        [Test]
        public void LastCellInUnitRule_DoesNothing_WhenMultipleEmpty_Row()
        {
            var board = TestHelpers.CreateEmptyBoard();
            // prepare row 2 with values 1..7 placed, leaving the last 2 cells empty
            for (int c = 0; c < 7; c++)
            {
                board.Cells[2, c].Value = c + 1; // values 1..7
                board.Cells[2, c].Candidates.Clear();
            }
            // last 2 cells at (2,7) and (2,8) remain empty and initially have full candidates
            var rule = new LastCellInUnitRule();
            Assert.IsFalse(rule.CanApply(board));

            var res = rule.Apply(board);
            Assert.IsFalse(res.Applied);
            // last 2 cells should remain empty
            Assert.IsNull(board.Cells[2, 7].Value);
            Assert.IsNull(board.Cells[2, 8].Value);
        }

        /**
         * Populate column 2 with values 1..8, leaving the last cell empty. The rule should place 9 in the last cell
         * and remove 9 from peers' candidate sets.
         */
        [Test]
        public void LastCellInUnitRule_PlacesMissingDigit_WhenOneEmpty_Column()
        {
            var board = TestHelpers.CreateEmptyBoard();
            // prepare column 2 with values 1..8 placed, leaving one empty cell
            for (int r = 0; r < 8; r++)
            {
                board.Cells[r, 2].Value = r + 1; // values 1..8
                board.Cells[r, 2].Candidates.Clear();
            }
            // last cell at (8,2) remains empty and initially has full candidates
            var rule = new LastCellInUnitRule();
            Assert.IsTrue(rule.CanApply(board));

            var res = rule.Apply(board);
            Assert.IsTrue(res.Applied);
            // missing digit should be 9
            Assert.AreEqual(9, board.Cells[8, 2].Value);
            foreach (var peer in board.GetPeers(board.Cells[8, 2])) Assert.IsFalse(peer.Candidates.Contains(9));
        }

        /**
         * Populate box 0,0 with values 1..8, leaving the last cell empty. The rule should place 9 in the last cell
         * and remove 9 from peers' candidate sets.
         */
        [Test]
        public void LastCellInUnitRule_PlacesMissingDigit_WhenOneEmpty_Box()
        {
            var board = TestHelpers.CreateEmptyBoard();
            // prepare box 0,0 with values 1..8 placed, leaving one empty cell
            for (int r = 0; r < 3; r++)
            {
                for (int c = 0; c < 3; c++)
                {
                    board.Cells[r, c].Value = r * 3 + c + 1; // values 1..8
                    board.Cells[r, c].Candidates.Clear();
                }
                board.Cells[2, 2].Value = null; // leave last cell empty
            }
            // last cell at (2,2) remains empty and initially has full candidates
            var rule = new LastCellInUnitRule();
            Assert.IsTrue(rule.CanApply(board));

            var res = rule.Apply(board);
            Assert.IsTrue(res.Applied);
            // missing digit should be 9
            Assert.AreEqual(9, board.Cells[2, 2].Value);
            foreach (var peer in board.GetPeers(board.Cells[2, 2])) Assert.IsFalse(peer.Candidates.Contains(9));
        }

        

        /**
         * Populate box 0,0 with values 2..8, leaving the last 2 cells empty. The rule should not be applicable.
         */
        [Test]
        public void LastCellInUnitRule_PlacesMissingDigit_WhenTwoEmpty_Box()
        {
            var board = TestHelpers.CreateEmptyBoard();
            // prepare box 0,0 with values 1..8 placed, leaving one empty cell
            for (int r = 0; r < 3; r++)
            {
                for (int c = 0; c < 3; c++)
                {
                    board.Cells[r, c].Value = r * 3 + c + 1; // values 1..8
                    board.Cells[r, c].Candidates.Clear();
                }
                board.Cells[0, 0].Value = null; // leave first cell empty
                board.Cells[2, 2].Value = null; // leave last cell empty
            }
            // 2 cells remain empty and rest have values
            var rule = new LastCellInUnitRule();
            Assert.IsFalse(rule.CanApply(board));

            var res = rule.Apply(board);
            Assert.IsFalse(res.Applied);
            // empty cells should remain empty
            Assert.IsNull(board.Cells[0, 0].Value);
            Assert.IsNull(board.Cells[2, 2].Value);
        }
    }
}
