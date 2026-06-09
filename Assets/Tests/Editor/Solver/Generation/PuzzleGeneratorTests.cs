using System;
using System.Collections.Generic;
using NUnit.Framework;
using Sudoku.Models;
using Sudoku.Solver;
using Sudoku.Solver.Rules;
using Sudoku.Solver.Unsolver;

namespace Sudoku.Tests.Unsolver
{
    public class PuzzleGeneratorTests
    {
        // ── Board helper ───────────────────────────────────────────────────────────

        private static Board MakeFullySolvedBoard()
        {
            int[,] values =
            {
                { 1, 2, 3, 4, 5, 6, 7, 8, 9 },
                { 4, 5, 6, 7, 8, 9, 1, 2, 3 },
                { 7, 8, 9, 1, 2, 3, 4, 5, 6 },
                { 2, 3, 4, 5, 6, 7, 8, 9, 1 },
                { 5, 6, 7, 8, 9, 1, 2, 3, 4 },
                { 8, 9, 1, 2, 3, 4, 5, 6, 7 },
                { 3, 4, 5, 6, 7, 8, 9, 1, 2 },
                { 6, 7, 8, 9, 1, 2, 3, 4, 5 },
                { 9, 1, 2, 3, 4, 5, 6, 7, 8 },
            };
            var board = new Board(9, 3, 3);
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    board.Cells[r, c] = new Cell(r, c, values[r, c], isGiven: false);
            return board;
        }

        private static List<ISudokuRule> MinimalRules() =>
            new List<ISudokuRule> { new NakedSingleRule(), new HiddenSingleRule() };

        // ── CloneBoard ─────────────────────────────────────────────────────────────

        [Test]
        public void CloneBoard_ProducesDeepCopy_ValuesCopied()
        {
            var source = MakeFullySolvedBoard();
            var clone = PuzzleGenerator.CloneBoard(source);

            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    Assert.AreEqual(source.Cells[r, c].Value, clone.Cells[r, c].Value,
                        $"Cloned cell [{r},{c}] value mismatch.");
        }

        [Test]
        public void CloneBoard_MutatingClone_DoesNotAffectSource()
        {
            var source = MakeFullySolvedBoard();
            var clone = PuzzleGenerator.CloneBoard(source);
            clone.Cells[0, 0].Value = null;

            Assert.IsNotNull(source.Cells[0, 0].Value, "Mutating clone must not affect source.");
        }

        // ── FinalizeBoard ──────────────────────────────────────────────────────────

        [Test]
        public void FinalizeBoard_ValuedCells_MarkedAsGiven()
        {
            var board = MakeFullySolvedBoard();
            board.Cells[0, 0].Value = null; // one empty cell
            PuzzleGenerator.FinalizeBoard(board);

            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    if (board.Cells[r, c].Value.HasValue)
                        Assert.IsTrue(board.Cells[r, c].IsGiven, $"Cell [{r},{c}] should be IsGiven.");
        }

        [Test]
        public void FinalizeBoard_EmptyCells_HaveCorrectCandidates()
        {
            var board = MakeFullySolvedBoard();
            // Remove cell (0,0) which has value 1.
            board.Cells[0, 0].Value = null;
            PuzzleGenerator.FinalizeBoard(board);

            var cell = board.Cells[0, 0];
            Assert.IsFalse(cell.IsGiven);
            // In the solved board, row 0 has 2-9 and col 0 has 4,7,2,5,8,3,6,9.
            // Candidates should contain only values not present in peers.
            Assert.IsTrue(cell.Candidates.Contains(1),
                "The removed value should be the only valid candidate.");
            Assert.AreEqual(1, cell.Candidates.Count,
                "Only the missing value should be a candidate.");
        }

        [Test]
        public void FinalizeBoard_ClearsChangeLog()
        {
            var board = MakeFullySolvedBoard();
            board.ChangeLog.Add(new CellChange { Row = 0, Column = 0 });
            PuzzleGenerator.FinalizeBoard(board);
            Assert.AreEqual(0, board.ChangeLog.Count, "ChangeLog should be cleared after finalize.");
            Assert.AreEqual(0, board.ChangeLogIndex);
        }

        // ── HasUniqueSolution ──────────────────────────────────────────────────────

        [Test]
        public void HasUniqueSolution_FullySolvedBoard_ReturnsTrue()
        {
            var board = MakeFullySolvedBoard();
            Assert.IsTrue(PuzzleGenerator.HasUniqueSolution(board));
        }

        [Test]
        public void HasUniqueSolution_BoardWithTwoSolutions_ReturnsFalse()
        {
            // Create a board with two empty cells that can swap their values,
            // producing two distinct solutions.
            var board = MakeFullySolvedBoard();
            // (0,0)=1 and (0,1)=2 are in the same row/box. Swapping would violate the
            // puzzle so instead remove two cells whose values don't constrain each other
            // such that both placements are valid. Build a degenerate case explicitly:
            // clear ALL cells to give maximal freedom.
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    board.Cells[r, c].Value = null;

            // Place only one digit — all 9 positions in any row are equivalent.
            // This board clearly has more than one solution.
            Assert.IsFalse(PuzzleGenerator.HasUniqueSolution(board));
        }

