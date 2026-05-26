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
            // values:
            // x1.|.8.|..7
            // .3.|5..|...
            // ..6|...|..5
            // -----------
            // ...|...|...
            // 2..|.5.|...
            // ...|...|...
            // -----------
            // 45.|...|...
            // ...|...|...
            // 9..|...|..5
            board.Cells[0, 1].Value = 1;
            board.Cells[0, 4].Value = 8;
            board.Cells[0, 8].Value = 7;
            board.Cells[1, 1].Value = 3;
            board.Cells[1, 3].Value = 5; // Not seen by 0,0
            board.Cells[2, 2].Value = 6;
            board.Cells[2, 8].Value = 5; // Not seen by 0,0
            board.Cells[4, 0].Value = 2;
            board.Cells[4, 4].Value = 5; // Not seen by 0,0
            board.Cells[6, 0].Value = 4;
            board.Cells[6, 1].Value = 5; // Not seen by 0,0
            board.Cells[8, 0].Value = 9;
            board.Cells[8, 8].Value = 5; // Not seen by 0,0

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
    }
}
