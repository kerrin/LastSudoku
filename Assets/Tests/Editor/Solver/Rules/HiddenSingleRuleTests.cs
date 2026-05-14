using NUnit.Framework;
using Sudoku.Solver.Rules;

namespace Sudoku.Tests.Editor
{
    /// <summary>
    /// Tests for <see cref="HiddenSingleRule"/>. A hidden single occurs when a digit can only go in one cell
    /// inside a unit due to candidate placement — the rule should place that digit.
    /// </summary>
    public class HiddenSingleRuleTests
    {
        /// <summary>
        /// Sets up row 1 so only column 5 contains candidate 3, asserts the rule assigns the value and removes candidates.
        /// </summary>
        [Test]
        public void HiddenSingleRule_FindsUniqueCandidateInUnit()
        {
            var board = TestHelpers.CreateEmptyBoard();
            // same setup as MissingSingle — HiddenSingle's logic is identical for this case
            for (int c = 0; c < 9; c++)
            {
                var cell = board.Cells[1, c];
                cell.Candidates.Clear();
                if (c == 5) { cell.Candidates.UnionWith(new[] { 3 }); }
                else { cell.Candidates.UnionWith(new[] { 1, 2 }); }
            }

            var rule = new HiddenSingleRule();
            Assert.IsTrue(rule.CanApply(board));

            var res = rule.Apply(board);
            Assert.IsTrue(res.Applied);
            Assert.AreEqual(3, board.Cells[1, 5].Value);
            foreach (var peer in board.GetPeers(board.Cells[1, 5])) Assert.IsFalse(peer.Candidates.Contains(3));
        }
    }
}
