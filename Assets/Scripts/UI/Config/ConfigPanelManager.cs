using UnityEngine;
using UnityEngine.UI;
using Sudoku.Scripts.UI.Config;

namespace Sudoku.Scripts.UI
{
    /**
     * Manager for opening and managing the configuration panel.
     * Handles creation of the config panel as a modal overlay with tabs.
     *
     * To use:
     * - Call OpenConfigPanel() to display the config panel
     * - The panel can be closed by pressing Escape or clicking outside
     */
    public class ConfigPanelManager : MonoBehaviour
    {
        private static ConfigPanelManager _instance;
        private GameObject _configPanelRoot;
        private ConfigPanel _configPanel;

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        /**
         * Open the configuration panel as a modal overlay.
         * Creates the panel if it doesn't already exist.
         */
        public void OpenConfigPanel()
        {
            if (_configPanelRoot != null && _configPanelRoot.activeSelf)
            {
                return; // Already open
            }

            if (_configPanelRoot == null)
            {
                CreateConfigPanel();
            }

            _configPanelRoot.SetActive(true);
        }

        /**
         * Close the configuration panel.
         */
        public void CloseConfigPanel()
        {
            if (_configPanelRoot != null)
            {
                _configPanelRoot.SetActive(false);
            }
        }

        /**
         * Create the configuration panel UI hierarchy.
         * Uses a modal-overlay pattern: full-screen dark background (click to close)
         * with a centred PanelContainer holding the actual ConfigPanel.
         */
        private void CreateConfigPanel()
        {
            // Prefer an existing high-sortingOrder canvas so the panel draws on top
            // of all game UI. Fall back to creating a dedicated canvas.
            Canvas canvas = FindSuitableCanvas();

            // Root overlay — full-screen, no ConfigPanel component here.
            _configPanelRoot = new GameObject("ConfigPanelOverlay", typeof(RectTransform));
            _configPanelRoot.transform.SetParent(canvas.transform, false);

            var rootRT = _configPanelRoot.GetComponent<RectTransform>();
            rootRT.anchorMin = Vector2.zero;
            rootRT.anchorMax = Vector2.one;
            rootRT.offsetMin = Vector2.zero;
            rootRT.offsetMax = Vector2.zero;

            // Semi-transparent backdrop — clicking it closes the panel.
            var bgGO    = new GameObject("Background", typeof(RectTransform), typeof(Image), typeof(Button));
            bgGO.transform.SetParent(_configPanelRoot.transform, false);

            var bgRT    = bgGO.GetComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;

            var bgImg   = bgGO.GetComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.65f);

            var bgBtn   = bgGO.GetComponent<Button>();
            bgBtn.transition = Selectable.Transition.None;
            bgBtn.onClick.AddListener(CloseConfigPanel);

            // Centred, fixed-size container that holds the real panel.
            var containerGO = new GameObject("PanelContainer", typeof(RectTransform));
            containerGO.transform.SetParent(_configPanelRoot.transform, false);

            var containerRT = containerGO.GetComponent<RectTransform>();
            containerRT.anchorMin = new Vector2(0.5f, 0.5f);
            containerRT.anchorMax = new Vector2(0.5f, 0.5f);
            containerRT.pivot     = new Vector2(0.5f, 0.5f);
            containerRT.sizeDelta = new Vector2(480f, 580f);

            // The ConfigPanel fills the container.
            var panelGO = new GameObject("Panel", typeof(RectTransform), typeof(ConfigPanel), typeof(Image));
            panelGO.transform.SetParent(containerGO.transform, false);

            var panelRT = panelGO.GetComponent<RectTransform>();
            panelRT.anchorMin = Vector2.zero;
            panelRT.anchorMax = Vector2.one;
            panelRT.offsetMin = Vector2.zero;
            panelRT.offsetMax = Vector2.zero;

            _configPanel = panelGO.GetComponent<ConfigPanel>();
            _configPanel.OnCloseRequested = CloseConfigPanel;

            // Register tabs — add more here for Audio, Graphics, etc.
            var rulesTab = panelGO.AddComponent<RulesConfigTab>();
            _configPanel.RegisterTab(rulesTab);

            // Build the panel (creates header with tabs+close and content area).
            _configPanel.BuildPanel();

            // Keyboard shortcut (Escape) to close.
            var keyHandler = panelGO.AddComponent<ConfigPanelKeyHandler>();
            keyHandler.OnEscapePressed += CloseConfigPanel;
        }

        /**
         * Find an existing canvas, preferring one with a higher sortingOrder
         * so the config panel renders on top. Creates a dedicated canvas if none exists.
         *
         * @returns A canvas to attach the panel to.
         */
        private Canvas FindSuitableCanvas()
        {
            Canvas best = null;
            foreach (var c in FindObjectsOfType<Canvas>())
            {
                if (c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                if (best == null || c.sortingOrder > best.sortingOrder)
                    best = c;
            }

            if (best != null) return best;

            // No overlay canvas found — create a dedicated one.
            var canvasGO   = new GameObject("ConfigPanelCanvas",
                                 typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas     = canvasGO.GetComponent<Canvas>();
            canvas.renderMode    = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder  = 200;

            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            return canvas;
        }
    }

    /**
     * Helper component to handle keyboard input for closing the config panel.
     * Uses the new Input System (UnityEngine.InputSystem) which is the active
     * input handler for this project.
     */
    public class ConfigPanelKeyHandler : MonoBehaviour
    {
        public System.Action OnEscapePressed;

        private void Update()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                OnEscapePressed?.Invoke();
            }
        }
    }
}
