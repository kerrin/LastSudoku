using NUnit.Framework;
using Sudoku.Solver.Rules;
using Sudoku.UI.Config;

namespace Sudoku.Tests.Editor.UI.Config
{
    public class RuleTogglePanelPersistenceTests
    {
        [Test]
        public void SaveCurrent_PersistsUpdatedRuleEnabledState_FromSolveModeRegistry()
        {
            var registry = new RuleRegistry();
            registry.Register(new ColouringRule());

            // Simulate Solve-mode toggle ON then save.
            registry.SetEnabled("ColouringRule", true);
            RuntimeConfigService.SaveCurrent(registry);

            // Toggle OFF and save again, ensuring the saved snapshot follows runtime toggles.
            registry.SetEnabled("ColouringRule", false);
            RuntimeConfigService.SaveCurrent(registry);

            var snapshot = registry.GetRulesWithStatus();
            var colouring = snapshot.Find(x => x.rule.GetType().Name == "ColouringRule");
            Assert.IsFalse(colouring.enabled);
        }
    }
}