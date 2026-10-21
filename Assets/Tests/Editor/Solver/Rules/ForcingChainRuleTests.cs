using System;
using System.Collections;
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

        [Test]
        public void ForcingChainRule_ContradictionBranch_RejectsImpossibleAssumption()
        {
            var board = TestHelpers.CreateEmptyBoard();
            var rule = new ForcingChainRule();

            ClearAllCandidates(board);
            AddCandidates(board, 0, 0, 1, 2);
            AddCandidates(board, 0, 4, 1, 3);
            AddCandidates(board, 0, 8, 1, 3);

            var result = rule.CalculateChanges(board);
            Assert.IsTrue(result.Apply);

            var change = result.Changes.FirstOrDefault(ch => ch.Row == 0 && ch.Column == 0);
            Assert.IsNotNull(change);
            Assert.IsTrue(
                change.RemovedCandidates.Contains(1) || change.NewValue == 2,
                "The contradiction should eliminate candidate 1 or place its only alternative, candidate 2.");
        }

        [Test]
        public void ForcingChainRule_FalseAssumptionContradiction_PlacesAssumptionCandidate()
        {
            var board = TestHelpers.CreateEmptyBoard();
            var rule = new ForcingChainRule();

            ClearAllCandidates(board);
            AddCandidates(board, 0, 0, 1, 2);
            AddCandidates(board, 0, 4, 2, 3);
            AddCandidates(board, 0, 8, 2, 3);

            var result = rule.CalculateChanges(board);
            Assert.IsTrue(result.Apply);

            var placement = result.Changes.FirstOrDefault(
                change => change.Row == 0 && change.Column == 0 && change.NewValue == 1);
            Assert.IsNotNull(placement);
            Assert.IsEmpty(placement.RemovedCandidates);
        }

        [Test]
        public void ForcingChainRule_FalseBranchContradiction_PlacesInsteadOfRemovingSeed()
        {
            var board = TestHelpers.CreateEmptyBoard();
            ClearAllCandidates(board);
            AddCandidates(board, 0, 0, 1, 2);

            var assembly = typeof(ForcingChainRule).Assembly;
            var planType = assembly.GetType("Sudoku.Solver.Rules.ForcingPlan", throwOnError: true);
            var plan = Activator.CreateInstance(planType);
            planType.GetField("SeedCandidate", BindingFlagsForPlan).SetValue(plan, 1);
            planType.GetField("ContradictionOnFalseBranch", BindingFlagsForPlan).SetValue(plan, true);

            var result = new RuleResult();
            var applyPlan = typeof(ForcingChainRule).GetMethod(
                "ApplyContradictionPlan",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(applyPlan);
            applyPlan.Invoke(new ForcingChainRule(), new object[] { board, result, plan });

            var placement = result.Changes.Single();
            Assert.AreEqual(0, placement.Row);
            Assert.AreEqual(0, placement.Column);
            Assert.AreEqual(1, placement.NewValue);
            Assert.IsEmpty(placement.RemovedCandidates);
        }

        [Test]
        public void ForcingChainRule_CommonFalseConclusion_RemovesTargetCandidate()
        {
            var board = TestHelpers.CreateEmptyBoard();
            var rule = new ForcingChainRule();

            ClearAllCandidates(board);
            AddCandidates(board, 0, 0, 1, 2);
            AddCandidates(board, 0, 4, 1, 3);
            AddCandidates(board, 4, 0, 2, 3);
            AddCandidates(board, 4, 4, 3, 6, 7);
            AddCandidates(board, 4, 8, 3, 8);
            AddCandidates(board, 8, 4, 3, 9);

            var result = rule.CalculateChanges(board);
            Assert.IsTrue(result.Apply);

            var target = result.Changes.FirstOrDefault(ch => ch.Row == 4 && ch.Column == 4);
            Assert.IsNotNull(target);
            Assert.IsTrue(target.RemovedCandidates.Contains(3));
        }

        [Test]
        public void ForcingChainRule_CommonTrueConclusion_PlacesValue()
        {
            var board = TestHelpers.CreateEmptyBoard();
            var rule = new ForcingChainRule();

            ClearAllCandidates(board);
            AddCandidates(board, 0, 0, 1, 2);
            AddCandidates(board, 0, 4, 1, 3);
            AddCandidates(board, 4, 0, 2, 3);
            AddCandidates(board, 4, 4, 3, 4);
            AddCandidates(board, 4, 8, 3, 8);
            AddCandidates(board, 8, 4, 3, 9);

            var result = rule.CalculateChanges(board);
            Assert.IsTrue(result.Apply);

            var placement = result.Changes.FirstOrDefault(
                change => change.Row == 4 && change.Column == 4 && change.NewValue == 4);
            Assert.IsNotNull(placement);
        }

        [Test]
        public void ForcingChainRule_Preview_UsesDedicatedSeedTag()
        {
            var board = CreateCommonConclusionBoard();
            var result = new ForcingChainRule().CalculateChanges(board);

            Assert.IsTrue(result.Apply);
            Assert.IsTrue(result.UsedCells.Any(
                cell => cell.HighlightTag == "Seed" && cell.Row == 0 && cell.Column == 0));
        }

        [Test]
        public void ForcingChainRule_UnitSeedPathEvidence_AlternatesTargetColoursAlongChain()
        {
            var board = CreateCommonConclusionBoard();
            var result = new ForcingChainRule().CalculateChanges(board);
            Assert.IsTrue(result.Apply);

            var chainTargets = result.UsedCells
                .Where(cell => cell.HighlightTag == "TargetA" || cell.HighlightTag == "TargetB")
                .ToList();
            Assert.IsTrue(chainTargets.Count > 2);
            Assert.IsTrue(chainTargets.Any(cell => cell.Row != 0 || cell.Column != 0));

            var targetTags = chainTargets.Select(cell => cell.HighlightTag).Distinct().ToList();
            Assert.IsTrue(targetTags.Contains("TargetA"));
            Assert.IsTrue(targetTags.Contains("TargetB"));
        }

        [Test]
        public void ForcingChainRule_ColourPrerequisiteDisabled_DoesNotApply()
        {
            var board = CreateCommonConclusionBoard();
            var rule = new ForcingChainRule();

            ColourSettings.GreenEnabled = true;
            ColourSettings.AmberEnabled = false;
            ColourSettings.RedEnabled = false;
            ColourSettings.BlueEnabled = false;

            Assert.IsFalse(rule.CanApply(board));
            var result = rule.CalculateChanges(board);
            Assert.IsFalse(result.Apply);
            Assert.IsEmpty(result.Changes);
        }

        [Test]
        public void ForcingChainRule_NoValidPlan_ReturnsNoApply()
        {
            var board = TestHelpers.CreateEmptyBoard();
            ClearAllCandidates(board);
            AddCandidates(board, 0, 0, 1, 2);

            var result = new ForcingChainRule().CalculateChanges(board);
            Assert.IsFalse(result.Apply);
            Assert.IsEmpty(result.Changes);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ForcingChainRule_EvidenceLimit_TruncatesPreview(bool useTrueBranch)
        {
            var assembly = typeof(ForcingChainRule).Assembly;
            var planType = assembly.GetType("Sudoku.Solver.Rules.ForcingPlan", throwOnError: true);
            var stateType = assembly.GetType("Sudoku.Solver.Rules.PropagationState", throwOnError: true);

            var plan = Activator.CreateInstance(planType);
            var trueBranch = Activator.CreateInstance(stateType);
            var falseBranch = Activator.CreateInstance(stateType);

            planType.GetField("SeedCandidate", BindingFlagsForPlan)
                .SetValue(plan, 1);
            planType.GetField("TrueBranch", BindingFlagsForPlan)
                .SetValue(plan, trueBranch);
            planType.GetField("FalseBranch", BindingFlagsForPlan)
                .SetValue(plan, falseBranch);

            var branch = useTrueBranch ? trueBranch : falseBranch;
            var assignmentOrderField = stateType.GetField("AssignmentOrder", BindingFlagsForPlan);
            var evidence = (IList)assignmentOrderField.GetValue(branch);
            for (int i = 0; i < 101; i++)
            {
                evidence.Add(i);
            }

            var result = new RuleResult();
            var appendEvidence = typeof(ForcingChainRule).GetMethod(
                "AppendEvidence",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(appendEvidence);

            appendEvidence.Invoke(new ForcingChainRule(), new object[]
            {
                TestHelpers.CreateEmptyBoard(),
                result,
                plan
            });

            Assert.AreEqual(20, result.UsedCells.Count);
        }

        private const System.Reflection.BindingFlags BindingFlagsForPlan =
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic;

        private static Board CreateCommonConclusionBoard()
        {
            var board = TestHelpers.CreateEmptyBoard();
            ClearAllCandidates(board);
            AddCandidates(board, 0, 0, 1, 2);
            AddCandidates(board, 0, 4, 1, 3);
            AddCandidates(board, 4, 0, 2, 3);
            AddCandidates(board, 4, 4, 3, 6, 7);
            AddCandidates(board, 4, 8, 3, 8);
            AddCandidates(board, 8, 4, 3, 9);
            return board;
        }

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
