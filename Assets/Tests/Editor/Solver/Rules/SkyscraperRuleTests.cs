using NUnit.Framework;
using Sudoku.Models;
using Sudoku.Solver.Rules;

namespace Sudoku.Tests.Editor
{
    public class SkyscraperRuleTests
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
            // Row 8: candidates at (7,0) and (8,1)
            // Shared columns:
            //  (0,0) and (7,0)
            //  (0,1) and (8,1)
            // Target cell (8,0) is a peer of both endpoints (shares column 0 with (0,0) and row 8 with (8,1)).

            // Clear all other candidates for digit d to shape the pattern
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    board.Cells[r, c].Candidates.Clear();

            board.Cells[0, 0].Candidates.Add(d);
            board.Cells[0, 1].Candidates.Add(d);
            board.Cells[7, 0].Candidates.Add(d);
            board.Cells[8, 1].Candidates.Add(d);

            // Candidate to be removed
            board.Cells[8, 0].Candidates.Add(d);

            var registry = new RuleRegistry();
            registry.Register(new SkyscraperRule());

            var (rule, result) = registry.ApplyNext(board);
            Assert.IsNotNull(rule);
            Assert.IsTrue(result.Apply);
            Assert.IsFalse(board.Cells[8, 0].Candidates.Contains(d));
        }
    }
}
