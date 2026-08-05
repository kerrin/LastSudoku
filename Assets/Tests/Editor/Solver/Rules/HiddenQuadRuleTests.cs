using NUnit.Framework;
using Sudoku.Solver.Rules;

namespace Sudoku.Tests.Editor
{
    public class HiddenQuadRuleTests
    {
        [Test]
        public void HiddenQuadRule_RowHiddenQuad_RemovesOtherCandidatesFromQuadCells()
        {
            var board = TestHelpers.CreateEmptyBoard();

            for (int column = 0; column < board.Size; column++)
            {
                board.Cells[0, column].Candidates.Clear();
            }

            // Digits 1,2,3,4 are collectively confined to four cells.
            board.Cells[0, 0].Candidates.UnionWith(new[] { 1, 2, 5 });
            board.Cells[0, 2].Candidates.UnionWith(new[] { 1, 3, 6 });
            board.Cells[0, 5].Candidates.UnionWith(new[] { 2, 4, 7 });
            board.Cells[0, 8].Candidates.UnionWith(new[] { 3, 4, 8 });

            board.Cells[0, 1].Candidates.UnionWith(new[] { 5, 6 });
            board.Cells[0, 3].Candidates.UnionWith(new[] { 6, 7 });
            board.Cells[0, 4].Candidates.UnionWith(new[] { 7, 8 });
            board.Cells[0, 6].Candidates.UnionWith(new[] { 8, 9 });
            board.Cells[0, 7].Candidates.UnionWith(new[] { 5, 9 });

            var rule = new HiddenQuadRule();

            var result = rule.CalculateChanges(board);

            Assert.IsTrue(result.Apply);
            Assert.AreEqual(4, result.Changes.Count, "Hidden Quad should trim all four quad cells.");

            result.EnactCandidates(board);

            CollectionAssert.AreEquivalent(new[] { 1, 2 }, board.Cells[0, 0].Candidates);
            CollectionAssert.AreEquivalent(new[] { 1, 3 }, board.Cells[0, 2].Candidates);
            CollectionAssert.AreEquivalent(new[] { 2, 4 }, board.Cells[0, 5].Candidates);
            CollectionAssert.AreEquivalent(new[] { 3, 4 }, board.Cells[0, 8].Candidates);
            CollectionAssert.AreEquivalent(new[] { 5, 6 }, board.Cells[0, 1].Candidates);
        }

        [Test]
        public void HiddenQuadRule_WhenNoCandidatesCanBeRemoved_ReturnsNotApplied()
        {
            var board = TestHelpers.CreateEmptyBoard();

            for (int column = 0; column < board.Size; column++)
            {
                board.Cells[0, column].Candidates.Clear();
            }

            board.Cells[0, 0].Candidates.UnionWith(new[] { 1, 2 });
            board.Cells[0, 2].Candidates.UnionWith(new[] { 1, 3 });
            board.Cells[0, 5].Candidates.UnionWith(new[] { 2, 4 });
            board.Cells[0, 8].Candidates.UnionWith(new[] { 3, 4 });

            board.Cells[0, 1].Candidates.UnionWith(new[] { 5, 6 });
            board.Cells[0, 3].Candidates.UnionWith(new[] { 6, 7 });
            board.Cells[0, 4].Candidates.UnionWith(new[] { 7, 8 });
            board.Cells[0, 6].Candidates.UnionWith(new[] { 8, 9 });
            board.Cells[0, 7].Candidates.UnionWith(new[] { 5, 9 });

            var rule = new HiddenQuadRule();

            Assert.IsFalse(rule.CanApply(board));
            var result = rule.CalculateChanges(board);
            Assert.IsFalse(result.Apply);
            Assert.IsEmpty(result.Changes);
        }
    }
}
