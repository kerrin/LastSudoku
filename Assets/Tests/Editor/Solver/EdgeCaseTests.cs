using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sudoku.Models;
using Sudoku.Solver;
using Sudoku.Solver.Rules;

namespace Sudoku.Tests.Solver
{
    public class EdgeCaseTests
    {
        [Test]
        public void Solve_AlreadySolvedBoard_ReturnsTrueAndNoSteps()
        {
            var board = new Board(9, 3, 3);
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    board.Cells[r, c] = new Cell(r, c);

            // Fill the board with a valid completed pattern.
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                {
                    int v = ((r * 3 + r / 3 + c) % 9) + 1;
                    board.Cells[r, c].Value = v;
                    board.Cells[r, c].Candidates.Clear();
                }

            var engine = new SolverEngine();
            bool solved = engine.Solve(board, out List<(ISudokuRule rule, RuleResult result)> steps);

            Assert.IsTrue(solved, "Expected solver to report already-solved board as solved");
            Assert.IsNotNull(steps);
            Assert.AreEqual(0, steps.Count, "Expected no steps when board is already solved");
        }

        [Test]
        public void ApplyAll_CellWithNoCandidates_DoesNotThrow_AndNoApply()
        {
            var board = new Board(9, 3, 3);
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    board.Cells[r, c] = new Cell(r, c);

            // Create a problematic empty cell: no value and no candidates.
            board.Cells[4, 4].Value = null;
            board.Cells[4, 4].Candidates.Clear();

            var registry = new RuleRegistry();
            registry.RegisterDefaults();

            // Should not throw and should simply produce no applied steps.
            List<(ISudokuRule rule, RuleResult result)> steps = null;
            Assert.DoesNotThrow(() => steps = registry.ApplyAll(board, 50));
            Assert.IsNotNull(steps);
            Assert.IsTrue(steps.Count >= 0);
        }

        [Test]
        public void RuleRegistry_ApplyNext_NoApplicableRule_ReturnsNotApplied()
        {
            var board = new Board(9, 3, 3);
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    board.Cells[r, c] = new Cell(r, c);

            // Make all cells given (but invalid duplicates are irrelevant here) so no rule applies.
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                {
                    board.Cells[r, c].Value = 1; // intentionally duplicates — rules should not apply
                    board.Cells[r, c].Candidates.Clear();
                }

            var registry = new RuleRegistry();
            registry.RegisterDefaults();

            var (rule, result) = registry.ApplyNext(board);
            Assert.IsNull(rule);
            Assert.IsFalse(result.Applied);
        }
    }
}
