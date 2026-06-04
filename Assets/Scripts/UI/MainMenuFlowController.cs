using UnityEngine;
using UnityEngine.UI;
using Sudoku.Solver;

namespace Sudoku.Scripts.UI
{
    /**
     * Controls the high-level runtime flow between startup menu and play mode.
     * It creates a simple menu UI at runtime and toggles play-related runtime UI.
     */
    [DisallowMultipleComponent]
    public class MainMenuFlowController : MonoBehaviour
    {
        private const string RuntimeCanvasName = "MainMenuRuntimeCanvas";
        private const string MenuPanelName = "MainMenuPanel";
        private const string PlayPanelName = "PlayModePanel";
        private const string SidePanelName = "SidePanel";
        private const string ChangeLogControlsName = "ChangeLogControls";

        private Canvas _runtimeCanvas;
        private GameObject _menuPanel;
        private GameObject _playPanel;
        private SolverRunner _runner;
        private BoardVisualizer _boardVisualizer;
        private BoardSidePanel _boardSidePanel;

        /**
         * Prepare runtime references and UI containers.
         */
        private void Awake()
        {
            ResolveSceneReferences();
            EnsureRuntimeCanvas();
            EnsureMenuPanel();
        }

        /**
         * Enter menu mode as the initial game state.
         */
        private void Start()
        {
            EnterMenuMode();
        }

        /**
         * Resolve references to scene components used by flow toggles.
         */
        private void ResolveSceneReferences()
        {
            _runner = Object.FindAnyObjectByType<SolverRunner>();
            _boardVisualizer = Object.FindAnyObjectByType<BoardVisualizer>();
            _boardSidePanel = Object.FindAnyObjectByType<BoardSidePanel>();
        }

