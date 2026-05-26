using NUnit.Framework;
using Sudoku.Models;
using Sudoku.Solver.Rules;

namespace Sudoku.Tests.Editor
{
    public class SkyscraperRuleTests
    {
        [Test]
        public void Skyscraper_RemovesCandidateSeeingBothEndpointsDifferentBox()
        {
            var board = new Board(9, 3, 3);
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    board.Cells[r, c] = new Cell(r, c);

            int d = 7;
            // Clear all other candidates for digit d to shape the pattern
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    board.Cells[r, c].Candidates.Clear();

            // candidates: (* are where the candidates are, that include 1 and can be removed by the skyscraper)
            // 1..|...|*.*
            // .**|...|.1.
            // ...|...|...
            // -----------
            // ...|...|...
            // ...|...|...
            // ...|...|...
            // -----------
            // ...|...|...
            // ...|...|...
            // 1..|...|.1.
            board.Cells[0, 0].Candidates.Add(d);
            board.Cells[1, 7].Candidates.Add(d);
            board.Cells[8, 0].Candidates.Add(d);
            board.Cells[8, 7].Candidates.Add(d);

            // Candidate to be removed (sees both roof endpoints from the same box)
            for(int a = 1; a <= 9; a++)
            {
                board.Cells[0, 6].Candidates.Add(a);
                board.Cells[0, 8].Candidates.Add(a);
                board.Cells[1, 1].Candidates.Add(a);
                board.Cells[1, 2].Candidates.Add(a);
            }

            var registry = new RuleRegistry();
            registry.Register(new SkyscraperRule());

            var (rule, result) = registry.ApplyNext(board);
            Assert.IsNotNull(rule);
            Assert.IsTrue(result.Apply);
            Assert.IsFalse(board.Cells[0, 6].Candidates.Contains(d));
            Assert.IsFalse(board.Cells[0, 8].Candidates.Contains(d));
            Assert.IsFalse(board.Cells[1, 1].Candidates.Contains(d));
            Assert.IsFalse(board.Cells[1, 2].Candidates.Contains(d));
        }

        

        [Test]
        public void Skyscraper_RemovesCandidateSeeingBothEndpointsSameBoxRoofAndFloorPairs()
        {
            var board = new Board(9, 3, 3);
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    board.Cells[r, c] = new Cell(r, c);

            int d = 7;
            
            // Clear all other candidates for digit d to shape the pattern
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    board.Cells[r, c].Candidates.Clear();
            // candidates: (* are where the candidates are, that include 1 and can be removed by the skyscraper)
            // ...|.*.|...
            // ...|..9|...
            // ...|9..|...
            // -----------
            // ...|...|...
            // ...|...|...
            // ...|9.9|...
            // -----------
            // ...|...|...
            // ...|...|...
            // ...|...|...
            board.Cells[1, 5].Candidates.Add(d);
            board.Cells[2, 3].Candidates.Add(d);
            board.Cells[5, 3].Candidates.Add(d);
            board.Cells[5, 5].Candidates.Add(d);

            // Candidate to be removed (sees both roof endpoints from the same box)
            for(int a = 1; a <= 9; a++)
            {
                board.Cells[0, 4].Candidates.Add(a);                
            }

            var registry = new RuleRegistry();
            registry.Register(new SkyscraperRule());

            var (rule, result) = registry.ApplyNext(board);
            Assert.IsNotNull(rule);
            Assert.IsTrue(result.Apply);
            Assert.IsFalse(board.Cells[0, 4].Candidates.Contains(d));
        }

        [Test]
        public void Skyscraper_DoesNotTrigger_MoreThanTwoCandidates()
        {
            var board = new Board(9, 3, 3);
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    board.Cells[r, c] = new Cell(r, c);

            int d = 2;
            // Clear all other candidates for digit d to shape the pattern
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    board.Cells[r, c].Candidates.Remove(d);

            // candidates: (* are where the candidates are, that include 1 and can be removed by the skyscraper)
            // ...|...|...
            // .2.|...|.2.
            // ...|...|...
            // -----------
            // ...|...|...
            // ...|...|...
            // ...|...|...
            // -----------
            // ...|...|...
            // .2.|...|2.2 Row has too many candidates for d
            // ...|...|...
            board.Cells[1, 1].Candidates.Add(d);
            board.Cells[1, 7].Candidates.Add(d);
            board.Cells[7, 1].Candidates.Add(d);
            board.Cells[7, 6].Candidates.Add(d);
            board.Cells[7, 8].Candidates.Add(d);

            var registry = new RuleRegistry();
            registry.Register(new SkyscraperRule());

            var (rule, result) = registry.ApplyNext(board);
            Assert.IsNull(rule);
            Assert.IsFalse(result.Apply);
        }
    }
}
