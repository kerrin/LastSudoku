using System.Linq;
using NUnit.Framework;
using Sudoku.Models;
using Sudoku.Solver.Rules;

namespace Sudoku.Tests.Editor
{
    public class SolverRulesTests
    {
        private Board CreateEmptyBoard()
        {
            var board = new Board(9, 3, 3);
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    board.Cells[r, c] = new Cell(r, c, null, false);
            return board;
        }

        [Test]
        public void BoardExtensions_GetRow_Column_Box_Peers_SetValue()
        {
            var board = CreateEmptyBoard();
            // verify row enumeration and box indices
            var row0 = board.GetRow(0).ToList();
            Assert.AreEqual(9, row0.Count);
            Assert.IsTrue(row0.All(c => c.Row == 0));

            var col1 = board.GetColumn(1).ToList();
            Assert.AreEqual(9, col1.Count);
            Assert.IsTrue(col1.All(c => c.Column == 1));

            // box 0 contains cells (0..2,0..2)
            var box0 = board.GetBox(0).ToList();
            Assert.AreEqual(9, box0.Count);
            Assert.IsTrue(box0.Any(c => c.Row == 0 && c.Column == 0));

            // peers: pick cell (1,1) and ensure peers excludes the cell itself and contains row/col/box cells
            var cell = board.Cells[1, 1];
            var peers = board.GetPeers(cell).ToList();
            Assert.IsFalse(peers.Contains(cell));
            // peers should include at least one row, column and box neighbour
            Assert.IsTrue(peers.Any(c => c.Row == 1 && c.Column != 1));
            Assert.IsTrue(peers.Any(c => c.Column == 1 && c.Row != 1));
            Assert.IsTrue(peers.Any(c => c.Box == cell.Box && !(c.Row == cell.Row && c.Column == cell.Column)));

            // SetValue should assign value and clear candidates
            board.SetValue(cell, 4);
            Assert.AreEqual(4, board.Cells[1, 1].Value);
            Assert.IsEmpty(board.Cells[1, 1].Candidates);
        }

        [Test]
        public void NakedSingleRule_AppliesAndRemovesCandidateFromPeers()
        {
            var board = CreateEmptyBoard();
            // make cell (0,0) a naked single with candidate 5
            var target = board.Cells[0, 0];
            target.Candidates.Clear();
            target.Candidates.Add(5);

            var rule = new NakedSingleRule();
            Assert.IsTrue(rule.CanApply(board));

            var res = rule.Apply(board);
            Assert.IsTrue(res.Applied);
            Assert.AreEqual(5, board.Cells[0, 0].Value);
            // peers must have had candidate 5 removed
            foreach (var peer in board.GetPeers(target))
            {
                Assert.IsFalse(peer.Candidates.Contains(5));
            }
        }

        [Test]
        public void MissingSingleRule_FindsUniqueCandidateInUnit()
        {
            var board = CreateEmptyBoard();
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

            var res = rule.Apply(board);
            Assert.IsTrue(res.Applied);
            Assert.AreEqual(7, board.Cells[0, 3].Value);
            // ensure peers no longer contain 7
            foreach (var peer in board.GetPeers(board.Cells[0, 3]))
            {
                Assert.IsFalse(peer.Candidates.Contains(7));
            }
        }

        [Test]
        public void HiddenSingleRule_FindsUniqueCandidateInUnit()
        {
            var board = CreateEmptyBoard();
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

        [Test]
        public void LastCellInUnitRule_PlacesMissingDigit_WhenOneEmpty()
        {
            var board = CreateEmptyBoard();
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

        [Test]
        public void RuleRegistry_RegisterDefaults_And_ApplyAll()
        {
            var board = CreateEmptyBoard();
            var registry = new RuleRegistry();
            registry.RegisterDefaults();
            Assert.AreEqual(4, registry.Rules.Count);

            // no changes on an empty board
            var applied = registry.ApplyAll(board);
            Assert.IsEmpty(applied);

            // make a naked single to ensure ApplyAll applies something
            var target = board.Cells[3, 3];
            target.Candidates.Clear();
            target.Candidates.Add(2);

            var results = registry.ApplyAll(board);
            Assert.IsNotEmpty(results);
            Assert.AreEqual("Naked Single", results[0].rule.Name);
        }
    }
}
