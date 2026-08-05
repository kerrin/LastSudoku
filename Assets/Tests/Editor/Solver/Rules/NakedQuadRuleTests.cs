using NUnit.Framework;
using Sudoku.Solver.Rules;

namespace Sudoku.Tests.Editor
{
    public class NakedQuadRuleTests
    {
        [Test]
        public void NakedQuadRule_RowNakedQuad_RemovesQuadCandidatesFromOtherCells()
        {
            var board = TestHelpers.CreateEmptyBoard();

            for (int column = 0; column < board.Size; column++)
            {
                board.Cells[0, column].Candidates.Clear();
            }

            board.Cells[0, 0].Candidates.UnionWith(new[] { 1, 2 });
            board.Cells[0, 2].Candidates.UnionWith(new[] { 1, 3 });
            board.Cells[0, 4].Candidates.UnionWith(new[] { 2, 4 });
            board.Cells[0, 6].Candidates.UnionWith(new[] { 3, 4 });

            board.Cells[0, 1].Candidates.UnionWith(new[] { 1, 5, 6 });
            board.Cells[0, 3].Candidates.UnionWith(new[] { 2, 7, 8 });
            board.Cells[0, 5].Candidates.UnionWith(new[] { 3, 4, 9 });
            board.Cells[0, 7].Candidates.UnionWith(new[] { 1, 2, 3, 4, 8 });
            board.Cells[0, 8].Candidates.UnionWith(new[] { 5, 6, 7 });

            var rule = new NakedQuadRule();
            var result = rule.CalculateChanges(board);

            Assert.IsTrue(result.Apply);
            Assert.AreEqual(4, result.Changes.Count, "Naked Quad should remove quad digits from all affected row cells.");

            result.EnactCandidates(board);

            CollectionAssert.AreEquivalent(new[] { 1, 2 }, board.Cells[0, 0].Candidates);
            CollectionAssert.AreEquivalent(new[] { 1, 3 }, board.Cells[0, 2].Candidates);
            CollectionAssert.AreEquivalent(new[] { 2, 4 }, board.Cells[0, 4].Candidates);
            CollectionAssert.AreEquivalent(new[] { 3, 4 }, board.Cells[0, 6].Candidates);
            CollectionAssert.AreEquivalent(new[] { 5, 6 }, board.Cells[0, 1].Candidates);
            CollectionAssert.AreEquivalent(new[] { 7, 8 }, board.Cells[0, 3].Candidates);
            CollectionAssert.AreEquivalent(new[] { 9 }, board.Cells[0, 5].Candidates);
            CollectionAssert.AreEquivalent(new[] { 8 }, board.Cells[0, 7].Candidates);
            CollectionAssert.AreEquivalent(new[] { 5, 6, 7 }, board.Cells[0, 8].Candidates);
        }

        [Test]
        public void NakedQuadRule_WhenNoEliminationsExist_ReturnsNotApplied()
        {
            var board = TestHelpers.CreateEmptyBoard();

            for (int column = 0; column < board.Size; column++)
            {
                board.Cells[0, column].Candidates.Clear();
            }

            board.Cells[0, 0].Candidates.UnionWith(new[] { 1, 2 });
            board.Cells[0, 2].Candidates.UnionWith(new[] { 1, 3 });
            board.Cells[0, 4].Candidates.UnionWith(new[] { 2, 4 });
            board.Cells[0, 6].Candidates.UnionWith(new[] { 3, 4 });

            // Keep non-quad cells outside the quad digits (1..4) so there is no elimination.
            // Use 5 candidates to ensure they cannot be selected as part of any naked-quad subset.
            board.Cells[0, 1].Candidates.UnionWith(new[] { 5, 6, 7, 8, 9 });
            board.Cells[0, 3].Candidates.UnionWith(new[] { 5, 6, 7, 8, 9 });
            board.Cells[0, 5].Candidates.UnionWith(new[] { 5, 6, 7, 8, 9 });
            board.Cells[0, 7].Candidates.UnionWith(new[] { 5, 6, 7, 8, 9 });
            board.Cells[0, 8].Candidates.UnionWith(new[] { 5, 6, 7, 8, 9 });

            var rule = new NakedQuadRule();

            Assert.IsFalse(rule.CanApply(board));
            var result = rule.CalculateChanges(board);
            Assert.IsFalse(result.Apply);
            Assert.IsEmpty(result.Changes);
        }
    }
}
