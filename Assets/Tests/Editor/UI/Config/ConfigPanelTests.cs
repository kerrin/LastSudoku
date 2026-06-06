using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Sudoku.UI.Config;
using Sudoku.UI;

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

        [SetUp]
        public void Setup()
        {
            // Create a test canvas
            _testCanvasGO = new GameObject("TestCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _testCanvas = _testCanvasGO.GetComponent<Canvas>();
            _testCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        [TearDown]
        public void Teardown()
        {
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
