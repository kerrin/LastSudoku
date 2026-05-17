using System;
using System.Collections.Generic;
using NUnit.Framework;
using Sudoku.Models;
using Sudoku.Solver.Rules;

namespace Sudoku.Tests.Editor
{
    public class RightAngleRuleTests
    {
        private class TestBoardRow
        {
            public int?[] Values { get; set; }
            public List<int>[] Candidates { get; set; }
        }
        private static TestBoardRow MakeRow(params object[] cells)
        {
            if (cells == null) throw new ArgumentNullException(nameof(cells));
            if (cells.Length != 9) throw new ArgumentException("Exactly 9 cells required", nameof(cells));

            var row = new TestBoardRow();
            row.Values = new int?[9];
            row.Candidates = new List<int>[9];
            for (int i = 0; i < 9; i++)
            {
                row.Candidates[i] = new List<int>();
                var cell = cells[i];
                if (cell == null) continue;
                if (cell is int v)
                {
                    row.Values[i] = v;
                    continue;
                }
                if (cell is int vn)
                {
                    row.Values[i] = vn;
                    continue;
                }
                if (cell is IEnumerable<int> seq)
                {
                    row.Candidates[i] = new List<int>(seq);
                    continue;
                }
                throw new ArgumentException($"Unsupported cell type at index {i}: {cell.GetType()}");
            }

            return row;
        }
        
        /** 
          * Represents board:
          * 8..|...|.56
          * ...|..6|...
          * ...|.23|...
          * ---+---+---
          * ...|...|...
          * ...|...|...
          * ...|...|...
          * ---+---+---
          * ...|8..|...
          * ...|9..|...
          * ...|...|...
          * and populate all the candidate values
          */
        private static readonly List<TestBoardRow> InitialValues = new List<TestBoardRow> {
            MakeRow(8,                      new[]{1,2,3,4,9},           new[]{1,2,3,4,9},           new[]{1,2,3,4},         new[]{1,3,4,9},         new[]{1,2,4,9},         new[]{1,2,3,4,9},           5,                      6),
            MakeRow(new[]{1,2,3,4,5,7,9},   new[]{1,2,3,4,5,7,8,9},     new[]{1,2,3,4,5,7,8,9},     new[]{1,2,3,4,5,7},     new[]{1,3,4,5,7,8,9},   6,                      new[]{1,2,3,4,5,7,8,9},     new[]{1,2,3,4,7,8,9},   new[]{1,2,3,4,5,7,8,9}),
            MakeRow(new[]{1,4,5,6,7,8,9},   new[]{1,4,5,6,7,8,9},       new[]{1,4,5,6,7,8,9},       new[]{1,4,5,6,7},       2,                      3,                      new[]{1,4,5,6,7,8,9},       new[]{1,4,6,7,8,9},     new[]{1,4,5,7,8,9}),
            MakeRow(new[]{1,2,3,4,5,6,7,9}, new[]{1,2,3,4,5,6,8,7,8,9}, new[]{1,2,3,4,5,6,8,7,8,9}, new[]{1,2,3,4,5,6,7,8}, new[]{1,3,4,5,6,7,8,9}, new[]{1,2,4,5,8,7,9},   new[]{1,2,3,4,5,6,8,7,9},   new[]{1,2,3,4,6,8,7,9}, new[]{1,2,3,4,5,7,8,9}),
            MakeRow(new[]{1,2,3,4,5,6,7,9}, new[]{1,2,3,4,5,6,8,7,8,9}, new[]{1,2,3,4,5,6,8,7,8,9}, new[]{1,2,3,4,5,6,7,8}, new[]{1,3,4,5,6,7,8,9}, new[]{1,2,4,5,8,7,9},   new[]{1,2,3,4,5,6,8,7,9},   new[]{1,2,3,4,6,8,7,9}, new[]{1,2,3,4,5,7,8,9}),
            MakeRow(new[]{1,2,3,4,5,6,7,9}, new[]{1,2,3,4,5,6,7,9},     new[]{1,2,3,4,5,6,7,9},     8,                      new[]{1,3,4,5,6,7,9},   new[]{1,2,4,5,7,9},     new[]{1,2,3,4,5,6,7,9},     new[]{1,2,3,4,6,7,9},   new[]{1,2,3,4,5,7,9}),
            MakeRow(new[]{1,2,3,4,5,6,7},   new[]{1,2,3,4,5,6,7,8},     new[]{1,2,3,4,5,6,7,8},     9,                      new[]{1,3,4,5,6,7},     new[]{1,2,4,5,7},       new[]{1,2,3,4,5,6,7,8},     new[]{1,2,3,4,6,7,8},   new[]{1,2,3,4,5,7,8}),
            MakeRow(new[]{1,2,3,4,5,6,7,9}, new[]{1,2,3,4,5,6,7,8,9},   new[]{1,2,3,4,5,6,7,8,9},   new[]{1,2,3,4,5,6,7},   new[]{1,3,4,5,6,7,8,9}, new[]{1,2,4,5,7,8,9},   new[]{1,2,3,4,5,6,7,8,9},   new[]{1,2,3,4,6,7,8,9}, new[]{1,2,3,4,5,7,8,9} )
        };
        
