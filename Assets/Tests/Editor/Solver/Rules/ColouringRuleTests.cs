using System.Linq;
using NUnit.Framework;
using Sudoku.Models;
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
        public void ColouringRule_PuzzleCodeRegression_TriggersTwoColourElimination_WithExpectedHighlights()
        {
            var board = PuzzleCodeGenerator.DecodeBoardFromCode("PexgJNCqJyeY81q73gz6Ptf1PxzMPJNMpvWpWFq0jwDy3r");
            var rule = new ColouringRule();
            Assert.IsNotNull(board);
            RecomputeCandidates(board);

            var result = rule.CalculateChanges(board);
            Assert.IsTrue(result.Apply);
            Assert.AreEqual(4, result.Changes.Count);
            StringAssert.Contains("removed 5", result.Description);

            var changedCells = result.Changes
                .Select(change => (change.Row, change.Column, Removed: change.RemovedCandidates.ToArray()))
                .ToList();

            Assert.IsTrue(changedCells.Any(change => change.Row == 4 && change.Column == 0 && change.Removed.Length == 1 && change.Removed[0] == 5));
            Assert.IsTrue(changedCells.Any(change => change.Row == 5 && change.Column == 0 && change.Removed.Length == 1 && change.Removed[0] == 5));
            Assert.IsTrue(changedCells.Any(change => change.Row == 6 && change.Column == 5 && change.Removed.Length == 1 && change.Removed[0] == 5));
            Assert.IsTrue(changedCells.Any(change => change.Row == 8 && change.Column == 5 && change.Removed.Length == 1 && change.Removed[0] == 5));

            Assert.AreEqual(4, result.UsedCells.Count(used => used.HighlightTag == "Deduction" && used.Candidate == 5));
            Assert.GreaterOrEqual(result.UsedCells.Count(used => used.HighlightTag == "TargetA" && used.Candidate == 5), 1);
            Assert.GreaterOrEqual(result.UsedCells.Count(used => used.HighlightTag == "TargetB" && used.Candidate == 5), 1);
            Assert.IsFalse(result.UsedCells.Any(used => used.HighlightTag == "Failure"));

            result.EnactCandidates(board);
            Assert.IsFalse(board.Cells[4, 0].Candidates.Contains(5));
            Assert.IsFalse(board.Cells[5, 0].Candidates.Contains(5));
            Assert.IsFalse(board.Cells[6, 5].Candidates.Contains(5));
            Assert.IsFalse(board.Cells[8, 5].Candidates.Contains(5));
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

        /**
         * Recompute legal candidates from solved values.
         *
         * @param board Target board.
         */
        private static void RecomputeCandidates(Board board)
        {
            for (int row = 0; row < board.Size; row++)
            {
                for (int column = 0; column < board.Size; column++)
                {
                    var cell = board.Cells[row, column];
                    cell.Candidates.Clear();
                    if (cell.Value.HasValue)
                    {
                        continue;
                    }

                    for (int digit = 1; digit <= board.Size; digit++)
                    {
                        cell.Candidates.Add(digit);
                    }

                    foreach (var peer in board.GetPeers(cell))
                    {
                        if (peer != null && peer.Value.HasValue)
                        {
                            cell.Candidates.Remove(peer.Value.Value);
                        }
                    }
                }
            }
        }
    }
}
