using NUnit.Framework;
using Sudoku.Solver.Rules;

namespace Sudoku.Tests.Editor
{
    public class HiddenPairRuleTests
    {
        [Test]
        public void HiddenPairRule_RowHiddenPair_RemovesOtherCandidatesFromPairCells()
        {
            var board = TestHelpers.CreateEmptyBoard();

            for (int column = 0; column < board.Size; column++)
            {
                board.Cells[0, column].Candidates.Clear();
            }

            board.Cells[0, 0].Candidates.UnionWith(new[] { 1, 2, 7 });
            board.Cells[0, 4].Candidates.UnionWith(new[] { 1, 2, 8 });

            board.Cells[0, 1].Candidates.UnionWith(new[] { 3, 4 });
            board.Cells[0, 2].Candidates.UnionWith(new[] { 5, 6 });
            board.Cells[0, 3].Candidates.UnionWith(new[] { 3, 6 });
            board.Cells[0, 5].Candidates.UnionWith(new[] { 4, 5 });
            board.Cells[0, 6].Candidates.UnionWith(new[] { 3, 8 });
            board.Cells[0, 7].Candidates.UnionWith(new[] { 6, 7 });
            board.Cells[0, 8].Candidates.UnionWith(new[] { 4, 9 });

            var rule = new HiddenPairRule();

            var result = rule.CalculateChanges(board);

            Assert.IsTrue(result.Apply);
            Assert.AreEqual(2, result.Changes.Count, "Hidden Pair should trim both pair cells.");

            result.EnactCandidates(board);

            CollectionAssert.AreEquivalent(new[] { 1, 2 }, board.Cells[0, 0].Candidates);
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, board.Cells[0, 4].Candidates);
            CollectionAssert.AreEquivalent(new[] { 3, 4 }, board.Cells[0, 1].Candidates);
        }

        [Test]
        public void HiddenPairRule_WhenNoCandidatesCanBeRemoved_ReturnsNotApplied()
        {
            var board = TestHelpers.CreateEmptyBoard();

            for (int column = 0; column < board.Size; column++)
            {
                board.Cells[0, column].Candidates.Clear();
            }

            board.Cells[0, 0].Candidates.UnionWith(new[] { 1, 2 });
            board.Cells[0, 4].Candidates.UnionWith(new[] { 1, 2 });

            board.Cells[0, 1].Candidates.UnionWith(new[] { 3, 4 });
            board.Cells[0, 2].Candidates.UnionWith(new[] { 5, 6 });
            board.Cells[0, 3].Candidates.UnionWith(new[] { 3, 6 });
            board.Cells[0, 5].Candidates.UnionWith(new[] { 4, 5 });
            board.Cells[0, 6].Candidates.UnionWith(new[] { 3, 8 });
            board.Cells[0, 7].Candidates.UnionWith(new[] { 6, 7 });
            board.Cells[0, 8].Candidates.UnionWith(new[] { 4, 9 });

            var rule = new HiddenPairRule();

            Assert.IsFalse(rule.CanApply(board));
            var result = rule.CalculateChanges(board);
            Assert.IsFalse(result.Apply);
            Assert.IsEmpty(result.Changes);
        }
    }
}
