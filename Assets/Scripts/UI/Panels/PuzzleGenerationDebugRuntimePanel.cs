using System;
using System.Collections.Generic;
using System.Text;
using Sudoku.Models;
using Sudoku.Solver;
using Sudoku.Solver.Rules;
using Sudoku.Solver.Unsolver;
using UnityEngine;
using UnityEngine.UI;

namespace Sudoku.UI.Panels
{
    [DisallowMultipleComponent]
    public class PuzzleGenerationDebugRuntimePanel : MonoBehaviour
    {
        private const int DebugGeneratorRetries = 1;
        private GameObject _panelGO;
        private Text _statusText;
        private Text _detailsText;
        private ScrollRect _detailsScrollRect;
        private RectTransform _detailsViewportRT;
        private RectTransform _detailsContentRT;
        private RectTransform _detailsTextRT;
        private SolverRunner _runner;
        private PuzzleGenerationDebugSession _session;
        private int _currentIndex = -1;

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            SetupPanel();
            RefreshPanel();
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (_runner == null)
            {
                _runner = FindAnyObjectByType<SolverRunner>();
            }

            RefreshPanel();
        }

        private void SetupPanel()
        {
            if (_panelGO != null)
            {
                return;
            }

            RectTransform parent = null;
            var side = GameObject.Find("SidePanel");
            if (side != null)
            {
                parent = side.transform as RectTransform;
            }

            if (parent == null)
            {
                var canvas = FindAnyObjectByType<Canvas>();
                if (canvas != null)
                {
                    parent = canvas.transform as RectTransform;
                }
            }

            if (parent == null)
            {
                return;
            }

            _panelGO = new GameObject("PuzzleGenerationDebugPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _panelGO.transform.SetParent(parent, false);
            var panelRT = _panelGO.GetComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0.5f, 0f);
            panelRT.anchorMax = new Vector2(0.5f, 0f);
            panelRT.pivot = new Vector2(0.5f, 0f);
            panelRT.sizeDelta = new Vector2(560f, 300f);
            panelRT.anchoredPosition = new Vector2(0f, 140f);

            var image = _panelGO.GetComponent<Image>();
            image.color = new Color(0.08f, 0.08f, 0.12f, 0.94f);

            CreateText(_panelGO.transform, "Header", "Generate Debug", 16, TextAnchor.UpperLeft, new Vector2(12f, -10f), new Vector2(-12f, -34f), out _);
            CreateText(_panelGO.transform, "Status", string.Empty, 13, TextAnchor.UpperLeft, new Vector2(12f, -38f), new Vector2(-12f, -96f), out _statusText);
            BuildDetailsScrollArea(_panelGO.transform);

            var buttons = new GameObject("Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            buttons.transform.SetParent(_panelGO.transform, false);
            var buttonsRT = buttons.GetComponent<RectTransform>();
            buttonsRT.anchorMin = new Vector2(0f, 0f);
            buttonsRT.anchorMax = new Vector2(1f, 0f);
            buttonsRT.pivot = new Vector2(0.5f, 0f);
            buttonsRT.offsetMin = new Vector2(12f, 10f);
            buttonsRT.offsetMax = new Vector2(-12f, 42f);

            var layout = buttons.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;
            layout.childAlignment = TextAnchor.MiddleCenter;

            CreateButton(buttons.transform, "Start", 72f, StartDebugSession);
            CreateButton(buttons.transform, "Step", 60f, Step);
            CreateButton(buttons.transform, "Step Into", 76f, StepInto);
            CreateButton(buttons.transform, "Next Rule", 76f, NextRule);
            CreateButton(buttons.transform, "Reset", 60f, ResetSession);
        }

        private void StartDebugSession()
        {
            _runner = FindAnyObjectByType<SolverRunner>();
            if (_runner == null)
            {
                return;
            }

            _runner.EnsureEngine();
            if (_runner.Registry == null)
            {
                return;
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
                return;
            }

            var random = new System.Random();
            var solved = RandomSolvedBoardGenerator.GenerateRandomSolvedBoard(random);
            var tracer = new PuzzleGenerationDebugTracer();
            var generator = new PuzzleGenerator(
                maxRetries: DebugGeneratorRetries,
                requireNonNakedContribution: true,
                debugTracer: tracer);

            try
            {
                generator.Generate(solved, enabledRules, random);
                _session = tracer.BuildSession();
                _currentIndex = _session.Events.Count > 0 ? 0 : -1;
                ApplyCurrentEvent();
            }
            catch (Exception ex)
            {
                _session = tracer.BuildSession();
                _currentIndex = _session.Events.Count > 0 ? 0 : -1;
                ApplyCurrentEvent();
                if (_statusText != null)
                {
                    _statusText.text = "Generation failed: " + ex.Message;
                }
            }
        }

        private void Step()
        {
            if (_session == null)
            {
                return;
            }

            if (_currentIndex + 1 >= _session.Events.Count)
            {
                return;
            }

            _currentIndex++;
            ApplyCurrentEvent();
        }

        private void StepInto()
        {
            if (_session == null)
            {
                return;
            }

            int next = _session.FindStepIntoIndex(_currentIndex);
            if (next < 0)
            {
                Step();
                return;
            }

            _currentIndex = next;
            ApplyCurrentEvent();
        }

        private void NextRule()
        {
            if (_session == null)
            {
                return;
            }

            int next = _session.FindNextDifferentTopLevelRuleIndex(_currentIndex);
            if (next < 0)
            {
                return;
            }

            _currentIndex = next;
            ApplyCurrentEvent();
        }

        private void ResetSession()
        {
            if (_session == null || _session.Events.Count == 0)
            {
                return;
            }

            _currentIndex = 0;
            ApplyCurrentEvent();
        }

        private void ApplyCurrentEvent()
        {
            if (_session == null || _currentIndex < 0 || _currentIndex >= _session.Events.Count)
            {
                return;
            }

            if (_runner == null)
            {
                _runner = FindAnyObjectByType<SolverRunner>();
            }

            if (_runner == null)
            {
                return;
            }

            var evt = _session.Events[_currentIndex];
            _runner.LoadDebugBoardSnapshot(evt.Snapshot, evt.HighlightResult);

            if (_statusText != null)
            {
                _statusText.text = BuildStatusText(evt);
            }

            if (_detailsText != null)
            {
                _detailsText.text = BuildDetailsText(evt);
            }

            RefreshDetailsLayout(resetScroll: true);
        }

        private void RefreshPanel()
        {
            if (_statusText == null || _detailsText == null)
            {
                return;
            }

            if (_session == null)
            {
                _statusText.text = "Start a debug generation session to inspect unsolve steps.";
                _detailsText.text = "Buttons: Start generates a traced puzzle. Step advances one event. Step Into enters nested rule work. Next Rule skips to the next top-level rule marker.";
                return;
            }

            if (_currentIndex >= 0 && _currentIndex < _session.Events.Count)
            {
                var evt = _session.Events[_currentIndex];
                _statusText.text = BuildStatusText(evt);
                _detailsText.text = BuildDetailsText(evt);
                RefreshDetailsLayout(resetScroll: false);
            }
        }

        private string BuildStatusText(PuzzleGenerationDebugEvent evt)
        {
            return $"Step {_currentIndex + 1}/{_session.Events.Count} | Depth {evt.Depth} | {evt.Kind} | {evt.RuleName}\n{evt.Title}";
        }

        private static string BuildDetailsText(PuzzleGenerationDebugEvent evt)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(evt.Description))
            {
                sb.AppendLine(evt.Description);
                sb.AppendLine();
            }

