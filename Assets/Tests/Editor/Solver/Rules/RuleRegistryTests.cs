using NUnit.Framework;
using Sudoku.Solver.Rules;

namespace Sudoku.Tests.Editor
{
    /// <summary>
    /// Tests for <see cref="RuleRegistry"/>, which holds the available rules and orchestrates applying them.
    /// The tests assert registration of defaults, that no rules fire on a truly empty board, and that
    /// registered rules are applied when the board contains a change.
    /// </summary>
    public class RuleRegistryTests
    {
        /// <summary>
        /// Ensure that calling <c>RegisterDefaults</c> populates the registry with the expected rules.
        /// </summary>
        [Test]
        public void RegisterDefaults_AddsExpectedRules()
        {
            var registry = new RuleRegistry();
            registry.RegisterDefaults();
            Assert.AreEqual(4, registry.Rules.Count);
        }

        /// <summary>
        /// When the board has no candidates or values, <c>ApplyAll</c> should return an empty result set.
        /// </summary>
        [Test]
        public void ApplyAll_NoChangesOnEmptyBoard()
        {
            var board = TestHelpers.CreateEmptyBoard();
            var registry = new RuleRegistry();
            registry.RegisterDefaults();
            var applied = registry.ApplyAll(board);
            Assert.IsEmpty(applied);
        }

        /// <summary>
        /// Create a simple change (a naked single) and verify that <c>ApplyAll</c> reports at least one applied rule
        /// and that the applied rule is the expected one.
        /// </summary>
        [Test]
        public void ApplyAll_AppliesRegisteredRules_WhenBoardHasChanges()
        {
            var board = TestHelpers.CreateEmptyBoard();
            var registry = new RuleRegistry();
            registry.RegisterDefaults();

            // make a naked single to ensure ApplyAll applies something
            var target = board.Cells[3, 3];
            target.Candidates.Clear();
            target.Candidates.Add(2);

            var results = registry.ApplyAll(board);
            Assert.IsNotEmpty(results);
            Assert.AreEqual("Naked Single", results[0].rule.Name);
        }
    }
}
