using System;
using System.Collections.Generic;
using Sudoku.Models;
using UnityEngine;
using UnityEngine.UI;

namespace Sudoku.UI.Panels
{
    /**
     * Full-screen modal panel that shows the user's saved puzzle list.
     *
     * Features:
     *  - Scrollable list of saved puzzles with mini-grid previews.
     *  - Per-entry Load, Move-Up, Move-Down, and Delete actions.
     *  - "Add from Code" row to import a puzzle by pasting its code.
     *
     * Usage:
     *  1. Call Initialize(canvas) once after instantiation.
     *  2. Subscribe to OnPuzzleLoadRequested before calling Open().
     *  3. Call Open() to show the panel; it closes automatically on Load.
     */
    [DisallowMultipleComponent]
    public class SavedPuzzleListPanel : MonoBehaviour
    {
        // Fired with the puzzle code when the user clicks "Load" on an entry.
        public event Action<string> OnPuzzleLoadRequested;
        // Fired with the puzzle code when the user clicks "Edit" on an entry.
        public event Action<string> OnPuzzleEditRequested;

        // Panel colours kept consistent with MainMenuFlowController styling.
        private static readonly Color ColourOverlay = new Color(0f, 0f, 0f, 0.72f);
        private static readonly Color ColourCard = new Color(0.08f, 0.08f, 0.08f, 0.97f);
        private static readonly Color ColourDivider = new Color(0.22f, 0.22f, 0.22f, 1f);
        private static readonly Color ColourInputBg = new Color(0.97f, 0.97f, 0.97f, 1f);
        private static readonly Color ColourInputText = new Color(0.08f, 0.08f, 0.08f, 1f);
        private static readonly Color ColourInputPlaceholder = new Color(0.42f, 0.42f, 0.42f, 0.85f);
        private static readonly Color ColourButtonPrimary = new Color(0.90f, 0.70f, 0.18f, 1f);
        private static readonly Color ColourButtonSecondary = new Color(0.83f, 0.85f, 0.88f, 1f);
        private static readonly Color ColourButtonDanger = new Color(0.80f, 0.22f, 0.22f, 1f);
        private static readonly Color ColourEntryBg = new Color(0.13f, 0.13f, 0.13f, 1f);
        private static readonly Color ColourScrollbar = new Color(0.35f, 0.35f, 0.35f, 1f);
        private static readonly Color ColourScrollHandle = new Color(0.60f, 0.60f, 0.60f, 1f);

        // Mini-preview colours (2 px per cell → 18×18 px rendered at 72×72).
        private static readonly Color PreviewColourEmpty = new Color(0.18f, 0.18f, 0.18f);
        private static readonly Color PreviewColourFilled = new Color(0.90f, 0.70f, 0.18f);
        private static readonly Color PreviewColourBoxLine = new Color(0.30f, 0.30f, 0.30f);

        private const int PreviewCellPx = 2;
        private const int PreviewSize = 9 * PreviewCellPx;  // 18 px

        private GameObject _overlay;
        private Transform _scrollContent;
        private GameObject _emptyLabel;
        private LayoutElement _emptyLabelLayout;
        private Text _emptyLabelText;
        private InputField _addCodeInput;
        private ScrollRect _scrollRect;
        private string _pendingDeletePuzzleId = string.Empty;

        // Tracks textures created for mini previews so they can be cleaned up.
        private readonly List<Texture2D> _previewTextures = new List<Texture2D>();

        // ------------------------------------------------------------------ //
        //  Lifecycle
        // ------------------------------------------------------------------ //

        /**
         * Build the panel UI and attach it to the provided canvas.
         * Call this once immediately after the component is added to a GameObject.
         *
         * @param canvas The canvas under which the overlay will be parented.
         */
        public void Initialize(Canvas canvas)
        {
            if (canvas == null)
            {
                Debug.LogWarning("SavedPuzzleListPanel: Initialize called with null canvas.");
                return;
            }

            BuildOverlay(canvas.transform);
            Close();
        }

        private void OnDestroy()
        {
            DestroyPreviewTextures();
        }

        // ------------------------------------------------------------------ //
        //  Public API
        // ------------------------------------------------------------------ //

        /**
         * Show the saved puzzle list panel and refresh its contents from disk.
         */
        public void Open()
        {
            gameObject.SetActive(true);
            if (_overlay != null)
            {
                _overlay.SetActive(true);
            }

            RefreshList();
        }

