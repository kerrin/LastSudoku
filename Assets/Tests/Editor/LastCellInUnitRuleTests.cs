using NUnit.Framework;
using Sudoku.Solver.Rules;

namespace Sudoku.Tests.Editor
{
    /// <summary>
    /// Tests for <see cref="LastCellInUnitRule"/>, which assigns the remaining missing digit when all
    /// but one cell in a unit are filled.
    /// </summary>
    public class LastCellInUnitRuleTests
    {
        /// <summary>
        /// Populate row 2 with values 1..8, leaving the last cell empty. The rule should place 9 in the last cell
        /// and remove 9 from peers' candidate sets.
        /// </summary>
        [Test]
        public void LastCellInUnitRule_PlacesMissingDigit_WhenOneEmpty()
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
    }
}
