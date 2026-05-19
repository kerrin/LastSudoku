using NUnit.Framework;
using Sudoku.Solver.Rules;

namespace Sudoku.Tests.Editor
{
    public class XWingRuleTests
    {
        [Test]
        public void XWing_RowBased_RemovesCandidatesInColumns()
        {
            var board = TestHelpers.CreateEmptyBoard();
            var rule = new XWingRule();

            int d = 5;
            // shape rows 0 and 2 to have candidates only at columns 0 and 3
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    board.Cells[r, c].Candidates.Clear();

            // candidates:
            // 5..|5..|...
            // 5..|...|...
            // 5..|5..|...
            // -----------
            // ...|...|...
            // .5.|...|...
            // ...|5..|...
            // -----------
            // ...|...|...
            // 5..|...|...
            // ...|...|...

            // give other arbitrary candidates so board isn't pristine
            board.Cells[0, 0].Candidates.Add(d);
            board.Cells[0, 3].Candidates.Add(d);
            board.Cells[2, 0].Candidates.Add(d);
            board.Cells[2, 3].Candidates.Add(d);

            // target to be removed: row 1 columns 0 and 3
            board.Cells[1, 0].Candidates.Add(d);
            board.Cells[5, 3].Candidates.Add(d);
            board.Cells[7, 0].Candidates.Add(d);
            // candidate to be unaffected: row 4 column 1 since it's not a candidate in the first place
            board.Cells[4, 2].Candidates.Add(d);

            var res = rule.CalculateChanges(board);
            Assert.IsTrue(res.Apply);
            // expect removals at (1,0), (5,3), (7,0)
            Assert.IsTrue(board.Cells[1, 0].Candidates.Contains(d)); // unchanged until enact
            res.EnactCandidates(board);
            Assert.IsFalse(board.Cells[1, 0].Candidates.Contains(d));
            Assert.IsFalse(board.Cells[5, 3].Candidates.Contains(d));
            Assert.IsFalse(board.Cells[7, 0].Candidates.Contains(d));
            // expect no change at (4,2) since it's not a candidate in the first place
            Assert.IsTrue(board.Cells[4, 2].Candidates.Contains(d));
        }

        [Test]
        public void XWing_ColumnBased_RemovesCandidatesInRows()
        {
            var board = TestHelpers.CreateEmptyBoard();
            var rule = new XWingRule();

            int d = 1;
            int d2 = 2;
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    board.Cells[r, c].Candidates.Clear();

            // candidates:
            // ...|...|...
            // 1.1|.1.|1.2
            // ...|...|...
            // -----------
            // ..1|...|...
            // ...|...|...
            // 11.|.1.|..1
            // -----------
            // ...|...|...
            // ...|...|...
            // ...|...|...

            // columns 0 and 4 have candidates at rows 1 and 6
            board.Cells[1, 0].Candidates.Add(d);
            board.Cells[6, 0].Candidates.Add(d);
            board.Cells[1, 4].Candidates.Add(d);
            board.Cells[6, 4].Candidates.Add(d);

            // candidate to be removed: row1 col2 and row6 col2
            board.Cells[1, 2].Candidates.Add(d);
            board.Cells[1, 6].Candidates.Add(d);
            board.Cells[6, 1].Candidates.Add(d);
            board.Cells[6, 8].Candidates.Add(d);

            // candidate to be unaffected: row4 col4 since it's not a candidate in the first place
            board.Cells[3, 2].Candidates.Add(d);
            board.Cells[1, 8].Candidates.Add(d2);

            var res = rule.CalculateChanges(board);
            Assert.IsTrue(res.Apply);
            res.EnactCandidates(board);
            // expect removals at (1,2), (1,6), (6,1), (6,8)
            Assert.IsFalse(board.Cells[1, 2].Candidates.Contains(d));
            Assert.IsFalse(board.Cells[1, 6].Candidates.Contains(d));
            Assert.IsFalse(board.Cells[6, 1].Candidates.Contains(d));
            Assert.IsFalse(board.Cells[6, 8].Candidates.Contains(d));

            // expect no change at (3,2) since it's not a candidate in the first place
            Assert.IsTrue(board.Cells[3, 2].Candidates.Contains(d));
            // expect no change at (1,8) since it's a different candidate
            Assert.IsTrue(board.Cells[1, 8].Candidates.Contains(d2));
        }

        

        [Test]
        public void XWing_BoxBased_RemovesCandidatesInBoxes()
        {
            var board = TestHelpers.CreateEmptyBoard();
            var rule = new XWingRule();

            int d = 7;
            int d2 = 9;
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    board.Cells[r, c].Candidates.Clear();

            // candidates:
            // 7..|...|..7
            // ..7|...|...
            // ...|...|..7
            // -----------
            // ...|...|...
            // ...|.7.|...
            // ...|...|...
            // -----------
            // ...|...|.7.
            // .7.|...|...
            // 7..|...|..7
            // boxes 0, 2, 6, 8 have candidates in corners
            board.Cells[0, 0].Candidates.Add(d);
            board.Cells[0, 8].Candidates.Add(d);
            board.Cells[8, 0].Candidates.Add(d);
            board.Cells[8, 8].Candidates.Add(d);

            // candidate to be removed: 
            // box0 (rows 0-2, cols 0-2)
            board.Cells[1, 2].Candidates.Add(d);
            // box2 (rows 0-2, cols 6-8)
            board.Cells[2, 8].Candidates.Add(d);
            // box6 (rows 6-8, cols 0-2)
            board.Cells[7, 1].Candidates.Add(d);
            // box8 (rows 6-8, cols 6-8)
            board.Cells[6, 7].Candidates.Add(d);            
            // box4 (rows 3-5, cols 3-5) should be unaffected since it doesn't have candidates in the first place
            board.Cells[4, 4].Candidates.Add(d);
            // candidate to be unaffected since it's a different candidate in box 0
            board.Cells[1, 8].Candidates.Add(d2);

            var res = rule.CalculateChanges(board);
            Assert.IsTrue(res.Apply);
            res.EnactCandidates(board);
            // expect removals at (1,2), (2,8), (7,1), (6,7)
            Assert.IsFalse(board.Cells[1, 2].Candidates.Contains(d));
            Assert.IsFalse(board.Cells[2, 8].Candidates.Contains(d));
            Assert.IsFalse(board.Cells[7, 1].Candidates.Contains(d));
            Assert.IsFalse(board.Cells[6, 7].Candidates.Contains(d));

            // expect no change at (4,4) since it's not a candidate in the first place
            Assert.IsTrue(board.Cells[4, 4].Candidates.Contains(d));
            // expect no change at (1,8) since it's a different candidate
            Assert.IsTrue(board.Cells[1, 8].Candidates.Contains(d2));
        }
    }
}
