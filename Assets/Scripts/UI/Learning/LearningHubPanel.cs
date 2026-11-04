using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Sudoku.Solver;
using Sudoku.UI.Menus;
using Sudoku.UI.Panels;

namespace Sudoku.UI.Learning
{
    [DisallowMultipleComponent]
    public sealed class LearningHubPanel : MonoBehaviour
    {
        private const float ExerciseRadialHoldThresholdSeconds = 0.35f;
        private const int LearningOverlayWindowId = 184731;
        private const string ProgressKeyPrefix = "Sudoku.LearningHub.Completed.";
        private readonly Color _panelColor = new Color(0.10f, 0.12f, 0.16f, 0.98f);
        private readonly Color _buttonColor = new Color(0.20f, 0.24f, 0.30f, 1f);
        private readonly Color _accentColor = new Color(0.88f, 0.73f, 0.28f, 1f);
        private readonly Color _linkColor = new Color(0.42f, 0.72f, 0.98f, 1f);

        private RectTransform _listContent;
        private RectTransform _detailContent;
        private Canvas _hubCanvas;
        private GameObject _overlay;
        private GameObject _radialInputBlocker;
        private Text _feedback;
        private Text _progress;
        private bool _showWiki;
        private LearningItem _selectedItem;
        private bool _selectedItemCompleted;
        private int _tutorialIndex;
        private readonly Dictionary<int, LearningBoardCell> _exerciseCellOverrides = new Dictionary<int, LearningBoardCell>();
        private int _selectedRow = -1;
        private int _selectedColumn = -1;
        private int _selectedDigit;
        private RadialMenuRuntime _exerciseRadialMenu;
        private BoardVisualizer _boardVisualizer;
        private RectTransform _exerciseHeldCellRect;
        private Vector2 _exerciseHeldPointerPosition;
        private float _exerciseHoldStartedAt;
        private int _exercisePointerId = int.MinValue;
        private bool _exerciseHoldActive;
        private bool _renderOverlayInBoardPass;
        private Rect _exerciseHeldGuiRect;
        private readonly Dictionary<Canvas, int> _previousCanvasSortingOrders = new Dictionary<Canvas, int>();
        private Vector2 _wikiListScroll;
        private Vector2 _wikiDetailScroll;

        public bool IsOverlayOpen => _overlay != null && _overlay.activeSelf;
        public bool IsWikiOverlayOpen => IsOverlayOpen && _showWiki;
        public bool IsRenderedByBoardVisualizer => IsOverlayOpen && _renderOverlayInBoardPass;
        public event System.Action OverlayClosed;

        private void Awake()
        {
            BuildInterface();
        }

        private void Update()
        {
            if (IsOverlayOpen && _boardVisualizer == null)
                _boardVisualizer = Object.FindAnyObjectByType<BoardVisualizer>();
            if (IsOverlayOpen)
            {
                bool renderInBoardPass = _boardVisualizer != null && _boardVisualizer.isActiveAndEnabled;
                if (_renderOverlayInBoardPass != renderInBoardPass)
                {
                    _renderOverlayInBoardPass = renderInBoardPass;
                    _hubCanvas.enabled = false;
                }
            }

            if (!_exerciseHoldActive || !IsOverlayOpen || _showWiki ||
                (_exerciseRadialMenu != null && _exerciseRadialMenu.IsOpen) ||
                Time.unscaledTime - _exerciseHoldStartedAt < ExerciseRadialHoldThresholdSeconds)
                return;

            if (!_exerciseHeldGuiRect.Contains(_exerciseHeldPointerPosition))
            {
                CancelExerciseCellHold();
                return;
            }

            var cell = FindExerciseCell(_selectedItem.Exercise, _selectedRow, _selectedColumn);
            OpenExerciseRadialMenu(_selectedRow, _selectedColumn, cell, _exerciseHeldGuiRect.center);
        }

        public void OpenTutorials()
        {
            CancelExerciseCellHold();
            _showWiki = false;
            PrepareOverlayRenderer();
            _tutorialIndex = 0;
            _exerciseCellOverrides.Clear();
            _selectedRow = -1;
            _selectedColumn = -1;
            _selectedDigit = 0;
            ShowOverlay();
            RefreshContent();
        }

        public void OpenWiki()
        {
            CancelExerciseCellHold();
            _showWiki = true;
            PrepareOverlayRenderer();
            ShowOverlay();
            RefreshContent();
        }

