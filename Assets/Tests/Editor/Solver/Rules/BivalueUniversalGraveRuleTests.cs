using NUnit.Framework;
using Sudoku.Models;
using Sudoku.Solver.Rules;

namespace Sudoku.Tests.Editor
{
    public class BivalueUniversalGraveRuleTests
    {
        [Test]
        public void BugPlusOne_StatePlacesEightAtRowEightColumnEight()
        {
            Board board = CreateBoardState();
            var rule = new BivalueUniversalGraveRule();

            Assert.IsTrue(rule.CanApply(board));
            RuleResult result = rule.CalculateChanges(board);

            Assert.IsTrue(result.Apply);
            Assert.AreEqual(1, result.Changes.Count);
            Assert.AreEqual(7, result.Changes[0].Row);
            Assert.AreEqual(7, result.Changes[0].Column);
            Assert.AreEqual(8, result.Changes[0].NewValue);

            result.EnactAll(board);
            Assert.AreEqual(8, board.Cells[7, 7].Value);
        }

        [Test]
        public void BugPlusOne_DoesNotApplyWhenAnotherCellIsNotBivalue()
        {
            Board board = CreateBoardState();
            board.Cells[0, 3].Candidates.Add(6);

            RuleResult result = new BivalueUniversalGraveRule().CalculateChanges(board);

            Assert.IsFalse(result.Apply);
            Assert.AreEqual(0, result.Changes.Count);
        }

        [Test]
        public void BugPlusOne_DoesNotApplyWhenCandidateCountsInAHouseAreNotTwoOrThree()
        {
            Board board = CreateBoardState();
            SetCandidates(board, 0, 3, 2, 6);

            RuleResult result = new BivalueUniversalGraveRule().CalculateChanges(board);

            Assert.IsFalse(result.Apply);
            Assert.AreEqual(0, result.Changes.Count);
        }

        [Test]
        public void BugPlusOne_DoesNotApplyWhenTwoCandidatesAppearThreeTimesInTargetHouse()
        {
            Board board = CreateBoardState();
            SetCandidates(board, 7, 7, 2, 7, 8);

            RuleResult result = new BivalueUniversalGraveRule().CalculateChanges(board);

            Assert.IsFalse(result.Apply);
            Assert.AreEqual(0, result.Changes.Count);
        }

        private static Board CreateBoardState()
        {
            Board board = TestHelpers.CreateEmptyBoard();
            int?[,] values =
            {
                { 4, 8, 3, null, 1, null, null, 5, null },
                { 5, 1, 6, 4, null, 3, null, null, 9 },
                { 7, 9, 2, 5, 8, 6, 1, 3, 4 },
                { 2, 6, 8, 7, 9, 4, 5, 1, 3 },
                { 9, 3, 5, 8, 6, 1, 2, 4, 7 },
                { 1, 7, 4, 3, 5, 2, 9, null, null },
                { 8, 2, 7, 6, 4, 5, 3, 9, 1 },
                { 3, 4, 9, 1, null, null, null, null, 5 },
                { 6, 5, 1, null, 3, null, 4, 7, null }
            };

            for (int row = 0; row < board.Size; row++)
            {
                for (int column = 0; column < board.Size; column++)
                {
                    Cell cell = board.Cells[row, column];
                    cell.Value = values[row, column];
                    cell.Candidates.Clear();
                }
            }

            SetCandidates(board, 0, 3, 2, 9);
            SetCandidates(board, 0, 5, 7, 9);
            SetCandidates(board, 0, 6, 6, 7);
            SetCandidates(board, 0, 8, 2, 6);
            SetCandidates(board, 1, 4, 2, 7);
            SetCandidates(board, 1, 6, 7, 8);
            SetCandidates(board, 1, 7, 2, 8);
            SetCandidates(board, 5, 7, 6, 8);
            SetCandidates(board, 5, 8, 6, 8);
            SetCandidates(board, 7, 4, 2, 7);
            SetCandidates(board, 7, 5, 7, 8);
            SetCandidates(board, 7, 6, 6, 8);
            SetCandidates(board, 7, 7, 2, 6, 8);
            SetCandidates(board, 8, 3, 2, 9);
            SetCandidates(board, 8, 5, 8, 9);
            SetCandidates(board, 8, 8, 2, 8);
            return board;
        }

        private static void SetCandidates(Board board, int row, int column, params int[] candidates)
        {
            Cell cell = board.Cells[row, column];
            cell.Value = null;
            cell.Candidates.Clear();
            cell.Candidates.UnionWith(candidates);
        }
    }
}
