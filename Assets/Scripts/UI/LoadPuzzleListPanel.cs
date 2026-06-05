using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using Sudoku.Models;
using UnityEngine;
using UnityEngine.UI;

namespace Sudoku.Scripts.UI
{
    /**
     * Full-screen modal panel that lists solved-state XML puzzle saves.
     *
     * Features:
     * - Scrollable list sorted newest-first.
     * - Preview + puzzle code + saved date/time + difficulty.
     * - Load action and delete action with confirmation.
     */
    [DisallowMultipleComponent]
    public class LoadPuzzleListPanel : MonoBehaviour
    {
        /** Fired with the solved-state payload when user clicks Load. */
        public event Action<SolvedPuzzleStateExport> OnPuzzleLoadRequested;

        private static readonly Color ColourOverlay = new Color(0f, 0f, 0f, 0.72f);
        private static readonly Color ColourCard = new Color(0.08f, 0.08f, 0.08f, 0.97f);
        private static readonly Color ColourDivider = new Color(0.22f, 0.22f, 0.22f, 1f);
        private static readonly Color ColourButtonPrimary = new Color(0.90f, 0.70f, 0.18f, 1f);
        private static readonly Color ColourButtonSecondary = new Color(0.83f, 0.85f, 0.88f, 1f);
        private static readonly Color ColourButtonDanger = new Color(0.80f, 0.22f, 0.22f, 1f);
        private static readonly Color ColourEntryBg = new Color(0.13f, 0.13f, 0.13f, 1f);
        private static readonly Color ColourScrollbar = new Color(0.35f, 0.35f, 0.35f, 1f);
        private static readonly Color ColourScrollHandle = new Color(0.60f, 0.60f, 0.60f, 1f);

        private static readonly Color PreviewColourEmpty = new Color(0.18f, 0.18f, 0.18f);
        private static readonly Color PreviewColourFilled = new Color(0.90f, 0.70f, 0.18f);
        private static readonly Color PreviewColourBoxLine = new Color(0.30f, 0.30f, 0.30f);

        private const int PreviewCellPx = 2;
        private const int PreviewSize = 9 * PreviewCellPx;

        private GameObject _overlay;
        private Transform _scrollContent;
        private ScrollRect _scrollRect;
        private GameObject _emptyLabel;
        private LayoutElement _emptyLabelLayout;
        private Text _emptyLabelText;
        private string _pendingDeleteFilePath = string.Empty;

        private readonly List<Texture2D> _previewTextures = new List<Texture2D>();

        private class LoadPuzzleEntry
        {
            public string FilePath;
            public SolvedPuzzleStateExport State;
            public DateTime SavedAtUtc;
            public string DifficultyLabel;
        }

        /**
         * Build panel UI under the provided canvas.
         *
         * @param canvas Canvas root for modal placement.
         */
        public void Initialize(Canvas canvas)
        {
            if (canvas == null)
            {
                Debug.LogWarning("LoadPuzzleListPanel: Initialize called with null canvas.");
                return;
            }

            BuildOverlay(canvas.transform);
            Close();
        }

        private void OnDestroy()
        {
            DestroyPreviewTextures();
        }

        /** Show modal and refresh list from disk. */
        public void Open()
        {
            gameObject.SetActive(true);
            if (_overlay != null)
            {
                _overlay.SetActive(true);
            }

            RefreshList();
        }

        /** Hide modal. */
        public void Close()
        {
            if (_overlay != null)
            {
                _overlay.SetActive(false);
            }

            gameObject.SetActive(false);
        }

        private void RefreshList()
        {
            if (_scrollContent == null)
            {
                return;
            }

            DestroyPreviewTextures();
            for (int i = _scrollContent.childCount - 1; i >= 0; i--)
            {
                var child = _scrollContent.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                if (_emptyLabel != null && child.gameObject == _emptyLabel)
                {
                    continue;
                }

                child.SetParent(null, false);
                Destroy(child.gameObject);
            }

            List<LoadPuzzleEntry> entries = LoadEntriesNewestFirst();

            if (!string.IsNullOrEmpty(_pendingDeleteFilePath) && !File.Exists(_pendingDeleteFilePath))
            {
                _pendingDeleteFilePath = string.Empty;
            }

            if (_emptyLabel != null)
            {
                bool isEmpty = entries.Count == 0;
                _emptyLabel.SetActive(true);

                if (_emptyLabelLayout != null)
                {
                    _emptyLabelLayout.ignoreLayout = !isEmpty;
                    _emptyLabelLayout.preferredHeight = isEmpty ? 64f : 0f;
                    _emptyLabelLayout.minHeight = isEmpty ? 64f : 0f;
                }

                if (_emptyLabelText != null)
                {
                    _emptyLabelText.text = isEmpty
                        ? "No valid solved-state XML saves found."
                        : string.Empty;
                }
            }

            for (int i = 0; i < entries.Count; i++)
            {
                BuildEntryRow(_scrollContent, entries[i]);
            }

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

        private void OnLoadClicked(LoadPuzzleEntry entry)
        {
            if (entry == null || entry.State == null)
            {
                return;
            }

            _pendingDeleteFilePath = string.Empty;
            OnPuzzleLoadRequested?.Invoke(entry.State);
            Close();
        }

        private void OnDeleteClicked(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            _pendingDeleteFilePath = filePath;
            RefreshList();
        }

        private void OnDeleteConfirmClicked(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"LoadPuzzleListPanel: Failed to delete save file '{filePath}'. {ex.Message}");
            }

