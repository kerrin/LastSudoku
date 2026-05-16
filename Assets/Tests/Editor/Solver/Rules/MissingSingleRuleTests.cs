using NUnit.Framework;
using Sudoku.Solver.Rules;

namespace Sudoku.Tests.Editor
{
    /**
     * Tests for the <see cref="MissingSingleRule"/>, which places a missing digit when it is the only
     * candidate for that digit within a unit (row/column/box).
     */
    public class MissingSingleRuleTests
    {
        /**
         * Configure row 0 so only column 3 contains candidate 7; verify the rule sets that cell to 7 and
         * removes candidate 7 from peers.
         */
        [Test]
        public void MissingSingleRule_FindsUniqueCandidateInUnit()
        {
            var board = TestHelpers.CreateEmptyBoard();
            // choose digit 7 and make only (0,3) in row 0 contain candidate 7
            for (int c = 0; c < 9; c++)
            {
                var cell = board.Cells[0, c];
                cell.Candidates.Clear();
                // all cells except column 3 do NOT have 7 as candidate
                if (c == 3) { cell.Candidates.UnionWith(new[] { 7 }); }
                else { cell.Candidates.UnionWith(new[] { 1, 2, 3 }); }
            }

            var rule = new MissingSingleRule();
            Assert.IsTrue(rule.CanApply(board));

            var res = rule.CalculateChanges(board);
            Assert.IsTrue(res.Apply);
            res.EnactAll(board);
            Assert.AreEqual(7, board.Cells[0, 3].Value);
            // ensure peers no longer contain 7
            foreach (var peer in board.GetPeers(board.Cells[0, 3]))
            {
                Assert.IsFalse(peer.Candidates.Contains(7));
            }
        }

        /**
         * Check when a cell has multiple candidates for a digit, but the digit is still a Missing Single for the unit.
         * 
         */
        [Test]
        public void MissingSingleRule_FindsMissingSingleEvenWhenMultipleCandidatesForDigit()
        {
            var board = TestHelpers.CreateEmptyBoard();
            // choose digit 7 and make only (0,3) in row 0 contain candidate 7, but also have other candidates
            for (int c = 0; c < 9; c++)
            {
                var cell = board.Cells[0, c];
                cell.Candidates.Clear();
                if (c == 3) { cell.Candidates.UnionWith(new[] { 7, 8, 9 }); }
                else { cell.Candidates.UnionWith(new[] { 1, 2, 3, 8, 9 }); }
            }

            var rule = new MissingSingleRule();
            Assert.IsTrue(rule.CanApply(board));

            var res = rule.CalculateChanges(board);
            Assert.IsTrue(res.Apply);
            res.EnactAll(board);
            Assert.AreEqual(7, board.Cells[0, 3].Value);
        }
    }
}
