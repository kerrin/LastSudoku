using UnityEngine;
using UnityEngine.UI;
using Sudoku.Solver;
using Sudoku.Models;

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
        private InputField _generatedCodeInput;
        private GameObject _puzzleCodeRow;
        private GameObject _overwriteConfirmRow;
        private string _pendingCodeToApply = string.Empty;
        private int _lastObservedBoardFingerprint = int.MinValue;

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
         * Observe board-state changes in create mode so generated code is invalidated after edits.
         */
        private void Update()
        {
            ResolveSceneReferences();

            if (_playPanel != null && _playPanel.activeInHierarchy)
            {
                var sidePanel = FindSidePanelIncludingInactive();
                if (sidePanel != null)
                {
                    EnsureBottomRightExitButton(sidePanel.transform);
                }
            }

            if (_runner == null || !_runner.IsPuzzleCreationMode || _runner.CurrentBoard == null)
            {
                _lastObservedBoardFingerprint = int.MinValue;
                return;
            }

            int fingerprint = ComputeBoardFingerprint(_runner.CurrentBoard);
            if (_lastObservedBoardFingerprint == int.MinValue)
            {
                _lastObservedBoardFingerprint = fingerprint;
                return;
            }

            if (fingerprint != _lastObservedBoardFingerprint)
            {
                _lastObservedBoardFingerprint = fingerprint;
                ClearPuzzleCodeText();
                HideOverwriteConfirmation();
            }
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
            panelRect.anchoredPosition = new Vector2(-8f, 58f);
            panelRect.sizeDelta = new Vector2(272f, 112f);

            var panelImage = _playPanel.GetComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0f);
            panelImage.raycastTarget = false;

            var oldHorizontal = _playPanel.GetComponent<HorizontalLayoutGroup>();
            if (oldHorizontal != null)
            {
                Destroy(oldHorizontal);
            }

            var vlg = _playPanel.GetComponent<VerticalLayoutGroup>();
            if (vlg == null)
            {
                vlg = _playPanel.AddComponent<VerticalLayoutGroup>();
            }

            vlg.padding = new RectOffset(0, 0, 0, 0);
            vlg.spacing = 6f;
            vlg.childAlignment = TextAnchor.MiddleRight;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;

            EnsurePuzzleCodeRow(_playPanel.transform);
            EnsureOverwriteConfirmationRow(_playPanel.transform);
            var actionsRow = EnsurePlayActionsRow(_playPanel.transform);

            EnsurePlayActionButton(actionsRow, "LoadPuzzleCodeButton", "Load Code", ApplyPuzzleCodeFromInput, false);
            EnsurePlayActionButton(actionsRow, "SaveBoardButton", "", GeneratePuzzleCode, false);
            ConfigurePlayButtonLayout(actionsRow, "LoadPuzzleCodeButton", 130f, 34f, 130f);
            ConfigurePlayButtonLayout(actionsRow, "SaveBoardButton", 70f, 34f, 70f);
            EnsureIconLabel(actionsRow.Find("SaveBoardButton"), "GenerateIconLabel", "⟳", 24);

            EnsureBottomRightExitButton(sidePanel.transform);

            UpdatePlayPanelCreateModeControls();
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

            var exitButton = FindObjectInLoadedScenesIncludingInactive("ExitCornerButton");
            if (exitButton != null)
            {
                exitButton.SetActive(false);
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

            var sidePanel = FindSidePanelIncludingInactive();
            if (sidePanel != null)
            {
                EnsureBottomRightExitButton(sidePanel.transform);
            }

            UpdatePlayPanelCreateModeControls();
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
         * Generate and display a puzzle code for the current board state.
         * The code encodes the board's cell values into a compact alphanumeric string.
         */
        private void GeneratePuzzleCode()
        {
            ResolveSceneReferences();
            if (_runner == null || _runner.CurrentBoard == null)
            {
                Debug.LogWarning("MainMenuFlowController: No board loaded. Cannot generate puzzle code.");
                return;
            }

            string code = PuzzleCodeGenerator.EncodeBoardToCode(_runner.CurrentBoard);
            if (string.IsNullOrEmpty(code))
            {
                Debug.LogError("MainMenuFlowController: Failed to encode board to puzzle code.");
                return;
            }

            if (_generatedCodeInput == null)
            {
                EnsurePlayPanel();
            }

            if (_generatedCodeInput != null)
            {
                _generatedCodeInput.text = code;
                StartCoroutine(SelectGeneratedCodeNextFrame());
            }

            _lastObservedBoardFingerprint = ComputeBoardFingerprint(_runner.CurrentBoard);

            Debug.Log($"MainMenuFlowController: Puzzle Code Generated: {code}");
        }

        /**
         * Decode the entered puzzle code and apply it to the current create-mode board.
         */
        private void ApplyPuzzleCodeFromInput()
        {
            ResolveSceneReferences();
            if (_runner == null)
            {
                Debug.LogWarning("MainMenuFlowController: No SolverRunner found. Cannot apply puzzle code.");
                return;
            }

            if (!_runner.IsPuzzleCreationMode)
            {
                Debug.LogWarning("MainMenuFlowController: Puzzle code loading is only available in Create Puzzle mode.");
                return;
            }

            if (_generatedCodeInput == null)
            {
                return;
            }

            string code = _generatedCodeInput.text != null ? _generatedCodeInput.text.Trim() : string.Empty;
            if (string.IsNullOrEmpty(code))
            {
                Debug.LogWarning("MainMenuFlowController: Enter a puzzle code before loading.");
                return;
            }

            var decoded = PuzzleCodeGenerator.DecodeBoardFromCode(code);
            if (decoded == null)
            {
                Debug.LogWarning("MainMenuFlowController: Puzzle code is invalid and could not be decoded.");
                return;
            }

            if (_runner.CurrentBoard == null)
            {
                _runner.CreateBlankBoard();
            }

            if (_runner.CurrentBoard == null)
            {
                Debug.LogWarning("MainMenuFlowController: No board is available to apply code values.");
                return;
            }

            int existingValueCount = CountSetValueCells(_runner.CurrentBoard);
            if (existingValueCount > 0)
            {
                ShowOverwriteConfirmation(code);
                return;
            }

            ApplyDecodedBoardValues(decoded);
        }

        /**
         * Confirm loading pending code and overwrite current values.
         */
        private void ConfirmOverwriteAndApplyPuzzleCode()
        {
            if (string.IsNullOrEmpty(_pendingCodeToApply))
            {
                HideOverwriteConfirmation();
                return;
            }

            var decoded = PuzzleCodeGenerator.DecodeBoardFromCode(_pendingCodeToApply);
            if (decoded == null)
            {
                Debug.LogWarning("MainMenuFlowController: Pending puzzle code became invalid.");
                HideOverwriteConfirmation();
                return;
            }

            ApplyDecodedBoardValues(decoded);
            HideOverwriteConfirmation();
        }

        /**
         * Cancel the overwrite confirmation prompt.
         */
        private void CancelOverwritePuzzleCode()
        {
            HideOverwriteConfirmation();
        }

        /**
         * Apply decoded values onto the current board and refresh solver/runtime state.
         */
        private void ApplyDecodedBoardValues(Sudoku.Models.Board decoded)
        {
            ResolveSceneReferences();
            if (_runner == null || decoded == null)
            {
                return;
            }

            var board = _runner.CurrentBoard;
            if (board == null || board.Cells == null || decoded.Cells == null || board.Size != decoded.Size)
            {
                Debug.LogWarning("MainMenuFlowController: Decoded board dimensions do not match the active board.");
                return;
            }

            for (int row = 0; row < board.Size; row++)
            {
                for (int col = 0; col < board.Size; col++)
                {
                    var target = board.Cells[row, col];
                    var source = decoded.Cells[row, col];
                    if (target == null || source == null)
                    {
                        continue;
                    }

                    target.Value = source.Value;
                    target.IsGiven = false;
                }
            }

            _runner.SyncCandidatesForCurrentBoard(skipFullSolveCheck: true, validateState: true);
            _lastObservedBoardFingerprint = ComputeBoardFingerprint(board);
            ClearPuzzleCodeText();
            Debug.Log("MainMenuFlowController: Puzzle code values were applied to the board.");
        }

        /**
         * Copy the generated puzzle code to the OS clipboard.
         */
        private void CopyGeneratedCodeToClipboard()
        {
            if (_generatedCodeInput == null || string.IsNullOrEmpty(_generatedCodeInput.text))
            {
                return;
            }

            GUIUtility.systemCopyBuffer = _generatedCodeInput.text;
            StartCoroutine(SelectGeneratedCodeNextFrame());
        }

        /**
         * Show overwrite confirmation row and store pending code.
         */
        private void ShowOverwriteConfirmation(string pendingCode)
        {
            _pendingCodeToApply = pendingCode ?? string.Empty;
            if (_overwriteConfirmRow != null)
            {
                _overwriteConfirmRow.SetActive(true);
            }
        }

        /**
         * Hide overwrite confirmation and clear pending code.
         */
        private void HideOverwriteConfirmation()
        {
            _pendingCodeToApply = string.Empty;
            if (_overwriteConfirmRow != null)
            {
                _overwriteConfirmRow.SetActive(false);
            }
        }

        /**
         * Clear the code textbox content.
         */
        private void ClearPuzzleCodeText()
        {
            if (_generatedCodeInput != null)
            {
                _generatedCodeInput.text = string.Empty;
            }
        }

        /**
         * Compute a stable fingerprint based on cell values only.
         */
        private static int ComputeBoardFingerprint(Sudoku.Models.Board board)
        {
            if (board == null || board.Cells == null)
            {
                return 0;
            }

            unchecked
            {
                int hash = 17;
                for (int row = 0; row < board.Size; row++)
                {
                    for (int col = 0; col < board.Size; col++)
                    {
                        var cell = board.Cells[row, col];
                        int value = cell != null && cell.Value.HasValue ? cell.Value.Value : 0;
                        hash = (hash * 31) + value;
                    }
                }

                return hash;
            }
        }

        /**
         * Count solved/value-set cells on a board.
         */
        private static int CountSetValueCells(Sudoku.Models.Board board)
        {
            if (board == null || board.Cells == null)
            {
                return 0;
            }

            int count = 0;
            for (int row = 0; row < board.Size; row++)
            {
                for (int col = 0; col < board.Size; col++)
                {
                    var cell = board.Cells[row, col];
                    if (cell != null && cell.Value.HasValue)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        /**
         * Select all text in the generated-code input so manual OS copy works immediately.
         */
        private System.Collections.IEnumerator SelectGeneratedCodeNextFrame()
        {
            yield return null;

            if (_generatedCodeInput == null)
            {
                yield break;
            }

            _generatedCodeInput.ActivateInputField();
            _generatedCodeInput.caretPosition = _generatedCodeInput.text.Length;
            _generatedCodeInput.selectionAnchorPosition = 0;
            _generatedCodeInput.selectionFocusPosition = _generatedCodeInput.text.Length;
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
        private static void ConfigurePlayButtonLayout(Transform parent, string buttonName, float preferredWidth = 104f, float preferredHeight = 36f, float minWidth = 96f)
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

            layout.preferredWidth = preferredWidth;
            layout.minWidth = minWidth;
            layout.preferredHeight = preferredHeight;
            layout.minHeight = preferredHeight;
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

        /**
         * Ensure the row containing the generated-code textbox and copy button exists.
         */
        private void EnsurePuzzleCodeRow(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            var rowTransform = parent.Find("PuzzleCodeRow");
            if (rowTransform == null)
            {
                _puzzleCodeRow = new GameObject("PuzzleCodeRow", typeof(RectTransform), typeof(CanvasRenderer));
                _puzzleCodeRow.transform.SetParent(parent, false);
            }
            else
            {
                _puzzleCodeRow = rowTransform.gameObject;
            }

            _puzzleCodeRow.transform.SetSiblingIndex(0);

            var rowLayout = _puzzleCodeRow.GetComponent<LayoutElement>();
            if (rowLayout == null)
            {
                rowLayout = _puzzleCodeRow.AddComponent<LayoutElement>();
            }

            rowLayout.preferredHeight = 34f;
            rowLayout.minHeight = 34f;
            rowLayout.flexibleWidth = 0f;
            rowLayout.flexibleHeight = 0f;

            var oldVertical = _puzzleCodeRow.GetComponent<VerticalLayoutGroup>();
            if (oldVertical != null)
            {
                Destroy(oldVertical);
            }

            var hlg = _puzzleCodeRow.GetComponent<HorizontalLayoutGroup>();
            if (hlg == null)
            {
                hlg = _puzzleCodeRow.AddComponent<HorizontalLayoutGroup>();
            }

            hlg.padding = new RectOffset(0, 0, 0, 0);
            hlg.spacing = 6f;
            hlg.childAlignment = TextAnchor.MiddleRight;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            EnsurePuzzleCodeInput(_puzzleCodeRow.transform);
            EnsurePuzzleCodeCopyButton(_puzzleCodeRow.transform);
        }

        /**
         * Ensure the generated-code input field exists.
         */
        private void EnsurePuzzleCodeInput(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            var inputTransform = parent.Find("PuzzleCodeInput");
            GameObject inputGO;
            if (inputTransform == null)
            {
                inputGO = new GameObject("PuzzleCodeInput", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(InputField));
                inputGO.transform.SetParent(parent, false);
            }
            else
            {
                inputGO = inputTransform.gameObject;
            }

            var layout = inputGO.GetComponent<LayoutElement>();
            if (layout == null)
            {
                layout = inputGO.AddComponent<LayoutElement>();
            }

            layout.preferredWidth = 206f;
            layout.minWidth = 206f;
            layout.preferredHeight = 34f;
            layout.minHeight = 34f;
            layout.flexibleWidth = 0f;
            layout.flexibleHeight = 0f;

            var image = inputGO.GetComponent<Image>();
            image.color = new Color(0.97f, 0.97f, 0.97f, 1f);

            var inputField = inputGO.GetComponent<InputField>();
            inputField.readOnly = false;
            inputField.lineType = InputField.LineType.SingleLine;

            var textTransform = inputGO.transform.Find("Text");
            if (textTransform == null)
            {
                var textGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                textGO.transform.SetParent(inputGO.transform, false);
                textTransform = textGO.transform;
            }

            var textRect = textTransform.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8f, 6f);
            textRect.offsetMax = new Vector2(-8f, -6f);

            var text = textTransform.GetComponent<Text>();
            text.alignment = TextAnchor.MiddleLeft;
            text.color = new Color(0.08f, 0.08f, 0.08f, 1f);
            text.fontSize = 13;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            text.font = font;

            var placeholderTransform = inputGO.transform.Find("Placeholder");
            if (placeholderTransform == null)
            {
                var placeholderGO = new GameObject("Placeholder", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                placeholderGO.transform.SetParent(inputGO.transform, false);
                placeholderTransform = placeholderGO.transform;
            }

            var placeholderRect = placeholderTransform.GetComponent<RectTransform>();
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = new Vector2(8f, 6f);
            placeholderRect.offsetMax = new Vector2(-8f, -6f);

            var placeholder = placeholderTransform.GetComponent<Text>();
            placeholder.text = "Paste or generate puzzle code";
            placeholder.alignment = TextAnchor.MiddleLeft;
            placeholder.color = new Color(0.42f, 0.42f, 0.42f, 0.85f);
            placeholder.fontSize = 11;
            placeholder.font = font;

            inputField.textComponent = text;
            inputField.placeholder = placeholder;

            _generatedCodeInput = inputField;
        }

        /**
         * Ensure the copy button next to the code textbox exists.
         */
        private void EnsurePuzzleCodeCopyButton(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            var buttonTransform = parent.Find("CopyPuzzleCodeButton");
            if (buttonTransform == null)
            {
                CreateMenuButton(parent, "CopyPuzzleCodeButton", "Copy", CopyGeneratedCodeToClipboard, true);
                buttonTransform = parent.Find("CopyPuzzleCodeButton");
            }

            if (buttonTransform == null)
            {
                return;
            }

            var button = buttonTransform.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(CopyGeneratedCodeToClipboard);
            }

            var layout = buttonTransform.GetComponent<LayoutElement>();
            if (layout == null)
            {
                layout = buttonTransform.gameObject.AddComponent<LayoutElement>();
            }

            layout.preferredWidth = 62f;
            layout.minWidth = 56f;
            layout.preferredHeight = 34f;
            layout.minHeight = 34f;
            layout.flexibleWidth = 0f;
            layout.flexibleHeight = 0f;

            var text = buttonTransform.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.text = string.Empty;
            }

            EnsureCopyIconGraphic(buttonTransform);
        }

        /**
         * Build a clear copy icon from simple UI shapes so it does not depend on font glyph support.
         */
        private static void EnsureCopyIconGraphic(Transform buttonTransform)
        {
            if (buttonTransform == null)
            {
                return;
            }

            var iconRoot = buttonTransform.Find("CopyIconGraphic");
            if (iconRoot == null)
            {
                var rootGO = new GameObject("CopyIconGraphic", typeof(RectTransform), typeof(CanvasRenderer));
                rootGO.transform.SetParent(buttonTransform, false);
                iconRoot = rootGO.transform;
            }

            var rootRect = iconRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            EnsureCopySheet(iconRoot, "BackSheet", new Vector2(-3f, 2f), new Vector2(11f, 13f));
            EnsureCopySheet(iconRoot, "FrontSheet", new Vector2(2f, -2f), new Vector2(11f, 13f));
        }

        /**
         * Ensure one outlined sheet used by the copy icon exists and is styled.
         */
        private static void EnsureCopySheet(Transform parent, string name, Vector2 anchoredPosition, Vector2 size)
        {
            var sheet = parent.Find(name);
            if (sheet == null)
            {
                var sheetGO = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                sheetGO.transform.SetParent(parent, false);
                sheet = sheetGO.transform;
            }

            var sheetRect = sheet.GetComponent<RectTransform>();
            sheetRect.anchorMin = new Vector2(0.5f, 0.5f);
            sheetRect.anchorMax = new Vector2(0.5f, 0.5f);
            sheetRect.pivot = new Vector2(0.5f, 0.5f);
            sheetRect.anchoredPosition = anchoredPosition;
            sheetRect.sizeDelta = size;

            var outer = sheet.GetComponent<Image>();
            outer.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);
            outer.raycastTarget = false;

            var inner = sheet.Find("Inner");
            if (inner == null)
            {
                var innerGO = new GameObject("Inner", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                innerGO.transform.SetParent(sheet, false);
                inner = innerGO.transform;
            }

            var innerRect = inner.GetComponent<RectTransform>();
            innerRect.anchorMin = Vector2.zero;
            innerRect.anchorMax = Vector2.one;
            innerRect.offsetMin = new Vector2(2f, 2f);
            innerRect.offsetMax = new Vector2(-2f, -2f);

            var innerImage = inner.GetComponent<Image>();
            innerImage.color = new Color(0.95f, 0.95f, 0.95f, 1f);
            innerImage.raycastTarget = false;
        }

        /**
         * Toggle create-mode-only controls in the play panel.
         */
        private void UpdatePlayPanelCreateModeControls()
        {
            ResolveSceneReferences();
            bool isCreateMode = _runner != null && _runner.IsPuzzleCreationMode;

            if (_puzzleCodeRow != null)
            {
                _puzzleCodeRow.SetActive(isCreateMode);
            }

            if (_playPanel != null)
            {
                var loadButton = _playPanel.transform.Find("PlayActionsRow/LoadPuzzleCodeButton");
                if (loadButton != null)
                {
                    loadButton.gameObject.SetActive(isCreateMode);
                }

                var generateButton = _playPanel.transform.Find("PlayActionsRow/SaveBoardButton");
                if (generateButton != null)
                {
                    generateButton.gameObject.SetActive(isCreateMode);
                }
            }

            if (_overwriteConfirmRow != null)
            {
                _overwriteConfirmRow.SetActive(isCreateMode && !string.IsNullOrEmpty(_pendingCodeToApply));
            }

            if (!isCreateMode && _generatedCodeInput != null)
            {
                _generatedCodeInput.text = string.Empty;
                HideOverwriteConfirmation();
            }
        }

        /**
         * Ensure overwrite-confirmation row exists.
         */
        private void EnsureOverwriteConfirmationRow(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            var row = parent.Find("OverwriteConfirmRow");
            if (row == null)
            {
                _overwriteConfirmRow = new GameObject("OverwriteConfirmRow", typeof(RectTransform), typeof(CanvasRenderer));
                _overwriteConfirmRow.transform.SetParent(parent, false);
            }
            else
            {
                _overwriteConfirmRow = row.gameObject;
            }

            _overwriteConfirmRow.transform.SetSiblingIndex(1);

            var layout = _overwriteConfirmRow.GetComponent<LayoutElement>();
            if (layout == null)
            {
                layout = _overwriteConfirmRow.AddComponent<LayoutElement>();
            }

            layout.preferredHeight = 28f;
            layout.minHeight = 28f;
            layout.flexibleHeight = 0f;

            var hlg = _overwriteConfirmRow.GetComponent<HorizontalLayoutGroup>();
            if (hlg == null)
            {
                hlg = _overwriteConfirmRow.AddComponent<HorizontalLayoutGroup>();
            }

            hlg.padding = new RectOffset(0, 0, 0, 0);
            hlg.spacing = 6f;
            hlg.childAlignment = TextAnchor.MiddleRight;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            EnsureOverwriteConfirmLabel(_overwriteConfirmRow.transform);
            EnsureOverwriteConfirmButtons(_overwriteConfirmRow.transform);
            _overwriteConfirmRow.SetActive(false);
        }

        /**
         * Ensure overwrite-confirmation message label exists.
         */
        private static void EnsureOverwriteConfirmLabel(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            var labelTransform = parent.Find("OverwriteConfirmText");
            GameObject labelGO;
            if (labelTransform == null)
            {
                labelGO = new GameObject("OverwriteConfirmText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                labelGO.transform.SetParent(parent, false);
            }
            else
            {
                labelGO = labelTransform.gameObject;
            }

            var layout = labelGO.GetComponent<LayoutElement>();
            if (layout == null)
            {
                layout = labelGO.AddComponent<LayoutElement>();
            }

            layout.preferredWidth = 168f;
            layout.minWidth = 140f;
            layout.preferredHeight = 28f;
            layout.minHeight = 28f;

            var text = labelGO.GetComponent<Text>();
            text.text = "Overwrite current board?";
            text.alignment = TextAnchor.MiddleRight;
            text.color = new Color(0.93f, 0.93f, 0.93f, 1f);
            text.fontSize = 12;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            text.font = font;
        }

        /**
         * Ensure overwrite-confirmation buttons exist.
         */
        private void EnsureOverwriteConfirmButtons(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            EnsurePlayActionButton(parent, "ConfirmOverwriteButton", "Yes", ConfirmOverwriteAndApplyPuzzleCode, true);
            EnsurePlayActionButton(parent, "CancelOverwriteButton", "No", CancelOverwritePuzzleCode, false);
            ConfigurePlayButtonLayout(parent, "ConfirmOverwriteButton", 48f, 28f, 48f);
            ConfigurePlayButtonLayout(parent, "CancelOverwriteButton", 48f, 28f, 48f);
        }

        /**
         * Ensure the row that holds play action buttons exists.
         */
        private static Transform EnsurePlayActionsRow(Transform parent)
        {
            if (parent == null)
            {
                return null;
            }

            var row = parent.Find("PlayActionsRow");
            if (row == null)
            {
                var rowGO = new GameObject("PlayActionsRow", typeof(RectTransform), typeof(CanvasRenderer));
                rowGO.transform.SetParent(parent, false);
                row = rowGO.transform;
            }

            row.SetSiblingIndex(1);

            var layout = row.GetComponent<LayoutElement>();
            if (layout == null)
            {
                layout = row.gameObject.AddComponent<LayoutElement>();
            }

            layout.preferredHeight = 34f;
            layout.minHeight = 34f;
            layout.preferredWidth = 206f;
            layout.minWidth = 206f;
            layout.flexibleWidth = 0f;
            layout.flexibleHeight = 0f;

            var oldVertical = row.GetComponent<VerticalLayoutGroup>();
            if (oldVertical != null)
            {
                Destroy(oldVertical);
            }

            var hlg = row.GetComponent<HorizontalLayoutGroup>();
            if (hlg == null)
            {
                hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            }

            hlg.padding = new RectOffset(0, 0, 0, 0);
            hlg.spacing = 6f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            row.SetSiblingIndex(2);

            return row;
        }

        /**
         * Ensure Exit To Menu appears at the bottom-right corner of the side panel.
         */
        private void EnsureBottomRightExitButton(Transform sidePanelTransform)
        {
            if (sidePanelTransform == null)
            {
                return;
            }

            EnsurePlayActionButton(sidePanelTransform, "ExitCornerButton", "Exit To Menu", ReturnToMenuFromPlay, true);
            ConfigurePlayButtonLayout(sidePanelTransform, "ExitCornerButton", 112f, 36f, 112f);

            var exitTransform = sidePanelTransform.Find("ExitCornerButton");
            if (exitTransform == null)
            {
                return;
            }

            var exitRect = exitTransform.GetComponent<RectTransform>();
            if (exitRect != null)
            {
                exitRect.anchorMin = new Vector2(1f, 0f);
                exitRect.anchorMax = new Vector2(1f, 0f);
                exitRect.pivot = new Vector2(1f, 0f);
                exitRect.anchoredPosition = new Vector2(-8f, 8f);
                exitRect.sizeDelta = new Vector2(112f, 36f);
            }

            exitTransform.SetAsLastSibling();
            exitTransform.gameObject.SetActive(true);
        }

        /**
         * Ensure a simple icon-like label exists over a button.
         */
        private static void EnsureIconLabel(Transform buttonTransform, string labelName, string glyph, int fontSize = 18)
        {
            if (buttonTransform == null)
            {
                return;
            }

            var labelTransform = buttonTransform.Find(labelName);
            GameObject labelGO;
            if (labelTransform == null)
            {
                labelGO = new GameObject(labelName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                labelGO.transform.SetParent(buttonTransform, false);
            }
            else
            {
                labelGO = labelTransform.gameObject;
            }

            var rect = labelGO.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var text = labelGO.GetComponent<Text>();
            text.text = glyph;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.1f, 0.1f, 0.1f, 1f);
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.raycastTarget = false;

            // Prefer symbol-capable OS fonts so icon glyphs render reliably.
            Font font = null;
            try
            {
                font = Font.CreateDynamicFontFromOSFont(new[]
                {
                    "Segoe Fluent Icons",
                    "Segoe MDL2 Assets",
                    "Segoe UI Symbol",
                    "Arial Unicode MS",
                    "Noto Sans Symbols"
                }, fontSize);
            }
            catch
            {
                // Ignore and fallback to built-in fonts below.
            }

            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            text.font = font;
        }
    }
}