            if (evt.HighlightResult == null || evt.HighlightResult.Changes == null || evt.HighlightResult.Changes.Count == 0)
            {
                AppendUsedCellSummary(sb, evt);
                sb.Append("No board changes recorded for this step.");
                return sb.ToString();
            }

            for (int i = 0; i < evt.HighlightResult.Changes.Count; i++)
            {
                var change = evt.HighlightResult.Changes[i];
                sb.Append("r").Append(change.Row + 1).Append("c").Append(change.Column + 1).Append(':');

                if (change.ClearValue)
                {
                    sb.Append(" clear value");
                }
                else if (change.NewValue.HasValue || change.OldValue.HasValue)
                {
                    sb.Append(' ').Append(change.OldValue.HasValue ? change.OldValue.Value.ToString() : ".");
                    sb.Append(" -> ");
                    sb.Append(change.NewValue.HasValue ? change.NewValue.Value.ToString() : ".");
                }

                if (change.RemovedCandidates != null && change.RemovedCandidates.Count > 0)
                {
                    sb.Append("  -[").Append(string.Join(",", change.RemovedCandidates)).Append(']');
                }

                if (change.AddedCandidates != null && change.AddedCandidates.Count > 0)
                {
                    sb.Append("  +[").Append(string.Join(",", change.AddedCandidates)).Append(']');
                }

                if (i < evt.HighlightResult.Changes.Count - 1)
                {
                    sb.AppendLine();
                }
            }

