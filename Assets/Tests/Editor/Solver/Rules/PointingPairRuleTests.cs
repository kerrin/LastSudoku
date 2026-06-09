using NUnit.Framework;
using Sudoku.Solver.Rules;

namespace Sudoku.Tests.Editor
{
    public class PointingPairRuleTests
    {
        [Test]
        public void PointingPairRule_BoxCandidatesInOneRow_RemovesDigitFromRowOutsideBox()
        {
            var board = TestHelpers.CreateEmptyBoard();

            for (int row = 0; row < board.Size; row++)
            {
                for (int column = 0; column < board.Size; column++)
                {
                    board.Cells[row, column].Candidates.Clear();
                }
            }

            // Box 0 candidates for digit 5 are restricted to row 0.
            board.Cells[0, 0].Candidates.UnionWith(new[] { 5, 7 });
            board.Cells[0, 1].Candidates.UnionWith(new[] { 5, 8 });

            board.Cells[1, 0].Candidates.UnionWith(new[] { 1, 2 });
            board.Cells[1, 1].Candidates.UnionWith(new[] { 2, 3 });
            board.Cells[1, 2].Candidates.UnionWith(new[] { 3, 4 });
            board.Cells[2, 0].Candidates.UnionWith(new[] { 1, 4 });
            board.Cells[2, 1].Candidates.UnionWith(new[] { 2, 6 });
            board.Cells[2, 2].Candidates.UnionWith(new[] { 1, 9 });

            // Same row outside the box still includes digit 5 and should be trimmed.
            board.Cells[0, 3].Candidates.UnionWith(new[] { 4, 5 });
            board.Cells[0, 7].Candidates.UnionWith(new[] { 5, 6 });
            board.Cells[0, 8].Candidates.UnionWith(new[] { 6, 9 });

            var rule = new PointingPairRule();
            var result = rule.CalculateChanges(board);

            Assert.IsTrue(result.Apply);
            Assert.AreEqual(2, result.Changes.Count, "Pointing Pair should remove the digit from row cells outside the box.");

            result.EnactCandidates(board);

            CollectionAssert.DoesNotContain(board.Cells[0, 3].Candidates, 5);
            CollectionAssert.DoesNotContain(board.Cells[0, 7].Candidates, 5);
            CollectionAssert.Contains(board.Cells[0, 8].Candidates, 9);
            CollectionAssert.Contains(board.Cells[0, 0].Candidates, 5);
            CollectionAssert.Contains(board.Cells[0, 1].Candidates, 5);
        }

        [Test]
        public void PointingPairRule_WhenNoOutsideTargetsExist_ReturnsNotApplied()
        {
            var board = TestHelpers.CreateEmptyBoard();

            for (int row = 0; row < board.Size; row++)
            {
                for (int column = 0; column < board.Size; column++)
                {
                    board.Cells[row, column].Candidates.Clear();
                }
            }

            board.Cells[0, 0].Candidates.UnionWith(new[] { 5, 7 });
            board.Cells[0, 1].Candidates.UnionWith(new[] { 5, 8 });

            board.Cells[1, 0].Candidates.UnionWith(new[] { 1, 2 });
            board.Cells[1, 1].Candidates.UnionWith(new[] { 2, 3 });
            board.Cells[1, 2].Candidates.UnionWith(new[] { 3, 4 });
            board.Cells[2, 0].Candidates.UnionWith(new[] { 1, 4 });
            board.Cells[2, 1].Candidates.UnionWith(new[] { 2, 6 });
            board.Cells[2, 2].Candidates.UnionWith(new[] { 1, 9 });

            // No row cells outside box contain candidate 5.
            board.Cells[0, 3].Candidates.UnionWith(new[] { 4, 6 });
            board.Cells[0, 7].Candidates.UnionWith(new[] { 6, 8 });

            var rule = new PointingPairRule();

            Assert.IsFalse(rule.CanApply(board));
            var result = rule.CalculateChanges(board);
            Assert.IsFalse(result.Apply);
            Assert.IsEmpty(result.Changes);
        }
    }
}