using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sudoku.Models;
using Sudoku.Solver;
using Sudoku.Solver.Rules;

namespace Sudoku.Tests.Solver
{
    public class SolverEngineTests
    {
        [Test]
        public void ApplyBasicRules_PlacesMissingValue()
        {
            var board = new Board(9, 3, 3);
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    board.Cells[r, c] = new Cell(r, c);

            // Fill row 0 with digits 2..9 at columns 1..8, leaving column 0 empty.
            for (int c = 1; c < 9; c++)
            {
                board.Cells[0, c].Value = c + 1; // 2..9
                board.Cells[0, c].Candidates.Clear();
            }

            var registry = new RuleRegistry();
            registry.RegisterMinimal();
            registry.RegisterMedium();
            registry.RegisterAdvanced();
            var engine = new SolverEngine(registry);

            // Apply rules (we don't expect the whole board to be solved).
            var steps = registry.ApplyAll(board, 10);

            // The empty cell at (0,0) should be filled with the missing digit (1).
            Assert.IsTrue(board.Cells[0, 0].Value.HasValue, "Expected a rule to place a value at (0,0)");
            Assert.AreEqual(1, board.Cells[0, 0].Value.Value);
            Assert.IsTrue(steps.Any(), "Expected at least one rule application");
            // At least one applied step must include a change that placed 1 at (0,0).
            Assert.IsTrue(steps.Any(s => s.result.Changes.Any(ch => ch.Row == 0 && ch.Column == 0 && ch.NewValue == 1)),
                "Expected a step to place 1 at (0,0)");
        }

        [Test]
        public void Solve_NearlyCompleteBoard_CompletesPuzzle()
        {
            var board = new Board(9, 3, 3);
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    board.Cells[r, c] = new Cell(r, c);

            // Populate with a known valid completed pattern (standard Sudoku base solution),
            // then clear a single cell so the solver must fill it.
            for (int r = 0; r < 9; r++)
            {
                for (int c = 0; c < 9; c++)
                {
                    int v = ((r * 3 + r / 3 + c) % 9) + 1;
                    board.Cells[r, c].Value = v;
                    board.Cells[r, c].Candidates.Clear();
                }
            }

            // Clear one cell (0,0) to be solved by the engine.
            board.Cells[0, 0].Value = null;
            board.Cells[0, 0].Candidates = new HashSet<int>(Enumerable.Range(1, 9));

            var engine = new SolverEngine();
            bool solved = engine.Solve(board, out List<(ISudokuRule rule, RuleResult result)> steps);

            Assert.IsTrue(solved, "Expected the engine to solve the nearly-complete board");
            Assert.IsTrue(board.Cells[0, 0].Value.HasValue, "Expected the empty cell to be filled");
            Assert.AreEqual(1, board.Cells[0, 0].Value.Value, "Expected the value placed to match the original solution pattern");
            Assert.IsTrue(steps.Count > 0, "Expected at least one rule application during solve");
        }
    }
}
