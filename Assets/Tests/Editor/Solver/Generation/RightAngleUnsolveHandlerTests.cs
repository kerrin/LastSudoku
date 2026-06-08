using System;
using System.Collections.Generic;
using NUnit.Framework;
using Sudoku.Models;
using Sudoku.Solver.Rules;
using Sudoku.Solver.Unsolver;

namespace Sudoku.Tests.Unsolver
{
    public class RightAngleUnsolveHandlerTests
    {
        private class TestBoardRow
        {
            public int?[] Values { get; set; }
            public List<int>[] Candidates { get; set; }
        }

        private static TestBoardRow MakeRow(params object[] cells)
        {
            if (cells == null)
            {
                throw new ArgumentNullException(nameof(cells));
            }

            if (cells.Length != 9)
            {
                throw new ArgumentException("Exactly 9 cells required", nameof(cells));
            }

            var row = new TestBoardRow
            {
                Values = new int?[9],
                Candidates = new List<int>[9]
            };

            for (int i = 0; i < 9; i++)
            {
                row.Candidates[i] = new List<int>();
                var cell = cells[i];
                if (cell == null)
                {
                    continue;
                }

                if (cell is int value)
                {
                    row.Values[i] = value;
                    continue;
                }

                if (cell is IEnumerable<int> sequence)
                {
                    row.Candidates[i] = new List<int>(sequence);
                    continue;
                }

                throw new ArgumentException($"Unsupported cell type at index {i}: {cell.GetType()}");
            }

            return row;
        }