        /**
         * Hide the saved puzzle list panel.
         */
        public void Close()
        {
            if (_overlay != null)
            {
                _overlay.SetActive(false);
            }

            gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ //
        //  List Management
        // ------------------------------------------------------------------ //

        /**
         * Reload all puzzle entries from the repository and rebuild the scroll list UI.
         */
        private void RefreshList()
        {
            if (_scrollContent == null)
            {
                return;
            }

            // Destroy all existing entry rows and their preview textures.
            DestroyPreviewTextures();
            for (int i = _scrollContent.childCount - 1; i >= 0; i--)
            {
                var child = _scrollContent.GetChild(i);
                if (child != null)
                {
                    // Keep the empty label object; toggle its layout instead.
                    if (_emptyLabel != null && child.gameObject == _emptyLabel)
                    {
                        continue;
                    }

                    // Remove from layout immediately so we do not keep one-frame
                    // ghost rows after Destroy() in play mode.
                    child.SetParent(null, false);
                    Destroy(child.gameObject);
                }
            }

            var puzzles = SavedPuzzleRepository.LoadAll();

            // Sanitize persisted data to avoid rendering blank/invalid rows.
            // Keep only entries that have a usable code.
            puzzles.RemoveAll(p => p == null || string.IsNullOrWhiteSpace(p.Code));
            puzzles.RemoveAll(p => PuzzleCodeGenerator.DecodeBoardFromCode(p.Code) == null);
            for (int i = 0; i < puzzles.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(puzzles[i].Name))
                {
                    puzzles[i].Name = $"Puzzle {i + 1}";
                }
            }

            if (!string.IsNullOrEmpty(_pendingDeletePuzzleId))
            {
                bool stillExists = puzzles.Exists(p => p != null && p.Id == _pendingDeletePuzzleId);
                if (!stillExists)
                {
                    _pendingDeletePuzzleId = string.Empty;
                }
            }

            if (_emptyLabel != null)
            {
                bool isEmpty = puzzles.Count == 0;
                _emptyLabel.SetActive(true);

                if (_emptyLabelLayout != null)
                {
                    _emptyLabelLayout.ignoreLayout = !isEmpty;
                    _emptyLabelLayout.preferredHeight = isEmpty ? 60f : 0f;
                    _emptyLabelLayout.minHeight = isEmpty ? 60f : 0f;
                }

                if (_emptyLabelText != null)
                {
                    _emptyLabelText.text = isEmpty
                        ? "No saved puzzles yet.\nUse \"Save\" during a puzzle, or paste a code above."
                        : string.Empty;
                }
            }

            for (int i = 0; i < puzzles.Count; i++)
            {
                // Capture loop variable for closures.
                var puzzle = puzzles[i];
                BuildEntryRow(_scrollContent, puzzle, isFirst: i == 0, isLast: i == puzzles.Count - 1);
            }

            // Force the content's ContentSizeFitter to recalculate immediately
            // so the scroll view starts at the correct position.
            Canvas.ForceUpdateCanvases();

            if (_scrollRect != null)
            {
                _scrollRect.StopMovement();
                _scrollRect.verticalNormalizedPosition = 1f;
                if (_scrollRect.verticalScrollbar != null)
                {
                    _scrollRect.verticalScrollbar.value = 1f;
                }

                if (_scrollRect.content != null)
                {
                    _scrollRect.content.anchoredPosition = Vector2.zero;
                }
            }

            StartCoroutine(EnsureScrollAtTopNextFrame());
        }

        /**
         * Re-apply top scroll one frame later after layout settles.
         */
        private System.Collections.IEnumerator EnsureScrollAtTopNextFrame()
        {
            yield return null;

            if (_scrollRect == null)
            {
                yield break;
            }

            _scrollRect.StopMovement();
            _scrollRect.verticalNormalizedPosition = 1f;
            if (_scrollRect.verticalScrollbar != null)
            {
                _scrollRect.verticalScrollbar.value = 1f;
            }

            if (_scrollRect.content != null)
            {
                _scrollRect.content.anchoredPosition = Vector2.zero;
            }
        }

        // ------------------------------------------------------------------ //
        //  Entry Callbacks
        // ------------------------------------------------------------------ //

        private void OnLoadClicked(string code)
        {
            _pendingDeletePuzzleId = string.Empty;
            OnPuzzleLoadRequested?.Invoke(code);
            Close();
        }

        private void OnEditClicked(string code)
        {
            _pendingDeletePuzzleId = string.Empty;
            OnPuzzleEditRequested?.Invoke(code);
            Close();
        }

        private void OnMoveUpClicked(string id)
        {
            _pendingDeletePuzzleId = string.Empty;
            SavedPuzzleRepository.MoveUp(id);
            RefreshList();
        }

        private void OnMoveDownClicked(string id)
        {
            _pendingDeletePuzzleId = string.Empty;
            SavedPuzzleRepository.MoveDown(id);
            RefreshList();
        }

        private void OnDeleteClicked(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return;
            }

            _pendingDeletePuzzleId = id;
            RefreshList();
        }

        private void OnDeleteConfirmClicked(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return;
            }

