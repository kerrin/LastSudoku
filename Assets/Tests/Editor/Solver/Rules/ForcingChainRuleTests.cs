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
        public void ForcingChainRule_PreviewSummary_DistinguishesContradictionPolarityAndVerity()
        {
            var panelType = typeof(ForcingChainRule).Assembly.GetType(
                "Sudoku.UI.Panels.ApplyRulePanel",
                throwOnError: true);
            var summarize = panelType.GetMethod(
                "BuildForcingChainPreviewSummary",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.NotNull(summarize);

            var removeResult = new RuleResult
            {
                Description = "Forcing Chain: assuming r5c5=5 is true leads to a contradiction; remove 5 from r5c5."
            };
            removeResult.Changes.Add(new CellChange { Row = 4, Column = 4, RemovedCandidates = new List<int> { 5 } });
            Assert.AreEqual(
                "If r5c5=5 is true, contradiction; remove 5 from r5c5",
                summarize.Invoke(null, new object[] { removeResult }));

            var setResult = new RuleResult
            {
                Description = "Forcing Chain: assuming r4c4=5 is false leads to a contradiction; set r4c4=5."
            };
            setResult.Changes.Add(new CellChange { Row = 3, Column = 3, NewValue = 5 });
            Assert.AreEqual(
                "If r4c4=5 is false, contradiction; set r4c4=5",
                summarize.Invoke(null, new object[] { setResult }));

            var verityResult = new RuleResult
            {
                Description = "Forcing Chain: r6c8=3 is impossible; therefore the remaining placement is r1c8=3."
            };
            verityResult.Changes.Add(new CellChange { Row = 0, Column = 7, NewValue = 3 });
            Assert.AreEqual(
                "Verity: r6c8=3 is impossible; set r1c8=3",
                summarize.Invoke(null, new object[] { verityResult }));
        }

        [Test]
        public void BoardVisualizer_ForcingChainColors_UseGreenForTrueAndRedForFalseWhenAvailable()
        {
            var visualizerType = typeof(ForcingChainRule).Assembly.GetType(
                "Sudoku.Solver.BoardVisualizer",
                throwOnError: true);
            var resolveColor = visualizerType.GetMethod(
                "ResolveChainHighlightColor",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.NotNull(resolveColor);

            var trueColor = (UnityEngine.Color)resolveColor.Invoke(null, new object[] { "TargetA", 0.52f });
            var falseColor = (UnityEngine.Color)resolveColor.Invoke(null, new object[] { "TargetB", 0.52f });
            Assert.AreEqual(HighlightColorPalette.Green.r, trueColor.r);
            Assert.AreEqual(HighlightColorPalette.Green.g, trueColor.g);
            Assert.AreEqual(HighlightColorPalette.Red.r, falseColor.r);
            Assert.AreEqual(HighlightColorPalette.Red.g, falseColor.g);

            var resolveUsedCellColor = visualizerType.GetMethod(
                "ResolveUsedCellHighlightColor",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.NotNull(resolveUsedCellColor);
            var fallbackColor = UnityEngine.Color.white;
            var expectedDeductionColor = new UnityEngine.Color(1f, 0.72f, 0.05f, 1f);
            foreach (string tag in new[] { "Deduction", "TrueDeduction", "FalseDeduction" })
            {
                var deductionColor = (UnityEngine.Color)resolveUsedCellColor.Invoke(
                    null,
                    new object[] { tag, fallbackColor });
                Assert.AreEqual(expectedDeductionColor, deductionColor, $"{tag} should remain yellow.");
            }

            ColourSettings.RedEnabled = false;
            var fallbackFalseColor = (UnityEngine.Color)resolveColor.Invoke(null, new object[] { "TargetB", 0.52f });
            Assert.AreEqual(HighlightColorPalette.Amber.r, fallbackFalseColor.r);
            Assert.AreEqual(HighlightColorPalette.Amber.g, fallbackFalseColor.g);

            ColourSettings.GreenEnabled = false;
            ColourSettings.AmberEnabled = false;
            ColourSettings.BlueEnabled = true;
            ColourSettings.RedEnabled = true;
            var fallbackTrueColor = (UnityEngine.Color)resolveColor.Invoke(null, new object[] { "TargetA", 0.52f });
            falseColor = (UnityEngine.Color)resolveColor.Invoke(null, new object[] { "TargetB", 0.52f });
            Assert.AreEqual(HighlightColorPalette.Blue.r, fallbackTrueColor.r);
            Assert.AreEqual(HighlightColorPalette.Blue.g, fallbackTrueColor.g);
            Assert.AreEqual(HighlightColorPalette.Red.r, falseColor.r);
            Assert.AreEqual(HighlightColorPalette.Red.g, falseColor.g);
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
        public void ForcingChainPropagation_CandidateOnlyR6C8ThreeAssumption_RemovesEveryOneFromBoxFive()
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
            Assert.AreEqual("Box 5 has no location left for a digit.", branch.ContradictionReason);
            CollectionAssert.AreEquivalent(
                new[] { "r4c4=1", "r4c6=1", "r5c4=1", "r5c6=1" },
                branch.ContradictionGroupCandidates.Select(candidate =>
                {
                    int row = ForcingChainLinkModel.GetRow(board, candidate) + 1;
                    int column = ForcingChainLinkModel.GetColumn(board, candidate) + 1;
                    int digit = ForcingChainLinkModel.GetDigit(board, candidate);
                    return $"r{row}c{column}={digit}";
                }));
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
                cell => cell.Row == 0 && cell.Column == 7 && cell.Candidate == 3 && cell.HighlightTag == "TrueDeduction"));
            Assert.IsTrue(result.UsedDirectionalLinks.Any(link => link.Kind == DirectionalLinkKind.Strong));
            Assert.IsTrue(result.UsedDirectionalLinks.Any(link => link.Kind == DirectionalLinkKind.Weak));
            Assert.IsTrue(result.Description.Contains("r6c8=3 is impossible"));
            Assert.IsTrue(result.Description.Contains("Box 5 has no location left for a digit"));
            Assert.IsTrue(result.UsedCells.Any(
                cell => cell.Row == 3 && cell.Column == 3 && cell.Candidate == 1 && cell.HighlightTag == "TargetB"));
            Assert.IsTrue(result.UsedCells.Any(
                cell => cell.Row == 3 && cell.Column == 5 && cell.Candidate == 1 && cell.HighlightTag == "TargetB"));
            Assert.IsTrue(result.UsedCells.Any(
                cell => cell.Row == 4 && cell.Column == 3 && cell.Candidate == 1 && cell.HighlightTag == "TargetB"));
            Assert.IsTrue(result.UsedCells.Any(
                cell => cell.Row == 4 && cell.Column == 5 && cell.Candidate == 1 && cell.HighlightTag == "TargetB"));
            int[] conflictingOnes =
            {
                ForcingChainLinkModel.MakeCandidateKey(board, 3, 3, 1),
                ForcingChainLinkModel.MakeCandidateKey(board, 3, 5, 1),
                ForcingChainLinkModel.MakeCandidateKey(board, 4, 3, 1),
                ForcingChainLinkModel.MakeCandidateKey(board, 4, 5, 1)
            };
            var contradictionConflictLinks = result.UsedDirectionalLinks
                .Where(link => link.Kind == DirectionalLinkKind.Conflict
                    && conflictingOnes.Contains(ForcingChainLinkModel.MakeCandidateKey(
                        board, link.Start.Row, link.Start.Column, link.Start.Digit))
                    && conflictingOnes.Contains(ForcingChainLinkModel.MakeCandidateKey(
                        board, link.End.Row, link.End.Column, link.End.Digit)))
                .ToList();
            Assert.AreEqual(6, contradictionConflictLinks.Count,
                "The four contradictory 1 candidates in Box 5 should be connected by blue conflict links.");
            for (int i = 0; i < conflictingOnes.Length; i++)
            {
                for (int j = i + 1; j < conflictingOnes.Length; j++)
                {
                    int first = conflictingOnes[i];
                    int second = conflictingOnes[j];
                    Assert.IsTrue(contradictionConflictLinks.Any(link =>
                    {
                        int start = ForcingChainLinkModel.MakeCandidateKey(
                            board, link.Start.Row, link.Start.Column, link.Start.Digit);
                        int end = ForcingChainLinkModel.MakeCandidateKey(
                            board, link.End.Row, link.End.Column, link.End.Digit);
                        return (start == first && end == second) || (start == second && end == first);
                    }));
                }
            }
            Assert.IsTrue(result.UsedCells.Any(
                cell => cell.Row == 3 && cell.Column == 5 && cell.Candidate == 3
                    && (cell.HighlightTag == "TargetA" || cell.HighlightTag == "TargetB")));
            Assert.IsTrue(
                new[] { "Seed", "TargetA" }.Contains(
                    result.UsedCells.Single(cell => cell.Row == 5 && cell.Column == 7 && cell.Candidate == 3).HighlightTag));
            Assert.AreEqual(
                "TargetB",
                result.UsedCells.Single(cell => cell.Row == 5 && cell.Column == 7 && cell.Candidate == 6).HighlightTag);
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
        public void ForcingChainRule_CompletionConclusion_DoesNotRenderAsDirectionalLink()
        {
            var board = TestHelpers.CreateEmptyBoard();
            int seed = ForcingChainLinkModel.MakeCandidateKey(board, 0, 0, 1);
            int hiddenIntermediate = ForcingChainLinkModel.MakeCandidateKey(board, 0, 0, 2);
            int support = ForcingChainLinkModel.MakeCandidateKey(board, 0, 1, 1);
            int conclusion = ForcingChainLinkModel.MakeCandidateKey(board, 0, 2, 1);

            var state = new PropagationState();
            state.TrueCandidates.Add(conclusion);
            state.FalseCandidates.Add(support);
            state.CompletionSupportsByCandidate[conclusion] = new List<int> { support };
            state.InferenceCausesByCandidate[support] = new List<InferenceCause>
            {
                new InferenceCause
                {
                    FromCandidate = hiddenIntermediate,
                    LinkKind = DirectionalLinkKind.Weak,
                    IsPreviewLink = true
                },
                new InferenceCause
                {
                    FromCandidate = seed,
                    LinkKind = DirectionalLinkKind.Weak,
                    IsPreviewLink = true
                }
            };
            state.InferenceCausesByCandidate[hiddenIntermediate] = new List<InferenceCause>
            {
                new InferenceCause
                {
                    FromCandidate = seed,
                    LinkKind = DirectionalLinkKind.Weak,
                    IsPreviewLink = false
                }
            };

            var plan = new ForcingPlan();
            plan.CommonTrueCandidates.Add(conclusion);
            plan.VerityBranches.Add(new ForcingChainBranch { SeedCandidate = seed, State = state });

            var result = new RuleResult();
            result.Changes.Add(new CellChange { Row = 0, Column = 2, NewValue = 1 });
            typeof(ForcingChainRule).GetMethod(
                "AppendEvidence",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(new ForcingChainRule(), new object[] { board, result, plan });

            Assert.IsTrue(result.UsedDirectionalLinks.Any(link =>
                link.Start.Row == 0 && link.Start.Column == 0 && link.Start.Digit == 1
                && link.End.Row == 0 && link.End.Column == 1 && link.End.Digit == 1));
            Assert.IsFalse(result.UsedDirectionalLinks.Any(link => link.Kind == DirectionalLinkKind.Completion));
            Assert.AreEqual(
                "TargetB",
                result.UsedCells.Single(cell => cell.Row == 0 && cell.Column == 1 && cell.Candidate == 1).HighlightTag);
            Assert.AreEqual(
                "TrueDeduction",
                result.UsedCells.Single(cell => cell.Row == 0 && cell.Column == 2 && cell.Candidate == 1).HighlightTag);
        }

        [Test]
        public void ForcingChainRule_VerityPreview_ConnectsConclusionToRejectedSameDigitBranch()
        {
            var board = TestHelpers.CreateEmptyBoard();
            int rejectedSeed = ForcingChainLinkModel.MakeCandidateKey(board, 0, 0, 1);
            int conclusion = ForcingChainLinkModel.MakeCandidateKey(board, 0, 2, 1);

            var rejectedState = new PropagationState
            {
                HasContradiction = true,
                ContradictionCandidate = rejectedSeed,
                ContradictionReason = "Contradiction"
            };
            var survivingState = new PropagationState();
            survivingState.TrueCandidates.Add(conclusion);
            var plan = new ForcingPlan();
            plan.CommonTrueCandidates.Add(conclusion);
            plan.VerityBranches.Add(new ForcingChainBranch { SeedCandidate = rejectedSeed, State = rejectedState });
            plan.VerityBranches.Add(new ForcingChainBranch { SeedCandidate = conclusion, State = survivingState });

            var result = new RuleResult();
            result.Changes.Add(new CellChange { Row = 0, Column = 2, NewValue = 1 });
            typeof(ForcingChainRule).GetMethod(
                "AppendEvidence",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(new ForcingChainRule(), new object[] { board, result, plan });

            Assert.IsTrue(result.UsedDirectionalLinks.Any(link =>
                link.Kind == DirectionalLinkKind.Weak
                && link.Start.Row == 0 && link.Start.Column == 2 && link.Start.Digit == 1
                && link.End.Row == 0 && link.End.Column == 0 && link.End.Digit == 1));
        }

        [Test]
        public void ForcingChainRule_CommonConclusionDescription_StatesBothSeedAssumptions()
        {
            var board = TestHelpers.CreateEmptyBoard();
            int seed = ForcingChainLinkModel.MakeCandidateKey(board, 0, 0, 1);
            var plan = new ForcingPlan { SeedCandidate = seed };

            var description = (string)typeof(ForcingChainRule).GetMethod(
                "BuildCommonConclusionDescription",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                .Invoke(null, new object[] { board, plan, 4, 4, 7 });

            Assert.AreEqual(
                "Forcing Chain: r1c1=1 and r1c1<>1 both force r5c5=7.",
                description);
        }

        [Test]
        public void ForcingChainRule_VerityDescription_ListsEachPlacementBranch()
        {
            var board = TestHelpers.CreateEmptyBoard();
            var plan = new ForcingPlan();
            plan.VerityBranches.Add(new ForcingChainBranch
            {
                SeedCandidate = ForcingChainLinkModel.MakeCandidateKey(board, 3, 3, 1),
                State = new PropagationState()
            });
            plan.VerityBranches.Add(new ForcingChainBranch
            {
                SeedCandidate = ForcingChainLinkModel.MakeCandidateKey(board, 3, 5, 1),
                State = new PropagationState()
            });
            plan.VerityBranches.Add(new ForcingChainBranch
            {
                SeedCandidate = ForcingChainLinkModel.MakeCandidateKey(board, 3, 6, 1),
                State = new PropagationState { HasContradiction = true }
            });

            var description = (string)typeof(ForcingChainRule).GetMethod(
                "BuildCommonConclusionDescription",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                .Invoke(null, new object[] { board, plan, 0, 6, 4 });

            Assert.AreEqual(
                "Forcing Chain: r4c7=1 is impossible; assuming r4c4=1, r4c6=1 each force r1c7=4.",
                description);
        }

        [Test]
        public void ForcingChainRule_VerityPreview_ShowsEveryCommonFalseConclusionPath()
        {
            var board = TestHelpers.CreateEmptyBoard();
            int firstSeed = ForcingChainLinkModel.MakeCandidateKey(board, 0, 0, 1);
            int firstIntermediate = ForcingChainLinkModel.MakeCandidateKey(board, 0, 1, 2);
            int secondSeed = ForcingChainLinkModel.MakeCandidateKey(board, 1, 0, 4);
            int secondIntermediate = ForcingChainLinkModel.MakeCandidateKey(board, 1, 1, 5);
            int conclusion = ForcingChainLinkModel.MakeCandidateKey(board, 0, 2, 3);

            var assembly = typeof(ForcingChainRule).Assembly;
            var flags = BindingFlagsForPlan;
            var planType = assembly.GetType("Sudoku.Solver.Rules.ForcingPlan", throwOnError: true);
            var branchType = assembly.GetType("Sudoku.Solver.Rules.ForcingChainBranch", throwOnError: true);
            var stateType = assembly.GetType("Sudoku.Solver.Rules.PropagationState", throwOnError: true);
            var causeType = assembly.GetType("Sudoku.Solver.Rules.InferenceCause", throwOnError: true);
            var causeListType = typeof(List<>).MakeGenericType(causeType);
            var plan = Activator.CreateInstance(planType);
            var branches = (IList)planType.GetField("VerityBranches", flags).GetValue(plan);
            ((IList<int>)planType.GetField("CommonFalseCandidates", flags).GetValue(plan)).Add(conclusion);

            void AddBranch(int seed, int intermediate)
            {
                var state = Activator.CreateInstance(stateType);
                var branch = Activator.CreateInstance(branchType);
                branchType.GetField("SeedCandidate", flags).SetValue(branch, seed);
                branchType.GetField("State", flags).SetValue(branch, state);
                branches.Add(branch);

                var falseCandidates = (ICollection<int>)stateType.GetField("FalseCandidates", flags).GetValue(state);
                falseCandidates.Add(intermediate);
                falseCandidates.Add(conclusion);

                var causesByCandidate = (IDictionary)stateType.GetField("InferenceCausesByCandidate", flags).GetValue(state);
                AddCause(causesByCandidate, intermediate, seed);
                AddCause(causesByCandidate, conclusion, intermediate);
                AddCause(causesByCandidate, conclusion, seed, DirectionalLinkKind.Weak);
            }

            void AddCause(
                IDictionary causesByCandidate,
                int candidate,
                int source,
                DirectionalLinkKind kind = DirectionalLinkKind.Strong)
            {
                if (!causesByCandidate.Contains(candidate))
                {
                    causesByCandidate[candidate] = Activator.CreateInstance(causeListType);
                }

                var cause = Activator.CreateInstance(causeType);
                causeType.GetField("FromCandidate", flags).SetValue(cause, source);
                causeType.GetField("LinkKind", flags).SetValue(cause, kind);
                causeType.GetField("IsPreviewLink", flags).SetValue(cause, true);
                ((IList)causesByCandidate[candidate]).Add(cause);
            }

            AddBranch(firstSeed, firstIntermediate);
            AddBranch(secondSeed, secondIntermediate);

            var result = new RuleResult();
            result.Changes.Add(new CellChange
            {
                Row = 0,
                Column = 2,
                RemovedCandidates = new List<int> { 3 }
            });
            var appendEvidence = typeof(ForcingChainRule).GetMethod(
                "AppendEvidence",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            appendEvidence.Invoke(new ForcingChainRule(), new[] { board, result, plan });

            Assert.IsTrue(result.UsedCells.Any(cell =>
                cell.Row == 0 && cell.Column == 0 && cell.Candidate == 1 && cell.HighlightTag == "Seed"));
            Assert.IsTrue(result.UsedCells.Any(cell =>
                cell.Row == 0 && cell.Column == 1 && cell.Candidate == 2));
            Assert.IsTrue(result.UsedCells.Any(cell =>
                cell.Row == 1 && cell.Column == 0 && cell.Candidate == 4 && cell.HighlightTag == "Seed"));
            Assert.IsTrue(result.UsedCells.Any(cell =>
                cell.Row == 1 && cell.Column == 1 && cell.Candidate == 5));
            Assert.IsTrue(result.UsedCells.Any(cell =>
                cell.Row == 0 && cell.Column == 2 && cell.Candidate == 3 && cell.HighlightTag == "FalseDeduction"));
            Assert.IsTrue(result.UsedDirectionalLinks.Any(link =>
                link.Start.Row == 0 && link.Start.Column == 0 && link.Start.Digit == 1
                && link.End.Row == 0 && link.End.Column == 1 && link.End.Digit == 2));
            Assert.IsTrue(result.UsedDirectionalLinks.Any(link =>
                link.Start.Row == 0 && link.Start.Column == 1 && link.Start.Digit == 2
                && link.End.Row == 0 && link.End.Column == 2 && link.End.Digit == 3));
            Assert.IsTrue(result.UsedDirectionalLinks.Any(link =>
                link.Start.Row == 1 && link.Start.Column == 0 && link.Start.Digit == 4
                && link.End.Row == 1 && link.End.Column == 1 && link.End.Digit == 5));
            Assert.IsTrue(result.UsedDirectionalLinks.Any(link =>
                link.Start.Row == 1 && link.Start.Column == 1 && link.Start.Digit == 5
                && link.End.Row == 0 && link.End.Column == 2 && link.End.Digit == 3));
            Assert.IsFalse(result.UsedDirectionalLinks.Any(link =>
                link.Kind == DirectionalLinkKind.Weak
                && link.Start.Row == 0 && link.Start.Column == 1 && link.Start.Digit == 2
                && link.End.Row == 0 && link.End.Column == 2 && link.End.Digit == 3));
        }

        [Test]
        public void ForcingChainRule_ScreenshotExample_ExplainsCandidateEliminationThroughUnitCompletion()
        {
            var board = CreateForcingChainExample2Board();

            var result = new ForcingChainRule().CalculateChangesForCandidate(
                board,
                row: 4,
                column: 4,
                digit: 5,
                valueTrue: false);

            Assert.IsTrue(result.Apply, result.Description);
            Assert.IsTrue(result.Changes.Any(change =>
                change.Row == 4 && change.Column == 4 && change.RemovedCandidates.Contains(5)));
            Assert.IsTrue(result.Description.Contains("Cell r3c9 has multiple true forced digits"));
            Assert.IsTrue(result.UsedCells.Any(cell =>
                cell.Row == 4 && cell.Column == 4 && cell.Candidate == 5 && cell.HighlightTag == "FalseDeduction"));
            Assert.AreEqual(
                1,
                result.UsedCells.Count(cell => cell.HighlightTag == "FalseDeduction"),
                "Only the candidate actually removed by the rule should use the deduction highlight.");
            Assert.IsTrue(result.UsedCells.Any(cell =>
                cell.HighlightTag == "Seed" || cell.HighlightTag == "TargetA" || cell.HighlightTag == "TargetB"));
            Assert.IsTrue(result.UsedCells.Any(cell =>
                cell.Row == 2 && cell.Column == 8 && cell.Candidate == 1
                && (cell.HighlightTag == "TargetA" || cell.HighlightTag == "TargetB")));
            Assert.IsTrue(result.UsedCells.Any(cell =>
                cell.Row == 2 && cell.Column == 8 && cell.Candidate == 3
                && (cell.HighlightTag == "TargetA" || cell.HighlightTag == "TargetB")));
            Assert.AreEqual(
                "TargetA",
                result.UsedCells.Single(cell => cell.Row == 2 && cell.Column == 0 && cell.Candidate == 5).HighlightTag);
            Assert.IsTrue(result.UsedDirectionalLinks.Any(link =>
                link.Kind == DirectionalLinkKind.Strong
                && link.Start.Row == 4 && link.Start.Column == 0 && link.Start.Digit == 5
                && link.End.Row == 2 && link.End.Column == 0 && link.End.Digit == 5));
            Assert.IsTrue(result.UsedDirectionalLinks.Any(link =>
                link.Kind == DirectionalLinkKind.Weak
                && link.Start.Row == 4 && link.Start.Column == 4 && link.Start.Digit == 5
                && link.End.Row == 4 && link.End.Column == 0 && link.End.Digit == 5));
            Assert.IsTrue(result.UsedDirectionalLinks.Any(link =>
                link.Kind == DirectionalLinkKind.Strong
                && link.Start.Row == 4 && link.Start.Column == 0 && link.Start.Digit == 5
                && link.End.Row == 4 && link.End.Column == 0 && link.End.Digit == 1));
            Assert.IsTrue(result.UsedDirectionalLinks.Any(link =>
                link.Kind == DirectionalLinkKind.Weak
                && link.Start.Row == 4 && link.Start.Column == 0 && link.Start.Digit == 1
                && link.End.Row == 4 && link.End.Column == 8 && link.End.Digit == 1));
            Assert.IsFalse(result.UsedDirectionalLinks.Any(link => link.Kind == DirectionalLinkKind.Completion));
            Assert.IsTrue(result.UsedDirectionalLinks.Any(link =>
                link.Kind == DirectionalLinkKind.Weak
                && link.Start.Row == 4 && link.Start.Column == 0 && link.Start.Digit == 1
                && link.End.Row == 6 && link.End.Column == 0 && link.End.Digit == 1));
            Assert.IsTrue(result.UsedDirectionalLinks.Any(link =>
                link.Kind == DirectionalLinkKind.Strong
                && link.Start.Row == 6 && link.Start.Column == 0 && link.Start.Digit == 1
                && link.End.Row == 6 && link.End.Column == 0 && link.End.Digit == 3));
            Assert.IsTrue(result.UsedDirectionalLinks.Any(link =>
                link.Kind == DirectionalLinkKind.Weak
                && link.Start.Row == 6 && link.Start.Column == 0 && link.Start.Digit == 3
                && link.End.Row == 2 && link.End.Column == 0 && link.End.Digit == 3));
            Assert.IsTrue(result.UsedDirectionalLinks.Any(link =>
                link.Kind == DirectionalLinkKind.Weak
                && link.Start.Row == 2 && link.Start.Column == 0 && link.Start.Digit == 3
                && link.End.Row == 2 && link.End.Column == 8 && link.End.Digit == 3));
            Assert.IsTrue(result.UsedDirectionalLinks.Any(link =>
                link.Kind == DirectionalLinkKind.Weak
                && link.Start.Row == 4 && link.Start.Column == 8 && link.Start.Digit == 1
                && link.End.Row == 2 && link.End.Column == 8 && link.End.Digit == 1));
            var conflictLinks = result.UsedDirectionalLinks.Where(link => link.Kind == DirectionalLinkKind.Conflict).ToList();
            Assert.AreEqual(1, conflictLinks.Count, "Only the two conflicting candidates within r3c9 should get a blue line.");
            Assert.IsTrue(conflictLinks.Any(link =>
                link.Kind == DirectionalLinkKind.Conflict
                && link.Start.Row == 2 && link.Start.Column == 8
                && link.End.Row == 2 && link.End.Column == 8
                && ((link.Start.Digit == 1 && link.End.Digit == 3)
                    || (link.Start.Digit == 3 && link.End.Digit == 1))));
            Assert.IsFalse(result.UsedCells.Any(cell => cell.HighlightTag == "Conflict"));
            Assert.IsFalse(result.UsedDirectionalLinks.Any(link => link.Kind == DirectionalLinkKind.Completion));
            Assert.IsTrue(result.Description.Contains("Completion steps:"));
            Assert.IsTrue(result.Description.Contains("r3c9=1 by unit completion"));
            Assert.IsFalse(result.UsedDirectionalLinks.Any(link =>
                link.Kind == DirectionalLinkKind.Weak
                && link.Start.Row == link.End.Row
                && link.Start.Column == link.End.Column));
            Assert.IsTrue(EvidenceLinksAreConnectedToDeduction(board, result));
            Assert.IsNotEmpty(result.UsedDirectionalLinks);
        }

        private static bool EvidenceLinksAreConnectedToDeduction(Board board, RuleResult result)
        {
            int CandidateKey(int row, int column, int digit) =>
                (row * board.Size + column) * board.Size + digit;

            var connected = new HashSet<int>(result.UsedCells
                .Where(cell => (cell.HighlightTag == "Deduction"
                    || cell.HighlightTag == "TrueDeduction"
                    || cell.HighlightTag == "FalseDeduction") && cell.Candidate.HasValue)
                .Select(cell => CandidateKey(cell.Row, cell.Column, cell.Candidate.Value)));

            bool expanded;
            do
            {
                expanded = false;
                foreach (var link in result.UsedDirectionalLinks)
                {
                    int start = CandidateKey(link.Start.Row, link.Start.Column, link.Start.Digit);
                    int end = CandidateKey(link.End.Row, link.End.Column, link.End.Digit);
                    if (connected.Contains(start) && connected.Add(end))
                    {
                        expanded = true;
                    }
                    if (connected.Contains(end) && connected.Add(start))
                    {
                        expanded = true;
                    }
                }
            }
            while (expanded);

            return result.UsedDirectionalLinks.All(link =>
            {
                int start = CandidateKey(link.Start.Row, link.Start.Column, link.Start.Digit);
                int end = CandidateKey(link.End.Row, link.End.Column, link.End.Digit);
                if (link.Kind == DirectionalLinkKind.Conflict
                    && link.Start.Row == link.End.Row
                    && link.Start.Column == link.End.Column)
                {
                    return result.UsedCells.Any(cell =>
                               cell.Row == link.Start.Row && cell.Column == link.Start.Column
                               && cell.Candidate == link.Start.Digit)
                        && result.UsedCells.Any(cell =>
                               cell.Row == link.End.Row && cell.Column == link.End.Column
                               && cell.Candidate == link.End.Digit);
                }

                return connected.Contains(start) && connected.Contains(end);
            });
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

        private static Board CreateForcingChainExample2Board()
        {
            var board = TestHelpers.CreateEmptyBoard();
            string[][] cells =
            {
                new[] { "v6", "v7", "34", "48", "v1", "v2", "v5", "89", "39" },
                new[] { "v8", "v9", "15", "57", "v3", "v6", "17", "v4", "v2" },
                new[] { "12345", "134", "1235", "4578", "v9", "578", "v6", "178", "137" },
                new[] { "134", "134", "1478", "178", "27", "v9", "1238", "v6", "v5" },
                new[] { "15", "v6", "1578", "v3", "257", "v4", "1289", "1279", "179" },
                new[] { "v9", "v2", "13578", "1578", "v6", "1578", "13", "13", "v4" },
                new[] { "13", "v8", "v6", "v2", "v4", "137", "79", "v5", "179" },
                new[] { "v7", "v5", "1234", "v9", "v8", "13", "147", "12", "v6" },
                new[] { "124", "14", "v9", "v6", "57", "157", "1247", "v3", "v8" }
            };

            for (int row = 0; row < board.Size; row++)
            {
                for (int column = 0; column < board.Size; column++)
                {
                    var cell = board.Cells[row, column];
                    cell.Candidates.Clear();
                    string token = cells[row][column];
                    if (token[0] == 'v')
                    {
                        cell.Value = token[1] - '0';
                        continue;
                    }

                    foreach (char digit in token)
                    {
                        cell.Candidates.Add(digit - '0');
                    }
                }
            }

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
