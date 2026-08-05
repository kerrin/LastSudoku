using NUnit.Framework;
using Sudoku.Solver.Rules;

namespace Sudoku.Tests.Editor
{
    public class NakedPairRuleTests
    {
        [Test]
        public void NakedPairRule_RowNakedPair_RemovesPairCandidatesFromOtherCells()
        {
            var board = TestHelpers.CreateEmptyBoard();

            for (int column = 0; column < board.Size; column++)
            {
                board.Cells[0, column].Candidates.Clear();
            }

            board.Cells[0, 0].Candidates.UnionWith(new[] { 1, 2 });
            board.Cells[0, 4].Candidates.UnionWith(new[] { 1, 2 });

            board.Cells[0, 1].Candidates.UnionWith(new[] { 1, 3, 5 });
            board.Cells[0, 2].Candidates.UnionWith(new[] { 2, 4, 6 });
            board.Cells[0, 3].Candidates.UnionWith(new[] { 1, 2, 7 });
            board.Cells[0, 5].Candidates.UnionWith(new[] { 3, 4, 8 });
            board.Cells[0, 6].Candidates.UnionWith(new[] { 2, 9 });
            board.Cells[0, 7].Candidates.UnionWith(new[] { 4, 8 });
            board.Cells[0, 8].Candidates.UnionWith(new[] { 1, 6, 9 });

            var rule = new NakedPairRule();
            var result = rule.CalculateChanges(board);

            Assert.IsTrue(result.Apply);
            Assert.AreEqual(5, result.Changes.Count, "Naked Pair should remove pair digits from all other row cells containing them.");

            result.EnactCandidates(board);

            CollectionAssert.AreEquivalent(new[] { 1, 2 }, board.Cells[0, 0].Candidates);
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, board.Cells[0, 4].Candidates);
            CollectionAssert.AreEquivalent(new[] { 3, 5 }, board.Cells[0, 1].Candidates);
            CollectionAssert.AreEquivalent(new[] { 4, 6 }, board.Cells[0, 2].Candidates);
            CollectionAssert.AreEquivalent(new[] { 7 }, board.Cells[0, 3].Candidates);
            CollectionAssert.AreEquivalent(new[] { 9 }, board.Cells[0, 6].Candidates);
            CollectionAssert.AreEquivalent(new[] { 6, 9 }, board.Cells[0, 8].Candidates);
        }

        [Test]
        public void NakedPairRule_WhenNoEliminationsExist_ReturnsNotApplied()
        {
            var board = TestHelpers.CreateEmptyBoard();

            for (int column = 0; column < board.Size; column++)
            {
                board.Cells[0, column].Candidates.Clear();
            }

            board.Cells[0, 0].Candidates.UnionWith(new[] { 1, 2 });
            board.Cells[0, 4].Candidates.UnionWith(new[] { 1, 2 });

            board.Cells[0, 1].Candidates.UnionWith(new[] { 3, 5 });
            board.Cells[0, 2].Candidates.UnionWith(new[] { 4, 6 });
            board.Cells[0, 3].Candidates.UnionWith(new[] { 7, 8 });
            board.Cells[0, 5].Candidates.UnionWith(new[] { 3, 4, 8 });
            board.Cells[0, 6].Candidates.UnionWith(new[] { 5, 9 });
            board.Cells[0, 7].Candidates.UnionWith(new[] { 4, 8 });
            board.Cells[0, 8].Candidates.UnionWith(new[] { 6, 9 });

            var rule = new NakedPairRule();

            Assert.IsFalse(rule.CanApply(board));
            var result = rule.CalculateChanges(board);
            Assert.IsFalse(result.Apply);
            Assert.IsEmpty(result.Changes);
        }
    }
}