            SavedPuzzleRepository.Delete(id);
            _pendingDeletePuzzleId = string.Empty;
            RefreshList();
        }

        private void OnDeleteCancelClicked()
        {
            _pendingDeletePuzzleId = string.Empty;
            RefreshList();
        }

        private void OnAddFromCodeClicked()
        {
            if (_addCodeInput == null)
            {
                return;
            }

            string code = _addCodeInput.text != null ? _addCodeInput.text.Trim() : string.Empty;
            if (string.IsNullOrEmpty(code))
            {
                Debug.LogWarning("SavedPuzzleListPanel: Enter a puzzle code to add.");
                return;
            }

            // Validate by decoding; this also auto-corrects ambiguous characters.
            var decoded = PuzzleCodeGenerator.DecodeBoardFromCode(code);
            if (decoded == null)
            {
                Debug.LogWarning("SavedPuzzleListPanel: The entered code is invalid and could not be decoded.");
                return;
            }

            // Re-encode the decoded board to get a canonical form.
            string canonical = PuzzleCodeGenerator.EncodeBoardToCode(decoded);
            if (string.IsNullOrEmpty(canonical))
            {
                Debug.LogWarning("SavedPuzzleListPanel: Failed to re-encode decoded board.");
                return;
            }

            int nextNumber = SavedPuzzleRepository.Count() + 1;
            var entry = new SavedPuzzle($"Puzzle {nextNumber}", canonical);
            entry.ApplyAnalysis(SavedPuzzleAnalysisGenerator.AnalyzeFromCode(canonical));
            SavedPuzzleRepository.Add(entry);

            _addCodeInput.text = string.Empty;
            RefreshList();
            Debug.Log($"SavedPuzzleListPanel: Added puzzle '{entry.Name}' from code.");
        }

        // ------------------------------------------------------------------ //
        //  UI Construction — Overlay and Card
        // ------------------------------------------------------------------ //

        private void BuildOverlay(Transform canvasTransform)
        {
            // Semi-transparent full-screen backdrop.
            _overlay = new GameObject("SavedPuzzlesOverlay",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            _overlay.transform.SetParent(transform, false);

            var overlayRect = _overlay.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;

            var overlayImage = _overlay.GetComponent<Image>();
            overlayImage.color = ColourOverlay;
            overlayImage.raycastTarget = true;

            // Block clicks on the backdrop from passing through to the board.
            var overlayButton = _overlay.GetComponent<Button>();
            overlayButton.transition = Selectable.Transition.None;
            overlayButton.onClick.AddListener(Close);

            // Centered modal card.
            var card = new GameObject("SavedPuzzlesCard",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            card.transform.SetParent(_overlay.transform, false);

            var cardRect = card.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(760f, 620f);
            cardRect.anchoredPosition = new Vector2(0f, 30f);

            var cardImage = card.GetComponent<Image>();
            cardImage.color = ColourCard;

            // Prevent overlay close when clicking inside the modal card.
            var cardBlocker = card.AddComponent<Button>();
            cardBlocker.transition = Selectable.Transition.None;

            BuildCardContents(card.transform, cardRect.sizeDelta);
        }

        private void BuildCardContents(Transform card, Vector2 cardSize)
        {
            // Vertical layout fills the card.
            var vlg = card.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(14, 14, 0, 10);
            vlg.spacing = 2f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            BuildHeaderRow(card);
            BuildAddCodeRow(card);
            BuildDivider(card);
            BuildScrollView(card, cardSize);
        }

        // ------------------------------------------------------------------ //
        //  UI Construction — Header
        // ------------------------------------------------------------------ //

        private void BuildHeaderRow(Transform parent)
        {
            var row = CreateHorizontalRow(parent, "HeaderRow", 36f);

            // Title label — takes up available space.
            var titleGO = new GameObject("TitleLabel",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            titleGO.transform.SetParent(row, false);

            var titleLayout = titleGO.AddComponent<LayoutElement>();
            titleLayout.preferredHeight = 36f;
            titleLayout.flexibleWidth = 1f;

            var titleText = titleGO.GetComponent<Text>();
            titleText.text = "Saved Puzzles";
            titleText.alignment = TextAnchor.MiddleLeft;
            titleText.color = Color.white;
            titleText.fontSize = 24;
            titleText.fontStyle = FontStyle.Bold;
            titleText.font = GetFont();

            var closeBtn = CreateButton(row, "HeaderCloseButton", "X", Close, false);
            SetButtonSize(closeBtn, 26f, 26f);

            var closeBtnImage = closeBtn.GetComponent<Image>();
            if (closeBtnImage != null)
            {
                closeBtnImage.color = new Color(0.55f, 0.55f, 0.55f, 1f);
            }

            var closeBtnText = closeBtn.GetComponentInChildren<Text>();
            if (closeBtnText != null)
            {
                closeBtnText.fontSize = 11;
            }
        }

        // ------------------------------------------------------------------ //
        //  UI Construction — Add From Code Row
        // ------------------------------------------------------------------ //

        private void BuildAddCodeRow(Transform parent)
        {
            var row = CreateHorizontalRow(parent, "AddCodeRow", 28f);

            // "Add from code:" label.
            var labelGO = new GameObject("AddCodeLabel",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelGO.transform.SetParent(row, false);

            var labelLayout = labelGO.AddComponent<LayoutElement>();
            labelLayout.preferredWidth = 130f;
            labelLayout.minWidth = 130f;
            labelLayout.preferredHeight = 28f;
            labelLayout.flexibleWidth = 0f;

            var labelText = labelGO.GetComponent<Text>();
            labelText.text = "Add from code:";
            labelText.alignment = TextAnchor.MiddleLeft;
            labelText.color = new Color(0.82f, 0.82f, 0.82f, 1f);
            labelText.fontSize = 14;
            labelText.font = GetFont();

            // Code input field.
            _addCodeInput = BuildInputField(row, "AddCodeInput", "Paste puzzle code…", 360f, 1f);

            // Add button.
            var addBtn = CreateButton(row, "AddCodeButton", "Add to List", OnAddFromCodeClicked, true);
            var addBtnLayout = addBtn.GetComponent<LayoutElement>();
            if (addBtnLayout == null)
            {
                addBtnLayout = addBtn.AddComponent<LayoutElement>();
            }

            addBtnLayout.preferredWidth = 110f;
            addBtnLayout.minWidth = 100f;
            addBtnLayout.preferredHeight = 28f;
            addBtnLayout.flexibleWidth = 0f;
        }

        // ------------------------------------------------------------------ //
        //  UI Construction — Divider
        // ------------------------------------------------------------------ //

        private static void BuildDivider(Transform parent)
        {
            var dividerGO = new GameObject("Divider",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            dividerGO.transform.SetParent(parent, false);

            var layout = dividerGO.AddComponent<LayoutElement>();
            layout.preferredHeight = 1f;
            layout.minHeight = 1f;
            layout.flexibleHeight = 0f;

            var image = dividerGO.GetComponent<Image>();
            image.color = ColourDivider;
        }

        // ------------------------------------------------------------------ //
        //  UI Construction — Scroll View
        // ------------------------------------------------------------------ //

        private void BuildScrollView(Transform parent, Vector2 cardSize)
        {
            // The scroll view should fill the remaining card height.
            // We allocate a fixed height; the VLG handles layout.
            var scrollViewGO = new GameObject("ScrollView",
                typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollViewGO.transform.SetParent(parent, false);

            var scrollLayout = scrollViewGO.AddComponent<LayoutElement>();
            // Fill remaining vertical space.
            scrollLayout.preferredHeight = cardSize.y - 56f;
            scrollLayout.minHeight = 200f;
            scrollLayout.flexibleHeight = 1f;

            var scrollImage = scrollViewGO.GetComponent<Image>();
            scrollImage.color = new Color(0f, 0f, 0f, 0f);
            scrollImage.raycastTarget = false;

            // Viewport with Mask.
            var viewportGO = new GameObject("Viewport",
                typeof(RectTransform), typeof(Image), typeof(Mask));
            viewportGO.transform.SetParent(scrollViewGO.transform, false);

            var viewportRect = viewportGO.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = new Vector2(1f, 1f);
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = new Vector2(-14f, 0f);

            var viewportImage = viewportGO.GetComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0.01f);  // Nearly transparent; required by Mask.

            var viewportMask = viewportGO.GetComponent<Mask>();
            viewportMask.showMaskGraphic = false;

            // Content container.
            var contentGO = new GameObject("Content",
                typeof(RectTransform), typeof(CanvasRenderer));
            contentGO.transform.SetParent(viewportGO.transform, false);

            _scrollContent = contentGO.transform;

            var contentRect = contentGO.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 0f);

            var contentVlg = contentGO.AddComponent<VerticalLayoutGroup>();
            contentVlg.padding = new RectOffset(4, 4, 4, 4);
            contentVlg.spacing = 6f;
            contentVlg.childAlignment = TextAnchor.UpperLeft;
            contentVlg.childControlWidth = true;
            contentVlg.childControlHeight = true;
            contentVlg.childForceExpandWidth = true;
            contentVlg.childForceExpandHeight = false;

            var contentFitter = contentGO.AddComponent<ContentSizeFitter>();
            contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Vertical scrollbar.
            var scrollbar = BuildScrollbar(scrollViewGO.transform);

            // Configure ScrollRect.
            var scrollRect = scrollViewGO.GetComponent<ScrollRect>();
            scrollRect.content = contentRect;
            scrollRect.viewport = viewportRect;
            scrollRect.verticalScrollbar = scrollbar;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 40f;
            _scrollRect = scrollRect;

            // Empty state label shown when the list has no entries.
            var emptyGO = new GameObject("EmptyLabel",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            emptyGO.transform.SetParent(contentGO.transform, false);
            _emptyLabel = emptyGO;

            _emptyLabelLayout = emptyGO.AddComponent<LayoutElement>();
            _emptyLabelLayout.preferredHeight = 60f;
            _emptyLabelLayout.minHeight = 60f;

            _emptyLabelText = emptyGO.GetComponent<Text>();
            _emptyLabelText.text = "No saved puzzles yet.\nUse \"Save\" during a puzzle, or paste a code above.";
            _emptyLabelText.alignment = TextAnchor.MiddleCenter;
            _emptyLabelText.color = new Color(0.55f, 0.55f, 0.55f, 1f);
            _emptyLabelText.fontSize = 14;
            _emptyLabelText.font = GetFont();
        }

        private static Scrollbar BuildScrollbar(Transform scrollViewParent)
        {
            var scrollbarGO = new GameObject("Scrollbar",
                typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            scrollbarGO.transform.SetParent(scrollViewParent, false);

            var scrollbarRect = scrollbarGO.GetComponent<RectTransform>();
            scrollbarRect.anchorMin = new Vector2(1f, 0f);
            scrollbarRect.anchorMax = new Vector2(1f, 1f);
            scrollbarRect.pivot = new Vector2(1f, 0.5f);
            scrollbarRect.offsetMin = new Vector2(-12f, 0f);
            scrollbarRect.offsetMax = new Vector2(0f, 0f);

            var scrollbarImage = scrollbarGO.GetComponent<Image>();
            scrollbarImage.color = ColourScrollbar;

            // Sliding area.
            var slidingAreaGO = new GameObject("SlidingArea", typeof(RectTransform));
            slidingAreaGO.transform.SetParent(scrollbarGO.transform, false);

            var slidingRect = slidingAreaGO.GetComponent<RectTransform>();
            slidingRect.anchorMin = Vector2.zero;
            slidingRect.anchorMax = Vector2.one;
            slidingRect.offsetMin = new Vector2(0f, 7f);
            slidingRect.offsetMax = new Vector2(0f, -7f);

            // Handle.
            var handleGO = new GameObject("Handle",
                typeof(RectTransform), typeof(Image));
            handleGO.transform.SetParent(slidingAreaGO.transform, false);

            var handleRect = handleGO.GetComponent<RectTransform>();
            handleRect.anchorMin = new Vector2(0f, 0f);
            handleRect.anchorMax = new Vector2(1f, 1f);
            handleRect.offsetMin = Vector2.zero;
            handleRect.offsetMax = Vector2.zero;

            var handleImage = handleGO.GetComponent<Image>();
            handleImage.color = ColourScrollHandle;

            // Wire up Scrollbar component.
            var scrollbar = scrollbarGO.GetComponent<Scrollbar>();
            scrollbar.handleRect = handleGO.GetComponent<RectTransform>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;

            return scrollbar;
        }

        // ------------------------------------------------------------------ //
        //  UI Construction — Puzzle Entry Row
        // ------------------------------------------------------------------ //

        private void BuildEntryRow(Transform parent, SavedPuzzle puzzle, bool isFirst, bool isLast)
        {
            if (puzzle == null)
            {
                return;
            }

            var rowGO = new GameObject($"Entry_{puzzle.Id}",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            rowGO.transform.SetParent(parent, false);

            var rowLayout = rowGO.AddComponent<LayoutElement>();
            rowLayout.preferredHeight = 76f;
            rowLayout.minHeight = 76f;
            rowLayout.flexibleHeight = 0f;

            var rowImage = rowGO.GetComponent<Image>();
            rowImage.color = ColourEntryBg;

            var rowHlg = rowGO.AddComponent<HorizontalLayoutGroup>();
            rowHlg.padding = new RectOffset(8, 8, 4, 4);
            rowHlg.spacing = 10f;
            rowHlg.childAlignment = TextAnchor.MiddleLeft;
            rowHlg.childControlWidth = true;
            rowHlg.childControlHeight = true;
            rowHlg.childForceExpandWidth = false;
            rowHlg.childForceExpandHeight = false;

            // Mini preview.
            BuildMiniPreview(rowGO.transform, puzzle.Code);

            // Info column: name and date.
            BuildInfoColumn(rowGO.transform, puzzle);

            // Action buttons column.
            BuildActionsColumn(rowGO.transform, puzzle, isFirst, isLast);
        }

        private void BuildMiniPreview(Transform parent, string code)
        {
            var previewGO = new GameObject("MiniPreview",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            previewGO.transform.SetParent(parent, false);

            var layout = previewGO.AddComponent<LayoutElement>();
            layout.preferredWidth = 60f;
            layout.minWidth = 60f;
            layout.preferredHeight = 60f;
            layout.minHeight = 60f;
            layout.flexibleWidth = 0f;
            layout.flexibleHeight = 0f;

            var rawImage = previewGO.GetComponent<RawImage>();
            var texture = BuildPreviewTexture(code);
            rawImage.texture = texture;
            rawImage.uvRect = new Rect(0f, 0f, 1f, 1f);

            // Border frame around preview.
            var borderGO = new GameObject("PreviewBorder",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            borderGO.transform.SetParent(previewGO.transform, false);

            var borderRect = borderGO.GetComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = new Vector2(-1f, -1f);
            borderRect.offsetMax = new Vector2(1f, 1f);

            var borderImage = borderGO.GetComponent<Image>();
            borderImage.color = new Color(0f, 0f, 0f, 0f);
            borderImage.raycastTarget = false;

            var outline = borderGO.AddComponent<Outline>();
            outline.effectColor = new Color(0.32f, 0.32f, 0.32f, 1f);
            outline.effectDistance = new Vector2(1f, -1f);

            borderGO.transform.SetAsFirstSibling();
        }

        /**
         * Create an 18×18 Texture2D miniature preview of the puzzle.
         * Each cell occupies 2×2 pixels; filled cells are gold, empty cells are dark.
         *
         * @param code Encoded puzzle code to visualise.
         * @returns A new Texture2D; ownership passes to the caller.
         */
        private Texture2D BuildPreviewTexture(string code)
        {
            var texture = new Texture2D(PreviewSize, PreviewSize, TextureFormat.RGB24, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "PuzzlePreview"
            };

            _previewTextures.Add(texture);

            // Default: all cells empty.
            var pixels = new Color[PreviewSize * PreviewSize];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = PreviewColourEmpty;
            }

            // Decode puzzle code to retrieve cell values.
            var decoded = string.IsNullOrEmpty(code)
                ? null
                : PuzzleCodeGenerator.DecodeBoardFromCode(code);

            if (decoded?.Cells != null)
            {
                for (int row = 0; row < decoded.Size && row < 9; row++)
                {
                    for (int col = 0; col < decoded.Size && col < 9; col++)
                    {
                        var cell = decoded.Cells[row, col];
                        bool filled = cell != null && cell.Value.HasValue;
                        Color cellColor = filled ? PreviewColourFilled : PreviewColourEmpty;

                        // Write 2×2 pixel block. Texture rows are bottom-up.
                        int pixelRow = (8 - row) * PreviewCellPx;
                        int pixelCol = col * PreviewCellPx;

                        for (int py = pixelRow; py < pixelRow + PreviewCellPx; py++)
                        {
                            for (int px = pixelCol; px < pixelCol + PreviewCellPx; px++)
                            {
                                pixels[py * PreviewSize + px] = cellColor;
                            }
                        }
                    }
                }

                // Draw subtle box-separator lines at columns 3 and 6 (rows in texture coords).
                DrawPreviewBoxLines(pixels);
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static void DrawPreviewBoxLines(Color[] pixels)
        {
            // Vertical box separators at cell columns 3 and 6.
            int[] colSeps = { 3, 6 };
            foreach (int col in colSeps)
            {
                int px = col * PreviewCellPx;
                for (int py = 0; py < PreviewSize; py++)
                {
                    pixels[py * PreviewSize + px] = PreviewColourBoxLine;
                }
            }

            // Horizontal box separators at cell rows 3 and 6.
            int[] rowSeps = { 3, 6 };
            foreach (int row in rowSeps)
            {
                int py = (8 - row) * PreviewCellPx + PreviewCellPx - 1;
                if (py >= 0 && py < PreviewSize)
                {
                    for (int px = 0; px < PreviewSize; px++)
                    {
                        pixels[py * PreviewSize + px] = PreviewColourBoxLine;
                    }
                }
            }
        }

        private void BuildInfoColumn(Transform parent, SavedPuzzle puzzle)
        {
            var colGO = new GameObject("InfoColumn",
                typeof(RectTransform), typeof(CanvasRenderer));
            colGO.transform.SetParent(parent, false);

            var colLayout = colGO.AddComponent<LayoutElement>();
            colLayout.flexibleWidth = 1f;
            colLayout.preferredHeight = 60f;
            colLayout.minHeight = 60f;

            var colRect = colGO.GetComponent<RectTransform>();
            colRect.sizeDelta = new Vector2(0f, 60f);

            // Puzzle name.
            var nameGO = new GameObject("NameLabel",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            nameGO.transform.SetParent(colGO.transform, false);

            var nameRect = nameGO.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, 0.52f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.offsetMin = new Vector2(0f, 0f);
            nameRect.offsetMax = new Vector2(0f, 0f);

            var nameText = nameGO.GetComponent<Text>();
            nameText.text = puzzle.Name;
            nameText.alignment = TextAnchor.LowerLeft;
            nameText.color = Color.white;
            nameText.fontSize = 15;
            nameText.fontStyle = FontStyle.Bold;
            nameText.font = GetFont();

            // Save date.
            var dateGO = new GameObject("DateLabel",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            dateGO.transform.SetParent(colGO.transform, false);

            var dateRect = dateGO.GetComponent<RectTransform>();
            dateRect.anchorMin = new Vector2(0f, 0f);
            dateRect.anchorMax = new Vector2(1f, 0.5f);
            dateRect.offsetMin = new Vector2(0f, 0f);
            dateRect.offsetMax = new Vector2(0f, 0f);

            var dateText = dateGO.GetComponent<Text>();
            string localDate = puzzle.SavedAt.ToLocalTime().ToString("dd MMM yyyy, HH:mm");
            string difficulty = string.IsNullOrWhiteSpace(puzzle.DifficultyLabel) ? "Unknown" : puzzle.DifficultyLabel;
            dateText.text = $"Saved: {localDate}  |  {difficulty}";
            dateText.alignment = TextAnchor.UpperLeft;
            dateText.color = new Color(0.62f, 0.62f, 0.62f, 1f);
            dateText.fontSize = 10;
            dateText.font = GetFont();
        }

        private void BuildActionsColumn(Transform parent, SavedPuzzle puzzle, bool isFirst, bool isLast)
        {
            var colGO = new GameObject("ActionsColumn",
                typeof(RectTransform), typeof(CanvasRenderer));
            colGO.transform.SetParent(parent, false);

            var colLayout = colGO.AddComponent<LayoutElement>();
            colLayout.preferredWidth = 180f;
            colLayout.minWidth = 180f;
            colLayout.preferredHeight = 60f;
            colLayout.flexibleWidth = 0f;

            var hlg = colGO.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(0, 0, 0, 0);
            hlg.spacing = 4f;
            hlg.childAlignment = TextAnchor.MiddleRight;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            // Spacer to push buttons right.
            var spacerGO = new GameObject("Spacer", typeof(RectTransform), typeof(CanvasRenderer));
            spacerGO.transform.SetParent(colGO.transform, false);
            var spacerLayout = spacerGO.AddComponent<LayoutElement>();
            spacerLayout.flexibleWidth = 1f;
            spacerLayout.preferredHeight = 72f;

            string puzzleId = puzzle.Id;
            string puzzleCode = puzzle.Code;
            bool isDeletePendingForRow = !string.IsNullOrEmpty(_pendingDeletePuzzleId) && _pendingDeletePuzzleId == puzzleId;

            // Load button.
            var loadBtn = CreateButton(colGO.transform, "LoadButton", "Load",
                () => OnLoadClicked(puzzleCode), true);
            SetButtonSize(loadBtn, 40f, 34f);

            if (isDeletePendingForRow)
            {
                var confirmBtn = CreateButton(colGO.transform, "ConfirmDeleteButton", "Yes",
                    () => OnDeleteConfirmClicked(puzzleId), false);
                SetButtonSize(confirmBtn, 36f, 34f);

                var confirmBtnImage = confirmBtn.GetComponent<Image>();
                if (confirmBtnImage != null)
                {
                    confirmBtnImage.color = new Color(0.80f, 0.22f, 0.22f, 1f);
                }

                var confirmBtnText = confirmBtn.GetComponentInChildren<Text>();
                if (confirmBtnText != null)
                {
                    confirmBtnText.color = Color.white;
                }

                var cancelBtn = CreateButton(colGO.transform, "CancelDeleteButton", "No",
                    OnDeleteCancelClicked, false);
                SetButtonSize(cancelBtn, 36f, 34f);

                return;
            }

            // Edit button opens puzzle creation mode with this puzzle preloaded.
            var editBtn = CreateButton(colGO.transform, "EditButton", "Edit",
                () => OnEditClicked(puzzleCode), false);
            SetButtonSize(editBtn, 40f, 34f);

            // Move Up button.
            var upBtn = CreateButton(colGO.transform, "UpButton", "↑",
                () => OnMoveUpClicked(puzzleId), false);
            SetButtonSize(upBtn, 24f, 34f);
            if (isFirst)
            {
                var upImage = upBtn.GetComponent<Image>();
                if (upImage != null) upImage.color = new Color(0.40f, 0.40f, 0.40f, 0.5f);
                var upButtonComp = upBtn.GetComponent<Button>();
                if (upButtonComp != null) upButtonComp.interactable = false;
            }

            // Move Down button.
            var downBtn = CreateButton(colGO.transform, "DownButton", "↓",
                () => OnMoveDownClicked(puzzleId), false);
            SetButtonSize(downBtn, 24f, 34f);
            if (isLast)
            {
                var downImage = downBtn.GetComponent<Image>();
                if (downImage != null) downImage.color = new Color(0.40f, 0.40f, 0.40f, 0.5f);
                var downButtonComp = downBtn.GetComponent<Button>();
                if (downButtonComp != null) downButtonComp.interactable = false;
            }

            // Delete button.
            var deleteBtn = CreateButton(colGO.transform, "DeleteButton", "✕",
                () => OnDeleteClicked(puzzleId), false);
            SetButtonSize(deleteBtn, 24f, 34f);
            var deleteBtnImage = deleteBtn.GetComponent<Image>();
            if (deleteBtnImage != null)
            {
                deleteBtnImage.color = ColourButtonDanger;
            }

            var deleteBtnText = deleteBtn.GetComponentInChildren<Text>();
            if (deleteBtnText != null)
            {
                deleteBtnText.color = Color.white;
            }
        }

        // ------------------------------------------------------------------ //
        //  UI Helpers
        // ------------------------------------------------------------------ //

        private static Transform CreateHorizontalRow(Transform parent, string name, float height)
        {
            var rowGO = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            rowGO.transform.SetParent(parent, false);

            var layout = rowGO.AddComponent<LayoutElement>();
            layout.preferredHeight = height;
            layout.minHeight = height;
            layout.flexibleHeight = 0f;

            var hlg = rowGO.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(0, 0, 0, 0);
            hlg.spacing = 8f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            return rowGO.transform;
        }

        private static GameObject CreateButton(
            Transform parent, string name, string label,
            UnityEngine.Events.UnityAction onClick, bool primary)
        {
            var btnGO = new GameObject(name,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            btnGO.transform.SetParent(parent, false);

            var image = btnGO.GetComponent<Image>();
            image.color = primary ? ColourButtonPrimary : ColourButtonSecondary;

            var button = btnGO.GetComponent<Button>();
            button.onClick.AddListener(onClick);

            var textGO = new GameObject("Text",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textGO.transform.SetParent(btnGO.transform, false);

            var textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textGO.GetComponent<Text>();
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = primary ? Color.black : new Color(0.1f, 0.1f, 0.1f, 1f);
            text.fontSize = 14;
            text.fontStyle = FontStyle.Bold;
            text.font = GetFont();

            return btnGO;
        }

        private static void SetButtonSize(GameObject btn, float width, float height)
        {
            if (btn == null) return;

            var layout = btn.GetComponent<LayoutElement>();
            if (layout == null)
            {
                layout = btn.AddComponent<LayoutElement>();
            }

            layout.preferredWidth = width;
            layout.minWidth = width;
            layout.preferredHeight = height;
            layout.minHeight = height;
            layout.flexibleWidth = 0f;
            layout.flexibleHeight = 0f;
        }

        private static InputField BuildInputField(
            Transform parent, string name, string placeholder,
            float preferredWidth, float flexibleWidth)
        {
            var inputGO = new GameObject(name,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(InputField));
            inputGO.transform.SetParent(parent, false);

            var layout = inputGO.AddComponent<LayoutElement>();
            layout.preferredWidth = preferredWidth;
            layout.preferredHeight = 34f;
            layout.minHeight = 34f;
            layout.flexibleWidth = flexibleWidth;

            var image = inputGO.GetComponent<Image>();
            image.color = ColourInputBg;

            var textGO = new GameObject("Text",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textGO.transform.SetParent(inputGO.transform, false);

            var textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8f, 4f);
            textRect.offsetMax = new Vector2(-8f, -4f);

            var text = textGO.GetComponent<Text>();
            text.alignment = TextAnchor.MiddleLeft;
            text.color = ColourInputText;
            text.fontSize = 13;
            text.font = GetFont();

            var placeholderGO = new GameObject("Placeholder",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            placeholderGO.transform.SetParent(inputGO.transform, false);

            var placeholderRect = placeholderGO.GetComponent<RectTransform>();
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = new Vector2(8f, 4f);
            placeholderRect.offsetMax = new Vector2(-8f, -4f);

            var placeholderText = placeholderGO.GetComponent<Text>();
            placeholderText.text = placeholder;
            placeholderText.alignment = TextAnchor.MiddleLeft;
            placeholderText.color = ColourInputPlaceholder;
            placeholderText.fontSize = 12;
            placeholderText.font = GetFont();

            var inputField = inputGO.GetComponent<InputField>();
            inputField.textComponent = text;
            inputField.placeholder = placeholderText;
            inputField.lineType = InputField.LineType.SingleLine;

            return inputField;
        }

        private void DestroyPreviewTextures()
        {
            foreach (var tex in _previewTextures)
            {
                if (tex != null)
                {
                    Destroy(tex);
                }
            }

            _previewTextures.Clear();
        }

        private static Font GetFont()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
    }
}
