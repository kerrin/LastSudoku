using System.Collections.Generic;
using NUnit.Framework;
using Sudoku.Models;
using Sudoku.Solver.Rules;

namespace Sudoku.Tests.Editor
{
    public class ForcingChainLinkModelTests
    {
        [Test]
        public void Build_AddsCellStrongLinkAndWeakConflict()
        {
            var board = TestHelpers.CreateEmptyBoard();
            ClearAllCandidates(board);
            AddCandidates(board, 0, 0, 1, 2);

            var model = ForcingChainLinkModel.Build(board);
            int one = ForcingChainLinkModel.MakeCandidateKey(board, 0, 0, 1);
            int two = ForcingChainLinkModel.MakeCandidateKey(board, 0, 0, 2);

            Assert.IsTrue(model.IsStrongLink(one, two));
            Assert.IsTrue(model.WeakLinksByKey[one].Contains(two));
            Assert.IsTrue(model.WeakLinksByKey[two].Contains(one));
        }

        [Test]
        public void Build_AddsConjugateStrongLinkAndSameDigitWeakConflict()
        {
            var board = TestHelpers.CreateEmptyBoard();
            ClearAllCandidates(board);
            AddCandidates(board, 0, 0, 1, 2);
            AddCandidates(board, 0, 4, 1, 3);

            var model = ForcingChainLinkModel.Build(board);
            int first = ForcingChainLinkModel.MakeCandidateKey(board, 0, 0, 1);
            int second = ForcingChainLinkModel.MakeCandidateKey(board, 0, 4, 1);

            Assert.IsTrue(model.IsStrongLink(first, second));
            Assert.IsTrue(model.WeakLinksByKey[first].Contains(second));
            Assert.IsTrue(model.WeakLinksByKey[second].Contains(first));
        }

        [Test]
        public void Build_IgnoresCandidatesInSolvedCells()
        {
            var board = TestHelpers.CreateEmptyBoard();
            ClearAllCandidates(board);
            AddCandidates(board, 0, 0, 1, 2);
            board.Cells[0, 0].Value = 1;

            var model = ForcingChainLinkModel.Build(board);

            Assert.IsEmpty(model.Candidates);
            Assert.IsEmpty(model.StrongLinksByKey);
            Assert.IsEmpty(model.WeakLinksByKey);
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
