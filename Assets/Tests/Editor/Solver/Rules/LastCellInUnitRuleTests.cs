using System.Collections.Generic;
using NUnit.Framework;
using Sudoku.Models;
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
                int v = c + 1;
                var cell = board.Cells[2, c];
                cell.Value = v; // values 1..8
                cell.Candidates.Clear();
                foreach (var peer in board.GetPeers(cell)) peer.Candidates.Remove(v);
            }
            // last cell at (2,8) remains empty and initially has full candidates
            var rule = new LastCellInUnitRule();
            Assert.IsTrue(rule.CanApply(board));

            // ensure empty peers contain candidate 9 (test setup)
            var target = board.Cells[2, 8];
            foreach (var peer in board.GetPeers(target)) Assert.IsTrue(peer.Value.HasValue || peer.Candidates.Contains(9), "Precondition: peer should contain 9 or be filled");

            var res = rule.CalculateChanges(board);
            Assert.IsTrue(res.Apply);
            res.EnactAll(board);
            // missing digit should be 9
            Assert.AreEqual(9, board.Cells[2, 8].Value);
            foreach (var peer in board.GetPeers(board.Cells[2, 8])) Assert.IsFalse(peer.Candidates.Contains(9));
            // Check other cells in the unit still have their candidates (only the missing digit should be removed)
            for (int c = 0; c < 8; c++)
            {
                Assert.IsFalse(board.Cells[2, c].Candidates.Contains(9), $"Expected cell (2,{c}) to not contain candidate 9");
            }
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
                int v = c + 1;
                var cell = board.Cells[2, c];
                cell.Value = v; // values 1..7
                cell.Candidates.Clear();
                foreach (var peer in board.GetPeers(cell)) peer.Candidates.Remove(v);
            }
            // last 2 cells at (2,7) and (2,8) remain empty and initially have full candidates
            var rule = new LastCellInUnitRule();
            Assert.IsFalse(rule.CanApply(board));

            var res = rule.CalculateChanges(board);
            Assert.IsFalse(res.Apply);
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
                int v = r + 1;
                var cell = board.Cells[r, 2];
                cell.Value = v; // values 1..8
                cell.Candidates.Clear();
                foreach (var peer in board.GetPeers(cell)) peer.Candidates.Remove(v);
            }
            // last cell at (8,2) remains empty and initially has full candidates
            var rule = new LastCellInUnitRule();
            Assert.IsTrue(rule.CanApply(board));

            // ensure empty peers contain candidate 9 (test setup)
            var target = board.Cells[8, 2];
            foreach (var peer in board.GetPeers(target)) Assert.IsTrue(peer.Value.HasValue || peer.Candidates.Contains(9), "Precondition: peer should contain 9 or be filled");

            var res = rule.CalculateChanges(board);
            Assert.IsTrue(res.Apply);
            res.EnactAll(board);
            // missing digit should be 9
            Assert.AreEqual(9, board.Cells[8, 2].Value);
            foreach (var peer in board.GetPeers(board.Cells[8, 2])) Assert.IsFalse(peer.Candidates.Contains(9));
            // Check other cells in the unit still have their candidates (only the missing digit should be removed)
            for (int r = 0; r < 8; r++)
            {
                Assert.IsFalse(board.Cells[r, 2].Candidates.Contains(9), $"Expected cell ({r},2) to not contain candidate 9");
            }
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
                    // skip the target cell (2,2) so we never temporarily assign 9 and remove it from peers
                    if (r == 2 && c == 2) continue;
                    int v = r * 3 + c + 1;
                    var cell = board.Cells[r, c];
                    cell.Value = v; // values 1..8
                    cell.Candidates.Clear();
                    foreach (var peer in board.GetPeers(cell)) peer.Candidates.Remove(v);
                }
            }
            // last cell at (2,2) remains empty and initially has full candidates
            var rule = new LastCellInUnitRule();
            Assert.IsTrue(rule.CanApply(board));

            // ensure empty peers in the same row/column/box contain candidate 9 (test setup)
            var target = board.Cells[2, 2];
            foreach (var peer in board.GetRow(target.Row)) if (!ReferenceEquals(peer, target)) Assert.IsTrue(peer.Value.HasValue || peer.Candidates.Contains(9), $"Precondition: row peer should contain 9 at ({peer.Row},{peer.Column})");
            foreach (var peer in board.GetColumn(target.Column)) if (!ReferenceEquals(peer, target)) Assert.IsTrue(peer.Value.HasValue || peer.Candidates.Contains(9), $"Precondition: column peer should contain 9 at ({peer.Row},{peer.Column})");
            foreach (var peer in board.GetBox(target.Box)) if (!ReferenceEquals(peer, target)) Assert.IsTrue(peer.Value.HasValue || peer.Candidates.Contains(9), $"Precondition: box peer should contain 9 at ({peer.Row},{peer.Column})");

            var res = rule.CalculateChanges(board);
            Assert.IsTrue(res.Apply);
            res.EnactAll(board);
            // missing digit should be 9
            Assert.AreEqual(9, board.Cells[2, 2].Value);
            foreach (var peer in board.GetPeers(board.Cells[2, 2])) Assert.IsFalse(peer.Candidates.Contains(9));
            // Check other cells in the unit still have their candidates (only the missing digit should be removed)
            for (int r = 0; r < 2; r++)
            {
                for (int c = 0; c < 3; c++) {
                    Assert.IsFalse(board.Cells[r, c].Candidates.Contains(9), $"Expected cell ({r},{c}) to not contain candidate 9");
                }
            }
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
                    int v = r * 3 + c + 1;
                    var cell = board.Cells[r, c];
                    cell.Value = v; // values 1..8
                    cell.Candidates.Clear();
                    foreach (var peer in board.GetPeers(cell)) peer.Candidates.Remove(v);
                }
                board.Cells[0, 0].Value = null; // leave first cell empty
                board.Cells[2, 2].Value = null; // leave last cell empty
            }
            // 2 cells remain empty and rest have values
            var rule = new LastCellInUnitRule();
            Assert.IsFalse(rule.CanApply(board));

            var res = rule.CalculateChanges(board);
            Assert.IsFalse(res.Apply);
            // empty cells should remain empty
            Assert.IsNull(board.Cells[0, 0].Value);
            Assert.IsNull(board.Cells[2, 2].Value);
        }

        /**
         * Populate row 2 with values 1..8, leaving the last cell empty. The rule should list the candidate as only 9 in the last cell
         * and remove 9 from peers' candidate sets.
         */
        [Test]
        public void LastCellInUnitRule_CandidatesForMissingDigit_WhenOneEmpty_Row()
        {
            var board = TestHelpers.CreateEmptyBoard();
            // prepare row 2 with values 1..8 placed, leaving one empty cell
            for (int c = 0; c < 8; c++)
            {
                board.Cells[2, c].Value = c + 1; // values 1..8
            }
            // last cell at (2,8) remains empty and initially has full candidates
            var rule = new LastCellInUnitRule();
            Assert.IsTrue(rule.CanApply(board));

            // precondition: peers should initially contain candidate 9
            var target = board.Cells[2, 8];
            foreach (Cell peer in board.GetPeers(target)) Assert.IsTrue(peer.Candidates.Contains(9), "Precondition: peer should contain 9");

            var res = rule.CalculateChanges(board);
            Assert.IsTrue(res.Apply);
            if (res.Apply) res.EnactCandidates(board);
            // missing digit should be 9 and only 9 in candidates
            Assert.IsTrue(board.Cells[2, 8].Candidates.Contains(9));
            Assert.AreEqual(1, board.Cells[2, 8].Candidates.Count);
            foreach (Cell peer in board.GetPeers(board.Cells[2, 8])) Assert.IsFalse(peer.Candidates.Contains(9), "For peer " + peer);
            // Check other cells in the unit still have their candidates (only the missing digit should be removed)
            for (int c = 0; c < 8; c++)   
            {
                Assert.IsFalse(board.Cells[2, c].Candidates.Contains(9), $"Expected cell (2,{c}) to not contain candidate 9");
            }
        }

        

        /**
         * Populate row 2 with values 1..8, leaving the last cell empty. The rule should list the candidate as only 9 in the last cell
         * and remove 9 from peers' candidate sets.
         */
        [Test]
        public void LastCellInUnitRule_CandidatesForMissingDigit_WhenMultipleEmpty_Row()
        {
            var board = TestHelpers.CreateEmptyBoard();
            // prepare row 2 with values 1..7 placed, leaving the last 2 cells empty
            for (int c = 0; c < 7; c++)
            {
                int v = c + 1;
                var cell = board.Cells[2, c];
                cell.Value = v; // values 1..7
                cell.Candidates.Clear();
                foreach (var peer in board.GetPeers(cell)) peer.Candidates.Remove(v);
            }
            // last 2 cells at (2,7) and (2,8) remain empty and initially have full candidates
            var rule = new LastCellInUnitRule();
            Assert.IsFalse(rule.CanApply(board));

            var res = rule.CalculateChanges(board);
            Assert.IsFalse(res.Apply);
            // missing digit should be 8 and 9 and only 8 and 9 in candidates for the two empty cells
            // (no changes enacted)
            Assert.IsTrue(board.Cells[2, 7].Candidates.Contains(8));
            Assert.IsTrue(board.Cells[2, 8].Candidates.Contains(9));
            // As we didn't apply any changes, the empty cells should have lost 1..7 from prior placements and thus contain only 8 and 9
            Assert.AreEqual(2, board.Cells[2, 8].Candidates.Count);
            Assert.AreEqual(2, board.Cells[2, 7].Candidates.Count);
        } 
    }
}
