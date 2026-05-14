using NUnit.Framework;
using Sudoku.Models;
using Sudoku.Solver.Rules;

namespace Sudoku.Tests.Editor.Solver
{
    public class SkyscraperTests
    {
        [Test]
        public void Skyscraper_RemovesCandidateSeeingBothEndpoints()
        {
            var board = new Board(9, 3, 3);
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    board.Cells[r, c] = new Cell(r, c);

            int d = 7;
            // Row 0: candidates at (0,0) and (0,1)
            // Row 2: candidates at (2,1) and (2,2)
            // Shared column is 1; endpoints are (0,0) and (2,2)
            // Target cell (2,0) sees both endpoints (shares column 0 with (0,0) and row 2 with (2,2)).

            // Clear all other candidates for digit d to shape the pattern
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    board.Cells[r, c].Candidates.Clear();

            board.Cells[0, 0].Candidates.Add(d);
            board.Cells[0, 1].Candidates.Add(d);
            board.Cells[2, 1].Candidates.Add(d);
            board.Cells[2, 2].Candidates.Add(d);

            // Candidate to be removed
            board.Cells[2, 0].Candidates.Add(d);

            var registry = new RuleRegistry();
            registry.Register(new SkyscraperRule());

            var (rule, result) = registry.ApplyNext(board);
            Assert.IsNotNull(rule);
            Assert.IsTrue(result.Applied);
            Assert.IsFalse(board.Cells[2, 0].Candidates.Contains(d));
        }
    }
}
