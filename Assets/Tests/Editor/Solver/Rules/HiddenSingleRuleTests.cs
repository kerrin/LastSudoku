using NUnit.Framework;
using Sudoku.Solver.Rules;

namespace Sudoku.Tests.Editor
{
    /** 
     * Tests for <see cref="HiddenSingleRule"/>. A hidden single occurs when a digit can only go in one cell
     * inside a unit due to candidate placement — the rule should place that digit.
     */
    public class HiddenSingleRuleTests
    {
        /**
         * Sets up row 1 so only column 5 contains candidate 3, asserts the rule assigns the value and removes candidates.
         */
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

            var res = rule.CalculateChanges(board);
            Assert.IsTrue(res.Apply);
            res.EnactAll(board);
            Assert.AreEqual(3, board.Cells[1, 5].Value);
            foreach (var peer in board.GetPeers(board.Cells[1, 5])) Assert.IsFalse(peer.Candidates.Contains(3));
        }

        /**
         * Set up box 0,0 so only cell 0,1 contains candidate 4, asserts the rule assigns the value and removes candidates.
         */
        [Test]
        public void HiddenSingleRule_FindsUniqueCandidateInBox()
        {
            var board = TestHelpers.CreateEmptyBoard();
            for (int r = 0; r < 3; r++)
            {
                for (int c = 0; c < 3; c++)
                {
                    var cell = board.Cells[r, c];
                    cell.Candidates.Clear();
                    if (r == 0 && c == 1) { cell.Candidates.UnionWith(new[] { 4 }); }
                    else { cell.Candidates.UnionWith(new[] { 1, 2 }); }
                }
            }

            var rule = new HiddenSingleRule();
            Assert.IsTrue(rule.CanApply(board));

            var res = rule.CalculateChanges(board);
            Assert.IsTrue(res.Apply);
            res.EnactAll(board);
            Assert.AreEqual(4, board.Cells[0, 1].Value);
            foreach (var peer in board.GetPeers(board.Cells[0, 1])) Assert.IsFalse(peer.Candidates.Contains(4));
        }
    }
}
