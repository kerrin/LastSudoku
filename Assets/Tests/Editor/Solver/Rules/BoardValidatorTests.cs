using NUnit.Framework;
using Sudoku.Models;
using Sudoku.Solver.Rules;

namespace Sudoku.Tests.Editor
{
    public class BoardValidatorTests
    {
        [Test]
        public void IsValid_EmptyBoard_ReturnsTrue()
        {
            var board = TestHelpers.CreateEmptyBoard();
            Assert.IsTrue(board.IsValid());
        }

        [Test]
        public void IsValid_RowDuplicate_ReturnsFalse()
        {
            var board = TestHelpers.CreateEmptyBoard();
            board.Cells[0, 0].Value = 7;
            board.Cells[0, 4].Value = 7;
            Assert.IsFalse(board.IsValid());
        }

        [Test]
        public void IsValid_ColumnDuplicate_ReturnsFalse()
        {
            var board = TestHelpers.CreateEmptyBoard();
            board.Cells[1, 2].Value = 3;
            board.Cells[5, 2].Value = 3;
            Assert.IsFalse(board.IsValid());
        }

        [Test]
        public void IsValid_BoxDuplicate_ReturnsFalse()
        {
            var board = TestHelpers.CreateEmptyBoard();
            board.Cells[0, 1].Value = 9;
            board.Cells[2, 2].Value = 9;
            Assert.IsFalse(board.IsValid());
        }
    }
}