        private void BuildInterface()
        {
            var canvasObject = new GameObject("LearningHubCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            _hubCanvas = canvasObject.GetComponent<Canvas>();
            _hubCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _hubCanvas.overrideSorting = true;
            _hubCanvas.sortingOrder = short.MaxValue;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _overlay = new GameObject("LearningHubOverlay", typeof(RectTransform), typeof(Image));
            _overlay.transform.SetParent(canvasObject.transform, false);
            SetRect(_overlay.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var overlayImage = _overlay.GetComponent<Image>();
            overlayImage.color = new Color(0f, 0f, 0f, 0.72f);

            var card = new GameObject("LearningHubCard", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(_overlay.transform, false);
            SetRect(card.GetComponent<RectTransform>(), new Vector2(0.035f, 0.035f), new Vector2(0.965f, 0.965f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            card.GetComponent<Image>().color = _panelColor;

            _radialInputBlocker = new GameObject("RadialMenuInputBlocker", typeof(RectTransform), typeof(Image));
            _radialInputBlocker.transform.SetParent(_overlay.transform, false);
            SetRect(_radialInputBlocker.GetComponent<RectTransform>(), Vector2.zero, Vector2.one,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _radialInputBlocker.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
            _radialInputBlocker.SetActive(false);

            var title = CreateText(card.transform, "Tutorials and Wiki", 30, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
            SetRect(title.rectTransform, new Vector2(0.025f, 0.91f), new Vector2(0.70f, 0.99f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            CreateButton(card.transform, "Close", _buttonColor, CloseOverlay,
                new Vector2(0.91f, 0.925f), new Vector2(0.98f, 0.985f));

            CreateButton(card.transform, "Tutorials", _buttonColor, () => SetMode(false),
                new Vector2(0.025f, 0.855f), new Vector2(0.14f, 0.91f));
            CreateButton(card.transform, "Wiki", _buttonColor, () => SetMode(true),
                new Vector2(0.15f, 0.855f), new Vector2(0.25f, 0.91f));

            var listViewport = CreateScrollView(card.transform, "LearningList",
                new Vector2(0.025f, 0.045f), new Vector2(0.275f, 0.84f), _panelColor, out _listContent);
            listViewport.GetComponent<ScrollRect>().horizontal = false;

            var detailViewport = CreateScrollView(card.transform, "LearningDetail",
                new Vector2(0.295f, 0.045f), new Vector2(0.975f, 0.84f), _panelColor, out _detailContent);
            detailViewport.GetComponent<ScrollRect>().horizontal = false;
            _overlay.SetActive(false);
        }

        private void SetMode(bool showWiki)
        {
            CancelExerciseCellHold();
            _showWiki = showWiki;
            PrepareOverlayRenderer();
            RefreshContent();
        }

        private void PrepareOverlayRenderer()
        {
            if (_boardVisualizer == null || !_boardVisualizer.isActiveAndEnabled)
                _boardVisualizer = Object.FindAnyObjectByType<BoardVisualizer>();

            _renderOverlayInBoardPass = _boardVisualizer != null && _boardVisualizer.isActiveAndEnabled;
            _hubCanvas.enabled = false;
        }

        private void ShowOverlay()
        {
            if (!IsOverlayOpen)
            {
                ChangeLogPanelRuntime.HidePanel();
                RaiseCanvasToFront();
            }
            _overlay.SetActive(true);
        }

        private void CloseOverlay()
        {
            CancelExerciseCellHold();
            _overlay.SetActive(false);
            _hubCanvas.enabled = true;
            _renderOverlayInBoardPass = false;
            if (_exerciseRadialMenu != null)
                _exerciseRadialMenu.Close();
            _radialInputBlocker.SetActive(false);
            RestoreCanvasSortingOrders();
            OverlayClosed?.Invoke();
        }

        private void RaiseCanvasToFront()
        {
            _previousCanvasSortingOrders.Clear();
            var canvases = FindObjectsByType<Canvas>();
            foreach (var canvas in canvases)
            {
                if (canvas == _hubCanvas)
                    continue;

                _previousCanvasSortingOrders[canvas] = canvas.sortingOrder;
                if (canvas.sortingOrder >= short.MaxValue)
                    canvas.sortingOrder = short.MaxValue - 1;
            }

            _hubCanvas.sortingOrder = short.MaxValue;
            _hubCanvas.transform.SetAsLastSibling();
        }

        private void RestoreCanvasSortingOrders()
        {
            foreach (var entry in _previousCanvasSortingOrders)
            {
                if (entry.Key != null)
                    entry.Key.sortingOrder = entry.Value;
            }
            _previousCanvasSortingOrders.Clear();
        }

        private void RefreshContent()
        {
            var items = _showWiki ? LearningContent.GetWikiArticles() : LearningContent.GetTutorials();
            if (items.Count == 0)
            {
                Debug.LogError("LearningHubPanel: The selected learning section has no content.");
                return;
            }

            if (!_showWiki)
                _tutorialIndex = Mathf.Clamp(_tutorialIndex, 0, items.Count - 1);
            else if (_selectedItem == null || _selectedItem.IsTutorial)
                _selectedItem = items[0];

            if (!_showWiki)
                _selectedItem = items[_tutorialIndex];

            ClearChildren(_listContent);
            ClearChildren(_detailContent);

            string currentSection = null;
            string currentSubsection = null;
            for (int i = 0; i < items.Count; i++)
            {
                int itemIndex = i;
                LearningItem item = items[i];
                string section = item.Category;
                string subsection = _showWiki ? item.Subsection : string.Empty;

                if (section != currentSection)
                {
                    CreateListHeader(_listContent, section);
                    currentSection = section;
                    currentSubsection = null;
                }

                if (!string.IsNullOrEmpty(subsection) && subsection != currentSubsection)
                {
                    CreateListSubheader(_listContent, subsection);
                    currentSubsection = subsection;
                }

                CreateButton(_listContent, item.Title, _buttonColor, () =>
                {
                    if (_showWiki)
                        _selectedItem = item;
                    else
                    {
                        _tutorialIndex = itemIndex;
                        _exerciseCellOverrides.Clear();
                        _selectedRow = -1;
                        _selectedColumn = -1;
                        _selectedDigit = 0;
                    }
                    RefreshContent();
                }, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 42f));
            }

            RenderSelectedItem();
        }

        private void RenderSelectedItem()
        {
            if (_selectedItem == null)
                return;

            _selectedItemCompleted = _selectedItem.IsTutorial && PlayerPrefs.GetInt(ProgressKeyPrefix + _selectedItem.Id, 0) == 1;
            CreateText(_detailContent, _selectedItem.Title, 26, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
            CreateText(_detailContent, _selectedItem.IsTutorial
                ? _selectedItem.Category
                : _selectedItem.Category + "  /  " + _selectedItem.Subsection, 15, FontStyle.Bold, _accentColor);
            if (_showWiki)
                CreateInlineWikiParagraph(_detailContent, _selectedItem.Explanation, _selectedItem.Links, 17);
            else
                CreateParagraph(_detailContent, _selectedItem.Explanation, 17);

            _feedback = null;
            if (_selectedItem.Exercise != null)
            {
                CreateText(_detailContent, "Try it", 21, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
                CreateParagraph(_detailContent, _selectedItem.Exercise.Prompt, 17);
                bool basicsExercise = _selectedItem.Id == "sudoku-basics";
                CreateExerciseBoard(_selectedItem.Exercise);
                if (basicsExercise)
                {
                    string selectedCell = _selectedRow < 0
                        ? "Cell: choose the highlighted empty cell"
                        : $"Cell: R{_selectedRow + 1}C{_selectedColumn + 1}";
                    string selectedDigit = _selectedDigit == 0 ? "Digit: choose 1-9" : $"Digit: {_selectedDigit}";
                    CreateText(_detailContent, $"{selectedCell}   |   {selectedDigit}", 15, FontStyle.Bold, _accentColor);
                    CreateDigitButtons();
                    CreateActionButtons();
                }
                _feedback = CreateText(_detailContent, string.Empty, 17, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
                if (_selectedItemCompleted)
                    _feedback.text = _selectedItem.Exercise.SuccessMessage;
            }

            if (_showWiki)
                return;

            _progress = CreateText(_detailContent, string.Empty, 15, FontStyle.Normal, TextAnchor.MiddleLeft, Color.white);
            UpdateProgress();
            var navigation = CreateHorizontalRow(_detailContent, "LessonNavigation", 48f);
            CreateButton(navigation.transform, "Previous", _buttonColor, () => MoveTutorial(-1), new Vector2(0f, 0f), new Vector2(0.48f, 1f));
            CreateButton(navigation.transform, "Next", _accentColor, () => MoveTutorial(1), new Vector2(0.52f, 0f), new Vector2(1f, 1f));
        }

        private void SelectExerciseCell(int row, int column)
        {
            _selectedRow = row;
            _selectedColumn = column;
            RefreshDetails();
        }

        private void SelectExerciseDigit(int digit)
        {
            _selectedDigit = digit;
            RefreshDetails();
        }

        private void ApplyExerciseAction(LearningAction action)
        {
            var exercise = _selectedItem?.Exercise;
            if (exercise == null)
                return;

            if (!exercise.IsCorrectAction(_selectedRow, _selectedColumn, _selectedDigit, action))
            {
                RefreshDetails("Not quite. Check the highlighted cell and the row, column, box, or candidate pattern.");
                return;
            }

            var current = FindExerciseCell(exercise, _selectedRow, _selectedColumn);
            var candidates = current != null ? current.Candidates.ToList() : new List<int>();
            int? value = current?.Value;
            switch (action)
            {
                case LearningAction.SetValue:
                    value = _selectedDigit;
                    candidates.Clear();
                    break;
                case LearningAction.AddCandidate:
                    if (!candidates.Contains(_selectedDigit))
                        candidates.Add(_selectedDigit);
                    candidates.Sort();
                    break;
                case LearningAction.RemoveCandidate:
                    candidates.Remove(_selectedDigit);
                    break;
            }

            _exerciseCellOverrides[_selectedRow * 9 + _selectedColumn] =
                new LearningBoardCell(_selectedRow, _selectedColumn, value, candidates.ToArray());
            _selectedItemCompleted = true;
            PlayerPrefs.SetInt(ProgressKeyPrefix + _selectedItem.Id, 1);
            PlayerPrefs.Save();
            RefreshDetails(exercise.SuccessMessage, true);
        }

        private void MoveTutorial(int offset)
        {
            CancelExerciseCellHold();
            _tutorialIndex = Mathf.Clamp(_tutorialIndex + offset, 0, LearningContent.GetTutorials().Count - 1);
            _exerciseCellOverrides.Clear();
            _selectedRow = -1;
            _selectedColumn = -1;
            _selectedDigit = 0;
            RefreshContent();
        }

        private void UpdateProgress()
        {
            if (_progress == null)
                return;

            int completed = 0;
            foreach (var tutorial in LearningContent.GetTutorials())
                if (PlayerPrefs.GetInt(ProgressKeyPrefix + tutorial.Id, 0) == 1)
                    completed++;
            _progress.text = $"Lesson {_tutorialIndex + 1} of {LearningContent.GetTutorials().Count}   |   {completed} completed";
        }

        private void RefreshDetails(string feedback = null, bool isCorrect = false)
        {
            ClearChildren(_detailContent);
            RenderSelectedItem();
            if (_feedback != null && feedback != null)
            {
                _feedback.text = feedback;
                _feedback.color = isCorrect
                    ? new Color(0.62f, 0.9f, 0.62f, 1f)
                    : new Color(1f, 0.62f, 0.58f, 1f);
            }

        }

        private void CreateExerciseBoard(LearningExercise exercise)
        {
            var boardRow = CreateHorizontalRow(_detailContent, "ExampleBoardCenteringRow", 296f);
            var boardRowLayout = boardRow.GetComponent<HorizontalLayoutGroup>();
            boardRowLayout.childAlignment = TextAnchor.MiddleCenter;
            boardRowLayout.childForceExpandWidth = false;

            var board = new GameObject("ExampleSudokuBoard", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            board.transform.SetParent(boardRow.transform, false);
            board.GetComponent<Image>().color = new Color(0.03f, 0.04f, 0.06f, 1f);
            var boardLayout = board.GetComponent<LayoutElement>();
            boardLayout.preferredWidth = 296f;
            boardLayout.preferredHeight = 296f;
            boardLayout.flexibleWidth = 0f;
            boardLayout.flexibleHeight = 0f;

            var gridObject = new GameObject("Cells", typeof(RectTransform), typeof(GridLayoutGroup));
            gridObject.transform.SetParent(board.transform, false);
            var gridRect = gridObject.GetComponent<RectTransform>();
            SetRect(gridRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(296f, 296f));
            var grid = gridObject.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(32f, 32f);
            grid.spacing = new Vector2(1f, 1f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 9;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.MiddleCenter;

            for (int row = 0; row < 9; row++)
            {
                for (int column = 0; column < 9; column++)
                    CreateExampleCell(gridObject.transform, exercise, row, column);
            }

            CreateBoardBoxLine(board.transform, true, 1f / 3f);
            CreateBoardBoxLine(board.transform, true, 2f / 3f);
            CreateBoardBoxLine(board.transform, false, 1f / 3f);
            CreateBoardBoxLine(board.transform, false, 2f / 3f);
        }

        private void CreateExampleCell(Transform parent, LearningExercise exercise, int row, int column)
        {
            bool basicsExercise = _selectedItem != null && _selectedItem.Id == "sudoku-basics";
            var cellObject = new GameObject($"R{row + 1}C{column + 1}", typeof(RectTransform), typeof(Image));
            cellObject.transform.SetParent(parent, false);
            var image = cellObject.GetComponent<Image>();
            bool isTarget = row == exercise.TargetRow && column == exercise.TargetColumn;
            bool isSelected = row == _selectedRow && column == _selectedColumn;
            image.color = isSelected
                ? new Color(0.93f, 0.80f, 0.37f, 1f)
                : isTarget
                    ? new Color(0.84f, 0.76f, 0.46f, 1f)
                    : new Color(0.91f, 0.92f, 0.93f, 1f);
            var cell = FindExerciseCell(exercise, row, column);
            if (basicsExercise)
            {
                var button = cellObject.AddComponent<Button>();
                button.targetGraphic = image;
                button.onClick.AddListener(() => SelectExerciseCell(row, column));
            }
            else
            {
                var trigger = cellObject.AddComponent<EventTrigger>();
                AddExercisePointerEvent(trigger, EventTriggerType.PointerDown,
                    eventData => BeginExerciseCellHold(row, column, cellObject.GetComponent<RectTransform>(), eventData));
                AddExercisePointerEvent(trigger, EventTriggerType.Drag, HandleExerciseCellDrag);
                AddExercisePointerEvent(trigger, EventTriggerType.PointerUp, HandleExerciseCellRelease);
            }

            if (cell?.Value != null)
            {
                var valueLabel = CreateText(cellObject.transform, cell.Value.Value.ToString(), 18, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.08f, 0.10f, 0.14f, 1f));
                SetRect(valueLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                return;
            }

            if (cell != null && cell.Candidates.Count > 0)
                CreateExampleCandidates(cellObject.transform, cell.Candidates);
        }

        private void CreateExampleCandidates(Transform parent, IReadOnlyList<int> candidates)
        {
            var candidateGridObject = new GameObject("Candidates", typeof(RectTransform), typeof(GridLayoutGroup));
            candidateGridObject.transform.SetParent(parent, false);
            SetRect(candidateGridObject.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var candidateGrid = candidateGridObject.GetComponent<GridLayoutGroup>();
            candidateGrid.cellSize = new Vector2(10f, 10f);
            candidateGrid.spacing = Vector2.zero;
            candidateGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            candidateGrid.constraintCount = 3;
            candidateGrid.childAlignment = TextAnchor.MiddleCenter;

            for (int digit = 1; digit <= 9; digit++)
            {
                var candidateLabel = CreateText(candidateGridObject.transform,
                    candidates.Contains(digit) ? digit.ToString() : string.Empty,
                    9, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.19f, 0.23f, 0.29f, 1f));
                candidateLabel.raycastTarget = false;
            }
        }

        private LearningBoardCell FindExerciseCell(LearningExercise exercise, int row, int column)
        {
            int key = row * 9 + column;
            return _exerciseCellOverrides.TryGetValue(key, out var overriddenCell)
                ? overriddenCell
                : exercise.FindCell(row, column);
        }

        private void CreateBoardBoxLine(Transform parent, bool vertical, float position)
        {
            var line = new GameObject("BoxBoundary", typeof(RectTransform), typeof(Image));
            line.transform.SetParent(parent, false);
            var rect = line.GetComponent<RectTransform>();
            const float thickness = 3f;
            if (vertical)
                SetRect(rect, new Vector2(position, 0f), new Vector2(position, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(thickness, 0f));
            else
                SetRect(rect, new Vector2(0f, position), new Vector2(1f, position), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, thickness));
            var image = line.GetComponent<Image>();
            image.color = new Color(0.04f, 0.05f, 0.07f, 1f);
            image.raycastTarget = false;
        }

        private void CreateDigitButtons()
        {
            var digitRow = CreateHorizontalRow(_detailContent, "ExampleDigitPicker", 34f);
            for (int digit = 1; digit <= 9; digit++)
            {
                int selectedDigit = digit;
                var button = CreateButton(digitRow.transform, digit.ToString(),
                    digit == _selectedDigit ? _accentColor : _buttonColor,
                    () => SelectExerciseDigit(selectedDigit));
                var layout = button.GetComponent<LayoutElement>();
                layout.preferredWidth = 30f;
                layout.flexibleWidth = 1f;
                var text = button.GetComponentInChildren<Text>();
                text.color = digit == _selectedDigit ? Color.black : Color.white;
                text.fontSize = 14;
            }
        }

        private void CreateActionButtons()
        {
            var actionRow = CreateHorizontalRow(_detailContent, "ExampleActions", 40f);
            CreateButton(actionRow.transform, "Set Value", _buttonColor, () => ApplyExerciseAction(LearningAction.SetValue));
        }

        private void OpenExerciseRadialMenu(int row, int column, LearningBoardCell cell, RectTransform cellRect)
        {
            Vector2 screenPosition = RectTransformUtility.WorldToScreenPoint(null, cellRect.position);
            OpenExerciseRadialMenu(row, column, cell,
                new Vector2(screenPosition.x, Screen.height - screenPosition.y));
        }

        private void OpenExerciseRadialMenu(int row, int column, LearningBoardCell cell, Vector2 guiPosition)
        {
            if (_exerciseRadialMenu == null)
            {
                var radialHost = new GameObject("LearningExerciseRadialMenu", typeof(RectTransform), typeof(RadialMenuRuntime));
                radialHost.transform.SetParent(_hubCanvas.transform, false);
                _exerciseRadialMenu = radialHost.GetComponent<RadialMenuRuntime>();
                _exerciseRadialMenu.SelectionCommitted += OnExerciseRadialSelectionCommitted;
            }

            _selectedRow = row;
            _selectedColumn = column;
            _exerciseRadialMenu.LinkSelectionMode = false;
            _exerciseRadialMenu.ValueOnlyMode = false;
            _exerciseRadialMenu.TutorialActionFilter = _selectedItem.Exercise.ExpectedAction switch
            {
                LearningAction.AddCandidate => RadialDigitActionType.AddCandidate,
                LearningAction.RemoveCandidate => RadialDigitActionType.RemoveCandidate,
                _ => RadialDigitActionType.DefaultDigit
            };
            _exerciseRadialMenu.SetCellContext(cell?.Value, cell?.Candidates, false);

            _exerciseRadialMenu.Open(guiPosition, "No Action");
            _radialInputBlocker.transform.SetAsLastSibling();
            _radialInputBlocker.SetActive(true);
        }

        private static void AddExercisePointerEvent(
            EventTrigger trigger, EventTriggerType eventType, System.Action<PointerEventData> callback)
        {
            var entry = new EventTrigger.Entry { eventID = eventType };
            entry.callback.AddListener(eventData => callback((PointerEventData)eventData));
            trigger.triggers.Add(entry);
        }

        private void BeginExerciseCellHold(int row, int column, RectTransform cellRect, PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || _selectedItem?.Exercise == null)
                return;

            CancelExerciseCellHold();
            _selectedRow = row;
            _selectedColumn = column;
            _exerciseHeldCellRect = cellRect;
            _exerciseHeldPointerPosition = eventData.position;
            _exerciseHoldStartedAt = Time.unscaledTime;
            _exercisePointerId = eventData.pointerId;
            _exerciseHoldActive = true;
        }

        private void HandleExerciseCellDrag(PointerEventData eventData)
        {
            if (!_exerciseHoldActive || eventData.pointerId != _exercisePointerId)
                return;

            _exerciseHeldPointerPosition = eventData.position;
            if (_exerciseRadialMenu != null && _exerciseRadialMenu.IsOpen)
            {
                _exerciseRadialMenu.UpdatePointer(ToGuiPosition(eventData.position));
                eventData.Use();
            }
            else if (_exerciseHeldCellRect == null ||
                     !RectTransformUtility.RectangleContainsScreenPoint(_exerciseHeldCellRect, eventData.position, null))
            {
                CancelExerciseCellHold();
            }
        }

        private void HandleExerciseCellRelease(PointerEventData eventData)
        {
            if (!_exerciseHoldActive || eventData.pointerId != _exercisePointerId)
                return;

            bool menuWasOpen = _exerciseRadialMenu != null && _exerciseRadialMenu.IsOpen;
            _exerciseHoldActive = false;
            _exercisePointerId = int.MinValue;
            _exerciseHeldCellRect = null;
            _exerciseHeldGuiRect = default;
            if (menuWasOpen)
            {
                _exerciseRadialMenu.ReleasePointer(ToGuiPosition(eventData.position));
                _radialInputBlocker.SetActive(false);
                eventData.Use();
            }
            else
            {
                CancelExerciseCellHold();
            }
        }

        private void CancelExerciseCellHold()
        {
            _exerciseHoldActive = false;
            _exercisePointerId = int.MinValue;
            _exerciseHeldCellRect = null;
            if (_exerciseRadialMenu != null && _exerciseRadialMenu.IsOpen)
                _exerciseRadialMenu.Close();
            if (_radialInputBlocker != null)
                _radialInputBlocker.SetActive(false);
        }

        private static Vector2 ToGuiPosition(Vector2 screenPosition)
        {
            return new Vector2(screenPosition.x, Screen.height - screenPosition.y);
        }

        private void OnExerciseRadialSelectionCommitted(RadialMenuSelection selection)
        {
            if (!selection.Digit.HasValue)
                return;

            _selectedDigit = selection.Digit.Value;
            LearningAction action;
            switch (selection.DigitActionType)
            {
                case RadialDigitActionType.AddCandidate:
                    action = LearningAction.AddCandidate;
                    break;
                case RadialDigitActionType.RemoveCandidate:
                    action = LearningAction.RemoveCandidate;
                    break;
                case RadialDigitActionType.DefaultDigit:
                    action = LearningAction.SetValue;
                    break;
                default:
                    return;
            }

            ApplyExerciseAction(action);
        }

        public void HandleExerciseRadialGUI()
        {
            if (!IsOverlayOpen || _exerciseRadialMenu == null || !_exerciseRadialMenu.IsOpen)
                return;

            var currentEvent = Event.current;
            if (currentEvent == null)
                return;

            _exerciseRadialMenu.RenderOverlayOnGUI();
            if (currentEvent.type == EventType.KeyDown && currentEvent.keyCode == KeyCode.Escape)
            {
                CancelExerciseCellHold();
                currentEvent.Use();
                return;
            }
            if (currentEvent.type == EventType.MouseMove || currentEvent.type == EventType.MouseDrag)
            {
                _exerciseRadialMenu.UpdatePointer(currentEvent.mousePosition);
                currentEvent.Use();
            }
        }

        private void OnGUI()
        {
            if (IsRenderedByBoardVisualizer)
                return;

            if (IsOverlayOpen)
            {
                if (_boardVisualizer == null)
                    _boardVisualizer = Object.FindAnyObjectByType<BoardVisualizer>();

                if (_boardVisualizer == null || !_boardVisualizer.isActiveAndEnabled)
                    RenderLearningOverlayOnGUI();
                return;
            }

            HandleExerciseRadialGUI();
        }

        public void RenderLearningOverlayOnGUI()
        {
            if (!IsOverlayOpen || Event.current == null)
                return;

            HandleImmediateExerciseRadialInput();
            int previousDepth = GUI.depth;
            Color previousColor = GUI.color;
            bool previousEnabled = GUI.enabled;
            GUI.depth = -10000;

            try
            {
                float screenWidth = Screen.width;
                float screenHeight = Screen.height;
                var screenRect = new Rect(0f, 0f, screenWidth, screenHeight);

                GUI.color = new Color(0f, 0f, 0f, 0.72f);
                GUI.DrawTexture(screenRect, Texture2D.whiteTexture, ScaleMode.StretchToFill);
                GUI.color = previousColor;

                GUI.Window(LearningOverlayWindowId, screenRect, DrawLearningOverlayWindow, GUIContent.none, GUIStyle.none);
                GUI.BringWindowToFront(LearningOverlayWindowId);

                var currentEvent = Event.current;
                if (currentEvent != null &&
                    (currentEvent.type == EventType.MouseDown || currentEvent.type == EventType.MouseUp ||
                     currentEvent.type == EventType.MouseDrag || currentEvent.type == EventType.MouseMove ||
                     currentEvent.type == EventType.ScrollWheel))
                {
                    currentEvent.Use();
                }
            }
            finally
            {
                GUI.color = previousColor;
                GUI.enabled = previousEnabled;
                GUI.depth = previousDepth;
            }
        }

        private void HandleImmediateExerciseRadialInput()
        {
            var currentEvent = Event.current;
            if (currentEvent == null)
                return;

            if (_exerciseRadialMenu != null && _exerciseRadialMenu.IsOpen)
            {
                if (currentEvent.type == EventType.KeyDown && currentEvent.keyCode == KeyCode.Escape)
                {
                    CancelExerciseCellHold();
                    currentEvent.Use();
                }
                else if (currentEvent.type == EventType.MouseMove || currentEvent.type == EventType.MouseDrag)
                {
                    _exerciseRadialMenu.UpdatePointer(currentEvent.mousePosition);
                    currentEvent.Use();
                }
                else if (currentEvent.type == EventType.MouseUp && currentEvent.button == 0)
                {
                    _exerciseHoldActive = false;
                    _exerciseRadialMenu.ReleasePointer(currentEvent.mousePosition);
                    _radialInputBlocker.SetActive(false);
                    currentEvent.Use();
                }
            }
            else if (_exerciseHoldActive &&
                     (currentEvent.type == EventType.MouseMove || currentEvent.type == EventType.MouseDrag))
            {
                _exerciseHeldPointerPosition = currentEvent.mousePosition;
                if (!_exerciseHeldGuiRect.Contains(currentEvent.mousePosition))
                    CancelExerciseCellHold();
            }
            else if (_exerciseHoldActive && currentEvent.type == EventType.MouseUp && currentEvent.button == 0)
            {
                CancelExerciseCellHold();
                currentEvent.Use();
            }
        }

        private void DrawLearningOverlayWindow(int windowId)
        {
            float screenWidth = Screen.width;
            float screenHeight = Screen.height;
            var panelRect = new Rect(screenWidth * 0.035f, screenHeight * 0.035f,
                screenWidth * 0.93f, screenHeight * 0.93f);

            Color previousColor = GUI.color;
            GUI.color = _panelColor;
            GUI.DrawTexture(panelRect, Texture2D.whiteTexture, ScaleMode.StretchToFill);
            GUI.color = previousColor;

            var headingStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            var sectionStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                normal = { textColor = _accentColor }
            };
            var bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                wordWrap = false,
                padding = new RectOffset(0, 0, 0, 0),
                normal = { textColor = Color.white }
            };
            var linkStyle = new GUIStyle(bodyStyle)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = _linkColor },
                hover = { textColor = Color.white }
            };

            float padding = Mathf.Max(18f, screenWidth * 0.02f);
            var titleRect = new Rect(panelRect.x + padding, panelRect.y + 12f,
                panelRect.width * 0.68f, 42f);
            GUI.Label(titleRect, "Tutorials and Wiki", headingStyle);

            var closeRect = new Rect(panelRect.xMax - padding - 92f, panelRect.y + 12f, 92f, 38f);
            if (GUI.Button(closeRect, "Close"))
                CloseOverlay();

            var tutorialsRect = new Rect(panelRect.x + padding, panelRect.y + 58f, 112f, 38f);
            if (GUI.Button(tutorialsRect, "Tutorials"))
                SetMode(false);
            var wikiRect = new Rect(tutorialsRect.xMax + 8f, tutorialsRect.y, 82f, tutorialsRect.height);
            if (GUI.Button(wikiRect, "Wiki"))
                SetMode(true);

            float contentTop = tutorialsRect.yMax + 16f;
            float contentBottom = panelRect.yMax - padding;
            float contentHeight = contentBottom - contentTop;
            float listWidth = panelRect.width * 0.245f;
            var listRect = new Rect(panelRect.x + padding, contentTop, listWidth, contentHeight);
            var detailRect = new Rect(listRect.xMax + padding, contentTop,
                panelRect.xMax - padding - (listRect.xMax + padding), contentHeight);

            var items = _showWiki ? LearningContent.GetWikiArticles() : LearningContent.GetTutorials();
            DrawLearningItemList(items, listRect, sectionStyle);
            if (_showWiki)
                DrawWikiArticleDetail(detailRect, headingStyle, sectionStyle, bodyStyle, linkStyle);
            else
                DrawTutorialDetail(detailRect, headingStyle, sectionStyle, bodyStyle);

            if (_exerciseRadialMenu != null && _exerciseRadialMenu.IsOpen)
                _exerciseRadialMenu.RenderOverlayOnGUI();
        }

        private void DrawLearningItemList(
            IReadOnlyList<LearningItem> items, Rect viewport, GUIStyle sectionStyle)
        {
            float contentHeight = Mathf.Max(viewport.height, items.Count * 52f + 48f);
            var contentRect = new Rect(0f, 0f, viewport.width - 18f, contentHeight);
            _wikiListScroll = GUI.BeginScrollView(viewport, _wikiListScroll, contentRect);

            float y = 0f;
            string currentSection = null;
            string currentSubsection = null;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                string subsection = _showWiki ? item.Subsection : string.Empty;
                if (item.Category != currentSection)
                {
                    GUI.Label(new Rect(4f, y, contentRect.width - 8f, 24f), item.Category, sectionStyle);
                    y += 26f;
                    currentSection = item.Category;
                    currentSubsection = null;
                }

                if (!string.IsNullOrEmpty(subsection) && subsection != currentSubsection)
                {
                    GUI.Label(new Rect(8f, y, contentRect.width - 12f, 21f),
                        subsection, GUI.skin.label);
                    y += 22f;
                    currentSubsection = subsection;
                }

                var itemRect = new Rect(8f, y, contentRect.width - 12f, 32f);
                if (GUI.Button(itemRect, item.Title) && _selectedItem != item)
                {
                    _selectedItem = item;
                    if (!_showWiki)
                    {
                        _tutorialIndex = i;
                        _exerciseCellOverrides.Clear();
                        _selectedRow = -1;
                        _selectedColumn = -1;
                        _selectedDigit = 0;
                    }
                    _wikiDetailScroll = Vector2.zero;
                }
                y += 36f;
            }

            GUI.EndScrollView();
        }

        private void DrawTutorialDetail(
            Rect viewport, GUIStyle headingStyle, GUIStyle sectionStyle, GUIStyle bodyStyle)
        {
            var tutorials = LearningContent.GetTutorials();
            _tutorialIndex = Mathf.Clamp(_tutorialIndex, 0, tutorials.Count - 1);
            _selectedItem = tutorials[_tutorialIndex];

            float contentWidth = viewport.width - 20f;
            var wrappedStyle = new GUIStyle(bodyStyle) { wordWrap = true };
            float y = 0f;
            float explanationHeight = wrappedStyle.CalcHeight(new GUIContent(_selectedItem.Explanation), contentWidth);
            float promptHeight = _selectedItem.Exercise == null
                ? 0f
                : wrappedStyle.CalcHeight(new GUIContent(_selectedItem.Exercise.Prompt), contentWidth);
            float exerciseBoardSize = _selectedItem.Exercise == null ? 0f : Mathf.Min(296f, contentWidth, 330f);
            float exerciseBoardHeight = exerciseBoardSize + 14f;
            float basicsControlsHeight = _selectedItem.Id == "sudoku-basics" ? 48f : 0f;
            float feedbackHeight = _selectedItem.Exercise == null ? 0f : 32f;
            float footerHeight = _selectedItem.IsTutorial ? 72f : 0f;
            float contentHeight = Mathf.Max(viewport.height,
                40f + 28f + explanationHeight + 20f + promptHeight + exerciseBoardHeight +
                basicsControlsHeight + feedbackHeight + footerHeight + 36f);
            var contentRect = new Rect(0f, 0f, contentWidth, contentHeight);
            _wikiDetailScroll = GUI.BeginScrollView(viewport, _wikiDetailScroll, contentRect);

            GUI.Label(new Rect(0f, y, contentWidth, 36f), _selectedItem.Title, headingStyle);
            y += 40f;
            GUI.Label(new Rect(0f, y, contentWidth, 24f), _selectedItem.Category, sectionStyle);
            y += 28f;
            GUI.Label(new Rect(0f, y, contentWidth, explanationHeight), _selectedItem.Explanation, wrappedStyle);
            y += explanationHeight + 18f;

            if (_selectedItem.Exercise != null)
            {
                GUI.Label(new Rect(0f, y, contentWidth, 28f), "Try it", sectionStyle);
                y += 28f;
                GUI.Label(new Rect(0f, y, contentWidth, promptHeight), _selectedItem.Exercise.Prompt, wrappedStyle);
                y += promptHeight + 6f;

                float boardSize = exerciseBoardSize;
                var boardRect = new Rect(0f, y, boardSize, boardSize);
                DrawExerciseBoardOnGUI(boardRect, _selectedItem.Exercise, _selectedItem.Id == "sudoku-basics");
                y += boardSize + 8f;

                if (_selectedItem.Id == "sudoku-basics")
                {
                    DrawBasicsExerciseControls(new Rect(0f, y, contentWidth, 44f));
                    y += 48f;
                }

                bool completed = PlayerPrefs.GetInt(ProgressKeyPrefix + _selectedItem.Id, 0) == 1;
                string feedback = completed ? _selectedItem.Exercise.SuccessMessage : string.Empty;
                GUI.Label(new Rect(0f, y, contentWidth, 32f), feedback, wrappedStyle);
                y += 34f;
            }

            if (_selectedItem.IsTutorial)
            {
                int completedCount = LearningContent.GetTutorials()
                    .Count(item => PlayerPrefs.GetInt(ProgressKeyPrefix + item.Id, 0) == 1);
                GUI.Label(new Rect(0f, y, contentWidth, 26f),
                    $"Lesson {_tutorialIndex + 1} of {tutorials.Count}   |   {completedCount} completed", sectionStyle);
                y += 30f;
                if (GUI.Button(new Rect(0f, y, 120f, 36f), "Previous"))
                    MoveTutorial(-1);
                if (GUI.Button(new Rect(128f, y, 120f, 36f), "Next"))
                    MoveTutorial(1);
            }

            GUI.EndScrollView();
        }

        private void DrawExerciseBoardOnGUI(Rect boardRect, LearningExercise exercise, bool basicsExercise)
        {
            Color previousColor = GUI.color;
            GUI.color = new Color(0.03f, 0.04f, 0.06f, 1f);
            GUI.DrawTexture(boardRect, Texture2D.whiteTexture, ScaleMode.StretchToFill);
            GUI.color = previousColor;

            float cellSize = boardRect.width / 9f;
            for (int row = 0; row < 9; row++)
            {
                for (int column = 0; column < 9; column++)
                {
                    var cellRect = new Rect(
                        boardRect.x + column * cellSize,
                        boardRect.y + row * cellSize,
                        cellSize - 1f,
                        cellSize - 1f);
                    var cell = FindExerciseCell(exercise, row, column);
                    bool target = row == exercise.TargetRow && column == exercise.TargetColumn;
                    bool selected = row == _selectedRow && column == _selectedColumn;
                    Color cellColor = selected
                        ? new Color(0.93f, 0.80f, 0.37f, 1f)
                        : target
                            ? new Color(0.84f, 0.76f, 0.46f, 1f)
                            : new Color(0.91f, 0.92f, 0.93f, 1f);
                    GUI.color = cellColor;
                    GUI.DrawTexture(cellRect, Texture2D.whiteTexture, ScaleMode.StretchToFill);
                    GUI.color = previousColor;

                    if (cell?.Value != null)
                    {
                        var valueColor = new Color(0.08f, 0.10f, 0.14f, 1f);
                        var valueStyle = new GUIStyle(GUI.skin.label)
                        {
                            alignment = TextAnchor.MiddleCenter,
                            fontSize = Mathf.Max(12, Mathf.RoundToInt(cellSize * 0.58f)),
                            fontStyle = FontStyle.Bold,
                            normal = { textColor = valueColor },
                            hover = { textColor = valueColor },
                            active = { textColor = valueColor },
                            focused = { textColor = valueColor }
                        };
                        GUI.Label(cellRect, cell.Value.Value.ToString(), valueStyle);
                    }
                    else if (cell != null && cell.Candidates.Count > 0)
                    {
                        DrawExerciseCandidates(cellRect, cell.Candidates);
                    }

                    if (!GUI.enabled || Event.current == null || cell?.Value != null)
                        continue;

                    if (Event.current.type == EventType.MouseDown && Event.current.button == 0 &&
                        cellRect.Contains(Event.current.mousePosition))
                    {
                        if (basicsExercise)
                        {
                            SelectExerciseCell(row, column);
                            Event.current.Use();
                        }
                        else
                        {
                            _selectedRow = row;
                            _selectedColumn = column;
                            Vector2 guiTopLeft = GUIUtility.GUIToScreenPoint(cellRect.min);
                            Vector2 guiBottomRight = GUIUtility.GUIToScreenPoint(cellRect.max);
                            _exerciseHeldGuiRect = Rect.MinMaxRect(
                                Mathf.Min(guiTopLeft.x, guiBottomRight.x),
                                Mathf.Min(guiTopLeft.y, guiBottomRight.y),
                                Mathf.Max(guiTopLeft.x, guiBottomRight.x),
                                Mathf.Max(guiTopLeft.y, guiBottomRight.y));
                            _exerciseHeldPointerPosition = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
                            _exerciseHoldStartedAt = Time.unscaledTime;
                            _exerciseHoldActive = true;
                            Event.current.Use();
                        }
                    }
                }
            }

            for (int i = 0; i <= 9; i++)
            {
                float thickness = i % 3 == 0 ? 3f : 1f;
                GUI.color = new Color(0.04f, 0.05f, 0.07f, 1f);
                GUI.DrawTexture(new Rect(boardRect.x + i * cellSize - thickness * 0.5f,
                    boardRect.y, thickness, boardRect.height), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(boardRect.x,
                    boardRect.y + i * cellSize - thickness * 0.5f, boardRect.width, thickness), Texture2D.whiteTexture);
            }
            GUI.color = previousColor;
        }

        private static void DrawExerciseCandidates(Rect cellRect, IReadOnlyList<int> candidates)
        {
            var candidateStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.Max(6, Mathf.RoundToInt(cellRect.width * 0.28f)),
                padding = new RectOffset(0, 0, 0, 0),
                normal = { textColor = new Color(0.19f, 0.23f, 0.29f, 1f) },
                hover = { textColor = new Color(0.19f, 0.23f, 0.29f, 1f) },
                active = { textColor = new Color(0.19f, 0.23f, 0.29f, 1f) },
                focused = { textColor = new Color(0.19f, 0.23f, 0.29f, 1f) }
            };
            float width = cellRect.width / 3f;
            float height = cellRect.height / 3f;
            for (int digit = 1; digit <= 9; digit++)
            {
                string text = candidates.Contains(digit) ? digit.ToString() : string.Empty;
                int index = digit - 1;
                GUI.Label(new Rect(cellRect.x + index % 3 * width,
                    cellRect.y + index / 3 * height, width, height), text, candidateStyle);
            }
        }

        private void DrawBasicsExerciseControls(Rect rect)
        {
            var labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = Color.white }
            };
            string cellText = _selectedRow < 0
                ? "Choose an empty cell:"
                : $"R{_selectedRow + 1}C{_selectedColumn + 1}:";
            GUI.Label(new Rect(rect.x, rect.y, 100f, rect.height), cellText, labelStyle);

            float x = rect.x + 98f;
            for (int digit = 1; digit <= 9; digit++)
            {
                float width = 32f;
                if (GUI.Button(new Rect(x, rect.y + 3f, width, 34f), digit.ToString()))
                    SelectExerciseDigit(digit);
                x += width + 2f;
            }

            if (GUI.Button(new Rect(x + 6f, rect.y + 3f, 96f, 34f), "Set Value"))
                ApplyExerciseAction(LearningAction.SetValue);
        }

        private void DrawWikiArticleDetail(
            Rect viewport,
            GUIStyle headingStyle,
            GUIStyle sectionStyle,
            GUIStyle bodyStyle,
            GUIStyle linkStyle)
        {
            if (_selectedItem == null)
                return;

            float contentWidth = viewport.width - 20f;
            float explanationHeight = EstimateWikiTextHeight(_selectedItem.Explanation, contentWidth, bodyStyle);
            float contentHeight = Mathf.Max(viewport.height, 105f + explanationHeight);
            var contentRect = new Rect(0f, 0f, contentWidth, contentHeight);
            _wikiDetailScroll = GUI.BeginScrollView(viewport, _wikiDetailScroll, contentRect);

            GUI.Label(new Rect(0f, 0f, contentWidth, 38f), _selectedItem.Title, headingStyle);
            GUI.Label(new Rect(0f, 42f, contentWidth, 24f),
                _selectedItem.Category + "  /  " + _selectedItem.Subsection, sectionStyle);
            DrawWikiExplanation(new Rect(0f, 78f, contentWidth, explanationHeight), _selectedItem, bodyStyle, linkStyle);

            GUI.EndScrollView();
        }

        private float EstimateWikiTextHeight(string text, float width, GUIStyle style)
        {
            float lineHeight = Mathf.Max(style.lineHeight, style.fontSize * 1.35f);
            float usedWidth = 0f;
            int lines = 1;
            foreach (string word in text.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries))
            {
                float wordWidth = style.CalcSize(new GUIContent(word)).x;
                float spaceWidth = style.CalcSize(new GUIContent(" ")).x;
                if (usedWidth > 0f && usedWidth + spaceWidth + wordWidth > width)
                {
                    lines++;
                    usedWidth = wordWidth;
                }
                else
                {
                    usedWidth += (usedWidth > 0f ? spaceWidth : 0f) + wordWidth;
                }
            }

            return lines * lineHeight + 8f;
        }

        private void DrawWikiExplanation(Rect rect, LearningItem article, GUIStyle bodyStyle, GUIStyle linkStyle)
        {
            float lineHeight = Mathf.Max(bodyStyle.lineHeight, bodyStyle.fontSize * 1.35f);
            float x = 0f;
            float y = 0f;
            int position = 0;

            while (position < article.Explanation.Length)
            {
                WikiLink link = FindNextInlineLink(article.Explanation, article.Links, position, out int linkPosition);
                if (link == null)
                {
                    DrawWikiTextRun(rect, article.Explanation.Substring(position), bodyStyle, lineHeight, ref x, ref y);
                    break;
                }

                if (linkPosition > position)
                    DrawWikiTextRun(rect, article.Explanation.Substring(position, linkPosition - position),
                        bodyStyle, lineHeight, ref x, ref y);

                DrawWikiLinkRun(rect, link, linkStyle, lineHeight, ref x, ref y);
                position = linkPosition + link.Label.Length;
            }
        }

        private void DrawWikiTextRun(
            Rect rect, string text, GUIStyle style, float lineHeight, ref float x, ref float y)
        {
            DrawWikiWords(rect, text, null, style, lineHeight, ref x, ref y);
        }

        private void DrawWikiLinkRun(
            Rect rect, WikiLink link, GUIStyle style, float lineHeight, ref float x, ref float y)
        {
            DrawWikiWords(rect, link.Label, link, style, lineHeight, ref x, ref y);
        }

        private void DrawWikiWords(
            Rect rect, string text, WikiLink link, GUIStyle style, float lineHeight, ref float x, ref float y)
        {
            string[] words = text.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries);
            float spaceWidth = style.CalcSize(new GUIContent(" ")).x;
            foreach (string word in words)
            {
                float wordWidth = style.CalcSize(new GUIContent(word)).x;
                float gap = x > 0f ? spaceWidth : 0f;
                if (x + gap + wordWidth > rect.width)
                {
                    x = 0f;
                    y += lineHeight;
                    gap = 0f;
                }

                var wordRect = new Rect(rect.x + x + gap, rect.y + y, wordWidth + 2f, lineHeight);
                if (link == null)
                {
                    GUI.Label(wordRect, word, style);
                }
                else
                {
                    if (GUI.Button(wordRect, word, style))
                        NavigateToWikiArticle(link.ArticleId);
                    Color previousColor = GUI.color;
                    GUI.color = _linkColor;
                    GUI.DrawTexture(new Rect(wordRect.x, wordRect.yMax - 2f, wordWidth, 1f),
                        Texture2D.whiteTexture, ScaleMode.StretchToFill);
                    GUI.color = previousColor;
                }

                x += gap + wordWidth;
            }
        }

        private void CreateListHeader(Transform parent, string title)
        {
            var header = CreateText(parent, title, 18, FontStyle.Bold, _accentColor);
            header.gameObject.AddComponent<LayoutElement>().preferredHeight = 28f;
        }

        private void CreateListSubheader(Transform parent, string title)
        {
            var subheader = CreateText(parent, title, 14, FontStyle.Bold, Color.white);
            subheader.gameObject.AddComponent<LayoutElement>().preferredHeight = 22f;
        }

        private void CreateInlineWikiParagraph(Transform parent, string explanation, IReadOnlyList<WikiLink> links, int fontSize)
        {
            var paragraph = new GameObject("WikiExplanation", typeof(RectTransform), typeof(LearningInlineFlowLayout),
                typeof(ContentSizeFitter), typeof(LayoutElement));
            paragraph.transform.SetParent(parent, false);
            var layout = paragraph.GetComponent<LearningInlineFlowLayout>();
            layout.LineSpacing = 2f;
            var fitter = paragraph.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var paragraphLayout = paragraph.GetComponent<LayoutElement>();
            paragraphLayout.flexibleWidth = 1f;

            int position = 0;
            while (position < explanation.Length)
            {
                WikiLink nextLink = FindNextInlineLink(explanation, links, position, out int linkPosition);
                if (nextLink == null)
                {
                    AddInlineTextRuns(layout.transform, explanation.Substring(position), fontSize);
                    break;
                }

                if (linkPosition > position)
                    AddInlineTextRuns(layout.transform, explanation.Substring(position, linkPosition - position), fontSize);
                AddInlineWikiLink(layout.transform, nextLink, fontSize);
                position = linkPosition + nextLink.Label.Length;
            }
        }

        private static WikiLink FindNextInlineLink(string text, IReadOnlyList<WikiLink> links, int start, out int position)
        {
            WikiLink selected = null;
            position = int.MaxValue;
            int selectedLength = -1;
            if (links == null)
                return null;

            foreach (var link in links)
            {
                if (link == null || string.IsNullOrEmpty(link.Label))
                    continue;

                int candidatePosition = text.IndexOf(link.Label, start, System.StringComparison.OrdinalIgnoreCase);
                while (candidatePosition >= 0)
                {
                    int end = candidatePosition + link.Label.Length;
                    bool leftBoundary = candidatePosition == 0 || !char.IsLetterOrDigit(text[candidatePosition - 1]);
                    bool rightBoundary = end == text.Length || !char.IsLetterOrDigit(text[end]);
                    if (leftBoundary && rightBoundary)
                    {
                        if (candidatePosition < position ||
                            (candidatePosition == position && link.Label.Length > selectedLength))
                        {
                            selected = link;
                            position = candidatePosition;
                            selectedLength = link.Label.Length;
                        }
                        break;
                    }
                    candidatePosition = text.IndexOf(link.Label, candidatePosition + 1, System.StringComparison.OrdinalIgnoreCase);
                }
            }

            return selected;
        }

        private void AddInlineTextRuns(Transform parent, string text, int fontSize)
        {
            int start = 0;
            while (start < text.Length)
            {
                bool whitespace = char.IsWhiteSpace(text[start]);
                int end = start + 1;
                while (end < text.Length && char.IsWhiteSpace(text[end]) == whitespace)
                    end++;

                string token = text.Substring(start, end - start);
                var label = CreateText(parent, token, fontSize, FontStyle.Normal, TextAnchor.MiddleLeft, Color.white);
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.verticalOverflow = VerticalWrapMode.Overflow;
                var element = label.gameObject.AddComponent<LayoutElement>();
                element.preferredWidth = Mathf.Max(0f, label.preferredWidth);
                element.preferredHeight = fontSize * 1.35f;
                start = end;
            }
        }

        private void AddInlineWikiLink(Transform parent, WikiLink link, int fontSize)
        {
            var buttonObject = new GameObject("WikiLink_" + link.ArticleId, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);
            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = buttonObject.GetComponent<Image>();
            buttonObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
            button.onClick.AddListener(() => NavigateToWikiArticle(link.ArticleId));

            var label = CreateText(buttonObject.transform, link.Label, fontSize, FontStyle.Bold,
                TextAnchor.MiddleLeft, _linkColor);
            SetRect(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            var underline = new GameObject("Underline", typeof(RectTransform), typeof(Image));
            underline.transform.SetParent(buttonObject.transform, false);
            SetRect(underline.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 1.5f));
            underline.GetComponent<Image>().color = _linkColor;
            underline.GetComponent<Image>().raycastTarget = false;
            var element = buttonObject.GetComponent<LayoutElement>();
            element.preferredWidth = label.preferredWidth + 3f;
            element.preferredHeight = fontSize * 1.35f;
        }

        private void NavigateToWikiArticle(string articleId)
        {
            var article = LearningContent.GetWikiArticles().FirstOrDefault(item => item.Id == articleId);
            if (article == null)
            {
                Debug.LogError($"LearningHubPanel: Wiki link points to missing article '{articleId}'.");
                return;
            }

            _showWiki = true;
            _selectedItem = article;
            RefreshContent();
        }

        private GameObject CreateScrollView(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color background, out RectTransform content)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            root.transform.SetParent(parent, false);
            SetRect(root.GetComponent<RectTransform>(), anchorMin, anchorMax, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            root.GetComponent<Image>().color = background;

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(root.transform, false);
            SetRect(viewport.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            var contentObject = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentObject.transform.SetParent(viewport.transform, false);
            content = contentObject.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            var layout = contentObject.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = contentObject.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = root.GetComponent<ScrollRect>();
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = content;
            scroll.vertical = true;
            scroll.horizontal = false;
            return root;
        }

        private Text CreateParagraph(Transform parent, string value, int fontSize)
        {
            var text = CreateText(parent, value, fontSize, FontStyle.Normal, TextAnchor.UpperLeft, Color.white);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            var fitter = text.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var layout = text.gameObject.AddComponent<LayoutElement>();
            layout.flexibleWidth = 1f;
            return text;
        }

        private Text CreateText(Transform parent, string value, int fontSize, FontStyle style, Color color)
        {
            return CreateText(parent, value, fontSize, style, TextAnchor.MiddleLeft, color);
        }

        private Text CreateText(Transform parent, string value, int fontSize, FontStyle style, TextAnchor alignment, Color color)
        {
            var textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(parent, false);
            var text = textObject.GetComponent<Text>();
            text.text = value;
            text.font = GetRuntimeFont();
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private GameObject CreateHorizontalRow(Transform parent, string name, float height)
        {
            var row = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            row.GetComponent<LayoutElement>().preferredHeight = height;
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            return row;
        }

        private GameObject CreateButton(
            Transform parent,
            string label,
            Color color,
            UnityEngine.Events.UnityAction onClick,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 position,
            Vector2 size)
        {
            var button = CreateButton(parent, label, color, onClick);
            var rect = button.GetComponent<RectTransform>();
            SetRect(rect, anchorMin, anchorMax, pivot, position, size);
            return button;
        }

        private GameObject CreateButton(Transform parent, string label, Color color, UnityEngine.Events.UnityAction onClick,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            return CreateButton(parent, label, color, onClick, anchorMin, anchorMax, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        }

        private GameObject CreateButton(Transform parent, string label, Color color, UnityEngine.Events.UnityAction onClick)
        {
            var buttonObject = new GameObject(label + "Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);
            buttonObject.GetComponent<Image>().color = color;
            buttonObject.GetComponent<Button>().onClick.AddListener(onClick);
            buttonObject.GetComponent<LayoutElement>().preferredHeight = 42f;

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelObject.transform.SetParent(buttonObject.transform, false);
            SetRect(labelObject.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var text = labelObject.GetComponent<Text>();
            text.text = label;
            text.font = GetRuntimeFont();
            text.fontSize = 16;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            return buttonObject;
        }

        private void ClearChildren(RectTransform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
                Destroy(parent.GetChild(i).gameObject);
        }

        private static Font GetRuntimeFont()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }

    public sealed class LearningInlineFlowLayout : LayoutGroup
    {
        public float LineSpacing { get; set; }

        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();
            SetLayoutInputForAxis(0f, 0f, -1f, 0);
        }

        public override void CalculateLayoutInputVertical()
        {
            float width = GetAvailableWidth();
            float lineWidth = 0f;
            float lineHeight = 0f;
            float totalHeight = padding.top + padding.bottom;

            for (int i = 0; i < rectChildren.Count; i++)
            {
                var child = rectChildren[i];
                float childWidth = GetChildWidth(child);
                float childHeight = GetChildHeight(child);
                if (lineWidth > 0f && lineWidth + childWidth > width)
                {
                    totalHeight += lineHeight + LineSpacing;
                    lineWidth = 0f;
                    lineHeight = 0f;
                }

                lineWidth += childWidth;
                lineHeight = Mathf.Max(lineHeight, childHeight);
            }

            if (rectChildren.Count > 0)
                totalHeight += lineHeight;
            SetLayoutInputForAxis(totalHeight, totalHeight, -1f, 1);
        }

        public override void SetLayoutHorizontal()
        {
            LayoutChildren(false);
        }

        public override void SetLayoutVertical()
        {
            LayoutChildren(true);
        }

        private float GetAvailableWidth()
        {
            float width = rectTransform.rect.width - padding.horizontal;
            return width > 0f ? width : Mathf.Max(1f, Screen.width * 0.6f - padding.horizontal);
        }

        private static float GetChildWidth(RectTransform child)
        {
            return Mathf.Max(1f, LayoutUtility.GetPreferredWidth(child));
        }

        private static float GetChildHeight(RectTransform child)
        {
            return Mathf.Max(1f, LayoutUtility.GetPreferredHeight(child));
        }

        private void LayoutChildren(bool setVertical)
        {
            float maxWidth = GetAvailableWidth();
            float x = padding.left;
            float y = -padding.top;
            float lineHeight = 0f;

            for (int i = 0; i < rectChildren.Count; i++)
            {
                var child = rectChildren[i];
                float width = Mathf.Min(GetChildWidth(child), maxWidth);
                float height = GetChildHeight(child);
                if (x > padding.left && x - padding.left + width > maxWidth)
                {
                    x = padding.left;
                    y -= lineHeight + LineSpacing;
                    lineHeight = 0f;
                }

                child.anchorMin = new Vector2(0f, 1f);
                child.anchorMax = new Vector2(0f, 1f);
                child.pivot = new Vector2(0f, 1f);
                if (!setVertical)
                    child.sizeDelta = new Vector2(width, height);
                else
                    child.anchoredPosition = new Vector2(x, y);

                x += width;
                lineHeight = Mathf.Max(lineHeight, height);
            }
        }
    }
}
