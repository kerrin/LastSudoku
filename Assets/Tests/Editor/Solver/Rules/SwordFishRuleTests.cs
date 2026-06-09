using NUnit.Framework;
using Sudoku.Solver.Rules;

namespace Sudoku.Tests.Editor
{
    public class SwordFishRuleTests
    {
        [Test]
        public void SwordFish_RowBased_RemovesCandidatesInColumns()
        {
            var board = TestHelpers.CreateEmptyBoard();
            var rule = new SwordFishRule();
            int d = 6;

            for (int row = 0; row < 9; row++)
            {
                for (int column = 0; column < 9; column++)
                {
                    board.Cells[row, column].Candidates.Clear();
                }
            }

            // Swordfish rows: 0,3,6 with union columns {1,4,7}
            board.Cells[0, 1].Candidates.Add(d);
            board.Cells[0, 4].Candidates.Add(d);
            board.Cells[3, 1].Candidates.Add(d);
            board.Cells[3, 7].Candidates.Add(d);
            board.Cells[6, 4].Candidates.Add(d);
            board.Cells[6, 7].Candidates.Add(d);

            // Targets in the same three columns, outside witness rows.
            board.Cells[1, 1].Candidates.Add(d);
            board.Cells[2, 4].Candidates.Add(d);
            board.Cells[8, 7].Candidates.Add(d);

            // Unrelated candidate should remain untouched.
            board.Cells[5, 5].Candidates.Add(d);

            var result = rule.CalculateChanges(board);
            Assert.IsTrue(result.Apply);

            result.EnactCandidates(board);
            Assert.IsFalse(board.Cells[1, 1].Candidates.Contains(d));
            Assert.IsFalse(board.Cells[2, 4].Candidates.Contains(d));
            Assert.IsFalse(board.Cells[8, 7].Candidates.Contains(d));
            Assert.IsTrue(board.Cells[5, 5].Candidates.Contains(d));
        }

        [Test]
        public void SwordFish_ColumnBased_RemovesCandidatesInRows()
        {
            var board = TestHelpers.CreateEmptyBoard();
            var rule = new SwordFishRule();
            int d = 2;

            for (int row = 0; row < 9; row++)
            {
                for (int column = 0; column < 9; column++)
                {
                    board.Cells[row, column].Candidates.Clear();
                }
            }

            // Swordfish columns: 0,3,8 with union rows {1,5,7}
            board.Cells[1, 0].Candidates.Add(d);
            board.Cells[5, 0].Candidates.Add(d);
            board.Cells[1, 3].Candidates.Add(d);
            board.Cells[7, 3].Candidates.Add(d);
            board.Cells[5, 8].Candidates.Add(d);
            board.Cells[7, 8].Candidates.Add(d);

            // Targets in the same three rows, outside witness columns.
            board.Cells[1, 2].Candidates.Add(d);
            board.Cells[5, 4].Candidates.Add(d);
            board.Cells[7, 6].Candidates.Add(d);

            var result = rule.CalculateChanges(board);
            Assert.IsTrue(result.Apply);

            result.EnactCandidates(board);
            Assert.IsFalse(board.Cells[1, 2].Candidates.Contains(d));
            Assert.IsFalse(board.Cells[5, 4].Candidates.Contains(d));
            Assert.IsFalse(board.Cells[7, 6].Candidates.Contains(d));
        }

        [Test]
        public void SwordFish_WhenNoOutsideTargetsExist_ReturnsNotApplied()
        {
            var board = TestHelpers.CreateEmptyBoard();
            var rule = new SwordFishRule();
            int d = 6;

            for (int row = 0; row < 9; row++)
            {
                for (int column = 0; column < 9; column++)
                {
                    board.Cells[row, column].Candidates.Clear();
                }
            }

            // Valid witness shape, but no removable candidates outside selected rows.
            board.Cells[0, 1].Candidates.Add(d);
            board.Cells[0, 4].Candidates.Add(d);
            board.Cells[3, 1].Candidates.Add(d);
            board.Cells[3, 7].Candidates.Add(d);
            board.Cells[6, 4].Candidates.Add(d);
            board.Cells[6, 7].Candidates.Add(d);

            Assert.IsFalse(rule.CanApply(board));
            var result = rule.CalculateChanges(board);
            Assert.IsFalse(result.Apply);
            Assert.IsEmpty(result.Changes);
        }

        [Test]
        public void SwordFish_WhenAnySelectedLineHasThreeCandidates_ReturnsNotApplied()
        {
            var board = TestHelpers.CreateEmptyBoard();
            var rule = new SwordFishRule();
            int d = 6;

            for (int row = 0; row < 9; row++)
            {
                for (int column = 0; column < 9; column++)
                {
                    board.Cells[row, column].Candidates.Clear();
                }
            }

            // Looks like a fish across rows 0,3,6 and columns 1,4,7,
            // but row 0 has three candidates. Strict Swordfish must reject this.
            board.Cells[0, 1].Candidates.Add(d);
            board.Cells[0, 4].Candidates.Add(d);
            board.Cells[0, 7].Candidates.Add(d);
            board.Cells[3, 1].Candidates.Add(d);
            board.Cells[3, 7].Candidates.Add(d);
            board.Cells[6, 4].Candidates.Add(d);
            board.Cells[6, 7].Candidates.Add(d);

            // Potential removals if relaxed detection were used.
            board.Cells[1, 1].Candidates.Add(d);
            board.Cells[2, 4].Candidates.Add(d);
            board.Cells[8, 7].Candidates.Add(d);

            Assert.IsFalse(rule.CanApply(board));
            var result = rule.CalculateChanges(board);
            Assert.IsFalse(result.Apply);
            Assert.IsEmpty(result.Changes);
        }
    }
}