        // ── Generate ───────────────────────────────────────────────────────────────

        [Test]
        public void Generate_WithNakedSingleOnly_ReturnsBoardWithFewerClues()
        {
            var solved = MakeFullySolvedBoard();
            var generator = new PuzzleGenerator();
            var puzzle = generator.Generate(solved, new List<ISudokuRule> { new NakedSingleRule() }, new Random(1));

            int givenCount = 0;
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    if (puzzle.Cells[r, c].IsGiven) givenCount++;

            Assert.Less(givenCount, 81, "Generated puzzle should have fewer than 81 givens.");
            Assert.Greater(givenCount, 0, "Generated puzzle must have at least one given.");
        }

        [Test]
        public void Generate_ProducesValidBoard()
        {
            var solved = MakeFullySolvedBoard();
            var generator = new PuzzleGenerator();
            var puzzle = generator.Generate(solved, MinimalRules(), new Random(42));
            Assert.IsTrue(puzzle.IsValid(), "Generated puzzle must be a valid Sudoku board.");
        }

        [Test]
        public void Generate_PuzzleHasUniqueSolution()
        {
            var solved = MakeFullySolvedBoard();
            var generator = new PuzzleGenerator();
            var puzzle = generator.Generate(solved, MinimalRules(), new Random(77));
            Assert.IsTrue(PuzzleGenerator.HasUniqueSolution(puzzle),
                "Generated puzzle must have exactly one solution.");
        }

        [Test]
        public void Generate_GivenCellsHaveCorrectCandidateState()
        {
            var solved = MakeFullySolvedBoard();
            var generator = new PuzzleGenerator();
            var puzzle = generator.Generate(solved, MinimalRules(), new Random(5));

            for (int r = 0; r < 9; r++)
            {
                for (int c = 0; c < 9; c++)
                {
                    var cell = puzzle.Cells[r, c];
                    if (cell.IsGiven)
                        Assert.AreEqual(0, cell.Candidates.Count,
                            $"Given cell [{r},{c}] should have no candidates.");
                    else
                        Assert.Greater(cell.Candidates.Count, 0,
                            $"Empty cell [{r},{c}] should have at least one candidate.");
                }
            }
        }

        [Test]
        public void Generate_WithNakedSingleOnly_PuzzleSolvableByNakedSingle()
        {
            var solved = MakeFullySolvedBoard();
            var generator = new PuzzleGenerator();
            var puzzle = generator.Generate(solved, new List<ISudokuRule> { new NakedSingleRule() }, new Random(3));

            // The puzzle should be fully solvable using only Naked Single.
            var registry = new RuleRegistry();
            registry.Register(new NakedSingleRule());
            var engine = new SolverEngine(registry);
            bool solvable = engine.Solve(puzzle, out _);

            Assert.IsTrue(solvable, "Puzzle generated with only NakedSingle should be solvable by NakedSingle alone.");
        }

        [Test]
        public void Generate_DoesNotMutateSolvedBoard()
        {
            var solved = MakeFullySolvedBoard();
            var snap = new int?[9, 9];
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    snap[r, c] = solved.Cells[r, c].Value;

            new PuzzleGenerator().Generate(solved, MinimalRules(), new Random(9));

            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    Assert.AreEqual(snap[r, c], solved.Cells[r, c].Value,
                        $"Source solved board cell [{r},{c}] must not be modified.");
        }

        [Test]
        public void Generate_DeterministicWithSameSeed()
        {
            var solved = MakeFullySolvedBoard();
            var gen = new PuzzleGenerator();

            var puzzle1 = gen.Generate(PuzzleGenerator.CloneBoard(solved), MinimalRules(), new Random(42));
            var puzzle2 = gen.Generate(PuzzleGenerator.CloneBoard(solved), MinimalRules(), new Random(42));

            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    Assert.AreEqual(puzzle1.Cells[r, c].Value, puzzle2.Cells[r, c].Value,
                        $"Puzzle generated with identical seed must be deterministic at [{r},{c}].");
        }

        [Test]
        public void Generate_NullSolvedBoard_ThrowsArgumentNullException()
        {
            var generator = new PuzzleGenerator();
            Assert.Throws<ArgumentNullException>(() =>
                generator.Generate(null, MinimalRules(), new Random(42)));
        }

        [Test]
        public void Generate_NullEnabledRules_ThrowsArgumentNullException()
        {
            var generator = new PuzzleGenerator();
            Assert.Throws<ArgumentNullException>(() =>
                generator.Generate(MakeFullySolvedBoard(), null, new Random(42)));
        }

