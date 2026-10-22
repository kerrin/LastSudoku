using System;
using System.Collections;
using System.Collections.Generic;
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
        public void ForcingChainPropagation_CandidateOnlyVerityBranch_ReachesR1C7AsFour()
        {
            var board = CreateCandidateOnlyVerityBoard();
            var model = ForcingChainLinkModel.Build(board);
            int seed = ForcingChainLinkModel.MakeCandidateKey(board, 3, 3, 1);
            int target = ForcingChainLinkModel.MakeCandidateKey(board, 0, 6, 4);

            var branches = ForcingChainBranchGenerator.Generate(
                board,
                model,
                new[]
                {
                    seed,
                    ForcingChainLinkModel.MakeCandidateKey(board, 3, 5, 1),
                    ForcingChainLinkModel.MakeCandidateKey(board, 3, 6, 1)
                },
                assumeTrue: true,
                enableUnitCompletion: true);

            Assert.AreEqual(3, branches.Count);
            var requestedBranch = branches.Single(branch => branch.SeedCandidate == seed).State;
            Assert.IsFalse(requestedBranch.HasContradiction, requestedBranch.ContradictionReason);
            Assert.IsTrue(requestedBranch.TrueCandidates.Contains(target));
            Assert.IsTrue(
                branches.All(branch => branch.State.HasContradiction || branch.State.TrueCandidates.Contains(target)),
                "Every row-4 candidate branch must either prove r1c7=4 or lead to a contradiction.");
        }

        [Test]
        public void ForcingChainPropagation_ContradictionCandidate_HasPreviewableInferenceCause()
        {
            var board = CreateCandidateOnlyVerityBoard();
            var model = ForcingChainLinkModel.Build(board);
            int seed = ForcingChainLinkModel.MakeCandidateKey(board, 5, 7, 3);

            var branch = ForcingChainPropagation.PropagateFromAssumption(
                board,
                model,
                seed,
                assumeTrue: true,
                enableUnitCompletion: true);

            Assert.IsTrue(branch.HasContradiction);
            Assert.IsTrue(branch.ContradictionCandidate.HasValue);
            Assert.IsTrue(branch.InferenceCausesByCandidate.TryGetValue(branch.ContradictionCandidate.Value, out var causes));
            Assert.IsNotEmpty(causes);
            Assert.IsTrue(causes.Any(cause => cause.IsPreviewLink));
        }

        [Test]
        public void ForcingChainRule_CandidateOnlyVerityExample_PlacesR1C7AsFour()
        {
            var board = CreateCandidateOnlyVerityBoard();

            var result = new ForcingChainRule().CalculateChangesForCandidate(board, 0, 6, 4);

            Assert.IsTrue(result.Apply, result.Description);
            var placement = result.Changes.SingleOrDefault(
                change => change.Row == 0 && change.Column == 6 && change.NewValue == 4);
            Assert.IsNotNull(placement, result.Description);
            Assert.IsEmpty(placement.RemovedCandidates);
            Assert.IsFalse(board.Cells[0, 6].Value.HasValue);
            Assert.IsTrue(result.UsedDirectionalLinks.Any(link =>
                link.Kind == DirectionalLinkKind.Strong
                && link.Start.Row == 0 && link.Start.Column == 6 && link.Start.Digit == 3
                && link.End.Row == 0 && link.End.Column == 6 && link.End.Digit == 4));
        }

        [Test]
        public void ForcingChainRule_CandidateOnlyVerityExample_DefaultSearchKeepsBestScoredDeduction()
        {
            var board = CreateCandidateOnlyVerityBoard();

            var result = new ForcingChainRule().CalculateChanges(board);

            Assert.IsTrue(result.Apply, result.Description);
            Assert.IsTrue(result.Changes.Any(
                change => change.Row == 0 && change.Column == 7 && change.NewValue == 3));
            Assert.IsTrue(result.UsedCells.Any(
                cell => cell.Row == 0 && cell.Column == 7 && cell.Candidate == 3 && cell.HighlightTag == "Deduction"));
            Assert.IsTrue(result.UsedDirectionalLinks.Any(link => link.Kind == DirectionalLinkKind.Strong));
            Assert.IsTrue(result.UsedDirectionalLinks.Any(link => link.Kind == DirectionalLinkKind.Weak));
            Assert.IsTrue(result.Description.Contains("r6c8=3 leads to a contradiction"));
            Assert.IsTrue(result.Description.Contains("Box 5 has no location left for a digit"));
            Assert.IsTrue(result.UsedCells.Any(
                cell => cell.Row == 3 && cell.Column == 3 && cell.Candidate == 1 && cell.HighlightTag == "Failure"));
            Assert.IsTrue(result.UsedCells.Any(
                cell => cell.Row == 3 && cell.Column == 5 && cell.Candidate == 1 && cell.HighlightTag == "Failure"));
            Assert.IsTrue(result.UsedCells.Any(
                cell => cell.Row == 4 && cell.Column == 3 && cell.Candidate == 1 && cell.HighlightTag == "Failure"));
            Assert.IsTrue(result.UsedCells.Any(
                cell => cell.Row == 4 && cell.Column == 5 && cell.Candidate == 1 && cell.HighlightTag == "Failure"));
            Assert.IsTrue(result.UsedDirectionalLinks.Any(link =>
                link.Kind == DirectionalLinkKind.Weak
                && link.Start.Row == 7 && link.Start.Column == 3 && link.Start.Digit == 1
                && link.End.Row == 4 && link.End.Column == 3 && link.End.Digit == 1));
            Assert.IsTrue(result.UsedDirectionalLinks.Any(link =>
                link.Kind == DirectionalLinkKind.Weak
                && link.Start.Row == 4 && link.Start.Column == 7 && link.Start.Digit == 1
                && link.End.Row == 4 && link.End.Column == 5 && link.End.Digit == 1));
        }

        [Test]
        public void ForcingChainRule_CandidateSpecificSearch_DoesNotReturnAnUnrelatedDeduction()
        {
            var board = CreateCandidateOnlyVerityBoard();

            var result = new ForcingChainRule().CalculateChangesForCandidate(board, 0, 0, 1);

            Assert.IsFalse(result.Apply);
            Assert.IsEmpty(result.Changes);
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
        public void ForcingChainRule_VerityPreview_ShowsConclusionPathBeforeLaterContradiction()
        {
            var board = TestHelpers.CreateEmptyBoard();
            int seed = ForcingChainLinkModel.MakeCandidateKey(board, 0, 0, 1);
            int conclusion = ForcingChainLinkModel.MakeCandidateKey(board, 0, 1, 2);
            int contradiction = ForcingChainLinkModel.MakeCandidateKey(board, 0, 2, 3);

            var assembly = typeof(ForcingChainRule).Assembly;
            var flags = BindingFlagsForPlan;
            var planType = assembly.GetType("Sudoku.Solver.Rules.ForcingPlan", throwOnError: true);
            var branchType = assembly.GetType("Sudoku.Solver.Rules.ForcingChainBranch", throwOnError: true);
            var stateType = assembly.GetType("Sudoku.Solver.Rules.PropagationState", throwOnError: true);
            var causeType = assembly.GetType("Sudoku.Solver.Rules.InferenceCause", throwOnError: true);
            var plan = Activator.CreateInstance(planType);
            var state = Activator.CreateInstance(stateType);
            var branch = Activator.CreateInstance(branchType);

            branchType.GetField("SeedCandidate", flags).SetValue(branch, seed);
            branchType.GetField("State", flags).SetValue(branch, state);
            planType.GetField("CommonTrueCandidates", flags).SetValue(plan, new List<int> { conclusion });
            ((IList)planType.GetField("VerityBranches", flags).GetValue(plan)).Add(branch);

            ((ICollection<int>)stateType.GetField("TrueCandidates", flags).GetValue(state)).Add(conclusion);
            stateType.GetField("HasContradiction", flags).SetValue(state, true);
            stateType.GetField("ContradictionCandidate", flags).SetValue(state, contradiction);
            stateType.GetField("ContradictionSourceCandidate", flags).SetValue(state, seed);
            stateType.GetField("ContradictionSourceLinkKind", flags).SetValue(state, DirectionalLinkKind.Strong);

            var cause = Activator.CreateInstance(causeType);
            causeType.GetField("FromCandidate", flags).SetValue(cause, seed);
            causeType.GetField("LinkKind", flags).SetValue(cause, DirectionalLinkKind.Strong);
            causeType.GetField("IsPreviewLink", flags).SetValue(cause, true);
            var causesByCandidate = (IDictionary)stateType.GetField("InferenceCausesByCandidate", flags).GetValue(state);
            var causeListType = typeof(List<>).MakeGenericType(causeType);
            causesByCandidate[conclusion] = Activator.CreateInstance(causeListType);
            ((IList)causesByCandidate[conclusion]).Add(cause);

            var result = new RuleResult();
            result.Changes.Add(new CellChange { Row = 0, Column = 1, NewValue = 2 });
            var appendEvidence = typeof(ForcingChainRule).GetMethod(
                "AppendEvidence",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            appendEvidence.Invoke(new ForcingChainRule(), new[] { board, result, plan });

            Assert.IsTrue(result.UsedCells.Any(cell =>
                cell.Row == 0 && cell.Column == 1 && cell.Candidate == 2));
            Assert.IsTrue(result.UsedCells.Any(cell =>
                cell.Row == 0 && cell.Column == 2 && cell.Candidate == 3));
            Assert.IsTrue(result.UsedDirectionalLinks.Any(link =>
                link.Start.Row == 0 && link.Start.Column == 0 && link.Start.Digit == 1
                && link.End.Row == 0 && link.End.Column == 1 && link.End.Digit == 2));
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

            Assert.AreEqual(100, result.UsedCells.Count);
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

        private static Board CreateCandidateOnlyVerityBoard()
        {
            var board = TestHelpers.CreateEmptyBoard();
            ClearAllCandidates(board);

            // Row 4 contains the three possible locations for digit 1.
            AddCandidates(board, 3, 3, 1, 6);
            AddCandidates(board, 3, 5, 1, 3, 5, 9); // Note 5 and 9 are not part of the forcing chain, but are needed for dudctions
            AddCandidates(board, 3, 6, 1, 3, 9);

            // Candidate links for the first branch:
            // r4c4=1 -> r8c4=6 -> r6c8=6 -> r1c8=3 -> r1c7=4.
            AddCandidates(board, 7, 3, 1, 6);
            AddCandidates(board, 7, 7, 1, 6);
            AddCandidates(board, 5, 7, 3, 6);
            AddCandidates(board, 0, 6, 3, 4);
            AddCandidates(board, 0, 7, 3, 5);

            // Candidate links for the remaining branches:
            // r4c6=1 -> r4c7=3 -> r1c7=4, and
            // r4c7=1 -> r5c8=5 -> r1c8=3 -> r1c7=4.
            AddCandidates(board, 4, 7, 1, 5);

            // Other candidates to make the leaks weak and strong as needed for the test
            AddCandidates(board, 0, 8, 4, 5);
            AddCandidates(board, 3, 2, 7, 9);
            AddCandidates(board, 3, 4, 5, 6, 7);
            AddCandidates(board, 3, 8, 5, 6, 7);
            AddCandidates(board, 4, 2, 2, 7, 9);
            AddCandidates(board, 4, 3, 1, 2, 4, 7, 9);
            AddCandidates(board, 4, 4, 4, 5, 7, 8);
            AddCandidates(board, 4, 5, 1, 2, 5, 9);
            AddCandidates(board, 4, 6, 1, 4, 8, 9);
            AddCandidates(board, 4, 8, 4, 5, 7, 8);
            AddCandidates(board, 5, 2, 2, 7, 9);
            AddCandidates(board, 5, 3, 2, 4, 6, 7, 9);
            AddCandidates(board, 5, 4, 4, 6, 7, 8);
            AddCandidates(board, 5, 5, 2, 3, 9);
            AddCandidates(board, 5, 6, 3, 4, 8, 9);
            AddCandidates(board, 5, 8, 4, 6, 7, 8);
            AddCandidates(board, 6, 3, 1, 2, 4, 6);
            AddCandidates(board, 6, 4, 4, 6);
            AddCandidates(board, 6, 5, 1, 2);
            AddCandidates(board, 6, 6, 1, 8);
            AddCandidates(board, 6, 8, 6, 8);
            AddCandidates(board, 8, 3, 7, 9);
            AddCandidates(board, 8, 4, 5, 7);
            AddCandidates(board, 8, 5, 5, 9);

            // Now add the values as they are also needed for deductions in the forcing chain.
            // TODO: KH: Check if we can remove these once the test passes, as they should in theory not be needed for the forcing chain to work.
            AddValue(board, 0, 0, 2);
            AddValue(board, 0, 1, 7);
            AddValue(board, 0, 2, 1);
            AddValue(board, 0, 3, 8);
            AddValue(board, 0, 4, 9);
            AddValue(board, 0, 5, 6);

            AddValue(board, 1, 0, 9);
            AddValue(board, 1, 1, 4);
            AddValue(board, 1, 2, 3);
            AddValue(board, 1, 3, 5);
            AddValue(board, 1, 4, 2);
            AddValue(board, 1, 5, 7);
            AddValue(board, 1, 6, 6);
            AddValue(board, 1, 7, 8);
            AddValue(board, 1, 8, 1);

            AddValue(board, 2, 0, 8);
            AddValue(board, 2, 1, 5);
            AddValue(board, 2, 2, 6);
            AddValue(board, 2, 3, 3);
            AddValue(board, 2, 4, 1);
            AddValue(board, 2, 5, 4);
            AddValue(board, 2, 6, 7);
            AddValue(board, 2, 7, 9);
            AddValue(board, 2, 8, 2);

            AddValue(board, 3, 0, 4);
            AddValue(board, 3, 1, 8);
            AddValue(board, 3, 7, 2);

            AddValue(board, 4, 0, 6);
            AddValue(board, 4, 1, 3);

            AddValue(board, 5, 0, 5);
            AddValue(board, 5, 1, 1);

            AddValue(board, 6, 0, 3);
            AddValue(board, 6, 1, 9);
            AddValue(board, 6, 2, 5);
            AddValue(board, 6, 7, 7);

            AddValue(board, 7, 0, 7);
            AddValue(board, 7, 1, 2);
            AddValue(board, 7, 2, 4);
            AddValue(board, 7, 4, 3);
            AddValue(board, 7, 5, 8);
            AddValue(board, 7, 6, 5);
            AddValue(board, 7, 8, 9);

            AddValue(board, 8, 0, 1);
            AddValue(board, 8, 1, 6);
            AddValue(board, 8, 2, 8);
            AddValue(board, 8, 6, 2);
            AddValue(board, 8, 7, 4);
            AddValue(board, 8, 8, 3);

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

        private static void AddValue(Board board, int row, int column, int value)
        {
            board.Cells[row, column].Value = value;
        }

    }
}
