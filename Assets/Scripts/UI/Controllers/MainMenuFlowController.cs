using UnityEngine;
using UnityEngine.UI;
using Sudoku.Solver;
using Sudoku.Solver.Rules;
using Sudoku.Models;
using System;
using System.Collections.Generic;
using Object = UnityEngine.Object;
using Sudoku.UI.Panels;
using Sudoku.UI.Menus;
using Sudoku.Solver.Unsolver;

namespace Sudoku.UI.Controllers
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
        private const string TimerLabelName = "SolveTimerLabel";

        private Canvas _runtimeCanvas;
        private GameObject _menuPanel;
        private GameObject _playPanel;
        private SolverRunner _runner;
        private BoardVisualizer _boardVisualizer;
        private BoardSidePanel _boardSidePanel;
        private InputField _generatedCodeInput;
        private InputField _startPuzzleCodeInput;
        private GameObject _puzzleCodeRow;
        private GameObject _solvePuzzleCodeRow;
        private InputField _solvePuzzleCodeInput;
        private GameObject _overwriteConfirmRow;
        private string _pendingCodeToApply = string.Empty;
        private SavedPuzzleListPanel _savedPuzzleListPanel;
        private LoadPuzzleListPanel _loadPuzzleListPanel;
        private string _startingPuzzleCode = string.Empty;
        private float _defaultRulesAreaTopOffset = float.NaN;
        private bool _hasCapturedRulesAreaTopOffset;
        private int _lastObservedBoardFingerprint = int.MinValue;
        private PuzzleSolveTimer _solveTimer;
        private Text _timerLabelText;
        private int _lastTimerBoardFingerprint = int.MinValue;

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
            PreloadSavedPuzzles();
            EnterMenuMode();
        }

        /**
         * Load saved puzzles from disk during startup so Saved Puzzles are
         * initialized before the panel is first opened.
         */
        private void PreloadSavedPuzzles()
        {
            // Warm the repository at startup so persistence issues surface
            // immediately and list metadata (such as count) is ready.
            var puzzles = SavedPuzzleRepository.LoadAll();

            // Create and initialize the list panel once at startup so it can
            // render from preloaded data without a first-open setup delay.
            EnsureSavedPuzzleListPanel();

            Debug.Log($"MainMenuFlowController: Preloaded {puzzles.Count} saved puzzle(s) from disk.");
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
         * Also refreshes the solve timer display and checks for puzzle completion.
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

            // Update timer label in solve mode.
            UpdateTimerDisplay();

            // Check for puzzle completion in solve mode.
            if (_solveTimer != null && _solveTimer.IsRunning && _runner != null && !_runner.IsPuzzleCreationMode && _runner.CurrentBoard != null)
            {
                CheckAndStopTimerIfPuzzleComplete();
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
            vlg.spacing = 6f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var fitter = _menuPanel.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            CreateLabel(_menuPanel.transform, "Title", "Sudoku", 54, FontStyle.Bold, 70f);
            CreateLabel(_menuPanel.transform, "Subtitle", "Choose an option to begin", 26, FontStyle.Normal, 46f);
            EnsureMenuPuzzleCodeRow(_menuPanel.transform);

            CreateMenuButton(_menuPanel.transform, "StartGeneratedPuzzleButton", "Start Puzzle", StartPuzzleFromCodeOrExisting, true);
            CreateMenuSpacer(_menuPanel.transform, "CreatePuzzleGroupSpacer", 14f);
            CreateMenuButton(_menuPanel.transform, "LoadPuzzleButton", "Load Puzzle", OpenLoadPuzzleList, false);
            CreateMenuButton(_menuPanel.transform, "SavedPuzzlesButton", "Saved Puzzles", OpenSavedPuzzleList, false);
            CreateMenuButton(_menuPanel.transform, "CreatePuzzleButton", "Create New Puzzle", CreateNewPuzzleStub, false);
            CreateMenuButton(_menuPanel.transform, "OpenConfigurationButton", "Config", OpenConfigurationStub, false);
            EnsureButtonTextPadding(_menuPanel.transform.Find("OpenConfigurationButton"), 0f, 0f);
            EnsureIconNearCenteredText(_menuPanel.transform.Find("OpenConfigurationButton"), "ConfigIconLabel", "⚙", 30, 18f, 2f);
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
            EnsureSolvePuzzleCodeRow(sidePanel.transform);
            EnsureTimerLabel(sidePanel.transform);
            EnsureSolveTimer();

            EnsurePlayActionButton(actionsRow, "LoadPuzzleCodeButton", "Load Code", ApplyPuzzleCodeFromInput, false);
            EnsurePlayActionButton(actionsRow, "SaveBoardButton", "", GeneratePuzzleCode, false);
            ConfigurePlayButtonLayout(actionsRow, "LoadPuzzleCodeButton", 86f, 34f, 70f);
            ConfigurePlayButtonLayout(actionsRow, "SaveBoardButton", 44f, 34f, 40f);
            EnsureIconLabel(actionsRow.Find("SaveBoardButton"), "GenerateIconLabel", "⟳", 24);

            EnsurePlayActionButton(actionsRow, "SaveToListButton", "Save", SaveCurrentPuzzleToList, false);
            ConfigurePlayButtonLayout(actionsRow, "SaveToListButton", 54f, 34f, 44f);

            EnsurePlayActionButton(actionsRow, "SaveSolvedStateButton", "Save Current Solved State", SaveCurrentSolvedStateToXml, false);
            ConfigurePlayButtonLayout(actionsRow, "SaveSolvedStateButton", 168f, 34f, 148f);

            EnsureBottomRightExitButton(sidePanel.transform);
            EnsureConfigCornerButton(sidePanel.transform);

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

            _savedPuzzleListPanel?.Close();
            _loadPuzzleListPanel?.Close();

            var exitButton = FindObjectInLoadedScenesIncludingInactive("ExitCornerButton");
            if (exitButton != null)
            {
                exitButton.SetActive(false);
            }

            // Pause the timer when returning to menu so time does not accumulate while idle.
            _solveTimer?.Pause();

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

            _savedPuzzleListPanel?.Close();
            _loadPuzzleListPanel?.Close();

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
            CacheStartingPuzzleCodeFromCurrentBoard();
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
         * Start puzzle mode using menu-entered code when provided.
         * Falls back to the default board when code is empty.
         */
        private void StartPuzzleFromCodeOrExisting()
        {
            string enteredCode = _startPuzzleCodeInput != null ? _startPuzzleCodeInput.text : string.Empty;
            StartPuzzleFromOptionalCode(enteredCode);
        }

        /**
         * Start puzzle mode from an optional puzzle code.
         *
         * @param code Optional code entered by the user.
         */
        private void StartPuzzleFromOptionalCode(string code)
        {
            ResolveSceneReferences();
            if (_runner == null)
            {
                Debug.LogWarning("MainMenuFlowController: No SolverRunner found. Cannot start puzzle.");
                return;
            }

            _runner.LoadBoardFromRows();

            string trimmed = string.IsNullOrWhiteSpace(code) ? string.Empty : code.Trim();
            if (!string.IsNullOrEmpty(trimmed))
            {
                var decoded = PuzzleCodeGenerator.DecodeBoardFromCode(trimmed);
                if (decoded != null)
                {
                    ApplyDecodedBoardValuesToCurrentBoard(decoded, markAsGiven: true);
                }
                else
                {
                    Debug.LogWarning("MainMenuFlowController: Entered puzzle code is invalid. Using the existing board.");
                }
            }
            else if (TryGenerateRandomPuzzleFromEnabledRules(out var generatedPuzzle))
            {
                ApplyGeneratedBoardValuesToCurrentBoard(generatedPuzzle);
            }

            CacheStartingPuzzleCodeFromCurrentBoard();
            _runner.SetInteractionMode(BoardInteractionMode.Puzzle);
            ConfigureBoardVisualizerForRunnerMode();

            // Start a fresh timer for this new puzzle.
            EnsureSolveTimer();
            _solveTimer.StartFresh();
            _lastTimerBoardFingerprint = int.MinValue;

            EnterPlayMode();

            ResolveSceneReferences();
            if (_boardSidePanel != null)
            {
                _boardSidePanel.RefreshPanelVisibilityForCurrentMode();
            }
        }

        /**
         * Generate a random puzzle by unsolving a random solved board with currently enabled rules.
         *
         * @param generatedPuzzle Outputs the generated puzzle board when successful.
         * @returns True when generation succeeds; otherwise false.
         */
        private bool TryGenerateRandomPuzzleFromEnabledRules(out Sudoku.Models.Board generatedPuzzle)
        {
            generatedPuzzle = null;

            if (_runner == null)
            {
                return false;
            }

            _runner.EnsureEngine();
            if (_runner.Registry == null)
            {
                Debug.LogWarning("MainMenuFlowController: Rule registry is unavailable. Using existing board.");
                return false;
            }

            var enabledRules = new List<ISudokuRule>();
            var rulesWithStatus = _runner.Registry.GetRulesWithStatus();
            for (int i = 0; i < rulesWithStatus.Count; i++)
            {
                var entry = rulesWithStatus[i];
                if (entry.enabled && entry.rule != null)
                {
                    enabledRules.Add(entry.rule);
                }
            }

            if (enabledRules.Count == 0)
            {
                Debug.LogWarning("MainMenuFlowController: No rules are enabled for random puzzle generation. Using existing board.");
                return false;
            }

            try
            {
                var random = new System.Random();
                var solved = RandomSolvedBoardGenerator.GenerateRandomSolvedBoard(random);
                var generator = new PuzzleGenerator(requireNonNakedContribution: true);
                generatedPuzzle = generator.Generate(solved, enabledRules, random);
                if (generatedPuzzle != null)
                {
                    string ruleSummary = string.IsNullOrWhiteSpace(generator.LastGenerationRuleUsageSummary)
                        ? "(no rule applications recorded)"
                        : generator.LastGenerationRuleUsageSummary;
                    Debug.Log($"MainMenuFlowController: Generated puzzle unsolve rule usage: {ruleSummary}");
                }
                return generatedPuzzle != null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"MainMenuFlowController: Random puzzle generation failed. Using existing board. {ex.Message}");
                return false;
            }
        }

        /**
         * Apply a generated board to the current runner board, preserving generated given flags.
         *
         * @param generated Generated puzzle board to copy from.
         */
        private void ApplyGeneratedBoardValuesToCurrentBoard(Sudoku.Models.Board generated)
        {
            ResolveSceneReferences();
            if (_runner == null || generated == null)
            {
                return;
            }

            var board = _runner.CurrentBoard;
            if (board == null || board.Cells == null || generated.Cells == null || board.Size != generated.Size)
            {
                Debug.LogWarning("MainMenuFlowController: Generated board dimensions do not match the active board.");
                return;
            }

            for (int row = 0; row < board.Size; row++)
            {
                for (int col = 0; col < board.Size; col++)
                {
                    var target = board.Cells[row, col];
                    var source = generated.Cells[row, col];
                    if (target == null || source == null)
                    {
                        continue;
                    }

                    target.Value = source.Value;
                    target.IsGiven = source.IsGiven;
                }
            }

            _runner.SyncCandidatesForCurrentBoard(skipFullSolveCheck: true, validateState: true);
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
         * Open the saved puzzle list panel overlay.
         */
        private void OpenSavedPuzzleList()
        {
            EnsureSavedPuzzleListPanel();
            if (_savedPuzzleListPanel != null)
            {
                _savedPuzzleListPanel.Open();
            }
        }

        /**
         * Open the solved-state load panel overlay.
         */
        private void OpenLoadPuzzleList()
        {
            EnsureLoadPuzzleListPanel();
            if (_loadPuzzleListPanel != null)
            {
                _loadPuzzleListPanel.Open();
            }
        }

        /**
         * Ensure the saved puzzle list panel has been created and initialized.
         */
        private void EnsureSavedPuzzleListPanel()
        {
            if (_savedPuzzleListPanel != null)
            {
                return;
            }

            if (_runtimeCanvas == null)
            {
                Debug.LogWarning("MainMenuFlowController: Cannot create saved puzzle panel - runtime canvas not found.");
                return;
            }

            var panelGO = new GameObject("SavedPuzzleListPanelHost", typeof(RectTransform));
            panelGO.transform.SetParent(_runtimeCanvas.transform, false);

            _savedPuzzleListPanel = panelGO.AddComponent<SavedPuzzleListPanel>();
            _savedPuzzleListPanel.Initialize(_runtimeCanvas);
            _savedPuzzleListPanel.OnPuzzleLoadRequested += OnSavedPuzzleLoadRequested;
            _savedPuzzleListPanel.OnPuzzleEditRequested += OnSavedPuzzleEditRequested;
        }

        /**
         * Ensure the solved-state load panel has been created and initialized.
         */
        private void EnsureLoadPuzzleListPanel()
        {
            if (_loadPuzzleListPanel != null)
            {
                return;
            }

            if (_runtimeCanvas == null)
            {
                Debug.LogWarning("MainMenuFlowController: Cannot create load puzzle panel - runtime canvas not found.");
                return;
            }

            var panelGO = new GameObject("LoadPuzzleListPanelHost", typeof(RectTransform));
            panelGO.transform.SetParent(_runtimeCanvas.transform, false);

            _loadPuzzleListPanel = panelGO.AddComponent<LoadPuzzleListPanel>();
            _loadPuzzleListPanel.Initialize(_runtimeCanvas);
            _loadPuzzleListPanel.OnPuzzleLoadRequested += OnSolvedStatePuzzleLoadRequested;
        }

        /**
         * Handle a load request from the saved puzzle list panel.
         * Closes the panel and starts solving the selected puzzle.
         *
         * @param code The encoded puzzle code to start.
         */
        private void OnSavedPuzzleLoadRequested(string code)
        {
            StartPuzzleFromOptionalCode(code);
        }

        /**
         * Handle a solved-state load request from the Load Puzzle panel.
         *
         * @param solvedState Serialized solved-state export selected by the user.
         */
        private void OnSolvedStatePuzzleLoadRequested(SolvedPuzzleStateExport solvedState)
        {
            StartPuzzleFromSolvedState(solvedState);
        }

        /**
         * Start solve mode from a previously saved solved-state export.
         *
         * @param solvedState Serialized solved-state payload loaded from XML.
         */
        private void StartPuzzleFromSolvedState(SolvedPuzzleStateExport solvedState)
        {
            ResolveSceneReferences();
            if (_runner == null)
            {
                Debug.LogWarning("MainMenuFlowController: No SolverRunner found. Cannot load solved-state puzzle.");
                return;
            }

            if (solvedState == null)
            {
                Debug.LogWarning("MainMenuFlowController: Cannot load solved-state puzzle - payload is null.");
                return;
            }

            _runner.LoadBoardFromRows();
            if (_runner.CurrentBoard == null)
            {
                Debug.LogWarning("MainMenuFlowController: No board is available for solved-state loading.");
                return;
            }

            var board = _runner.CurrentBoard;
            if (board.Cells == null)
            {
                Debug.LogWarning("MainMenuFlowController: Current board grid is missing and cannot be loaded.");
                return;
            }

            string initialCode = string.IsNullOrWhiteSpace(solvedState.InitialPuzzleCode)
                ? solvedState.PuzzleCode
                : solvedState.InitialPuzzleCode;

            var initialDecoded = PuzzleCodeGenerator.DecodeBoardFromCode(initialCode);
            if (initialDecoded != null && initialDecoded.Size == board.Size)
            {
                ApplyDecodedBoardValuesToCurrentBoard(initialDecoded, markAsGiven: true);
            }

            var cellByPosition = new Dictionary<(int row, int col), SolvedPuzzleCellExport>();
            if (solvedState.Cells != null)
            {
                for (int i = 0; i < solvedState.Cells.Count; i++)
                {
                    var exportCell = solvedState.Cells[i];
                    if (exportCell == null)
                    {
                        continue;
                    }

                    if (exportCell.Row < 0 || exportCell.Column < 0 || exportCell.Row >= board.Size || exportCell.Column >= board.Size)
                    {
                        continue;
                    }

                    cellByPosition[(exportCell.Row, exportCell.Column)] = exportCell;
                }
            }

            for (int row = 0; row < board.Size; row++)
            {
                for (int col = 0; col < board.Size; col++)
                {
                    var target = board.Cells[row, col];
                    if (target == null)
                    {
                        continue;
                    }

                    if (!cellByPosition.TryGetValue((row, col), out var source))
                    {
                        continue;
                    }

                    target.Value = source.Value;
                    target.IsGiven = source.IsGiven;
                    if (target.Candidates == null)
                    {
                        target.Candidates = new HashSet<int>();
                    }

                    target.Candidates.Clear();
                    if (!target.Value.HasValue && source.Candidates != null)
                    {
                        for (int i = 0; i < source.Candidates.Count; i++)
                        {
                            int candidate = source.Candidates[i];
                            if (candidate >= 1 && candidate <= board.Size)
                            {
                                target.Candidates.Add(candidate);
                            }
                        }
                    }
                }
            }

            var restoredChangeLog = new List<Solver.Rules.CellChange>();
            if (solvedState.ChangeLog != null)
            {
                for (int i = 0; i < solvedState.ChangeLog.Count; i++)
                {
                    var source = solvedState.ChangeLog[i];
                    if (source == null)
                    {
                        continue;
                    }

                    if (source.Row < 0 || source.Column < 0 || source.Row >= board.Size || source.Column >= board.Size)
                    {
                        continue;
                    }

                    var change = new Solver.Rules.CellChange
                    {
                        Row = source.Row,
                        Column = source.Column,
                        OldValue = source.OldValue,
                        NewValue = source.NewValue,
                        ClearValue = source.ClearValue,
                        ForceSetValue = source.ForceSetValue,
                        ValueOnlySet = source.ValueOnlySet,
                        GroupId = source.GroupId,
                        SourceRuleName = source.SourceRuleName,
                        SourceRuleDescription = source.SourceRuleDescription,
                        RemovedCandidates = source.RemovedCandidates != null ? new List<int>(source.RemovedCandidates) : new List<int>(),
                        AddedCandidates = source.AddedCandidates != null ? new List<int>(source.AddedCandidates) : new List<int>()
                    };

                    restoredChangeLog.Add(change);
                }
            }

            board.ChangeLog = restoredChangeLog;
            board.ChangeLogIndex = Mathf.Clamp(solvedState.ChangeLogIndex, 0, board.ChangeLog.Count);
            board.NextChangeGroupId = solvedState.NextChangeGroupId > 0
                ? solvedState.NextChangeGroupId
                : board.ChangeLog.Count + 1;

            _startingPuzzleCode = string.IsNullOrWhiteSpace(initialCode) ? string.Empty : initialCode.Trim();
            UpdateSolvePuzzleCodeDisplay();

            _runner.SetInteractionMode(BoardInteractionMode.Puzzle);
            _runner.ValidateCurrentBoardState(skipFullSolveCheck: true);
            ChangeLogRuntimeControls.RefreshButtonStates();

            ConfigureBoardVisualizerForRunnerMode();

            // Restore the timer from saved state so solve time continues where it left off.
            EnsureSolveTimer();
            _solveTimer.RestoreAndResume(solvedState.ElapsedSeconds);
            _lastTimerBoardFingerprint = int.MinValue;

            EnterPlayMode();

            ResolveSceneReferences();
            if (_boardSidePanel != null)
            {
                _boardSidePanel.RefreshPanelVisibilityForCurrentMode();
            }

            _lastObservedBoardFingerprint = ComputeBoardFingerprint(board);
            Debug.Log("MainMenuFlowController: Loaded solved-state puzzle from XML save.");
        }

        /**
         * Handle an edit request from the saved puzzle list panel.
         * Loads the selected puzzle into create mode so values can be edited.
         *
         * @param code Encoded puzzle code to load for editing.
         */
        private void OnSavedPuzzleEditRequested(string code)
        {
            StartPuzzleEditFromCode(code);
        }

        /**
         * Enter create mode and preload a puzzle code as editable values.
         *
         * @param code Puzzle code to decode and load.
         */
        private void StartPuzzleEditFromCode(string code)
        {
            ResolveSceneReferences();
            if (_runner == null)
            {
                Debug.LogWarning("MainMenuFlowController: No SolverRunner found. Cannot edit saved puzzle.");
                return;
            }

            string trimmed = string.IsNullOrWhiteSpace(code) ? string.Empty : code.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                Debug.LogWarning("MainMenuFlowController: Cannot edit saved puzzle - code is empty.");
                return;
            }

            var decoded = PuzzleCodeGenerator.DecodeBoardFromCode(trimmed);
            if (decoded == null)
            {
                Debug.LogWarning("MainMenuFlowController: Cannot edit saved puzzle - code is invalid.");
                return;
            }

            _runner.CreateBlankBoard();
            if (_runner.CurrentBoard == null)
            {
                Debug.LogWarning("MainMenuFlowController: No board is available for saved puzzle editing.");
                return;
            }

            ApplyDecodedBoardValues(decoded);
            _runner.SetInteractionMode(BoardInteractionMode.PuzzleCreation);
            ConfigureBoardVisualizerForRunnerMode();
            EnterPlayMode();

            ResolveSceneReferences();
            if (_boardSidePanel != null)
            {
                _boardSidePanel.RefreshPanelVisibilityForCurrentMode();
            }
        }

        /**
         * Save the currently active puzzle to the saved puzzle list.
         * In create mode the board's current values are encoded; in solve mode
         * the captured starting puzzle code is saved.
         */
        private void SaveCurrentPuzzleToList()
        {
            ResolveSceneReferences();

            string code = string.Empty;

            if (_runner != null && _runner.IsPuzzleCreationMode && _runner.CurrentBoard != null)
            {
                // Save the created puzzle's current board state.
                code = PuzzleCodeGenerator.EncodeBoardToCode(_runner.CurrentBoard);
            }
            else if (!string.IsNullOrEmpty(_startingPuzzleCode))
            {
                // Save the solve-mode starting puzzle code.
                code = _startingPuzzleCode;
            }

            if (string.IsNullOrEmpty(code))
            {
                Debug.LogWarning("MainMenuFlowController: Cannot save - no active puzzle board.");
                return;
            }

            int nextNumber = SavedPuzzleRepository.Count() + 1;
            var entry = new SavedPuzzle($"Puzzle {nextNumber}", code);
            entry.ApplyAnalysis(SavedPuzzleAnalysisGenerator.AnalyzeFromCode(code));
            SavedPuzzleRepository.Add(entry);

            Debug.Log($"MainMenuFlowController: Saved '{entry.Name}' to the puzzle list.");
        }

        /**
         * Save the solve-mode current board state to XML in Documents/My Games/Last Sudoku.
         */
        private void SaveCurrentSolvedStateToXml()
        {
            ResolveSceneReferences();
            if (_runner == null || _runner.CurrentBoard == null)
            {
                Debug.LogWarning("MainMenuFlowController: Cannot export solved state - no active puzzle board.");
                return;
            }

            if (_runner.IsPuzzleCreationMode)
            {
                Debug.LogWarning("MainMenuFlowController: Solved-state XML export is only available in Solve Puzzle mode.");
                return;
            }

            bool isWindows = Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer;
            if (!isWindows)
            {
                Debug.LogWarning("MainMenuFlowController: Solved-state XML export currently targets Windows documents path.");
                return;
            }

            string currentPuzzleCode = PuzzleCodeGenerator.EncodeBoardToCode(_runner.CurrentBoard);
            if (string.IsNullOrWhiteSpace(currentPuzzleCode))
            {
                Debug.LogWarning("MainMenuFlowController: Cannot export solved state - failed to generate puzzle code.");
                return;
            }

            try
            {
                double elapsed = _solveTimer != null ? _solveTimer.ElapsedSeconds : 0.0;
                var exportModel = SolvedPuzzleStateExport.FromBoard(_runner.CurrentBoard, currentPuzzleCode, _startingPuzzleCode, elapsed);
                string fullPath = SolvedPuzzleStateXmlExporter.Save(exportModel);
                Debug.Log($"MainMenuFlowController: Saved solved-state XML to '{fullPath}'.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"MainMenuFlowController: Failed to export solved state XML. {ex.Message}");
            }
        }

        /**
         * Stub: open game configuration.
         */
        private void OpenConfigurationStub()
        {
            var configManager = FindAnyObjectByType<ConfigPanelManager>();
            if (configManager == null)
            {
                // Create the config manager if it doesn't exist
                var managerGO = new GameObject("ConfigPanelManager", typeof(ConfigPanelManager));
                configManager = managerGO.GetComponent<ConfigPanelManager>();
            }
            configManager.OpenConfigPanel();
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

            if (_runner.IsPuzzleCreationMode)
            {
                _runner.HandleRuleToggleChanged("PuzzleCodeLoadRefresh", true);
                RefreshCreateModeStatusPanels();
            }

            _lastObservedBoardFingerprint = ComputeBoardFingerprint(board);
            ClearPuzzleCodeText();
            Debug.Log("MainMenuFlowController: Puzzle code values were applied to the board.");
        }

        /**
         * Force all create-mode status panels to refresh immediately.
         */
        private static void RefreshCreateModeStatusPanels()
        {
            var panels = Resources.FindObjectsOfTypeAll<CreateModeStatusPanel>();
            for (int i = 0; i < panels.Length; i++)
            {
                var panel = panels[i];
                if (panel == null)
                {
                    continue;
                }

                var go = panel.gameObject;
                if (go == null || !go.scene.IsValid() || !go.scene.isLoaded)
                {
                    continue;
                }

                panel.RefreshStatus(force: true);
            }
        }

        /**
         * Apply decoded values onto the current board and optionally mark them as givens.
         *
         * @param decoded Decoded board values.
         * @param markAsGiven True to lock non-empty values as givens.
         */
        private void ApplyDecodedBoardValuesToCurrentBoard(Sudoku.Models.Board decoded, bool markAsGiven)
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
                    target.IsGiven = markAsGiven && source.Value.HasValue;
                }
            }

            _runner.SyncCandidatesForCurrentBoard(skipFullSolveCheck: true, validateState: true);
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
         * Copy the solve-mode starting puzzle code to the OS clipboard.
         */
        private void CopySolvePuzzleCodeToClipboard()
        {
            if (_solvePuzzleCodeInput == null || string.IsNullOrEmpty(_solvePuzzleCodeInput.text))
            {
                return;
            }

            GUIUtility.systemCopyBuffer = _solvePuzzleCodeInput.text;
        }

        /**
         * Cache the starting puzzle code from the currently active board.
         */
        private void CacheStartingPuzzleCodeFromCurrentBoard()
        {
            ResolveSceneReferences();
            if (_runner == null || _runner.CurrentBoard == null)
            {
                _startingPuzzleCode = string.Empty;
                return;
            }

            _startingPuzzleCode = PuzzleCodeGenerator.EncodeBoardToCode(_runner.CurrentBoard) ?? string.Empty;
            UpdateSolvePuzzleCodeDisplay();
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
         * Create vertical spacing in the main menu layout.
         *
         * @param parent Parent transform.
         * @param objectName Name for the spacer game object.
         * @param height Spacer height in pixels.
         */
        private static void CreateMenuSpacer(Transform parent, string objectName, float height)
        {
            var spacerGO = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer));
            spacerGO.transform.SetParent(parent, false);

            var layout = spacerGO.AddComponent<LayoutElement>();
            layout.preferredHeight = height;
            layout.minHeight = height;
            layout.flexibleHeight = 0f;
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
            layout.preferredHeight = 64f;

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
            label.fontSize = 26;
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
         * Ensure the startup-menu row containing code input and start button exists.
         */
        private void EnsureMenuPuzzleCodeRow(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            var rowTransform = parent.Find("MenuPuzzleCodeRow");
            GameObject rowGO;
            if (rowTransform == null)
            {
                rowGO = new GameObject("MenuPuzzleCodeRow", typeof(RectTransform), typeof(CanvasRenderer));
                rowGO.transform.SetParent(parent, false);
            }
            else
            {
                rowGO = rowTransform.gameObject;
            }

            rowGO.transform.SetSiblingIndex(2);

            var rowLayout = rowGO.GetComponent<LayoutElement>();
            if (rowLayout == null)
            {
                rowLayout = rowGO.AddComponent<LayoutElement>();
            }

            rowLayout.preferredHeight = 34f;
            rowLayout.minHeight = 34f;
            rowLayout.flexibleHeight = 0f;

            var hlg = rowGO.GetComponent<HorizontalLayoutGroup>();
            if (hlg == null)
            {
                hlg = rowGO.AddComponent<HorizontalLayoutGroup>();
            }

            hlg.padding = new RectOffset(0, 0, 0, 0);
            hlg.spacing = 8f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            EnsureMenuPuzzleCodeInput(rowGO.transform);
        }

        /**
         * Ensure the startup-menu puzzle code input exists.
         */
        private void EnsureMenuPuzzleCodeInput(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            var inputTransform = parent.Find("MenuPuzzleCodeInput");
            GameObject inputGO;
            if (inputTransform == null)
            {
                inputGO = new GameObject("MenuPuzzleCodeInput", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(InputField));
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

            layout.preferredWidth = 500f;
            layout.minWidth = 480f;
            layout.preferredHeight = 34f;
            layout.minHeight = 34f;
            layout.flexibleWidth = 1f;
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
            placeholder.text = "Enter puzzle code (optional)";
            placeholder.alignment = TextAnchor.MiddleLeft;
            placeholder.color = new Color(0.42f, 0.42f, 0.42f, 0.85f);
            placeholder.fontSize = 11;
            placeholder.font = font;

            inputField.textComponent = text;
            inputField.placeholder = placeholder;

            _startPuzzleCodeInput = inputField;
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
         * Ensure the solve-mode puzzle-code row exists at the top of the side panel.
         */
        private void EnsureSolvePuzzleCodeRow(Transform sidePanel)
        {
            if (sidePanel == null)
            {
                return;
            }

            var rowTransform = sidePanel.Find("SolvePuzzleCodeRow");
            if (rowTransform == null)
            {
                _solvePuzzleCodeRow = new GameObject("SolvePuzzleCodeRow", typeof(RectTransform), typeof(CanvasRenderer));
                _solvePuzzleCodeRow.transform.SetParent(sidePanel, false);
            }
            else
            {
                _solvePuzzleCodeRow = rowTransform.gameObject;
            }

            var rowRect = _solvePuzzleCodeRow.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.anchoredPosition = new Vector2(0f, -8f);
            rowRect.sizeDelta = new Vector2(-16f, 34f);
            rowRect.SetAsFirstSibling();

            var hlg = _solvePuzzleCodeRow.GetComponent<HorizontalLayoutGroup>();
            if (hlg == null)
            {
                hlg = _solvePuzzleCodeRow.AddComponent<HorizontalLayoutGroup>();
            }

            hlg.padding = new RectOffset(0, 0, 0, 0);
            hlg.spacing = 6f;
            hlg.childAlignment = TextAnchor.MiddleRight;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            EnsureSolvePuzzleCodeLabel(_solvePuzzleCodeRow.transform);
            EnsureSolvePuzzleCodeInput(_solvePuzzleCodeRow.transform);
            EnsureSolvePuzzleCodeCopyButton(_solvePuzzleCodeRow.transform);
            UpdateSolvePuzzleCodeDisplay();
        }

        /**
         * Ensure the solve-mode puzzle-code label exists.
         */
        private static void EnsureSolvePuzzleCodeLabel(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            var labelTransform = parent.Find("SolvePuzzleCodeLabel");
            GameObject labelGO;
            if (labelTransform == null)
            {
                labelGO = new GameObject("SolvePuzzleCodeLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
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

            layout.preferredWidth = 86f;
            layout.minWidth = 86f;
            layout.preferredHeight = 34f;
            layout.minHeight = 34f;

            var text = labelGO.GetComponent<Text>();
            text.text = "Puzzle Code";
            text.alignment = TextAnchor.MiddleRight;
            text.color = Color.white;
            text.fontSize = 13;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            text.font = font;
        }

        /**
         * Ensure the solve-mode puzzle-code input exists.
         */
        private void EnsureSolvePuzzleCodeInput(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            var inputTransform = parent.Find("SolvePuzzleCodeInput");
            GameObject inputGO;
            if (inputTransform == null)
            {
                inputGO = new GameObject("SolvePuzzleCodeInput", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(InputField));
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

            layout.preferredWidth = 260f;
            layout.minWidth = 240f;
            layout.preferredHeight = 34f;
            layout.minHeight = 34f;
            layout.flexibleWidth = 0f;

            var image = inputGO.GetComponent<Image>();
            image.color = new Color(0.97f, 0.97f, 0.97f, 1f);

            var inputField = inputGO.GetComponent<InputField>();
            inputField.readOnly = true;
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
            text.fontSize = 12;
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
            placeholder.text = string.Empty;
            placeholder.alignment = TextAnchor.MiddleLeft;
            placeholder.color = new Color(0.42f, 0.42f, 0.42f, 0.85f);
            placeholder.fontSize = 11;
            placeholder.font = font;

            inputField.textComponent = text;
            inputField.placeholder = placeholder;

            _solvePuzzleCodeInput = inputField;
        }

        /**
         * Ensure the copy button for solve-mode puzzle code exists.
         */
        private void EnsureSolvePuzzleCodeCopyButton(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            var buttonTransform = parent.Find("CopySolvePuzzleCodeButton");
            if (buttonTransform == null)
            {
                CreateMenuButton(parent, "CopySolvePuzzleCodeButton", "Copy", CopySolvePuzzleCodeToClipboard, true);
                buttonTransform = parent.Find("CopySolvePuzzleCodeButton");
            }

            if (buttonTransform == null)
            {
                return;
            }

            var button = buttonTransform.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(CopySolvePuzzleCodeToClipboard);
            }

            var layout = buttonTransform.GetComponent<LayoutElement>();
            if (layout == null)
            {
                layout = buttonTransform.gameObject.AddComponent<LayoutElement>();
            }

            layout.preferredWidth = 38f;
            layout.minWidth = 34f;
            layout.preferredHeight = 34f;
            layout.minHeight = 34f;
            layout.flexibleWidth = 0f;

            var text = buttonTransform.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.text = string.Empty;
            }

            EnsureCopyIconGraphic(buttonTransform);
        }

        /**
         * Refresh solve-mode puzzle-code text from the captured starting code.
         */
        private void UpdateSolvePuzzleCodeDisplay()
        {
            if (_solvePuzzleCodeInput == null)
            {
                return;
            }

            _solvePuzzleCodeInput.text = string.IsNullOrEmpty(_startingPuzzleCode)
                ? string.Empty
                : _startingPuzzleCode;
        }

        /**
         * Offset the side-panel rules area down when the solve puzzle-code row and timer are visible.
         *
         * @param showSolveRow True when solve-mode row should be visible.
         */
        private void UpdateRulesAreaTopOffset(bool showSolveRow)
        {
            var sidePanel = FindSidePanelIncludingInactive();
            if (sidePanel == null)
            {
                return;
            }

            var rulesArea = sidePanel.transform.Find("RulesArea") as RectTransform;
            if (rulesArea == null)
            {
                return;
            }

            if (!_hasCapturedRulesAreaTopOffset)
            {
                _defaultRulesAreaTopOffset = rulesArea.anchoredPosition.y;
                _hasCapturedRulesAreaTopOffset = true;
            }

            // Shift down only for the puzzle-code row; the timer now shares that same row.
            float extraOffset = showSolveRow ? 42f : 0f;
            float targetY = _defaultRulesAreaTopOffset - extraOffset;

            var anchored = rulesArea.anchoredPosition;
            if (!Mathf.Approximately(anchored.y, targetY))
            {
                anchored.y = targetY;
                rulesArea.anchoredPosition = anchored;
            }
        }

        /**
         * Ensure the PuzzleSolveTimer component exists on this GameObject.
         */
        private void EnsureSolveTimer()
        {
            if (_solveTimer != null)
            {
                return;
            }

            _solveTimer = GetComponent<PuzzleSolveTimer>();
            if (_solveTimer == null)
            {
                _solveTimer = gameObject.AddComponent<PuzzleSolveTimer>();
            }
        }

        /**
         * Ensure the timer label UI element exists in the side panel, positioned between
         * the puzzle-code row and the rules area (Toggle Rules).
         * Only visible in Solve Puzzle mode.
         *
         * @param sidePanelTransform The SidePanel transform.
         */
        private void EnsureTimerLabel(Transform sidePanelTransform)
        {
            if (sidePanelTransform == null)
            {
                return;
            }

            var existing = sidePanelTransform.Find(TimerLabelName);
            GameObject labelGO;
            if (existing == null)
            {
                labelGO = new GameObject(TimerLabelName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                labelGO.transform.SetParent(sidePanelTransform, false);
            }
            else
            {
                labelGO = existing.gameObject;
            }

            // Position at top-left on the same row as SolvePuzzleCodeRow.
            var rect = labelGO.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(8f, -8f);
            rect.sizeDelta = new Vector2(116f, 34f);

            var text = labelGO.GetComponent<Text>();
            text.text = "0:00:00";
            text.alignment = TextAnchor.MiddleLeft;
            text.color = Color.white;
            text.fontSize = 22;
            text.fontStyle = FontStyle.Bold;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            text.font = font;

            _timerLabelText = text;
        }

        /**
         * Refresh the timer label text from the current timer state.
         * Only runs in Solve Puzzle mode when the label is visible.
         */
        private void UpdateTimerDisplay()
        {
            if (_timerLabelText == null)
            {
                // Attempt to recover the reference if it was lost (e.g. after scene reload).
                var sidePanel = FindSidePanelIncludingInactive();
                if (sidePanel != null)
                {
                    var labelTransform = sidePanel.transform.Find(TimerLabelName);
                    if (labelTransform != null)
                    {
                        _timerLabelText = labelTransform.GetComponent<Text>();
                    }
                }
            }

            if (_timerLabelText == null || _solveTimer == null)
            {
                return;
            }

            _timerLabelText.text = _solveTimer.GetDisplayString();
        }

        /**
         * Show or hide the timer label based on the current interaction mode.
         *
         * @param isSolveMode True when the timer should be visible.
         */
        private void UpdateTimerLabelVisibility(bool isSolveMode)
        {
            var sidePanel = FindSidePanelIncludingInactive();
            if (sidePanel == null)
            {
                return;
            }

            var labelTransform = sidePanel.transform.Find(TimerLabelName);
            if (labelTransform == null)
            {
                return;
            }

            labelTransform.gameObject.SetActive(isSolveMode);
        }

        /**
         * Check if every cell on the board has a value, then validate the solution.
         * If the board is fully filled and valid, stop the timer permanently.
         */
        private void CheckAndStopTimerIfPuzzleComplete()
        {
            if (_runner == null || _runner.CurrentBoard == null)
            {
                return;
            }

            var board = _runner.CurrentBoard;

            // Compute a fingerprint to skip redundant checks when nothing changed.
            int fingerprint = ComputeBoardFingerprint(board);
            if (fingerprint == _lastTimerBoardFingerprint)
            {
                return;
            }

            _lastTimerBoardFingerprint = fingerprint;

            // Only validate once all cells have a value.
            int size = board.Size;
            for (int row = 0; row < size; row++)
            {
                for (int col = 0; col < size; col++)
                {
                    var cell = board.Cells[row, col];
                    if (cell == null || !cell.Value.HasValue)
                    {
                        // Board is not fully filled yet.
                        return;
                    }
                }
            }

            // All cells filled — validate the solution.
            if (board.IsValid())
            {
                _solveTimer.StopOnCompletion();
                Debug.Log($"MainMenuFlowController: Puzzle solved! Time: {_solveTimer.GetDisplayString()}");
            }
        }


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
            bool isSolveMode = _runner != null && !_runner.IsPuzzleCreationMode;

            if (_puzzleCodeRow != null)
            {
                _puzzleCodeRow.SetActive(isCreateMode);
            }

            if (_solvePuzzleCodeRow != null)
            {
                _solvePuzzleCodeRow.SetActive(isSolveMode);
            }

            UpdateRulesAreaTopOffset(isSolveMode);
            UpdateTimerLabelVisibility(isSolveMode);
            UpdateSolvePuzzleCodeDisplay();

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

            if (_playPanel != null)
            {
                var saveToListButton = _playPanel.transform.Find("PlayActionsRow/SaveToListButton");
                if (saveToListButton != null)
                {
                    saveToListButton.gameObject.SetActive(true);

                    var saveToListLabel = saveToListButton.GetComponentInChildren<Text>();
                    if (saveToListLabel != null)
                    {
                        saveToListLabel.text = isSolveMode ? "Save Initial Puzzle" : "Save";
                        saveToListLabel.fontSize = isSolveMode ? 12 : 14;
                    }

                    if (isSolveMode)
                    {
                        ConfigurePlayButtonLayout(_playPanel.transform.Find("PlayActionsRow"), "SaveToListButton", 96f, 34f, 84f);
                    }
                    else
                    {
                        ConfigurePlayButtonLayout(_playPanel.transform.Find("PlayActionsRow"), "SaveToListButton", 54f, 34f, 44f);
                    }
                }

                var saveSolvedStateButton = _playPanel.transform.Find("PlayActionsRow/SaveSolvedStateButton");
                if (saveSolvedStateButton != null)
                {
                    saveSolvedStateButton.gameObject.SetActive(isSolveMode);
                    if (isSolveMode)
                    {
                        var solvedStateLabel = saveSolvedStateButton.GetComponentInChildren<Text>();
                        if (solvedStateLabel != null)
                        {
                            solvedStateLabel.text = "Save Current Solved State";
                            solvedStateLabel.fontSize = 12;
                        }

                        ConfigurePlayButtonLayout(_playPanel.transform.Find("PlayActionsRow"), "SaveSolvedStateButton", 168f, 34f, 148f);
                    }
                }

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
            layout.preferredWidth = 0f;
            layout.minWidth = 0f;
            layout.flexibleWidth = 1f;
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
            hlg.spacing = 4f;
            hlg.childAlignment = TextAnchor.MiddleRight;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            row.SetSiblingIndex(2);

            return row;
        }

        /**
         * Ensure a Config button appears immediately to the left of ExitCornerButton.
         * Uses the same absolute-positioned anchoring so it sits flush at the bottom of the side panel.
         *
         * @param sidePanelTransform The side panel's transform to host the button.
         */
        private void EnsureConfigCornerButton(Transform sidePanelTransform)
        {
            if (sidePanelTransform == null)
            {
                return;
            }

            EnsurePlayActionButton(sidePanelTransform, "ConfigCornerButton", "Config", OpenConfigurationStub, false);
            ConfigurePlayButtonLayout(sidePanelTransform, "ConfigCornerButton", 80f, 36f, 80f);

            var configTransform = sidePanelTransform.Find("ConfigCornerButton");
            if (configTransform == null)
            {
                return;
            }

            // Position: same baseline as ExitCornerButton, immediately to its left.
            // ExitCornerButton: anchoredPosition=(-8,8), width=112 → left edge at -120 from right.
            // Config button right edge: -120 - 8 = -128 from right, with pivot=(1,0).
            var configRect = configTransform.GetComponent<RectTransform>();
            if (configRect != null)
            {
                configRect.anchorMin = new Vector2(1f, 0f);
                configRect.anchorMax = new Vector2(1f, 0f);
                configRect.pivot = new Vector2(1f, 0f);
                configRect.anchoredPosition = new Vector2(-128f, 8f);
                configRect.sizeDelta = new Vector2(80f, 36f);
            }

            EnsureButtonTextPadding(configTransform, 22f, 6f);
            EnsureLeadingIconLabel(configTransform, "ConfigIconLabel", "⚙", 16, 6f);

            configTransform.SetAsLastSibling();
            configTransform.gameObject.SetActive(true);
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
         * Ensure a left-aligned icon label exists on a button.
         *
         * @param buttonTransform Target button transform.
         * @param labelName Child label object name.
         * @param glyph Icon glyph text.
         * @param fontSize Icon font size.
         * @param leftInset Left inset in pixels from the button edge.
         */
        private static void EnsureLeadingIconLabel(Transform buttonTransform, string labelName, string glyph, int fontSize = 16, float leftInset = 14f)
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
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(leftInset, 0f);
            rect.sizeDelta = new Vector2(18f, 0f);

            var text = labelGO.GetComponent<Text>();
            text.text = glyph;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.1f, 0.1f, 0.1f, 1f);
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.raycastTarget = false;

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

        /**
         * Ensure an icon label is positioned immediately left of centered button text.
         *
         * @param buttonTransform Target button transform.
         * @param labelName Child label object name.
         * @param glyph Icon glyph text.
         * @param fontSize Icon font size.
         * @param gap Horizontal gap in pixels between icon and text.
         */
        private static void EnsureIconNearCenteredText(Transform buttonTransform, string labelName, string glyph, int fontSize = 24, float gap = 8f, float verticalOffset = 0f)
        {
            if (buttonTransform == null)
            {
                return;
            }

            var textTransform = buttonTransform.Find("Text");
            var text = textTransform != null ? textTransform.GetComponent<Text>() : null;

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
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            float textWidth = 46f;
            if (text != null)
            {
                textWidth = Mathf.Max(20f, text.preferredWidth);
            }

            float iconAdvance = Mathf.Max(16f, fontSize - 2f);
            rect.anchoredPosition = new Vector2(-(textWidth * 0.5f + gap), verticalOffset);
            rect.sizeDelta = new Vector2(iconAdvance, fontSize + 6f);

            var iconText = labelGO.GetComponent<Text>();
            iconText.text = glyph;
            iconText.alignment = TextAnchor.MiddleCenter;
            iconText.color = new Color(0.1f, 0.1f, 0.1f, 1f);
            iconText.fontSize = fontSize;
            iconText.fontStyle = FontStyle.Bold;
            iconText.raycastTarget = false;

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

            iconText.font = font;
        }

        /**
         * Add horizontal padding to a button's text child so leading icons do not overlap labels.
         *
         * @param buttonTransform Target button transform.
         * @param leftPadding Left text inset.
         * @param rightPadding Right text inset.
         */
        private static void EnsureButtonTextPadding(Transform buttonTransform, float leftPadding, float rightPadding)
        {
            if (buttonTransform == null)
            {
                return;
            }

            var textTransform = buttonTransform.Find("Text");
            if (textTransform == null)
            {
                return;
            }

            var rect = textTransform.GetComponent<RectTransform>();
            if (rect == null)
            {
                return;
            }

            rect.offsetMin = new Vector2(leftPadding, rect.offsetMin.y);
            rect.offsetMax = new Vector2(-rightPadding, rect.offsetMax.y);
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