            _pendingDeleteFilePath = string.Empty;
            RefreshList();
        }

        private void OnDeleteCancelClicked()
        {
            _pendingDeleteFilePath = string.Empty;
            RefreshList();
        }

        private List<LoadPuzzleEntry> LoadEntriesNewestFirst()
        {
            var entries = new List<LoadPuzzleEntry>();
            string directory = SolvedPuzzleStateXmlExporter.GetOutputDirectory();
            if (!Directory.Exists(directory))
            {
                return entries;
            }

            string[] files;
            try
            {
                files = Directory.GetFiles(directory, "*.xml", SearchOption.TopDirectoryOnly);
            }
            catch (Exception ex)
            {
                Debug.LogError($"LoadPuzzleListPanel: Failed reading save directory '{directory}'. {ex.Message}");
                return entries;
            }

            for (int i = 0; i < files.Length; i++)
            {
                string filePath = files[i];
                if (!TryLoadSolvedState(filePath, out var state))
                {
                    continue;
                }

                string currentCode = state.PuzzleCode != null ? state.PuzzleCode.Trim() : string.Empty;
                if (string.IsNullOrEmpty(currentCode))
                {
                    continue;
                }

                if (PuzzleCodeGenerator.DecodeBoardFromCode(currentCode) == null)
                {
                    continue;
                }

                string difficultySourceCode = !string.IsNullOrWhiteSpace(state.InitialPuzzleCode)
                    ? state.InitialPuzzleCode.Trim()
                    : currentCode;

                string difficultyLabel = "Unknown";
                var analysis = SavedPuzzleAnalysisGenerator.AnalyzeFromCode(difficultySourceCode);
                if (analysis != null && !string.IsNullOrWhiteSpace(analysis.DifficultyLabel))
                {
                    difficultyLabel = analysis.DifficultyLabel;
                }

                DateTime savedUtc = state.SavedAtUtcTicks > 0
                    ? new DateTime(state.SavedAtUtcTicks, DateTimeKind.Utc)
                    : File.GetLastWriteTimeUtc(filePath);

                entries.Add(new LoadPuzzleEntry
                {
                    FilePath = filePath,
                    State = state,
                    SavedAtUtc = savedUtc,
                    DifficultyLabel = difficultyLabel,
                });
            }

            entries.Sort((a, b) => b.SavedAtUtc.CompareTo(a.SavedAtUtc));
            return entries;
        }

        private static bool TryLoadSolvedState(string filePath, out SolvedPuzzleStateExport state)
        {
            state = null;
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return false;
            }

