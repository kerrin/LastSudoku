using NUnit.Framework;
using Sudoku.Models;
using Sudoku.Solver.Rules;

namespace Sudoku.Tests.Editor
{
    public class ForcingChainPropagationTests
    {
        [Test]
        public void Generate_ReturnsOnePropagationBranchPerSeed()
        {
            var board = TestHelpers.CreateEmptyBoard();
            ClearAllCandidates(board);
            AddCandidates(board, 0, 0, 1, 2);
            var model = ForcingChainLinkModel.Build(board);
            int firstSeed = ForcingChainLinkModel.MakeCandidateKey(board, 0, 0, 1);
            int secondSeed = ForcingChainLinkModel.MakeCandidateKey(board, 0, 0, 2);

            var branches = ForcingChainBranchGenerator.Generate(
                board,
                model,
                new[] { firstSeed, secondSeed },
                assumeTrue: true,
                enableUnitCompletion: false);

            Assert.AreEqual(2, branches.Count);
            CollectionAssert.AreEqual(new[] { firstSeed, secondSeed }, branches.ConvertAll(branch => branch.SeedCandidate));
            Assert.IsTrue(branches.TrueForAll(branch => branch.State.TrueCandidates.Contains(branch.SeedCandidate)));
        }

        [Test]
        public void PropagateFromAssumption_FalseCellCandidateForcesItsStrongPartner()
        {
            var board = TestHelpers.CreateEmptyBoard();
            ClearAllCandidates(board);
            AddCandidates(board, 0, 0, 1, 2);
            var model = ForcingChainLinkModel.Build(board);
            int assumedFalse = ForcingChainLinkModel.MakeCandidateKey(board, 0, 0, 1);
            int forcedTrue = ForcingChainLinkModel.MakeCandidateKey(board, 0, 0, 2);

            var branch = ForcingChainPropagation.PropagateFromAssumption(
                board,
                model,
                assumedFalse,
                assumeTrue: false,
                enableUnitCompletion: false);

            Assert.IsFalse(branch.HasContradiction, branch.ContradictionReason);
            Assert.IsTrue(branch.FalseCandidates.Contains(assumedFalse));
            Assert.IsTrue(branch.TrueCandidates.Contains(forcedTrue));
        }

        [Test]
        public void PropagateFromAssumption_TrueCandidateExcludesPeerAndCompletesCell()
        {
            var board = TestHelpers.CreateEmptyBoard();
            ClearAllCandidates(board);
            AddCandidates(board, 0, 0, 1, 2);
            AddCandidates(board, 0, 4, 1, 3);
            var model = ForcingChainLinkModel.Build(board);
            int assumedTrue = ForcingChainLinkModel.MakeCandidateKey(board, 0, 0, 1);
            int excluded = ForcingChainLinkModel.MakeCandidateKey(board, 0, 4, 1);
            int completed = ForcingChainLinkModel.MakeCandidateKey(board, 0, 4, 3);

            var branch = ForcingChainPropagation.PropagateFromAssumption(
                board,
                model,
                assumedTrue,
                assumeTrue: true,
                enableUnitCompletion: false);

            Assert.IsFalse(branch.HasContradiction, branch.ContradictionReason);
            Assert.IsTrue(branch.TrueCandidates.Contains(assumedTrue));
            Assert.IsTrue(branch.FalseCandidates.Contains(excluded));
            Assert.IsTrue(branch.TrueCandidates.Contains(completed));
        }

        private static void ClearAllCandidates(Board board)
        {
            foreach (Cell cell in board.Cells)
            {
                cell.Candidates.Clear();
            }
        }

        private static void AddCandidates(Board board, int row, int column, params int[] candidates)
        {
            foreach (int candidate in candidates)
            {
                board.Cells[row, column].Candidates.Add(candidate);
            }
        }
    }
}
