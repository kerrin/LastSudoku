using NUnit.Framework;
using Sudoku.Solver.Rules;

namespace Sudoku.Tests.Editor
{
    public class NakedTripleRuleTests
    {
        [Test]
        public void NakedTripleRule_RowNakedTriple_RemovesTripleCandidatesFromOtherCells()
        {
            var board = TestHelpers.CreateEmptyBoard();

            for (int column = 0; column < board.Size; column++)
            {
                board.Cells[0, column].Candidates.Clear();
            }

            board.Cells[0, 0].Candidates.UnionWith(new[] { 1, 2 });
            board.Cells[0, 4].Candidates.UnionWith(new[] { 1, 3 });
            board.Cells[0, 8].Candidates.UnionWith(new[] { 2, 3 });

            board.Cells[0, 1].Candidates.UnionWith(new[] { 1, 4, 5 });
            board.Cells[0, 2].Candidates.UnionWith(new[] { 2, 6, 7 });
            board.Cells[0, 3].Candidates.UnionWith(new[] { 3, 4, 8 });
            board.Cells[0, 5].Candidates.UnionWith(new[] { 1, 2, 3, 9 });
            board.Cells[0, 6].Candidates.UnionWith(new[] { 4, 6 });
            board.Cells[0, 7].Candidates.UnionWith(new[] { 5, 7 });

            var rule = new NakedTripleRule();
            var result = rule.CalculateChanges(board);

            Assert.IsTrue(result.Apply);
            Assert.AreEqual(4, result.Changes.Count, "Naked Triple should remove triple digits from all affected row cells.");

            result.EnactCandidates(board);

            CollectionAssert.AreEquivalent(new[] { 1, 2 }, board.Cells[0, 0].Candidates);
            CollectionAssert.AreEquivalent(new[] { 1, 3 }, board.Cells[0, 4].Candidates);
            CollectionAssert.AreEquivalent(new[] { 2, 3 }, board.Cells[0, 8].Candidates);
            CollectionAssert.AreEquivalent(new[] { 4, 5 }, board.Cells[0, 1].Candidates);
            CollectionAssert.AreEquivalent(new[] { 6, 7 }, board.Cells[0, 2].Candidates);
            CollectionAssert.AreEquivalent(new[] { 4, 8 }, board.Cells[0, 3].Candidates);
            CollectionAssert.AreEquivalent(new[] { 9 }, board.Cells[0, 5].Candidates);
            CollectionAssert.AreEquivalent(new[] { 4, 6 }, board.Cells[0, 6].Candidates);
        }

        [Test]
        public void NakedTripleRule_WhenNoEliminationsExist_ReturnsNotApplied()
        {
            var board = TestHelpers.CreateEmptyBoard();

            for (int column = 0; column < board.Size; column++)
            {
                board.Cells[0, column].Candidates.Clear();
            }

            board.Cells[0, 0].Candidates.UnionWith(new[] { 1, 2 });
            board.Cells[0, 4].Candidates.UnionWith(new[] { 1, 3 });
            board.Cells[0, 8].Candidates.UnionWith(new[] { 2, 3 });

            board.Cells[0, 1].Candidates.UnionWith(new[] { 4, 5 });
            board.Cells[0, 2].Candidates.UnionWith(new[] { 6, 7 });
            board.Cells[0, 3].Candidates.UnionWith(new[] { 4, 8 });
            board.Cells[0, 5].Candidates.UnionWith(new[] { 8, 9 });
            board.Cells[0, 6].Candidates.UnionWith(new[] { 4, 6 });
            board.Cells[0, 7].Candidates.UnionWith(new[] { 5, 7 });

            var rule = new NakedTripleRule();

            Assert.IsFalse(rule.CanApply(board));
            var result = rule.CalculateChanges(board);
            Assert.IsFalse(result.Apply);
            Assert.IsEmpty(result.Changes);
        }
    }
}