            try
            {
                var serializer = new XmlSerializer(typeof(SolvedPuzzleStateExport));
                using (var stream = File.OpenRead(filePath))
                {
                    state = serializer.Deserialize(stream) as SolvedPuzzleStateExport;
                }

                return state != null;
            }
            catch
            {
                return false;
            }
        }

        private void BuildOverlay(Transform canvasTransform)
        {
            _overlay = new GameObject("LoadPuzzleOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            _overlay.transform.SetParent(transform, false);

            var overlayRect = _overlay.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;

            var overlayImage = _overlay.GetComponent<Image>();
            overlayImage.color = ColourOverlay;
            overlayImage.raycastTarget = true;

            var overlayButton = _overlay.GetComponent<Button>();
            overlayButton.transition = Selectable.Transition.None;
            overlayButton.onClick.AddListener(Close);

            var card = new GameObject("LoadPuzzleCard", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            card.transform.SetParent(_overlay.transform, false);

            var cardRect = card.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(920f, 640f);
            cardRect.anchoredPosition = new Vector2(0f, 30f);

            var cardImage = card.GetComponent<Image>();
            cardImage.color = ColourCard;

            var cardBlocker = card.AddComponent<Button>();
            cardBlocker.transition = Selectable.Transition.None;

            BuildCardContents(card.transform, cardRect.sizeDelta);
        }

        private void BuildCardContents(Transform card, Vector2 cardSize)
        {
            var vlg = card.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(14, 14, 0, 10);
            vlg.spacing = 2f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            BuildHeaderRow(card);
            BuildDivider(card);
            BuildScrollView(card, cardSize);
        }

        private void BuildHeaderRow(Transform parent)
        {
            var row = CreateHorizontalRow(parent, "HeaderRow", 36f);

            var titleGO = new GameObject("TitleLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            titleGO.transform.SetParent(row, false);

            var titleLayout = titleGO.AddComponent<LayoutElement>();
            titleLayout.preferredHeight = 36f;
            titleLayout.flexibleWidth = 1f;

            var titleText = titleGO.GetComponent<Text>();
            titleText.text = "Load Puzzle";
            titleText.alignment = TextAnchor.MiddleLeft;
            titleText.color = Color.white;
            titleText.fontSize = 24;
            titleText.fontStyle = FontStyle.Bold;
            titleText.font = GetFont();

            var closeBtn = CreateButton(row, "HeaderCloseButton", "X", Close, false);
            SetButtonSize(closeBtn, 26f, 26f);
        }

        private static void BuildDivider(Transform parent)
        {
            var dividerGO = new GameObject("Divider", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            dividerGO.transform.SetParent(parent, false);

            var layout = dividerGO.AddComponent<LayoutElement>();
            layout.preferredHeight = 1f;
            layout.minHeight = 1f;
            layout.flexibleHeight = 0f;

            var image = dividerGO.GetComponent<Image>();
            image.color = ColourDivider;
        }

        private void BuildScrollView(Transform parent, Vector2 cardSize)
        {
            var scrollViewGO = new GameObject("ScrollView", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollViewGO.transform.SetParent(parent, false);

            var scrollLayout = scrollViewGO.AddComponent<LayoutElement>();
            scrollLayout.preferredHeight = cardSize.y - 52f;
            scrollLayout.minHeight = 220f;
            scrollLayout.flexibleHeight = 1f;

            var scrollImage = scrollViewGO.GetComponent<Image>();
            scrollImage.color = new Color(0f, 0f, 0f, 0f);
            scrollImage.raycastTarget = false;

            var viewportGO = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewportGO.transform.SetParent(scrollViewGO.transform, false);

            var viewportRect = viewportGO.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = new Vector2(1f, 1f);
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = new Vector2(-14f, 0f);

            var viewportImage = viewportGO.GetComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0.01f);

            var viewportMask = viewportGO.GetComponent<Mask>();
            viewportMask.showMaskGraphic = false;

            var contentGO = new GameObject("Content", typeof(RectTransform), typeof(CanvasRenderer));
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

            var scrollbar = BuildScrollbar(scrollViewGO.transform);

            var scrollRect = scrollViewGO.GetComponent<ScrollRect>();
            scrollRect.content = contentRect;
            scrollRect.viewport = viewportRect;
            scrollRect.verticalScrollbar = scrollbar;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 40f;
            _scrollRect = scrollRect;

            var emptyGO = new GameObject("EmptyLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            emptyGO.transform.SetParent(contentGO.transform, false);
            _emptyLabel = emptyGO;

            _emptyLabelLayout = emptyGO.AddComponent<LayoutElement>();
            _emptyLabelLayout.preferredHeight = 64f;
            _emptyLabelLayout.minHeight = 64f;

            _emptyLabelText = emptyGO.GetComponent<Text>();
            _emptyLabelText.text = "No valid solved-state XML saves found.";
            _emptyLabelText.alignment = TextAnchor.MiddleCenter;
            _emptyLabelText.color = new Color(0.55f, 0.55f, 0.55f, 1f);
            _emptyLabelText.fontSize = 14;
            _emptyLabelText.font = GetFont();
        }

        private static Scrollbar BuildScrollbar(Transform scrollViewParent)
        {
            var scrollbarGO = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            scrollbarGO.transform.SetParent(scrollViewParent, false);

            var scrollbarRect = scrollbarGO.GetComponent<RectTransform>();
            scrollbarRect.anchorMin = new Vector2(1f, 0f);
            scrollbarRect.anchorMax = new Vector2(1f, 1f);
            scrollbarRect.pivot = new Vector2(1f, 0.5f);
            scrollbarRect.offsetMin = new Vector2(-12f, 0f);
            scrollbarRect.offsetMax = new Vector2(0f, 0f);

            var scrollbarImage = scrollbarGO.GetComponent<Image>();
            scrollbarImage.color = ColourScrollbar;

            var slidingAreaGO = new GameObject("SlidingArea", typeof(RectTransform));
            slidingAreaGO.transform.SetParent(scrollbarGO.transform, false);

            var slidingRect = slidingAreaGO.GetComponent<RectTransform>();
            slidingRect.anchorMin = Vector2.zero;
            slidingRect.anchorMax = Vector2.one;
            slidingRect.offsetMin = new Vector2(0f, 7f);
            slidingRect.offsetMax = new Vector2(0f, -7f);

            var handleGO = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handleGO.transform.SetParent(slidingAreaGO.transform, false);

            var handleRect = handleGO.GetComponent<RectTransform>();
            handleRect.anchorMin = new Vector2(0f, 0f);
            handleRect.anchorMax = new Vector2(1f, 1f);
            handleRect.offsetMin = Vector2.zero;
            handleRect.offsetMax = Vector2.zero;

            var handleImage = handleGO.GetComponent<Image>();
            handleImage.color = ColourScrollHandle;

            var scrollbar = scrollbarGO.GetComponent<Scrollbar>();
            scrollbar.handleRect = handleGO.GetComponent<RectTransform>();
            scrollbar.direction = Scrollbar.Direction.TopToBottom;

            return scrollbar;
        }

        private void BuildEntryRow(Transform parent, LoadPuzzleEntry entry)
        {
            if (entry == null || entry.State == null)
            {
                return;
            }

            string rowNameToken = Path.GetFileNameWithoutExtension(entry.FilePath) ?? Guid.NewGuid().ToString("N");
            var rowGO = new GameObject($"Entry_{rowNameToken}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            rowGO.transform.SetParent(parent, false);

            var rowLayout = rowGO.AddComponent<LayoutElement>();
            rowLayout.preferredHeight = 92f;
            rowLayout.minHeight = 92f;
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

            BuildMiniPreview(rowGO.transform, entry.State.PuzzleCode);
            BuildInfoColumn(rowGO.transform, entry);
            BuildActionsColumn(rowGO.transform, entry);
        }

        private void BuildMiniPreview(Transform parent, string code)
        {
            var previewGO = new GameObject("MiniPreview", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            previewGO.transform.SetParent(parent, false);

            var layout = previewGO.AddComponent<LayoutElement>();
            layout.preferredWidth = 72f;
            layout.minWidth = 72f;
            layout.preferredHeight = 72f;
            layout.minHeight = 72f;
            layout.flexibleWidth = 0f;
            layout.flexibleHeight = 0f;

            var rawImage = previewGO.GetComponent<RawImage>();
            var texture = BuildPreviewTexture(code);
            rawImage.texture = texture;
            rawImage.uvRect = new Rect(0f, 0f, 1f, 1f);
        }

        private Texture2D BuildPreviewTexture(string code)
        {
            var texture = new Texture2D(PreviewSize, PreviewSize, TextureFormat.RGB24, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "LoadPuzzlePreview"
            };

            _previewTextures.Add(texture);

            var pixels = new Color[PreviewSize * PreviewSize];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = PreviewColourEmpty;
            }

            var decoded = string.IsNullOrEmpty(code) ? null : PuzzleCodeGenerator.DecodeBoardFromCode(code);
            if (decoded?.Cells != null)
            {
                for (int row = 0; row < decoded.Size && row < 9; row++)
                {
                    for (int col = 0; col < decoded.Size && col < 9; col++)
                    {
                        var cell = decoded.Cells[row, col];
                        bool filled = cell != null && cell.Value.HasValue;
                        Color cellColor = filled ? PreviewColourFilled : PreviewColourEmpty;

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

                DrawPreviewBoxLines(pixels);
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static void DrawPreviewBoxLines(Color[] pixels)
        {
            int[] colSeps = { 3, 6 };
            foreach (int col in colSeps)
            {
                int px = col * PreviewCellPx;
                for (int py = 0; py < PreviewSize; py++)
                {
                    pixels[py * PreviewSize + px] = PreviewColourBoxLine;
                }
            }

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

        private void BuildInfoColumn(Transform parent, LoadPuzzleEntry entry)
        {
            var colGO = new GameObject("InfoColumn", typeof(RectTransform), typeof(CanvasRenderer));
            colGO.transform.SetParent(parent, false);

            var colLayout = colGO.AddComponent<LayoutElement>();
            colLayout.flexibleWidth = 1f;
            colLayout.preferredHeight = 76f;
            colLayout.minHeight = 76f;

            var codeGO = new GameObject("CodeLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            codeGO.transform.SetParent(colGO.transform, false);

            var codeRect = codeGO.GetComponent<RectTransform>();
            codeRect.anchorMin = new Vector2(0f, 0.50f);
            codeRect.anchorMax = new Vector2(1f, 1f);
            codeRect.offsetMin = Vector2.zero;
            codeRect.offsetMax = Vector2.zero;

            var codeText = codeGO.GetComponent<Text>();
            codeText.text = $"Code: {entry.State.PuzzleCode}";
            codeText.alignment = TextAnchor.LowerLeft;
            codeText.color = Color.white;
            codeText.fontSize = 13;
            codeText.fontStyle = FontStyle.Bold;
            codeText.font = GetFont();

            var metaGO = new GameObject("MetaLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            metaGO.transform.SetParent(colGO.transform, false);

            var metaRect = metaGO.GetComponent<RectTransform>();
            metaRect.anchorMin = new Vector2(0f, 0f);
            metaRect.anchorMax = new Vector2(1f, 0.5f);
            metaRect.offsetMin = Vector2.zero;
            metaRect.offsetMax = Vector2.zero;

            var metaText = metaGO.GetComponent<Text>();
            string localDate = entry.SavedAtUtc.ToLocalTime().ToString("dd MMM yyyy, HH:mm");
            metaText.text = $"Saved: {localDate}  |  {entry.DifficultyLabel}";
            metaText.alignment = TextAnchor.UpperLeft;
            metaText.color = new Color(0.62f, 0.62f, 0.62f, 1f);
            metaText.fontSize = 11;
            metaText.font = GetFont();
        }

        private void BuildActionsColumn(Transform parent, LoadPuzzleEntry entry)
        {
            var colGO = new GameObject("ActionsColumn", typeof(RectTransform), typeof(CanvasRenderer));
            colGO.transform.SetParent(parent, false);

            var colLayout = colGO.AddComponent<LayoutElement>();
            colLayout.preferredWidth = 168f;
            colLayout.minWidth = 168f;
            colLayout.preferredHeight = 76f;
            colLayout.flexibleWidth = 0f;

            var hlg = colGO.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(0, 0, 0, 0);
            hlg.spacing = 4f;
            hlg.childAlignment = TextAnchor.MiddleRight;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            var spacerGO = new GameObject("Spacer", typeof(RectTransform), typeof(CanvasRenderer));
            spacerGO.transform.SetParent(colGO.transform, false);
            var spacerLayout = spacerGO.AddComponent<LayoutElement>();
            spacerLayout.flexibleWidth = 1f;
            spacerLayout.preferredHeight = 76f;

            bool isDeletePendingForRow = !string.IsNullOrEmpty(_pendingDeleteFilePath) &&
                                         string.Equals(_pendingDeleteFilePath, entry.FilePath, StringComparison.OrdinalIgnoreCase);

            var loadBtn = CreateButton(colGO.transform, "LoadButton", "Load", () => OnLoadClicked(entry), true);
            SetButtonSize(loadBtn, 48f, 34f);

            if (isDeletePendingForRow)
            {
                var confirmBtn = CreateButton(colGO.transform, "ConfirmDeleteButton", "Yes", () => OnDeleteConfirmClicked(entry.FilePath), false);
                SetButtonSize(confirmBtn, 36f, 34f);
                var confirmBtnImage = confirmBtn.GetComponent<Image>();
                if (confirmBtnImage != null)
                {
                    confirmBtnImage.color = ColourButtonDanger;
                }

                var confirmBtnText = confirmBtn.GetComponentInChildren<Text>();
                if (confirmBtnText != null)
                {
                    confirmBtnText.color = Color.white;
                }

                var cancelBtn = CreateButton(colGO.transform, "CancelDeleteButton", "No", OnDeleteCancelClicked, false);
                SetButtonSize(cancelBtn, 36f, 34f);
                return;
            }

            var deleteBtn = CreateButton(colGO.transform, "DeleteButton", "X", () => OnDeleteClicked(entry.FilePath), false);
            SetButtonSize(deleteBtn, 32f, 34f);
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

        private static GameObject CreateButton(Transform parent, string name, string label, UnityEngine.Events.UnityAction onClick, bool primary)
        {
            var btnGO = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            btnGO.transform.SetParent(parent, false);

            var image = btnGO.GetComponent<Image>();
            image.color = primary ? ColourButtonPrimary : ColourButtonSecondary;

            var button = btnGO.GetComponent<Button>();
            button.onClick.AddListener(onClick);

            var textGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
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
            if (btn == null)
            {
                return;
            }

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
