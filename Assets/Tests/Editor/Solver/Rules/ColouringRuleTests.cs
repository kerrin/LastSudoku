using System.Linq;
using NUnit.Framework;
using Sudoku.Solver.Rules;
using Sudoku.UI.Config;

namespace Sudoku.Tests.Editor
{
    public class ColouringRuleTests
    {
        private bool _originalGreen;
        private bool _originalAmber;
        private bool _originalRed;
        private bool _originalBlue;

        [SetUp]
        public void Setup()
        {
            _originalGreen = ColourSettings.GreenEnabled;
            _originalAmber = ColourSettings.AmberEnabled;
            _originalRed = ColourSettings.RedEnabled;
            _originalBlue = ColourSettings.BlueEnabled;

            ColourSettings.GreenEnabled = true;
            ColourSettings.AmberEnabled = true;
            ColourSettings.RedEnabled = true;
            ColourSettings.BlueEnabled = false;
        }

        [TearDown]
        public void Teardown()
        {
            ColourSettings.GreenEnabled = _originalGreen;
            ColourSettings.AmberEnabled = _originalAmber;
            ColourSettings.RedEnabled = _originalRed;
            ColourSettings.BlueEnabled = _originalBlue;
        }

        [Test]
        public void ColouringRule_SameColourContradiction_RemovesContradictingColourCandidates()
        {
            var board = TestHelpers.CreateEmptyBoard();
            var rule = new ColouringRule();
            const int digit = 5;

            ClearAllCandidates(board);

            // Minimal valid chain:
            // r1c1 - r1c2 (row), r1c2 - r2c2 (column)
            // Colors r1c1 and r2c2 the same, and they contradict in box 1.
            // No node sees more than two same-digit peers.
            board.Cells[0, 0].Candidates.Add(digit); // r1c1
            board.Cells[0, 1].Candidates.Add(digit); // r1c2
            board.Cells[1, 1].Candidates.Add(digit); // r2c2

            var result = rule.CalculateChanges(board);
            Assert.IsTrue(result.Apply);

            var removed = result.Changes
                .Select(c => (c.Row, c.Column))
                .OrderBy(x => x.Row)
                .ThenBy(x => x.Column)
                .ToList();

            CollectionAssert.AreEquivalent(new[] { (0, 0), (1, 1) }, removed);
            Assert.IsTrue(result.UsedCells.Any(u => u.HighlightTag == "Failure" && u.Candidate == digit));
            Assert.IsTrue(result.UsedCells.Any(u => u.HighlightTag == "Deduction" && u.Row == 0 && u.Column == 0 && u.Candidate == digit));
            Assert.IsTrue(result.UsedCells.Any(u => u.HighlightTag == "Deduction" && u.Row == 1 && u.Column == 1 && u.Candidate == digit));

            result.EnactCandidates(board);
            Assert.IsFalse(board.Cells[0, 0].Candidates.Contains(digit));
            Assert.IsFalse(board.Cells[1, 1].Candidates.Contains(digit));
            Assert.IsTrue(board.Cells[0, 1].Candidates.Contains(digit));
        }

        [Test]
        public void ColouringRule_TwoColourIntersection_IsInconclusive_WhenNoContradictionExists()
        {
            var board = TestHelpers.CreateEmptyBoard();
            var rule = new ColouringRule();
            const int digit = 7;

            ClearAllCandidates(board);

            // Chain with no same-colour contradiction:
            // r1c1 - r1c2 (row), r1c2 - r4c2 (column), r4c2 - r4c3 (row)
            board.Cells[0, 0].Candidates.Add(digit);
            board.Cells[0, 1].Candidates.Add(digit);
            board.Cells[3, 1].Candidates.Add(digit);
            board.Cells[3, 2].Candidates.Add(digit);

            // Target sees both colours (box + column visibility).
            board.Cells[1, 2].Candidates.Add(digit);

            // Keep column 3 from becoming a strong link for the target.
            board.Cells[8, 2].Candidates.Add(digit);

            var result = rule.CalculateChanges(board);
            Assert.IsFalse(result.Apply);
            Assert.IsEmpty(result.Changes);

            result.EnactCandidates(board);
            Assert.IsTrue(board.Cells[1, 2].Candidates.Contains(digit));
            Assert.IsTrue(board.Cells[8, 2].Candidates.Contains(digit));
        }