        [Test]
        public void Generate_EmptyEnabledRules_ThrowsArgumentException()
        {
            var generator = new PuzzleGenerator();
            Assert.Throws<ArgumentException>(() =>
                generator.Generate(MakeFullySolvedBoard(), new List<ISudokuRule>(), new Random(42)));
        }

        [Test]
        public void Generate_WithRotationalSymmetryMode_ProducesRotationallySymmetricGivens()
        {
            var solved = MakeFullySolvedBoard();
            var generator = new PuzzleGenerator(
                maxRetries: 50,
                requireNonNakedContribution: false,
                clueSymmetryMode: PuzzleClueSymmetryMode.Rotational180);

            var puzzle = generator.Generate(solved, MinimalRules(), new Random(84));
            int size = puzzle.Size;

            for (int row = 0; row < size; row++)
            {
                for (int column = 0; column < size; column++)
                {
                    int pairRow = (size - 1) - row;
                    int pairColumn = (size - 1) - column;

                    Assert.AreEqual(
                        puzzle.Cells[row, column].IsGiven,
                        puzzle.Cells[pairRow, pairColumn].IsGiven,
                        $"Given symmetry mismatch between [{row},{column}] and [{pairRow},{pairColumn}].");
                }
            }
        }

        [Test]
        public void Generate_WithCandidateOnlyRules_DoesNotThrow()
        {
            // Candidate-only rules should be silently skipped via NotSupported.
            var solved = MakeFullySolvedBoard();
            var rules = new List<ISudokuRule>
            {
                new NakedSingleRule(),
                new BoxLineRule(),    // candidate-only stub
                new SkyscraperRule(), // candidate-only stub
            };
            Assert.DoesNotThrow(() =>
                new PuzzleGenerator().Generate(solved, rules, new Random(11)));
        }

        [Test]
        public void Generate_RepeatedAddRemoveTransition_IsBlockedByTransitionGuard()
        {
            var solved = MakeFullySolvedBoard();
            var addRule = new LoopAddRule();
            var removeRule = new LoopRemoveRule();
            var addHandler = new LoopAddUnsolveHandler();
            var removeHandler = new LoopRemoveUnsolveHandler();

            var generator = new PuzzleGenerator(
                maxRetries: 1,
                maxIterations: 100,
                minimumOtherRulePasses: 0,
                requireNonNakedContribution: false,
                handlerResolver: rule =>
                {
                    if (rule is LoopAddRule) return addHandler;
                    if (rule is LoopRemoveRule) return removeHandler;
                    return new CandidateOnlyUnsolveHandler(rule.Name);
                });

            Assert.DoesNotThrow(() =>
            {
                var puzzle = generator.Generate(solved, new List<ISudokuRule> { addRule, removeRule }, new Random(1));
                Assert.IsTrue(PuzzleGenerator.HasUniqueSolution(puzzle));
            });

            Assert.AreEqual(3, addHandler.Calls,
                "Expected add handler to stop after the repeated transition is blocked.");
            Assert.AreEqual(2, removeHandler.Calls,
                "Expected remove handler to stop after the repeated transition is blocked.");
        }

        private sealed class LoopAddRule : ISudokuRule
        {
            public string Name => "Loop Add";
            public bool CanApply(Board board) => true;
            public RuleResult CalculateChanges(Board board) => new RuleResult { Apply = false };
            public Difficulty Difficulty => Difficulty.Hard;
        }

        private sealed class LoopRemoveRule : ISudokuRule
        {
            public string Name => "Loop Remove";
            public bool CanApply(Board board) => true;
            public RuleResult CalculateChanges(Board board) => new RuleResult { Apply = false };
            public Difficulty Difficulty => Difficulty.Medium;
        }

        private sealed class LoopAddUnsolveHandler : IUnsolveHandler
        {
            public string RuleName => nameof(LoopAddRule);
            public int Calls { get; private set; }

            public UnsolveResult TryUnsolve(Board board, Random random)
            {
                Calls++;
                var cell = board.Cells[0, 0];
                if (cell.Value.HasValue)
                {
                    return UnsolveResult.NoApplicableMove;
                }

                cell.Value = 1;
                cell.IsGiven = false;
                return UnsolveResult.Success;
            }
        }

        private sealed class LoopRemoveUnsolveHandler : IUnsolveHandler
        {
            public string RuleName => nameof(LoopRemoveRule);
            public int Calls { get; private set; }

            public UnsolveResult TryUnsolve(Board board, Random random)
            {
                Calls++;
                var cell = board.Cells[0, 0];
                if (!cell.Value.HasValue)
                {
                    return UnsolveResult.NoApplicableMove;
                }

                cell.Value = null;
                cell.IsGiven = false;
                return UnsolveResult.Success;
            }
        }
    }
}
