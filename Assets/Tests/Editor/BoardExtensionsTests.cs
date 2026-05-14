using System.Linq;
using NUnit.Framework;
using Sudoku.Models;
using Sudoku.Solver.Rules;

namespace Sudoku.Tests.Editor
{
    /// <summary>
    /// Tests for the Board extension helpers located in <see cref="Sudoku.Solver.Rules.BoardExtensions"/>.
    /// Each test verifies a single helper method so failures point to a concrete behaviour.
    /// </summary>
    public class BoardExtensionsTests
    {
        /// <summary>
        /// Verify that <c>GetRow</c> enumerates exactly nine cells and they all report the requested row index.
        /// </summary>
        [Test]
        public void GetRow_ReturnsNineCells_AllInSameRow()
        {
            var board = TestHelpers.CreateEmptyBoard();
            var row0 = board.GetRow(0).ToList();
            Assert.AreEqual(9, row0.Count);
            Assert.IsTrue(row0.All(c => c.Row == 0));
        }

        /// <summary>
        /// Verify that <c>GetColumn</c> enumerates exactly nine cells and they all report the requested column index.
        /// </summary>
        [Test]
        public void GetColumn_ReturnsNineCells_AllInSameColumn()
        {
            var board = TestHelpers.CreateEmptyBoard();
            var col1 = board.GetColumn(1).ToList();
            Assert.AreEqual(9, col1.Count);
            Assert.IsTrue(col1.All(c => c.Column == 1));
        }

        /// <summary>
        /// Verify that <c>GetBox</c> returns the expected nine cells for the given box index.
        /// </summary>
        [Test]
        public void GetBox_ReturnsNineCells_ContainsExpectedCoordinates()
        {
            var board = TestHelpers.CreateEmptyBoard();
            var box0 = board.GetBox(0).ToList();
            Assert.AreEqual(9, box0.Count);
            Assert.IsTrue(box0.Any(c => c.Row == 0 && c.Column == 0));
        }

        /// <summary>
        /// Ensure <c>GetPeers</c> excludes the target cell and includes cells sharing row, column and box.
        /// </summary>
        [Test]
        public void GetPeers_ExcludesSelf_IncludesRowColumnAndBoxNeighbors()
        {
            var board = TestHelpers.CreateEmptyBoard();
            var cell = board.Cells[1, 1];
            var peers = board.GetPeers(cell).ToList();
            Assert.IsFalse(peers.Contains(cell));
            Assert.IsTrue(peers.Any(c => c.Row == 1 && c.Column != 1));
            Assert.IsTrue(peers.Any(c => c.Column == 1 && c.Row != 1));
            Assert.IsTrue(peers.Any(c => c.Box == cell.Box && !(c.Row == cell.Row && c.Column == cell.Column)));
        }

        /// <summary>
        /// Verify <c>SetValue</c> assigns the provided value and clears candidates for the cell.
        /// </summary>
        [Test]
        public void SetValue_AssignsValue_AndClearsCandidates()
        {
            var board = TestHelpers.CreateEmptyBoard();
            var cell = board.Cells[1, 1];
            board.SetValue(cell, 4);
            Assert.AreEqual(4, board.Cells[1, 1].Value);
            Assert.IsEmpty(board.Cells[1, 1].Candidates);
        }
    }
}
