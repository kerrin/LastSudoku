using NUnit.Framework;
using Sudoku.Models;
using Sudoku.Solver.Rules;

namespace Sudoku.Tests.Editor
{
    /** 
     * Tests for <see cref="RuleRegistry"/>, which holds the available rules and orchestrates applying them.
     * The tests assert registration of defaults, that no rules fire on a truly empty board, and that
     * registered rules are applied when the board contains a change.
     */
    public class RuleRegistryTests
    {
        /** Ensure that calling <c>RegisterDefaults</c> populates the registry with the expected rules. */
        [Test]
        public void RegisterMinimal_AddsExpectedRules()
        {
            var registry = new RuleRegistry();
            registry.RegisterMinimal();
            Assert.AreEqual(4, registry.Rules.Count);
        }

        /** Ensure that calling <c>RegisterMedium</c> populates the registry with the expected rules. */
        [Test]
        public void RegisterMedium_AddsExpectedRules()
        {
            var registry = new RuleRegistry();
            registry.RegisterMedium();
            Assert.AreEqual(3, registry.Rules.Count);
        }

        /** Ensure that calling <c>RegisterAdvanced</c> populates the registry with the expected rules. */
        [Test]
        public void RegisterAdvanced_AddsExpectedRules()
        {
            var registry = new RuleRegistry();
            registry.RegisterAdvanced();
            Assert.AreEqual(2, registry.Rules.Count);
        }

        /** When the board has no candidates or values, <c>ApplyAll</c> should return an empty result set. */
        [Test]
        public void ApplyAll_NoChangesOnEmptyBoard()
        {
            var board = TestHelpers.CreateEmptyBoard();
            var registry = new RuleRegistry();
            registry.RegisterMinimal();
            registry.RegisterMedium();
            registry.RegisterAdvanced();
            var applied = registry.ApplyAll(board);
            Assert.IsEmpty(applied);
        }

        /** 
         * Create a simple change (a naked single) and verify that <c>ApplyAll</c> reports at least one applied rule
         * and that the applied rule is the expected one. 
         */
        [Test]
        public void ApplyAll_AppliesRegisteredRules_WhenBoardHasChanges()
        {
            Board board = TestHelpers.CreateEmptyBoard();
            var registry = new RuleRegistry();
            registry.RegisterMinimal();
            registry.RegisterMedium();
            registry.RegisterAdvanced();

            // make a naked single to ensure ApplyAll applies something
            foreach(Cell cell in board.GetBox(0)) {
                if(cell.Row == 1 && cell.Column == 1) continue; // Ignore the center cell of the box to create a naked single there for 5
                board.Cells[cell.Row, cell.Column].Value = cell.Row * board.BoxHeight + cell.Column + 1;
            }
            
            var results = registry.ApplyAll(board);
            Assert.IsNotEmpty(results);
            Assert.AreEqual("Naked Single", results[0].rule.Name);
            Assert.AreEqual(5, board.Cells[1, 1].Value);
        }
    }
}
