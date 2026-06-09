using NUnit.Framework;
using Sudoku.Solver.Rules;

namespace Sudoku.Tests.Editor
{
    public class HiddenTripleRuleTests
    {
        [Test]
        public void HiddenTripleRule_RowHiddenTriple_RemovesOtherCandidatesFromTripleCells()
        {
            var board = TestHelpers.CreateEmptyBoard();

            for (int column = 0; column < board.Size; column++)
            {
                board.Cells[0, column].Candidates.Clear();
            }

            board.Cells[0, 0].Candidates.UnionWith(new[] { 1, 2, 3, 7 });
            board.Cells[0, 4].Candidates.UnionWith(new[] { 1, 2, 3, 8 });
            board.Cells[0, 8].Candidates.UnionWith(new[] { 1, 2, 3, 9 });

            board.Cells[0, 1].Candidates.UnionWith(new[] { 4, 5 });
            board.Cells[0, 2].Candidates.UnionWith(new[] { 4, 6 });
            board.Cells[0, 3].Candidates.UnionWith(new[] { 5, 6 });
            board.Cells[0, 5].Candidates.UnionWith(new[] { 4, 5, 6 });
            board.Cells[0, 6].Candidates.UnionWith(new[] { 4, 8 });
            board.Cells[0, 7].Candidates.UnionWith(new[] { 5, 9 });

            var rule = new HiddenTripleRule();

            var result = rule.CalculateChanges(board);

            Assert.IsTrue(result.Apply);
            Assert.AreEqual(3, result.Changes.Count, "Hidden Triple should trim all three triple cells.");

            result.EnactCandidates(board);

            CollectionAssert.AreEquivalent(new[] { 1, 2, 3 }, board.Cells[0, 0].Candidates);
            CollectionAssert.AreEquivalent(new[] { 1, 2, 3 }, board.Cells[0, 4].Candidates);
            CollectionAssert.AreEquivalent(new[] { 1, 2, 3 }, board.Cells[0, 8].Candidates);
            CollectionAssert.AreEquivalent(new[] { 4, 5 }, board.Cells[0, 1].Candidates);
        }

        [Test]
        public void HiddenTripleRule_WhenNoCandidatesCanBeRemoved_ReturnsNotApplied()
        {
            var board = TestHelpers.CreateEmptyBoard();

            for (int column = 0; column < board.Size; column++)
            {
                board.Cells[0, column].Candidates.Clear();
            }

            board.Cells[0, 0].Candidates.UnionWith(new[] { 1, 2, 3 });
            board.Cells[0, 4].Candidates.UnionWith(new[] { 1, 2, 3 });
            board.Cells[0, 8].Candidates.UnionWith(new[] { 1, 2, 3 });

            board.Cells[0, 1].Candidates.UnionWith(new[] { 4, 5 });
            board.Cells[0, 2].Candidates.UnionWith(new[] { 4, 6 });
            board.Cells[0, 3].Candidates.UnionWith(new[] { 5, 6 });
            board.Cells[0, 5].Candidates.UnionWith(new[] { 4, 5, 6 });
            board.Cells[0, 6].Candidates.UnionWith(new[] { 4, 8 });
            board.Cells[0, 7].Candidates.UnionWith(new[] { 5, 9 });

            var rule = new HiddenTripleRule();

            Assert.IsFalse(rule.CanApply(board));
            var result = rule.CalculateChanges(board);
            Assert.IsFalse(result.Apply);
            Assert.IsEmpty(result.Changes);
        }
    }
}