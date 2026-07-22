using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Sudoku.UI.Config;
using Sudoku.UI;
using Sudoku.Solver.Rules;
using System.Reflection;

namespace Sudoku.Tests.Editor.UI.Config
{
    /**
     * Tests for the configuration panel system.
     * Verifies that the ConfigPanel, tabs, and ConfigPanelManager work correctly.
     */
    public class ConfigPanelTests
    {
        private GameObject _testCanvasGO;
        private Canvas _testCanvas;
        private ConfigPanelManager _configManager;
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

            // Create a test canvas
            _testCanvasGO = new GameObject("TestCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _testCanvas = _testCanvasGO.GetComponent<Canvas>();
            _testCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        [TearDown]
        public void Teardown()
        {
            ColourSettings.GreenEnabled = _originalGreen;
            ColourSettings.AmberEnabled = _originalAmber;
            ColourSettings.RedEnabled = _originalRed;
            ColourSettings.BlueEnabled = _originalBlue;

            if (_testCanvasGO != null)
            {
                Object.DestroyImmediate(_testCanvasGO);
            }
            if (_configManager != null && _configManager.gameObject != null)
            {
                Object.DestroyImmediate(_configManager.gameObject);
            }
        }

        [Test]
        public void ConfigPanelManager_CanBeCreated()
        {
            // Create config manager
            var managerGO = new GameObject("ConfigPanelManager", typeof(ConfigPanelManager));
            _configManager = managerGO.GetComponent<ConfigPanelManager>();

            Assert.IsNotNull(_configManager);
        }

        [Test]
        public void ConfigPanelManager_OpenClose_TogglesUnderlyingGraphicRaycasters()
        {
            Assert.IsNotNull(_testCanvasGO);

            var raycaster = _testCanvasGO.GetComponent<GraphicRaycaster>();
            Assert.IsNotNull(raycaster);
            raycaster.enabled = true;

            var managerGO = new GameObject("ConfigPanelManager", typeof(ConfigPanelManager));
            _configManager = managerGO.GetComponent<ConfigPanelManager>();

            _configManager.OpenConfigPanel();
            Assert.IsFalse(raycaster.enabled, "Opening config should block click-through to underlying uGUI.");

            _configManager.CloseConfigPanel();
            Assert.IsTrue(raycaster.enabled, "Closing config should restore prior raycaster state.");
        }

        [Test]
        public void ConfigPanel_CanBuildWithRulesTab()
        {
            // Create config panel
            var panelGO = new GameObject("ConfigPanel", typeof(RectTransform), typeof(ConfigPanel));
            var configPanel = panelGO.GetComponent<ConfigPanel>();

            // Create and register a test tab
            var tabGO = panelGO.AddComponent<RulesConfigTab>();
            tabGO.TabName = "Rules";
            configPanel.RegisterTab(tabGO);

            // Build the panel
            configPanel.BuildPanel();

            // Verify structure was created (Header contains TabsArea + optional close button)
            var header = panelGO.transform.Find("Header");
            Assert.IsNotNull(header, "Header row should be created");

            var contentArea = panelGO.transform.Find("ContentArea");
            Assert.IsNotNull(contentArea, "Content area should be created");
        }

        [Test]
        public void ConfigTab_PopulateContent_CanBeOverridden()
        {
            var tabGO = new GameObject("TestTab", typeof(RectTransform));
            var tab = tabGO.AddComponent<TestConfigTab>();

            var contentParent = new GameObject("ContentParent", typeof(RectTransform));
            var contentRect = contentParent.GetComponent<RectTransform>();

            // Call PopulateContent
            tab.PopulateContent(contentRect);

            // Verify test content was created
            Assert.IsTrue(tab.WasPopulateCalled, "PopulateContent should have been called");
            Assert.AreEqual(1, contentRect.childCount, "One child should be added during population");
        }

        [Test]
        public void ConfigPanelManager_RequiresAutoDisableWarning_WhenSecondColourWouldDisableColouringRule()
        {
            var managerGO = new GameObject("ConfigPanelManager", typeof(ConfigPanelManager));
            _configManager = managerGO.GetComponent<ConfigPanelManager>();

            var registry = new RuleRegistry();
            registry.Register(new ColouringRule());
            SetPrivateField(_configManager, "_registry", registry);

            ColourSettings.GreenEnabled = true;
            ColourSettings.AmberEnabled = true;
            ColourSettings.RedEnabled = false;
            ColourSettings.BlueEnabled = false;

            bool requiresWarning = (bool)InvokePrivateMethod(_configManager, "RequiresColouringRuleAutoDisableWarning");
            Assert.IsTrue(requiresWarning);
        }

        [Test]
        public void ConfigPanelManager_ConfirmDisable_DisablesSelectedColourAndColouringRule()
        {
            var managerGO = new GameObject("ConfigPanelManager", typeof(ConfigPanelManager));
            _configManager = managerGO.GetComponent<ConfigPanelManager>();

            var registry = new RuleRegistry();
            registry.Register(new ColouringRule());
            SetPrivateField(_configManager, "_registry", registry);

            ColourSettings.GreenEnabled = true;
            ColourSettings.AmberEnabled = true;
            ColourSettings.RedEnabled = false;
            ColourSettings.BlueEnabled = false;

            var pendingEnumType = typeof(ConfigPanelManager).GetNestedType("PendingColourDisable", BindingFlags.NonPublic);
            Assert.IsNotNull(pendingEnumType);

            object amberValue = System.Enum.Parse(pendingEnumType, "Amber");
            SetPrivateField(_configManager, "_pendingColourDisable", amberValue);
            SetPrivateField(_configManager, "_showColouringAutoDisableWarning", true);

            InvokePrivateMethod(_configManager, "ConfirmColourDisableWithAutoRuleDisable");

            Assert.IsFalse(ColourSettings.AmberEnabled);
            Assert.IsFalse(registry.GetRulesWithStatus().Find(x => x.rule.GetType().Name == "ColouringRule").enabled);
        }

        [Test]
        public void ConfigPanelManager_RequiresAutoDisableWarning_RepeatsAfterRuleReEnabled()
        {
            var managerGO = new GameObject("ConfigPanelManager", typeof(ConfigPanelManager));
            _configManager = managerGO.GetComponent<ConfigPanelManager>();

            var registry = new RuleRegistry();
            registry.Register(new ColouringRule());
            SetPrivateField(_configManager, "_registry", registry);

            // First cycle: two enabled colours + colouring rule enabled.
            ColourSettings.GreenEnabled = true;
            ColourSettings.AmberEnabled = true;
            ColourSettings.RedEnabled = false;
            ColourSettings.BlueEnabled = false;
            Assert.IsTrue((bool)InvokePrivateMethod(_configManager, "RequiresColouringRuleAutoDisableWarning"));

            // Simulate confirm path side-effects.
            registry.SetEnabled("ColouringRule", false);
            ColourSettings.AmberEnabled = false;

            // User later restores two colours and re-enables Colouring rule.
            ColourSettings.AmberEnabled = true;
            registry.SetEnabled("ColouringRule", true);

            Assert.IsTrue((bool)InvokePrivateMethod(_configManager, "RequiresColouringRuleAutoDisableWarning"));
        }

        /**
         * Set a private instance field for test-only configuration.
         *
         * @param target Target instance.
         * @param fieldName Field name to assign.
         * @param value New field value.
         */
        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found.");
            field.SetValue(target, value);
        }

        /**
         * Invoke a private parameterless method and return its result.
         *
         * @param target Target instance.
         * @param methodName Method name to invoke.
         * @returns Method return value, if any.
         */
        private static object InvokePrivateMethod(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Method '{methodName}' not found.");
            return method.Invoke(target, null);
        }
    }

    /**
     * Test implementation of ConfigTab for testing purposes.
     */
    public class TestConfigTab : ConfigTab
    {
        public bool WasPopulateCalled { get; private set; }

        public override void PopulateContent(RectTransform contentRoot)
        {
            WasPopulateCalled = true;
            // Create a simple test element
            var testGO = new GameObject("TestElement", typeof(RectTransform));
            testGO.transform.SetParent(contentRoot, false);
        }
    }
}
