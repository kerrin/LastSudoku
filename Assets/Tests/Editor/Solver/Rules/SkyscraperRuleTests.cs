using NUnit.Framework;
using Sudoku.Models;
using Sudoku.Solver.Rules;

namespace Sudoku.Tests.Editor
{
    public class SkyscraperRuleTests
    {
        [Test]
        public void Skyscraper_RemovesCandidateSeeingBothEndpointsSameBox()
        {
            var board = new Board(9, 3, 3);
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    board.Cells[r, c] = new Cell(r, c);

            int d = 7;
            // Construct a valid skyscraper pattern where both supporting rows are true conjugate pairs
            // and the two roof endpoints share the same 3x3 box so a third cell in that box
            // can see both roofs without being in either supporting row.
            // Supporting rows: r1=0, r2=1, shared column b=0
            // Row 0: candidates at (0,0) and (0,1)
            // Row 1: candidates at (1,0) and (1,2)
            // Roof endpoints: (0,1) and (1,2) (both inside top-left box).
            // Target cell (2,1) is in the same box and sees both roofs, so it should be eliminated.

            // Clear all other candidates for digit d to shape the pattern
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    board.Cells[r, c].Candidates.Clear();

            board.Cells[0, 0].Candidates.Add(d);
            board.Cells[0, 1].Candidates.Add(d);
            board.Cells[1, 0].Candidates.Add(d);
            board.Cells[1, 2].Candidates.Add(d);

            // Candidate to be removed (sees both roof endpoints from the same box)
            board.Cells[2, 1].Candidates.Add(d);

            var registry = new RuleRegistry();
            registry.Register(new SkyscraperRule());

            var (rule, result) = registry.ApplyNext(board);
            Assert.IsNotNull(rule);
            Assert.IsTrue(result.Apply);
            Assert.IsFalse(board.Cells[2, 1].Candidates.Contains(d));
        }

        [Test]
        public void Skyscraper_RemovesCandidateSeeingBothEndpointsDifferentBox()
        {
            var board = new Board(9, 3, 3);
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    board.Cells[r, c] = new Cell(r, c);

            int d = 7;
            // Construct a valid skyscraper pattern where both supporting rows are true conjugate pairs
            // and the two roof endpoints are in different 3x3 boxes so a third cell in a different box
            // can see both roofs without being in either supporting row.
            // Supporting rows: r1=0, r2=1, shared column b=0
            // Row 0: candidates at (0,0) and (0,1)
            // Row 1: candidates at (1,0) and (1,2)
            // Roof endpoints: (0,1) and (1,2) (in different boxes).
            // Target cell (2,1) is in a different box and sees both roofs, so it should be eliminated.

            // Clear all other candidates for digit d to shape the pattern
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    board.Cells[r, c].Candidates.Clear();

            board.Cells[1, 1].Candidates.Add(d);
            board.Cells[2, 7].Candidates.Add(d);
            board.Cells[7, 1].Candidates.Add(d);
            board.Cells[7, 7].Candidates.Add(d);

            // Candidate to be removed (sees both roof endpoints from different boxes)
            // Place it at (1,8): it shares the top-left box with (0,1) and column 2 with (7,2),
            // and is not in either supporting row (0 or 7).
            board.Cells[1, 8].Candidates.Add(d);

            var registry = new RuleRegistry();
            registry.Register(new SkyscraperRule());

            var (rule, result) = registry.ApplyNext(board);
            Assert.IsNotNull(rule);
            Assert.IsTrue(result.Apply);
            Assert.IsFalse(board.Cells[8, 2].Candidates.Contains(d));
        }
    }
}
