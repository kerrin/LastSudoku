using NUnit.Framework;
using Sudoku.Models;
using Sudoku.Solver.Rules;

namespace Sudoku.Tests.Editor
{
    public class StrongLinkIntersectionRuleTests
    {
        [Test, Ignore("Disabled by request")]
        public void RightAngle_RemovesCandidateSeeingBothEndpoints()
        {
            var board = new Board(9, 3, 3);
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    board.Cells[r, c] = new Cell(r, c);

            int d = 4;

            // Box 0 (top-left): put two candidates in row 1 -> strong link
            board.Cells[1, 0].Candidates.Clear(); board.Cells[1, 0].Candidates.Add(d);
            board.Cells[1, 1].Candidates.Clear(); board.Cells[1, 1].Candidates.Add(d);

            // Box 2 (top-right): put two candidates in column 2 -> strong link
            board.Cells[0, 2].Candidates.Clear(); board.Cells[0, 2].Candidates.Add(d);
            board.Cells[2, 2].Candidates.Clear(); board.Cells[2, 2].Candidates.Add(d);

            // A cell that sees both endpoints: (1,2) shares row with (1,0) and column with (2,2)
            board.Cells[1, 2].Candidates.Clear(); board.Cells[1, 2].Candidates.Add(d);

            var registry = new RuleRegistry();
            registry.Register(new StrongLinkIntersectionRule());

            var (rule, result) = registry.ApplyNext(board);
            Assert.IsNotNull(rule);
            Assert.IsTrue(result.Applied);
            Assert.IsFalse(board.Cells[1, 2].Candidates.Contains(d));
        }
    }
}