        private static readonly List<TestBoardRow> RightAngleFixture = new List<TestBoardRow>
        {
            MakeRow(8,                      new[] { 1, 2, 3, 4, 9 },           new[] { 1, 2, 3, 4, 9 },           new[] { 1, 2, 3, 4 },         new[] { 1, 3, 4, 9 },         new[] { 1, 2, 4, 9 },         new[] { 1, 2, 3, 4, 9 },           5,                      6),
            MakeRow(new[] { 1, 2, 3, 4, 5, 7, 9 },   new[] { 1, 2, 3, 4, 5, 7, 8, 9 },     new[] { 1, 2, 3, 4, 5, 7, 8, 9 },     new[] { 1, 2, 3, 4, 5, 7 },     8,                      6,                      new[] { 1, 2, 3, 4, 5, 7, 8, 9 },     new[] { 1, 2, 3, 4, 7, 8, 9 },   new[] { 1, 2, 3, 4, 5, 7, 8, 9 }),
            MakeRow(new[] { 1, 4, 5, 6, 7, 8, 9 },   new[] { 1, 4, 5, 6, 7, 8, 9 },       new[] { 1, 4, 5, 6, 7, 8, 9 },       new[] { 1, 4, 5, 6, 7 },       2,                      3,                      new[] { 1, 4, 5, 6, 7, 8, 9 },       new[] { 1, 4, 6, 7, 8, 9 },     new[] { 1, 4, 5, 7, 8, 9 }),
            MakeRow(new[] { 1, 2, 3, 4, 5, 6, 7, 9 }, new[] { 1, 2, 3, 4, 5, 6, 8, 7, 8, 9 }, new[] { 1, 2, 3, 4, 5, 6, 8, 7, 8, 9 }, new[] { 1, 2, 3, 4, 5, 6, 7, 8 }, new[] { 1, 3, 4, 5, 6, 7, 8, 9 }, new[] { 1, 2, 4, 5, 8, 7, 9 },   new[] { 1, 2, 3, 4, 5, 6, 8, 7, 9 },   new[] { 1, 2, 3, 4, 6, 8, 7, 9 }, new[] { 1, 2, 3, 4, 5, 7, 8, 9 }),
            MakeRow(new[] { 1, 2, 3, 4, 5, 6, 7, 9 }, new[] { 1, 2, 3, 4, 5, 6, 8, 7, 8, 9 }, new[] { 1, 2, 3, 4, 5, 6, 8, 7, 8, 9 }, new[] { 1, 2, 3, 4, 5, 6, 7, 8 }, new[] { 1, 3, 4, 5, 6, 7, 8, 9 }, new[] { 1, 2, 4, 5, 8, 7, 9 },   new[] { 1, 2, 3, 4, 5, 6, 8, 7, 9 },   new[] { 1, 2, 3, 4, 6, 8, 7, 9 }, new[] { 1, 2, 3, 4, 5, 7, 8, 9 }),
            MakeRow(new[] { 1, 2, 3, 4, 5, 6, 7, 9 }, new[] { 1, 2, 3, 4, 5, 6, 7, 9 },     new[] { 1, 2, 3, 4, 5, 6, 7, 9 },     8,                      new[] { 1, 3, 4, 5, 6, 7, 9 },   new[] { 1, 2, 4, 5, 7, 9 },     new[] { 1, 2, 3, 4, 5, 6, 7, 9 },     new[] { 1, 2, 3, 4, 6, 7, 9 },   new[] { 1, 2, 3, 4, 5, 7, 9 }),
            MakeRow(new[] { 1, 2, 3, 4, 5, 6, 7 },   new[] { 1, 2, 3, 4, 5, 6, 7, 8 },     new[] { 1, 2, 3, 4, 5, 6, 7, 8 },     9,                      new[] { 1, 3, 4, 5, 6, 7 },     new[] { 1, 2, 4, 5, 7 },       new[] { 1, 2, 3, 4, 5, 6, 7, 8 },     new[] { 1, 2, 3, 4, 6, 7, 8 },   new[] { 1, 2, 3, 4, 5, 7, 8 }),
            MakeRow(new[] { 1, 2, 3, 4, 5, 6, 7, 9 }, new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 },   new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 },   new[] { 1, 2, 3, 4, 5, 6, 7 },   new[] { 1, 3, 4, 5, 6, 7, 8, 9 }, new[] { 1, 2, 4, 5, 7, 8, 9 },   new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 },   new[] { 1, 2, 3, 4, 6, 7, 8, 9 }, new[] { 1, 2, 3, 4, 5, 7, 8, 9 })
        };

        [Test]
        public void GetHandler_ForRightAngleRule_ReturnsRightAngleHandler()
        {
            var handler = UnsolveHandlerRegistry.GetHandler(new RightAngleRule());

            Assert.IsInstanceOf<RightAngleUnsolveHandler>(handler);
        }

        [Test]
        public void TryUnsolve_RemovesRightAngleCell_AndSolverRestoresIt()
        {
            var board = MakeBoardFromFixture();
            var handler = new RightAngleUnsolveHandler();

            int before = CountFilled(board);
            Assert.AreEqual(9, before, "Expected the base fixture to start with 9 filled values.");

            var result = handler.TryUnsolve(board, new Random(0));

            Assert.AreEqual(UnsolveResult.Success, result);
            Assert.IsNull(board.Cells[1, 4].Value, "Expected the Right Angle target cell to be removed.");
            Assert.Less(CountFilled(board), before, "Expected at least one value to be removed.");

            RecomputeCandidates(board);

            var registry = new RuleRegistry();
            registry.Register(new NakedSingleRule());
            registry.Register(new HiddenSingleRule());
            registry.Register(new RightAngleRule());

            var (rule, applyResult) = registry.ApplyNext(board);

            Assert.IsNotNull(rule);
            Assert.IsTrue(applyResult.Apply);
            Assert.IsTrue(
                rule is NakedSingleRule || rule is HiddenSingleRule || rule is RightAngleRule,
                "Expected the removed value to be restored by one of the enabled value-placement rules.");
            Assert.AreEqual(8, board.Cells[1, 4].Value, "Expected the removed value to be restored.");
        }

        [Test]
        public void TryUnsolve_DoesNotRemoveGivenCells()
        {
            var board = MakeBoardFromFixture();
            for (int r = 0; r < board.Size; r++)
            {
                for (int c = 0; c < board.Size; c++)
                {
                    if (board.Cells[r, c].Value.HasValue)
                    {
                        board.Cells[r, c].IsGiven = true;
                    }
                }
            }

            var result = new RightAngleUnsolveHandler().TryUnsolve(board, new Random(0));

            Assert.AreEqual(UnsolveResult.NoApplicableMove, result);
            Assert.AreEqual(9, CountFilled(board), "Given cells must remain untouched.");
        }

        [Test]
        public void BuildCandidateList_FallsBackToRightAngleCellsThatEasierRulesCanAlsoSolve()
        {
            var board = MakeFallbackBoardFromFixture();
            var handler = new RightAngleUnsolveHandler();

            var candidates = handler.BuildCandidateList(board);

            Assert.AreEqual(1, candidates.Count, "Expected the fallback fixture to expose exactly one Right Angle removal.");
            Assert.AreEqual(1, candidates[0].Row);
            Assert.AreEqual(4, candidates[0].Column);
        }

        [Test]
        public void TryUnsolve_WhenOnlyFallbackCandidatesExist_RemovesOne()
        {
            var board = MakeFallbackBoardFromFixture();
            var handler = new RightAngleUnsolveHandler();

            int before = CountFilled(board);
            Assert.AreEqual(14, before, "Expected the fallback fixture to start with 14 filled values.");

            var result = handler.TryUnsolve(board, new Random(0));

            Assert.AreEqual(UnsolveResult.Success, result);
            Assert.IsNull(board.Cells[1, 4].Value, "Expected the Right Angle fallback target cell to be removed.");
            Assert.Less(CountFilled(board), before, "Expected at least one value to be removed from the fallback fixture.");
        }

        [Test]
        public void TryUnsolve_CanReinstateSupportValue_WhenNeededForRightAngle()
        {
            var solvedReference = MakeBoardFromFixture();
            var board = PuzzleGenerator.CloneBoard(solvedReference);

            // Remove a required support value for the (1,4) Right Angle deduction.
            board.Cells[5, 3].Value = null;
            board.Cells[5, 3].IsGiven = false;

            var plainHandler = new RightAngleUnsolveHandler();
            Assert.AreEqual(
                UnsolveResult.NoApplicableMove,
                plainHandler.TryUnsolve(PuzzleGenerator.CloneBoard(board), new Random(0)),
                "Without solved-value context this position should not produce a Right Angle unsolve move.");

            var assistedHandler = new RightAngleUnsolveHandler();
            assistedHandler.SetSolvedBoard(solvedReference);

            int before = CountFilled(board);
            var result = assistedHandler.TryUnsolve(board, new Random(0));

            Assert.AreEqual(UnsolveResult.Success, result);
            Assert.AreEqual(8, board.Cells[5, 3].Value, "Expected helper support value to be reinstated from solved reference.");
            Assert.IsNull(board.Cells[1, 4].Value, "Expected assisted Right Angle unsolve to remove the target cell.");
            Assert.LessOrEqual(CountFilled(board), before, "Expected helper add/remove plus optional contextual removals to not increase fill-count.");
        }

        private static Board MakeBoardFromFixture()
        {
            var board = new Board(9, 3, 3);
            int rows = Math.Min(9, RightAngleFixture.Count);
            for (int r = 0; r < 9; r++)
            {
                for (int c = 0; c < 9; c++)
                {
                    var cell = new Cell(r, c)
                    {
                        Value = r < rows ? RightAngleFixture[r].Values[c] : null,
                        IsGiven = false
                    };

                    cell.Candidates.Clear();
                    if (r < rows)
                    {
                        foreach (var candidate in RightAngleFixture[r].Candidates[c])
                        {
                            cell.Candidates.Add(candidate);
                        }
                    }

                    board.Cells[r, c] = cell;
                }
            }

            return board;
        }

        private static Board MakeFallbackBoardFromFixture()
        {
            var board = MakeBoardFromFixture();

            board.Cells[1, 0].Value = 1;
            board.Cells[1, 1].Value = 4;
            board.Cells[1, 2].Value = 5;
            board.Cells[1, 6].Value = 7;
            board.Cells[1, 7].Value = 9;

            return board;
        }

        private static void RecomputeCandidates(Board board)
        {
            for (int r = 0; r < board.Size; r++)
            {
                for (int c = 0; c < board.Size; c++)
                {
                    var cell = board.Cells[r, c];
                    cell.Candidates.Clear();
                    if (!cell.Value.HasValue)
                    {
                        for (int digit = 1; digit <= board.Size; digit++)
                        {
                            cell.Candidates.Add(digit);
                        }
                    }
                }
            }

            for (int r = 0; r < board.Size; r++)
            {
                for (int c = 0; c < board.Size; c++)
                {
                    var cell = board.Cells[r, c];
                    if (cell.Value.HasValue)
                    {
                        continue;
                    }

                    foreach (var peer in board.GetPeers(cell))
                    {
                        if (peer.Value.HasValue)
                        {
                            cell.Candidates.Remove(peer.Value.Value);
                        }
                    }
                }
            }
        }

        private static int CountFilled(Board board)
        {
            int count = 0;
            for (int r = 0; r < board.Size; r++)
            {
                for (int c = 0; c < board.Size; c++)
                {
                    if (board.Cells[r, c].Value.HasValue)
                    {
                        count++;
                    }
                }
            }

            return count;
        }
    }
}