        /**
         * Ensure the dedicated runtime canvas for menu/play controls exists.
         */
        private void EnsureRuntimeCanvas()
        {
            var existing = GameObject.Find(RuntimeCanvasName);
            if (existing != null)
            {
                _runtimeCanvas = existing.GetComponent<Canvas>();
                if (_runtimeCanvas != null)
                {
                    return;
                }
            }

            var canvasGO = new GameObject(RuntimeCanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _runtimeCanvas = canvasGO.GetComponent<Canvas>();
            _runtimeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        /**
         * Ensure the startup menu panel and buttons exist.
         */
        private void EnsureMenuPanel()
        {
            var existing = _runtimeCanvas.transform.Find(MenuPanelName);
            if (existing != null)
            {
                _menuPanel = existing.gameObject;
                return;
            }

            _menuPanel = new GameObject(MenuPanelName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _menuPanel.transform.SetParent(_runtimeCanvas.transform, false);

            var menuRect = _menuPanel.GetComponent<RectTransform>();
            menuRect.anchorMin = new Vector2(0.5f, 0.5f);
            menuRect.anchorMax = new Vector2(0.5f, 0.5f);
            menuRect.pivot = new Vector2(0.5f, 0.5f);
            menuRect.sizeDelta = new Vector2(560f, 520f);
            menuRect.anchoredPosition = Vector2.zero;

            var menuImage = _menuPanel.GetComponent<Image>();
            menuImage.color = new Color(0.08f, 0.08f, 0.08f, 0.9f);

            var vlg = _menuPanel.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(28, 28, 24, 24);
            vlg.spacing = 12f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var fitter = _menuPanel.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            CreateLabel(_menuPanel.transform, "Title", "Sudoku", 44, FontStyle.Bold, 56f);
            CreateLabel(_menuPanel.transform, "Subtitle", "Choose an option to begin", 20, FontStyle.Normal, 36f);

            CreateMenuButton(_menuPanel.transform, "StartGeneratedPuzzleButton", "Start Generated Puzzle", StartGeneratedPuzzle, true);
            CreateMenuButton(_menuPanel.transform, "StartSeedPuzzleButton", "Start Puzzle From Seed", StartPuzzleFromSeedStub, false);
            CreateMenuButton(_menuPanel.transform, "CreatePuzzleButton", "Create New Puzzle", CreateNewPuzzleStub, false);
            CreateMenuButton(_menuPanel.transform, "OpenConfigurationButton", "Open Configuration", OpenConfigurationStub, false);
            CreateMenuButton(_menuPanel.transform, "ExitGameButton", "Exit Game", ExitGame, true);
        }

        /**
         * Ensure the in-play controls panel exists.
         */
        private void EnsurePlayPanel()
        {
            var sidePanel = FindSidePanelIncludingInactive();
            if (sidePanel == null)
            {
                Debug.LogWarning("MainMenuFlowController: Cannot create play panel because SidePanel is not found in the scene.");
                return;
            }

            // Remove any orphaned play panel that is not parented to SidePanel.
            var orphan = FindObjectInLoadedScenesIncludingInactive(PlayPanelName);
            if (orphan != null && orphan.transform.parent != sidePanel.transform)
            {
                Destroy(orphan);
            }

            var existing = sidePanel.transform.Find(PlayPanelName);
            if (existing == null)
            {
                _playPanel = new GameObject(PlayPanelName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                _playPanel.transform.SetParent(sidePanel.transform, false);
            }
            else
            {
                _playPanel = existing.gameObject;
            }

            var panelRect = _playPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(1f, 0f);
            panelRect.anchorMax = new Vector2(1f, 0f);
            panelRect.pivot = new Vector2(1f, 0f);
            panelRect.anchoredPosition = new Vector2(-8f, 8f);
            panelRect.sizeDelta = new Vector2(216f, 48f);

            var panelImage = _playPanel.GetComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0f);
            panelImage.raycastTarget = false;

            var oldVertical = _playPanel.GetComponent<VerticalLayoutGroup>();
            if (oldVertical != null)
            {
                Destroy(oldVertical);
            }

            var hlg = _playPanel.GetComponent<HorizontalLayoutGroup>();
            if (hlg == null)
            {
                hlg = _playPanel.AddComponent<HorizontalLayoutGroup>();
            }

            hlg.padding = new RectOffset(0, 0, 0, 0);
            hlg.spacing = 8f;
            hlg.childAlignment = TextAnchor.MiddleRight;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            EnsurePlayActionButton(_playPanel.transform, "SaveBoardButton", "Save Board", SaveBoardStub, false);
            EnsurePlayActionButton(_playPanel.transform, "ExitToMenuButton", "Exit To Menu", ReturnToMenuFromPlay, true);
            ConfigurePlayButtonLayout(_playPanel.transform, "SaveBoardButton");
            ConfigurePlayButtonLayout(_playPanel.transform, "ExitToMenuButton");
        }

        /**
         * Enter the startup menu state and hide play UI.
         */
        private void EnterMenuMode()
        {
            ResolveSceneReferences();
            if (_runner != null)
            {
                _runner.UnloadBoard();
            }

            if (_menuPanel != null)
            {
                _menuPanel.SetActive(true);
            }

            if (_playPanel != null)
            {
                _playPanel.SetActive(false);
            }

            SetPlayUiVisible(false);
        }

        /**
         * Enter play state and show gameplay UI.
         */
        private void EnterPlayMode()
        {
            if (_menuPanel != null)
            {
                _menuPanel.SetActive(false);
            }

            SetPlayUiVisible(true);
            EnsureSidePanelVisible();

            EnsurePlayPanel();
            if (_playPanel != null)
            {
                _playPanel.SetActive(true);
            }
            else
            {
                StartCoroutine(EnsurePlayPanelDeferred());
            }

            ResolveSceneReferences();
            if (_boardSidePanel != null)
            {
                _boardSidePanel.RefreshPanelVisibilityForCurrentMode();
            }
        }

        /**
         * Retry creation of play controls while SidePanel is being created.
         */
        private System.Collections.IEnumerator EnsurePlayPanelDeferred()
        {
            const int maxFrames = 45;
            for (int i = 0; i < maxFrames; i++)
            {
                EnsurePlayPanel();
                if (_playPanel != null)
                {
                    _playPanel.SetActive(true);
                    yield break;
                }

                yield return null;
            }
        }

        /**
         * Toggle runtime play UI (board, side panel, and changelog controls).
         *
         * @param visible True to show gameplay UI; false to hide it.
         */
        private void SetPlayUiVisible(bool visible)
        {
            ResolveSceneReferences();

            if (_boardVisualizer != null)
            {
                _boardVisualizer.enabled = visible;
            }

            if (_boardSidePanel != null)
            {
                _boardSidePanel.enabled = visible;
            }

            var sidePanel = FindSidePanelIncludingInactive();
            if (sidePanel != null)
            {
                sidePanel.SetActive(visible);
            }

            var changelogControls = sidePanel != null
                ? sidePanel.transform.Find(ChangeLogControlsName)?.gameObject
                : FindObjectInLoadedScenesIncludingInactive(ChangeLogControlsName);
            if (changelogControls != null)
            {
                changelogControls.SetActive(visible);
            }
        }

        /**
         * Ensure a visible side panel exists for puzzle mode even if prior state toggles
         * left it hidden or it has not been created yet.
         */
        private void EnsureSidePanelVisible()
        {
            NormalizeDuplicateSidePanels();

            var sidePanel = FindSidePanelIncludingInactive();
            if (sidePanel == null)
            {
                var canvas = Object.FindAnyObjectByType<Canvas>();
                if (canvas == null)
                {
                    return;
                }

                sidePanel = new GameObject(SidePanelName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                sidePanel.transform.SetParent(canvas.transform, false);

                var rt = sidePanel.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(1f, 0f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 0.5f);
                rt.sizeDelta = new Vector2(320f, 0f);
                rt.anchoredPosition = Vector2.zero;
            }

            sidePanel.SetActive(true);

            var image = sidePanel.GetComponent<Image>();
            if (image == null)
            {
                image = sidePanel.AddComponent<Image>();
            }

            image.enabled = true;
            // Always normalize to the intended runtime side-panel color so
            // scene-authored tint values (e.g., green test colors) do not leak
            // into puzzle mode.
            image.color = new Color(0f, 0f, 0f, 0.6f);
            image.raycastTarget = false;
        }

        /**
         * Find a GameObject in loaded scenes by name, including inactive objects.
         *
         * @param objectName Name of the target object.
         * @returns First matching scene object, or null when not found.
         */
        private static GameObject FindObjectInLoadedScenesIncludingInactive(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            var transforms = Resources.FindObjectsOfTypeAll<Transform>();
            for (int i = 0; i < transforms.Length; i++)
            {
                var t = transforms[i];
                if (t == null || t.name != objectName)
                {
                    continue;
                }

                var go = t.gameObject;
                if (!go.scene.IsValid() || !go.scene.isLoaded)
                {
                    continue;
                }

                return go;
            }

            return null;
        }

        /**
         * Resolve the primary SidePanel from loaded scenes, including inactive objects.
         * Prefers a panel that already contains the expected runtime children.
         *
         * @returns Matching SidePanel GameObject, or null.
         */
        private static GameObject FindSidePanelIncludingInactive()
        {
            var transforms = Resources.FindObjectsOfTypeAll<Transform>();
            GameObject fallback = null;

            for (int i = 0; i < transforms.Length; i++)
            {
                var t = transforms[i];
                if (t == null || t.name != SidePanelName)
                {
                    continue;
                }

                var go = t.gameObject;
                if (!go.scene.IsValid() || !go.scene.isLoaded)
                {
                    continue;
                }

                if (t.Find("RulesArea") != null || t.Find(ChangeLogControlsName) != null)
                {
                    return go;
                }

                if (fallback == null)
                {
                    fallback = go;
                }
            }

            return fallback;
        }

        /**
         * Prevent duplicate SidePanel instances by deactivating extras and keeping
         * the most complete instance active.
         */
        private static void NormalizeDuplicateSidePanels()
        {
            var transforms = Resources.FindObjectsOfTypeAll<Transform>();
            var panels = new System.Collections.Generic.List<GameObject>();

            for (int i = 0; i < transforms.Length; i++)
            {
                var t = transforms[i];
                if (t == null || t.name != SidePanelName)
                {
                    continue;
                }

                var go = t.gameObject;
                if (!go.scene.IsValid() || !go.scene.isLoaded)
                {
                    continue;
                }

                panels.Add(go);
            }

            if (panels.Count <= 1)
            {
                return;
            }

            GameObject primary = null;
            for (int i = 0; i < panels.Count; i++)
            {
                var p = panels[i];
                if (p.transform.Find("RulesArea") != null)
                {
                    primary = p;
                    break;
                }
            }

            if (primary == null)
            {
                primary = panels[0];
            }

            for (int i = 0; i < panels.Count; i++)
            {
                var panel = panels[i];
                if (panel == primary)
                {
                    continue;
                }

                panel.SetActive(false);
            }
        }

        /**
         * Start the generated puzzle flow (currently reuses the predefined board rows).
         */
        private void StartGeneratedPuzzle()
        {
            ResolveSceneReferences();
            if (_runner == null)
            {
                Debug.LogWarning("MainMenuFlowController: No SolverRunner found. Cannot start generated puzzle.");
                return;
            }

            _runner.LoadBoardFromRows();
            _runner.SetInteractionMode(BoardInteractionMode.Puzzle);
            ConfigureBoardVisualizerForRunnerMode();
            EnterPlayMode();

            ResolveSceneReferences();
            if (_boardSidePanel != null)
            {
                _boardSidePanel.RefreshPanelVisibilityForCurrentMode();
            }
        }

        /**
         * Return from play mode back to the main menu.
         */
        private void ReturnToMenuFromPlay()
        {
            EnterMenuMode();
        }

        /**
         * Stub: start puzzle from seed.
         */
        private void StartPuzzleFromSeedStub()
        {
            Debug.Log("MainMenuFlowController: Start Puzzle From Seed is not implemented yet.");
        }

        /**
         * Stub: create a new puzzle from scratch.
         */
        private void CreateNewPuzzleStub()
        {
            ResolveSceneReferences();
            if (_runner == null)
            {
                Debug.LogWarning("MainMenuFlowController: No SolverRunner found. Cannot create a new puzzle board.");
                return;
            }

            _runner.CreateBlankBoard();
            ConfigureBoardVisualizerForRunnerMode();
            EnterPlayMode();

            ResolveSceneReferences();
            if (_boardSidePanel != null)
            {
                _boardSidePanel.RefreshPanelVisibilityForCurrentMode();
            }
        }

        /**
         * Align board visualizer rendering defaults with the runner interaction mode.
         */
        private void ConfigureBoardVisualizerForRunnerMode()
        {
            if (_boardVisualizer == null || _runner == null)
            {
                return;
            }

            if (_runner.IsPuzzleCreationMode)
            {
                _boardVisualizer.ShowCandidates = true;
                _boardVisualizer.DigitActionMode = BoardVisualizer.NumericRadialActionMode.SetValue;
            }
            else
            {
                _boardVisualizer.ShowCandidates = true;
                _boardVisualizer.DigitActionMode = BoardVisualizer.NumericRadialActionMode.ModifierDriven;
            }
        }

        /**
         * Stub: open game configuration.
         */
        private void OpenConfigurationStub()
        {
            Debug.Log("MainMenuFlowController: Open Configuration is not implemented yet.");
        }

        /**
         * Stub: save the current board state.
         */
        private void SaveBoardStub()
        {
            Debug.Log("MainMenuFlowController: Save Board is not implemented yet.");
        }

        /**
         * Exit the game application.
         */
        private void ExitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /**
         * Create a styled label under a parent transform.
         *
         * @param parent Parent transform.
         * @param objectName Name for the label game object.
         * @param text Label text.
         * @param fontSize Font size.
         * @param fontStyle Font style.
         * @param height Preferred label height.
         */
        private static void CreateLabel(Transform parent, string objectName, string text, int fontSize, FontStyle fontStyle, float height)
        {
            var labelGO = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelGO.transform.SetParent(parent, false);

            var layout = labelGO.AddComponent<LayoutElement>();
            layout.preferredHeight = height;

            var rect = labelGO.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0f, height);

            var label = labelGO.GetComponent<Text>();
            label.text = text;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.fontSize = fontSize;
            label.fontStyle = fontStyle;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            label.font = font;
        }

        /**
         * Create a styled button under a parent transform.
         *
         * @param parent Parent transform.
         * @param objectName Name for the button game object.
         * @param text Button label.
         * @param onClick Click callback.
         * @param primaryStyle True for primary look, false for secondary look.
         */
        private static void CreateMenuButton(Transform parent, string objectName, string text, UnityEngine.Events.UnityAction onClick, bool primaryStyle)
        {
            var buttonGO = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonGO.transform.SetParent(parent, false);

            var layout = buttonGO.AddComponent<LayoutElement>();
            layout.preferredHeight = 52f;

            var image = buttonGO.GetComponent<Image>();
            image.color = primaryStyle ? new Color(0.90f, 0.70f, 0.18f, 1f) : new Color(0.83f, 0.85f, 0.88f, 1f);

            var button = buttonGO.GetComponent<Button>();
            button.onClick.AddListener(onClick);

            var textGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textGO.transform.SetParent(buttonGO.transform, false);

            var textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var label = textGO.GetComponent<Text>();
            label.text = text;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = primaryStyle ? Color.black : new Color(0.1f, 0.1f, 0.1f, 1f);
            label.fontSize = 20;
            label.fontStyle = FontStyle.Bold;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            label.font = font;
        }

        /**
         * Apply compact sizing for play-mode buttons so they do not overflow the side panel.
         *
         * @param parent Parent transform containing the button child.
         * @param buttonName Name of the button child to configure.
         */
        private static void ConfigurePlayButtonLayout(Transform parent, string buttonName)
        {
            if (parent == null || string.IsNullOrWhiteSpace(buttonName))
            {
                return;
            }

            var buttonTransform = parent.Find(buttonName);
            if (buttonTransform == null)
            {
                return;
            }

            var layout = buttonTransform.GetComponent<LayoutElement>();
            if (layout == null)
            {
                layout = buttonTransform.gameObject.AddComponent<LayoutElement>();
            }

            layout.preferredWidth = 104f;
            layout.minWidth = 96f;
            layout.preferredHeight = 36f;
            layout.minHeight = 36f;
            layout.flexibleWidth = 0f;
            layout.flexibleHeight = 0f;

            var text = buttonTransform.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.fontSize = 14;
            }
        }

        /**
         * Ensure a play-mode action button exists and has the expected callback.
         *
         * @param parent Parent transform containing action buttons.
         * @param buttonName Child object name for the button.
         * @param label Button label.
         * @param onClick Click callback.
         * @param primaryStyle True for primary style coloring.
         */
        private static void EnsurePlayActionButton(Transform parent, string buttonName, string label, UnityEngine.Events.UnityAction onClick, bool primaryStyle)
        {
            if (parent == null)
            {
                return;
            }

            var child = parent.Find(buttonName);
            if (child == null)
            {
                CreateMenuButton(parent, buttonName, label, onClick, primaryStyle);
                child = parent.Find(buttonName);
            }

            if (child == null)
            {
                return;
            }

            var button = child.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(onClick);
            }

            var image = child.GetComponent<Image>();
            if (image != null)
            {
                image.color = primaryStyle ? new Color(0.90f, 0.70f, 0.18f, 1f) : new Color(0.83f, 0.85f, 0.88f, 1f);
            }

            var text = child.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.text = label;
            }
        }
    }
}