            AppendUsedCellSummary(sb, evt);

            return sb.ToString();
        }

        private static void AppendUsedCellSummary(StringBuilder sb, PuzzleGenerationDebugEvent evt)
        {
            if (evt?.HighlightResult?.UsedCells == null || evt.HighlightResult.UsedCells.Count == 0)
            {
                return;
            }

            if (sb.Length > 0 && sb[sb.Length - 1] != '\n')
            {
                sb.AppendLine();
            }

            if (sb.Length > 0)
            {
                sb.AppendLine();
            }

            sb.Append("Highlights: ");
            for (int i = 0; i < evt.HighlightResult.UsedCells.Count; i++)
            {
                var used = evt.HighlightResult.UsedCells[i];
                if (used == null)
                {
                    continue;
                }

                if (i > 0)
                {
                    sb.Append(" | ");
                }

                string tag = string.IsNullOrWhiteSpace(used.HighlightTag) ? "Info" : used.HighlightTag;
                sb.Append(tag)
                    .Append(": r")
                    .Append(used.Row + 1)
                    .Append("c")
                    .Append(used.Column + 1);
            }
        }

        private void RefreshDetailsLayout(bool resetScroll)
        {
            if (_detailsText == null || _detailsContentRT == null || _detailsTextRT == null || _detailsViewportRT == null)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            float preferredHeight = Mathf.Max(_detailsText.preferredHeight + 8f, _detailsViewportRT.rect.height);
            _detailsContentRT.sizeDelta = new Vector2(0f, preferredHeight);
            _detailsTextRT.sizeDelta = new Vector2(0f, preferredHeight);

            Canvas.ForceUpdateCanvases();
            if (resetScroll && _detailsScrollRect != null)
            {
                _detailsScrollRect.verticalNormalizedPosition = 1f;
            }
        }

        private static void CreateText(
            Transform parent,
            string name,
            string text,
            int fontSize,
            TextAnchor anchor,
            Vector2 offsetMin,
            Vector2 offsetMax,
            out Text label)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(offsetMin.x, offsetMax.y);
            rt.offsetMax = new Vector2(offsetMax.x, offsetMin.y);

            label = go.AddComponent<Text>();
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = anchor;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.supportRichText = true;
            label.color = Color.white;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (label.font == null)
            {
                label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
        }

        private void BuildDetailsScrollArea(Transform parent)
        {
            var scrollGO = new GameObject("DetailsScroll", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ScrollRect));
            scrollGO.transform.SetParent(parent, false);

            var scrollRT = scrollGO.GetComponent<RectTransform>();
            scrollRT.anchorMin = new Vector2(0f, 0f);
            scrollRT.anchorMax = new Vector2(1f, 1f);
            scrollRT.offsetMin = new Vector2(12f, 50f);
            scrollRT.offsetMax = new Vector2(-12f, -104f);

            var scrollBg = scrollGO.GetComponent<Image>();
            scrollBg.color = new Color(0.04f, 0.04f, 0.08f, 0.5f);

            var viewportGO = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D));
            viewportGO.transform.SetParent(scrollGO.transform, false);
            _detailsViewportRT = viewportGO.GetComponent<RectTransform>();
            _detailsViewportRT.anchorMin = Vector2.zero;
            _detailsViewportRT.anchorMax = Vector2.one;
            _detailsViewportRT.offsetMin = Vector2.zero;
            _detailsViewportRT.offsetMax = new Vector2(-18f, 0f);

            var viewportImage = viewportGO.GetComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0f);

            var contentGO = new GameObject("Content", typeof(RectTransform));
            contentGO.transform.SetParent(viewportGO.transform, false);
            _detailsContentRT = contentGO.GetComponent<RectTransform>();
            _detailsContentRT.anchorMin = new Vector2(0f, 1f);
            _detailsContentRT.anchorMax = new Vector2(1f, 1f);
            _detailsContentRT.pivot = new Vector2(0.5f, 1f);
            _detailsContentRT.offsetMin = new Vector2(0f, 0f);
            _detailsContentRT.offsetMax = new Vector2(0f, 0f);
            _detailsContentRT.sizeDelta = new Vector2(0f, 900f);

            var textGO = new GameObject("Details", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textGO.transform.SetParent(contentGO.transform, false);
            _detailsTextRT = textGO.GetComponent<RectTransform>();
            _detailsTextRT.anchorMin = new Vector2(0f, 1f);
            _detailsTextRT.anchorMax = new Vector2(1f, 1f);
            _detailsTextRT.pivot = new Vector2(0.5f, 1f);
            _detailsTextRT.offsetMin = new Vector2(0f, 0f);
            _detailsTextRT.offsetMax = new Vector2(0f, 0f);
            _detailsTextRT.sizeDelta = new Vector2(0f, 900f);

            _detailsText = textGO.GetComponent<Text>();
            _detailsText.text = string.Empty;
            _detailsText.fontSize = 12;
            _detailsText.alignment = TextAnchor.UpperLeft;
            _detailsText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _detailsText.verticalOverflow = VerticalWrapMode.Overflow;
            _detailsText.supportRichText = true;
            _detailsText.color = Color.white;
            _detailsText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_detailsText.font == null)
            {
                _detailsText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            var scrollbarGO = new GameObject("Scrollbar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Scrollbar));
            scrollbarGO.transform.SetParent(scrollGO.transform, false);
            var scrollbarRT = scrollbarGO.GetComponent<RectTransform>();
            scrollbarRT.anchorMin = new Vector2(1f, 0f);
            scrollbarRT.anchorMax = new Vector2(1f, 1f);
            scrollbarRT.pivot = new Vector2(1f, 1f);
            scrollbarRT.offsetMin = new Vector2(-16f, 2f);
            scrollbarRT.offsetMax = new Vector2(0f, -2f);

            var scrollbarImage = scrollbarGO.GetComponent<Image>();
            scrollbarImage.color = new Color(0.15f, 0.15f, 0.2f, 0.95f);

            var slidingAreaGO = new GameObject("SlidingArea", typeof(RectTransform));
            slidingAreaGO.transform.SetParent(scrollbarGO.transform, false);
            var slidingAreaRT = slidingAreaGO.GetComponent<RectTransform>();
            slidingAreaRT.anchorMin = Vector2.zero;
            slidingAreaRT.anchorMax = Vector2.one;
            slidingAreaRT.offsetMin = new Vector2(2f, 2f);
            slidingAreaRT.offsetMax = new Vector2(-2f, -2f);

            var handleGO = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            handleGO.transform.SetParent(slidingAreaGO.transform, false);
            var handleRT = handleGO.GetComponent<RectTransform>();
            handleRT.anchorMin = Vector2.zero;
            handleRT.anchorMax = Vector2.one;
            handleRT.offsetMin = Vector2.zero;
            handleRT.offsetMax = Vector2.zero;

            var handleImage = handleGO.GetComponent<Image>();
            handleImage.color = new Color(0.75f, 0.75f, 0.82f, 0.95f);

            var scrollbar = scrollbarGO.GetComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.targetGraphic = handleImage;
            scrollbar.handleRect = handleRT;

            _detailsScrollRect = scrollGO.GetComponent<ScrollRect>();
            _detailsScrollRect.viewport = _detailsViewportRT;
            _detailsScrollRect.content = _detailsContentRT;
            _detailsScrollRect.horizontal = false;
            _detailsScrollRect.vertical = true;
            _detailsScrollRect.scrollSensitivity = 24f;
            _detailsScrollRect.verticalScrollbar = scrollbar;
            _detailsScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        }

        private static void CreateButton(Transform parent, string text, float width, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(text + "Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            var layout = go.GetComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.preferredHeight = 28f;

            var img = go.GetComponent<Image>();
            img.color = new Color(0.9f, 0.9f, 0.9f, 1f);

            var btn = go.GetComponent<Button>();
            btn.onClick.AddListener(onClick);

            var textGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer));
            textGO.transform.SetParent(go.transform, false);
            var textRT = textGO.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;

            var txt = textGO.AddComponent<Text>();
            txt.text = text;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.black;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (txt.font == null)
            {
                txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            txt.fontSize = 12;
        }
    }
}