        [Test]
        public void ColouringRule_NoElimination_ReturnsNotApplied()
        {
            var board = TestHelpers.CreateEmptyBoard();
            var rule = new ColouringRule();
            const int digit = 9;

            ClearAllCandidates(board);

            // A single strong link by itself is insufficient for an elimination.
            board.Cells[0, 0].Candidates.Add(digit);
            board.Cells[0, 1].Candidates.Add(digit);

            var result = rule.CalculateChanges(board);
            Assert.IsFalse(result.Apply);
            Assert.IsEmpty(result.Changes);
        }

        [Test]
        public void ColouringRule_WhenNodeSeesMoreThanTwoComponentPeers_IsInvalidAndDoesNotApply()
        {
            var board = TestHelpers.CreateEmptyBoard();
            var rule = new ColouringRule();
            const int digit = 4;

            ClearAllCandidates(board);

            // Base chain:
            // r1c1 - r1c2 (row), r1c2 - r2c2 (column), r2c2 - r2c3 (row), r2c3 - r3c3 (column)
            // Extra topology makes r1c2 see three same-digit nodes in the component (r1c1, r2c2, r3c3),
            // which must invalidate the chain under the clarified rule.
            board.Cells[0, 0].Candidates.Add(digit); // r1c1
            board.Cells[0, 1].Candidates.Add(digit); // r1c2
            board.Cells[1, 1].Candidates.Add(digit); // r2c2
            board.Cells[1, 2].Candidates.Add(digit); // r2c3
            board.Cells[2, 2].Candidates.Add(digit); // r3c3

            var result = rule.CalculateChanges(board);
            Assert.IsFalse(result.Apply);
            Assert.IsEmpty(result.Changes);
        }

        [Test]
        public void ColouringRule_WhenStrongLinkComponentIsOddCycle_IsInvalidAndDoesNotApply()
        {
            var board = TestHelpers.CreateEmptyBoard();
            var rule = new ColouringRule();
            const int digit = 6;

            ClearAllCandidates(board);

            // Build a 5-cycle in the strong-link graph:
            // r1c1-r4c1 (col1), r4c1-r4c5 (row4), r4c5-r3c5 (col5), r3c5-r3c3 (row3), r3c3-r1c1 (box1)
            // This is non-bipartite and must be rejected.
            board.Cells[0, 0].Candidates.Add(digit); // r1c1
            board.Cells[3, 0].Candidates.Add(digit); // r4c1
            board.Cells[3, 4].Candidates.Add(digit); // r4c5
            board.Cells[2, 4].Candidates.Add(digit); // r3c5
            board.Cells[2, 2].Candidates.Add(digit); // r3c3

            var result = rule.CalculateChanges(board);
            Assert.IsFalse(result.Apply);
            Assert.IsEmpty(result.Changes);
        }

        [Test]
        public void ColouringRule_WhenFewerThanTwoColoursEnabled_IsDisabled()
        {
            var board = TestHelpers.CreateEmptyBoard();
            var rule = new ColouringRule();
            const int digit = 5;

            ClearAllCandidates(board);
            board.Cells[0, 0].Candidates.Add(digit);
            board.Cells[0, 1].Candidates.Add(digit);
            board.Cells[1, 1].Candidates.Add(digit);
            board.Cells[1, 2].Candidates.Add(digit);

            ColourSettings.GreenEnabled = true;
            ColourSettings.AmberEnabled = false;
            ColourSettings.RedEnabled = false;
            ColourSettings.BlueEnabled = false;

            Assert.IsFalse(rule.CanApply(board));
            var result = rule.CalculateChanges(board);
            Assert.IsFalse(result.Apply);
            Assert.IsEmpty(result.Changes);
        }

        /**
         * Remove all candidates from all cells so tests can author exact patterns.
         *
         * @param board Target board.
         */
        private static void ClearAllCandidates(Sudoku.Models.Board board)
        {
            for (int row = 0; row < board.Size; row++)
            {
                for (int column = 0; column < board.Size; column++)
                {
                    board.Cells[row, column].Candidates.Clear();
                }
            }
        }
    }
}
