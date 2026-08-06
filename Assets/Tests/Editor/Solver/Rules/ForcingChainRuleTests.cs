using System.Linq;
using NUnit.Framework;
using Sudoku.Models;
using Sudoku.Solver.Rules;
using Sudoku.UI.Config;

namespace Sudoku.Tests.Editor
{
    public class ForcingChainRuleTests
    {
        private bool _originalGreen;
        private bool _originalAmber;
        private bool _originalRed;
        private bool _originalBlue;

        [SetUp]
        public void Setup()
        {
            _originalGreen = ColourSettings.GreenEnabled;
            _originalAmber = ColourSettings.AmberEnabled;
            _originalRed = ColourSettings.RedEnabled;
            _originalBlue = ColourSettings.BlueEnabled;

            ColourSettings.GreenEnabled = true;
            ColourSettings.AmberEnabled = true;
            ColourSettings.RedEnabled = true;
            ColourSettings.BlueEnabled = false;
        }

        [TearDown]
        public void Teardown()
        {
            ColourSettings.GreenEnabled = _originalGreen;
            ColourSettings.AmberEnabled = _originalAmber;
            ColourSettings.RedEnabled = _originalRed;
            ColourSettings.BlueEnabled = _originalBlue;
        }

        // [Test]
        // public void ForcingChainRule_Example1_ContradictionBranch_RemovesAssumptionCandidate()
        // {
        //     var board = TestHelpers.CreateEmptyBoard();
        //     var rule = new ForcingChainRule();

        //     ClearAllCandidates(board);

        //     // Seed cell S: r1c1 has {1,2}; assume r1c1=1 creates a contradiction chain.
        //     AddCandidates(board, 0, 0, 1, 2);
        //     AddCandidates(board, 0, 4, 1, 3);
        //     AddCandidates(board, 0, 8, 1, 3);

        //     var result = rule.CalculateChanges(board);
        //     Assert.IsTrue(result.Apply);

        //     var change = result.Changes.FirstOrDefault(ch => ch.Row == 0 && ch.Column == 0);
        //     Assert.IsNotNull(change);
        //     Assert.IsTrue(change.RemovedCandidates.Contains(1));
        //     Assert.IsFalse(change.NewValue.HasValue);
        // }

        [Test]
        public void ForcingChainRule_Example2_CommonFalseConclusion_RemovesTargetCandidate()
        {
            var board = TestHelpers.CreateEmptyBoard();
            var rule = new ForcingChainRule();

            ClearAllCandidates(board);

            // Branch A (S=true) forces r5c5<>3 via r1c5=3.
            // Branch B (S=false) forces r5c5<>3 via r5c1=3.
            // Keep 3 non-conjugate at r5c5 by adding extra 3s in row 5 and column 5.
            AddCandidates(board, 0, 0, 1, 2);     // S
            AddCandidates(board, 0, 4, 1, 3);     // A
            AddCandidates(board, 4, 0, 2, 3);     // D
            AddCandidates(board, 4, 4, 3, 6, 7);  // Y target
            AddCandidates(board, 4, 8, 3, 8);     // extra row-5 digit-3 candidate
            AddCandidates(board, 8, 4, 3, 9);     // extra column-5 digit-3 candidate

            var result = rule.CalculateChanges(board);
            Assert.IsTrue(result.Apply);

            var target = result.Changes.FirstOrDefault(ch => ch.Row == 4 && ch.Column == 4);
            Assert.IsNotNull(target);
            Assert.IsTrue(target.RemovedCandidates.Contains(3));
        }

        [Test]
        public void ForcingChainRule_Example3_CommonTrueConclusion_PlacesValue()
        {
            var board = TestHelpers.CreateEmptyBoard();
            var rule = new ForcingChainRule();

            ClearAllCandidates(board);

            // Same forcing scaffold as example 2, but target is bi-value.
            // Both branches force r5c5<>3, therefore r5c5=4.
            // Keep 3 non-conjugate at r5c5 by adding extra 3s in row 5 and column 5.
            AddCandidates(board, 0, 0, 1, 2);  // S
            AddCandidates(board, 0, 4, 1, 3);  // A
            AddCandidates(board, 4, 0, 2, 3);  // D
            AddCandidates(board, 4, 4, 3, 4);  // Y target
            AddCandidates(board, 4, 8, 3, 8);  // extra row-5 digit-3 candidate
            AddCandidates(board, 8, 4, 3, 9);  // extra column-5 digit-3 candidate

            var result = rule.CalculateChanges(board);
            Assert.IsTrue(result.Apply);

            var placement = result.Changes.FirstOrDefault(ch => ch.Row == 4 && ch.Column == 4 && ch.NewValue == 4);
            Assert.IsNotNull(placement);
        }

        [Test]
        public void ForcingChainRule_Example4_ColourPrerequisiteDisabled_DoesNotApply()
        {
            var board = TestHelpers.CreateEmptyBoard();
            var rule = new ForcingChainRule();

            ClearAllCandidates(board);
            AddCandidates(board, 0, 0, 1, 2);
            AddCandidates(board, 0, 4, 1, 3);
            AddCandidates(board, 4, 0, 2, 3);
            AddCandidates(board, 4, 4, 3, 4);

            ColourSettings.GreenEnabled = true;
            ColourSettings.AmberEnabled = false;
            ColourSettings.RedEnabled = false;
            ColourSettings.BlueEnabled = false;

            Assert.IsFalse(rule.CanApply(board));
            var result = rule.CalculateChanges(board);
            Assert.IsFalse(result.Apply);
            Assert.IsEmpty(result.Changes);
        }

        /**
         * Remove all candidates from all cells.
         *
         * @param board Target board.
         */
        private static void ClearAllCandidates(Board board)
        {
            for (int row = 0; row < board.Size; row++)
            {
                for (int column = 0; column < board.Size; column++)
                {
                    board.Cells[row, column].Candidates.Clear();
                }
            }
        }

        /**
         * Add the given candidates to one cell.
         *
         * @param board Target board.
         * @param row Cell row index.
         * @param column Cell column index.
         * @param candidates Candidate digits to assign.
         */
        private static void AddCandidates(Board board, int row, int column, params int[] candidates)
        {
            var cell = board.Cells[row, column];
            cell.Candidates.Clear();
            for (int i = 0; i < candidates.Length; i++)
            {
                cell.Candidates.Add(candidates[i]);
            }
        }
    }
}
