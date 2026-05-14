using NUnit.Framework;
using Sudoku.Models;
using Sudoku.Solver.Rules;

namespace Sudoku.Tests.Editor
{
    public class EmptyRectangleRuleTests
    {
        [Test]
        public void EmptyRectangle_RemovesCandidateOutsideBox()
        {
            var board = new Board(9, 3, 3);
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    board.Cells[r, c] = new Cell(r, c);

            int digit = 5;
            int box = 0; // top-left 3x3 (rows 0-2, cols 0-2)

            // Remove digit from all cells in the box except two in the same row (row 1, cols 0 and 1)
            for (int r = 0; r < 3; r++)
            {
                for (int c = 0; c < 3; c++)
                {
                    if (!(r == 1 && (c == 0 || c == 1)))
                    {
                        board.Cells[r, c].Candidates.Remove(digit);
                    }
                }
            }

            // Ensure outside-row candidate exists at (1,3)
            Assert.IsTrue(board.Cells[1, 3].Candidates.Contains(digit));

            var registry = new RuleRegistry();
            registry.Register(new EmptyRectangleRule());

            var (rule, result) = registry.ApplyNext(board);
            Assert.IsNotNull(rule);
            Assert.IsTrue(result.Applied);
            Assert.IsFalse(board.Cells[1, 3].Candidates.Contains(digit));
        }
    }
}
