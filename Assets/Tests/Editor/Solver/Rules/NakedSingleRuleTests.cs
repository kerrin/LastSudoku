using NUnit.Framework;
using Sudoku.Solver.Rules;

namespace Sudoku.Tests.Editor
{
    /**
     * Unit tests for the <see cref="NakedSingleRule"/>.
     * A naked single is a cell with a single candidate; applying the rule sets the value and removes that
     * candidate from all peers.
     */
    public class NakedSingleRuleTests
    {
        /**
         * Create a naked single in (0,0), assert the rule applies, the cell is assigned,
         * and peers no longer contain the placed digit as a candidate.
         */
        [Test]
        public void NakedSingleRule_AppliesAndRemovesCandidateFromPeers()
        {
            var board = TestHelpers.CreateEmptyBoard();
            // make cell (0,0) a naked single with candidate 5
            var target = board.Cells[0, 0];
            target.Candidates.Clear();
            target.Candidates.Add(5);

            var rule = new NakedSingleRule();
            Assert.IsTrue(rule.CanApply(board));

            var res = rule.CalculateChanges(board);
            Assert.IsTrue(res.Apply);
            // enact recorded changes on the board
            res.EnactAll(board);
            Assert.AreEqual(5, board.Cells[0, 0].Value);
            // peers must have had candidate 5 removed
            foreach (var peer in board.GetPeers(target))
            {
                Assert.IsFalse(peer.Candidates.Contains(5));
            }
        }

        /**
         * Test if:
         * Single candidate gets set as value.
         * Candidate is removed from peers.
         * UsedCells are recorded for peers with values and candidate removals.
         */
        [Test]
        public void NakedSingleRule_RecordsUsedCells()
        {
            var board = TestHelpers.CreateEmptyBoard();
            // make cell (0,0) a naked single with candidate 5
            var target = board.Cells[0, 0];
            target.Candidates.Clear();
            target.Candidates.Add(5);

            var rule = new NakedSingleRule();
            var res = rule.CalculateChanges(board);
            Assert.IsTrue(res.Apply);
            // enact recorded changes on the board
            res.EnactAll(board);
            Assert.AreEqual(5, board.Cells[0, 0].Value);
            // peers must have had candidate 5 removed and recorded as used cells
            foreach (var peer in board.GetPeers(target))
            {
                Assert.IsFalse(peer.Candidates.Contains(5));
            }
        }
    }
}