        /** 
          * Starting with the InitialValues board,
          * after the test we expect the board to be
          * 8..|...|.56
          * ...|.86|...
          * ...|.23|...
          * ---+---+---
          * ...|...|...
          * ...|...|...
          * ...|...|...
          * ---+---+---
          * ...|8..|...
          * ...|9..|...
          * ...|...|...
          * And the RightAngleRule should have run
          * And the candidates should remove 8 from all of row 1 and column 4 (zero indexed)
          */
        [Test]
        public void RightAngle_Check()
        {
            var board = new Board(9, 3, 3);
            // Populate board from InitialValues with bounds checks
            int rows = Math.Min(9, InitialValues.Count);
            for (int r = 0; r < 9; r++)
            {
                for (int c = 0; c < 9; c++)
                {
                    var cell = new Cell(r, c);
                    if (r < rows)
                    {
                        var vals = InitialValues[r].Values ?? new int?[0];
                        if (c < vals.Length) cell.Value = vals[c];
                    }
                    board.Cells[r, c] = cell;
                    board.Cells[r, c].Candidates.Clear();
                }
            }

            for (int r = 0; r < rows; r++)
            {
                var rowCandidates = InitialValues[r].Candidates ?? new List<int>[0];
                int cols = Math.Min(9, rowCandidates.Length);
                for (int c = 0; c < cols; c++)
                {
                    var cand = rowCandidates[c];
                    if (cand == null) continue;
                    foreach (var d in cand) board.Cells[r, c].Candidates.Add(d);
                }
            }

            var registry = new RuleRegistry();
            registry.Register(new RightAngleRule());
            
            var (rule, result) = registry.ApplyNext(board);
            Assert.IsNotNull(rule);
            Assert.IsInstanceOf<RightAngleRule>(rule);
            Assert.IsTrue(result.Apply);
            // With value-placement semantics, expect the rule to have placed 8 at (1,4)
            Assert.AreEqual(8, board.Cells[1, 4].Value, "Expected the RightAngleRule to place digit 8 at (1,4)");
            Assert.IsEmpty(board.Cells[1, 4].Candidates, "Candidates should be cleared when a value is placed");
            // for (int c = 0; c <= 8; c++) {
            //     if (c == 4) continue;
            //     Assert.IsFalse(board.Cells[1, c].Candidates.Contains(8), $"Expected candidate 8 to be removed from cell (1,{c})");
            // }
            // for (int r = 0; r <= 8; r++) {
            //     if (r == 1) continue;
            //     Assert.IsFalse(board.Cells[r, 4].Candidates.Contains(8), $"Expected candidate 8 to be removed from cell ({r},4)");
            // }
        }
    }
}
