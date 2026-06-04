using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Sudoku.Models;
using Sudoku.Solver;
using Sudoku.Solver.Rules;

namespace Sudoku.Scripts.UI
{
    /**
     * Runtime-only Change Log panel. Created on demand by `ChangeLogRuntimeControls`.
     * Lists grouped changes and allows jumping the board to any group's end index.
     */
    [DisallowMultipleComponent]
    public class ChangeLogPanelRuntime : MonoBehaviour
    {
        private static ChangeLogPanelRuntime _instance;
        private GameObject _panelGO;
        private RectTransform _contentRT;
        private int _lastChangeLogCount = -1;
        private int _lastGroupsHash = 0;
        private Canvas _panelCanvas;

        // Hover-seek state: tracks whether a row hover is in progress so the board
        // can be temporarily seeked to the hovered state and restored on exit.
        private bool _isHovering = false;
        private int _savedIndexBeforeHover = -1;

        public static void TogglePanel()
        {
            if (_instance == null)
            {
                // Create singleton host under Canvas
                var canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
                if (canvas == null)
                {
                    Debug.LogWarning("ChangeLogPanelRuntime: No Canvas found to attach panel to.");
                    return;
                }
                var host = new GameObject("ChangeLogPanelRuntime", typeof(RectTransform));
                // If a SidePanel exists, parent the host to the same parent so we can match its size/location.
                var side = GameObject.Find("SidePanel");
                if (side != null)
                {
                    var sideRT = side.transform as RectTransform;
                    // Parent the host to the Canvas's parent (scene root) to avoid inheriting any scale on the existing Canvas.
                    var parentForHost = canvas.transform.parent != null ? canvas.transform.parent : canvas.transform;
                    host.transform.SetParent(parentForHost, false);
                    // copy anchors/position/size so host covers the SidePanel area
                    if (sideRT != null)
                    {
                        var hostRT = host.GetComponent<RectTransform>();
                        hostRT.anchorMin = sideRT.anchorMin;
                        hostRT.anchorMax = sideRT.anchorMax;
                        hostRT.anchoredPosition = sideRT.anchoredPosition;
                        hostRT.pivot = sideRT.pivot;
                        hostRT.sizeDelta = sideRT.sizeDelta;
                    }
                    host.transform.SetAsLastSibling();
                }
                else
                {
                    host.transform.SetParent(canvas.transform, false);
                    // make host fill the entire canvas so panel anchors work as expected
                    var hostRT = host.GetComponent<RectTransform>();
                    hostRT.anchorMin = new Vector2(0f, 0f);
                    hostRT.anchorMax = new Vector2(1f, 1f);
                    hostRT.offsetMin = Vector2.zero;
                    hostRT.offsetMax = Vector2.zero;
                    host.transform.SetAsLastSibling();
                }
                _instance = host.AddComponent<ChangeLogPanelRuntime>();
                Debug.Log("ChangeLogPanelRuntime: creating panel host");
                _instance.BuildPanel();
                _instance.Refresh();
                return;
            }

            if (_instance._panelGO != null)
            {
                bool active = _instance._panelGO.activeSelf;
                _instance._panelGO.SetActive(!active);
                if (!active) _instance.Refresh();
            }
        }

        private void BuildPanel()
        {
            // Root panel
            _panelGO = new GameObject("ChangeLogPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            // Parent directly under the host (which itself is under the main Canvas).
            _panelGO.transform.SetParent(this.transform, false);
            // Add a dedicated Canvas on the panel so it sorts above other UI
            var panelCanvas = _panelGO.AddComponent<Canvas>();
            _panelCanvas = panelCanvas;
            panelCanvas.overrideSorting = true;
            // Use simple overlay canvas so runtime UI remains independent of scene cameras
            panelCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            panelCanvas.sortingOrder = 32767; // ensure topmost
            _panelGO.AddComponent<CanvasScaler>();
            _panelGO.AddComponent<GraphicRaycaster>();
            // No camera fallback — keep overlay canvas for simplicity
            // Ensure panel is the last sibling to appear in front
            _panelGO.transform.SetAsLastSibling();
            var rt = _panelGO.GetComponent<RectTransform>();
            // If a SidePanel exists, match its rect so the panel overlays it exactly.
            var side = GameObject.Find("SidePanel");
            if (side != null)
            {
                var sideRT = side.transform as RectTransform;
                if (sideRT != null)
                {
                    rt.anchorMin = sideRT.anchorMin;
                    rt.anchorMax = sideRT.anchorMax;
                    rt.pivot = sideRT.pivot;
                    rt.sizeDelta = sideRT.sizeDelta;
                    // Position the panel so it overlays the side panel: posX = half width, posY = -half height
                    float posX = rt.sizeDelta.x * 0.5f;
                    float posY = -rt.sizeDelta.y * 0.5f;
                    rt.anchoredPosition = new Vector2(posX, posY);
                        // Avoid copying parent's scale/rotation which can make the UI tiny or rotated.
                        rt.localScale = Vector3.one;
                        rt.localRotation = Quaternion.identity;
                }
                else
                {
                    rt.anchorMin = new Vector2(0.02f, 0.02f);
                    rt.anchorMax = new Vector2(0.98f, 0.98f);
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                }
            }
            else
            {
                // Make the panel much larger and centered over the canvas
                rt.anchorMin = new Vector2(0.02f, 0.02f);
                rt.anchorMax = new Vector2(0.98f, 0.98f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            var img = _panelGO.GetComponent<Image>();
            img.color = new Color(0.12f, 0.12f, 0.12f, 0.95f);

            // Title + close
            var titleGO = CreateTextChild(_panelGO.transform, "Title", "Change Log", 18, TextAnchor.UpperLeft);
            // (Debug button removed)
            var closeBtn = CreateButton(_panelGO.transform, "Close", 80, null);
            var titleRT = titleGO.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0f, 1f);
            titleRT.anchorMax = new Vector2(0.6f, 1f);
            titleRT.pivot = new Vector2(0f, 1f);
            titleRT.anchoredPosition = new Vector2(12, -12);

            var closeRT = closeBtn.GetComponent<RectTransform>();
            closeRT.anchorMin = new Vector2(1f, 1f);
            closeRT.anchorMax = new Vector2(1f, 1f);
            closeRT.pivot = new Vector2(1f, 1f);
            closeRT.anchoredPosition = new Vector2(-12, -12);
            // Ensure the close button actually closes the panel by wiring the listener to Close()
            var closeButtonComp = closeBtn.GetComponent<Button>();
            if (closeButtonComp != null)
            {
                closeButtonComp.onClick.RemoveAllListeners();
                closeButtonComp.onClick.AddListener(() => Close());
                closeButtonComp.interactable = true;
                var imgComp = closeBtn.GetComponent<Image>();
                if (imgComp != null) imgComp.raycastTarget = true;
            }

            // Ensure an EventSystem exists so UI receives clicks
            if (EventSystem.current == null)
            {
                var esGO = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            }

            // Scrollable content
            var scrollGO = new GameObject("Scroll", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ScrollRect));
            scrollGO.transform.SetParent(_panelGO.transform, false);
            var scrollRT = scrollGO.GetComponent<RectTransform>();
            scrollRT.anchorMin = new Vector2(0f, 0f);
            scrollRT.anchorMax = new Vector2(1f, 1f);
            scrollRT.offsetMin = new Vector2(12, 48);
            // offsetMax.y = -50 so the scroll area starts 50px below the panel top,
            // clearing the title bar (24px tall at -12) and close button (28px tall at -12).
            scrollRT.offsetMax = new Vector2(-12, -50);

            var scrollImg = scrollGO.GetComponent<Image>();
            scrollImg.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);

            var viewportGO = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            viewportGO.transform.SetParent(scrollGO.transform, false);
            var vpRT = viewportGO.GetComponent<RectTransform>();
            vpRT.anchorMin = new Vector2(0f, 0f);
            vpRT.anchorMax = new Vector2(1f, 1f);
            vpRT.offsetMin = Vector2.zero;
            vpRT.offsetMax = Vector2.zero;
            var vpImg = viewportGO.GetComponent<Image>(); vpImg.color = new Color(0, 0, 0, 0.0f);
            vpImg.raycastTarget = false;
            // RectMask2D clips children by the viewport rect without requiring a visible Image,
            // which makes it the correct choice here (Mask requires a non-transparent graphic).
            viewportGO.AddComponent<RectMask2D>();

            var contentGO = new GameObject("Content", typeof(RectTransform));
            contentGO.transform.SetParent(viewportGO.transform, false);
            _contentRT = contentGO.GetComponent<RectTransform>();
            _contentRT.anchorMin = new Vector2(0f, 1f);
            _contentRT.anchorMax = new Vector2(1f, 1f);
            _contentRT.pivot = new Vector2(0.5f, 1f);
            _contentRT.anchoredPosition = Vector2.zero;
            _contentRT.sizeDelta = new Vector2(0, 0);

            var scrollRect = scrollGO.GetComponent<ScrollRect>();
            scrollRect.content = _contentRT;
            scrollRect.viewport = vpRT;
            scrollRect.horizontal = false;
            scrollRect.scrollSensitivity = 30f;

            // Vertical scrollbar: anchored to the right edge of the scroll container.
            var scrollbarGO = new GameObject("VerticalScrollbar",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Scrollbar));
            scrollbarGO.transform.SetParent(scrollGO.transform, false);
            var scrollbarRT = scrollbarGO.GetComponent<RectTransform>();
            scrollbarRT.anchorMin        = new Vector2(1f, 0f);
            scrollbarRT.anchorMax        = new Vector2(1f, 1f);
            scrollbarRT.pivot            = new Vector2(1f, 0.5f);
            scrollbarRT.sizeDelta        = new Vector2(12f, 0f);
            scrollbarRT.anchoredPosition = Vector2.zero;
            var scrollbarImg = scrollbarGO.GetComponent<Image>();
            scrollbarImg.color = new Color(0.15f, 0.15f, 0.15f, 0.8f);

            // The SlidingArea constrains the handle travel range.
            var slidingAreaGO = new GameObject("SlidingArea", typeof(RectTransform));
            slidingAreaGO.transform.SetParent(scrollbarGO.transform, false);
            var slidingAreaRT = slidingAreaGO.GetComponent<RectTransform>();
            slidingAreaRT.anchorMin = Vector2.zero;
            slidingAreaRT.anchorMax = Vector2.one;
            slidingAreaRT.offsetMin = new Vector2(2f, 6f);
            slidingAreaRT.offsetMax = new Vector2(-2f, -6f);

            // Handle — the draggable thumb inside the scrollbar.
            var handleGO = new GameObject("Handle",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            handleGO.transform.SetParent(slidingAreaGO.transform, false);
            var handleRT = handleGO.GetComponent<RectTransform>();
            handleRT.anchorMin = Vector2.zero;
            handleRT.anchorMax = Vector2.one;
            handleRT.offsetMin = Vector2.zero;
            handleRT.offsetMax = Vector2.zero;
            var handleImg = handleGO.GetComponent<Image>();
            handleImg.color = new Color(0.6f, 0.6f, 0.6f, 1f);

            var scrollbarComp = scrollbarGO.GetComponent<Scrollbar>();
            scrollbarComp.handleRect    = handleRT;
            scrollbarComp.direction     = Scrollbar.Direction.BottomToTop;
            scrollbarComp.targetGraphic = handleImg;

            // Wire the scrollbar to the ScrollRect; it will auto-hide and expand the viewport
            // when content fits entirely within the panel.
            scrollRect.verticalScrollbar           = scrollbarComp;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;

            // Ensure the close button is above the scroll area so it receives clicks
            if (closeBtn != null) closeBtn.transform.SetAsLastSibling();

            // Add a vertical layout to content
            var layout = contentGO.AddComponent<VerticalLayoutGroup>();
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.spacing = 6;
            var fitter = contentGO.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        /**
         * Creates a full-width row in the ChangeLog list. The entire row acts as a button:
         * clicking it triggers onClick, hovering shows a board preview via onEnter/onExit.
         *
         * @param parent      Content RectTransform to parent the row under.
         * @param name        GameObject name (used for highlight lookup).
         * @param labelText   Primary label shown in the row.
         * @param descText    Optional secondary description (shown smaller, grey). Pass null to omit.
         * @param onClick     Invoked when the row is clicked.
         * @param onEnter     Invoked when the pointer enters the row (show preview).
         * @param onExit      Invoked when the pointer exits the row (hide preview).
         * @returns The created row GameObject.
         */
        private GameObject CreateChangeLogRow(Transform parent, string name, string labelText, string descText,
            Action onClick, Action onEnter, Action onExit)
        {
            var row = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            row.transform.SetParent(parent, false);

            var img = row.GetComponent<Image>();
            var normalColor    = new Color(0.15f, 0.15f, 0.15f, 0.6f);
            var highlightColor = new Color(0.30f, 0.30f, 0.30f, 0.8f);
            img.color = normalColor;
            img.raycastTarget = true;

            // Prefer a taller row when there is description text
            var le = row.AddComponent<LayoutElement>();
            le.preferredHeight = string.IsNullOrEmpty(descText) ? 36 : 52;

            // Vertical stack for label + optional description
            var v = row.AddComponent<VerticalLayoutGroup>();
            v.childForceExpandHeight = false;
            v.childForceExpandWidth  = true;
            v.childControlHeight     = true;
            v.childControlWidth      = true;
            v.padding = new RectOffset(10, 8, 6, 6);
            v.spacing = 2;

            CreateTextChild(row.transform, "Label", labelText, 14, TextAnchor.MiddleLeft);

            if (!string.IsNullOrEmpty(descText))
            {
                var descGO  = CreateTextChild(row.transform, "Desc", descText, 12, TextAnchor.UpperLeft);
                var descTxt = descGO.GetComponent<Text>();
                if (descTxt != null) descTxt.color = new Color(0.7f, 0.7f, 0.7f, 1f);
            }

            // EventTrigger handles click, pointer enter, and pointer exit.
            // Using EventTrigger (rather than Button) avoids the known issue where
            // adding EventTrigger to a Selectable suppresses its event handling.
            var trigger = row.AddComponent<EventTrigger>();

            var clickEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            clickEntry.callback.AddListener((_) =>
            {
                try { onClick(); }
                finally { if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null); }
            });
            trigger.triggers.Add(clickEntry);

            var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enterEntry.callback.AddListener((_) => { img.color = highlightColor; onEnter(); });
            trigger.triggers.Add(enterEntry);

            var exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exitEntry.callback.AddListener((_) => { img.color = normalColor; onExit(); });
            trigger.triggers.Add(exitEntry);

            return row;
        }

        private GameObject CreateButton(Transform parent, string text, float width, Action onClick)
        {
            var go = new GameObject(text + "Btn", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(width, 28);
            var img = go.GetComponent<Image>(); img.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            var btn = go.GetComponent<Button>();
            // Prevent the button from appearing to act like a toggle by disabling
            // selectable transitions and navigation, and clear selection after click.
            btn.transition = Selectable.Transition.None;
            var nav = new Navigation(); nav.mode = Navigation.Mode.None; btn.navigation = nav;
            if (onClick != null)
            {
                btn.onClick.AddListener(() => {
                    try { onClick(); }
                    finally
                    {
                        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
                    }
                });
            }
            var txt = CreateTextChild(go.transform, "Text", text, 14, TextAnchor.MiddleCenter);
            var txtRT = txt.GetComponent<RectTransform>(); txtRT.anchorMin = new Vector2(0f, 0f); txtRT.anchorMax = new Vector2(1f, 1f);
            return go;
        }

        private void Close()
        {
            if (_panelGO != null) _panelGO.SetActive(false);
        }

        private GameObject CreateTextChild(Transform parent, string name, string text, int fontSize, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 24);
            var txt = go.AddComponent<Text>();
            txt.text = text;
            txt.alignment = anchor;
            txt.color = Color.white;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.font = font;
            txt.fontSize = fontSize;
            return go;
        }

        public void Refresh()
        {
            var runner = UnityEngine.Object.FindAnyObjectByType<SolverRunner>();
            if (runner == null || runner.CurrentBoard == null)
            {
                // if no board, clear content once and show message
                if (_lastChangeLogCount != 0)
                {
                    foreach (Transform child in _contentRT) Destroy(child.gameObject);
                    CreateTextChild(_contentRT, "Empty", "No active board found.", 14, TextAnchor.MiddleCenter);
                    _lastChangeLogCount = 0;
                    _lastGroupsHash = 0;
                }
                return;
            }

            Board board = runner.CurrentBoard;
            var groups = board.GetChangeLogSummary();

            // compute a simple hash of groups so we can detect changes without rebuilding every frame
            int groupsHash = 17;
            unchecked
            {
                groupsHash = groupsHash * 23 + board.ChangeLog.Count;
                groupsHash = groupsHash * 23 + board.ChangeLogIndex;
                for (int i = 0; i < groups.Count; i++)
                {
                    var g = groups[i];
                    groupsHash = groupsHash * 23 + g.GroupId;
                    groupsHash = groupsHash * 23 + g.StartIndex;
                    groupsHash = groupsHash * 23 + g.EndIndex;
                }
            }

            // Update header text every refresh but only rebuild children when the groups actually change
            bool needsRebuild = (_lastChangeLogCount != board.ChangeLog.Count) || (_lastGroupsHash != groupsHash);

            if (needsRebuild)
            {
                // Recreate children only when the change log actually changed
                foreach (Transform child in _contentRT) Destroy(child.gameObject);

                // Initial State row — always the first entry, jumps back to the puzzle's starting state
                CreateChangeLogRow(_contentRT, "InitialState", "[0] Initial State", null,
                    onClick: () =>
                    {
                        _isHovering = false; // prevent onExit from restoring after Refresh() destroys the rows
                        if (runner?.CurrentBoard == null) return;
                        runner.CurrentBoard.SeekChangeLogIndex(0);
                        runner.SyncCandidatesForCurrentBoard();
                        runner.ClearPreview();
                        runner.SetLastRuleResultFromChangeLogRange(0, 0);
                        runner.RunCreationSolveAnalysisIfNeeded();
                        Refresh();
                        foreach (var p in FindObjectsByType<ApplyRulePanel>()) p.RefreshList();
                        foreach (var p in FindObjectsByType<CreateModeStatusPanel>()) p.RefreshStatus();
                        ChangeLogRuntimeControls.RefreshButtonStates();
                    },
                    onEnter: () =>
                    {
                        if (runner?.CurrentBoard == null) return;
                        if (!_isHovering) { _savedIndexBeforeHover = runner.CurrentBoard.ChangeLogIndex; _isHovering = true; }
                        runner.CurrentBoard.SeekChangeLogIndex(0);
                        runner.SetLastRuleResultFromChangeLogRange(0, 0);
                    },
                    onExit: () => RestoreFromHover(runner));

                for (int i = 0; i < groups.Count; i++)
                {
                    var g = groups[i];
                    CreateChangeLogRow(_contentRT, $"Group_{g.GroupId}",
                        $"[{g.GroupId}] {g.RuleName} ({g.ChangesCount} changes)",
                        g.Description,
                        onClick: () =>
                        {
                            _isHovering = false; // prevent onExit from restoring after Refresh() destroys the rows
                            if (runner?.CurrentBoard == null) return;
                            runner.CurrentBoard.SeekChangeLogIndex(g.EndIndex);
                            runner.SyncCandidatesForCurrentBoard();
                            try { runner.SetLastRuleResultFromChangeLogRange(g.StartIndex, g.EndIndex); }
                            catch (Exception) { runner?.ClearPreview(); }
                            runner.RunCreationSolveAnalysisIfNeeded();
                            Refresh();
                            foreach (var p in FindObjectsByType<ApplyRulePanel>()) p.RefreshList();
                            foreach (var p in FindObjectsByType<CreateModeStatusPanel>()) p.RefreshStatus();
                            ChangeLogRuntimeControls.RefreshButtonStates();
                        },
                        onEnter: () =>
                        {
                            if (runner?.CurrentBoard == null) return;
                            if (!_isHovering) { _savedIndexBeforeHover = runner.CurrentBoard.ChangeLogIndex; _isHovering = true; }
                            runner.CurrentBoard.SeekChangeLogIndex(g.EndIndex);
                            try { runner.SetLastRuleResultFromChangeLogRange(g.StartIndex, g.EndIndex); }
                            catch (Exception) { runner?.ClearPreview(); }
                        },
                        onExit: () => RestoreFromHover(runner));
                }

                _lastChangeLogCount = board.ChangeLog.Count;
                _lastGroupsHash = groupsHash;

                // Force layout rebuild so the VerticalLayoutGroup / ContentSizeFitter compute sizes immediately
                UnityEngine.Canvas.ForceUpdateCanvases();
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(_contentRT);

                // Scroll to show the currently active row centred in the viewport.
                var scrollRect = _contentRT.parent != null && _contentRT.parent.parent != null ? _contentRT.parent.parent.GetComponent<ScrollRect>() : null;
                if (scrollRect != null)
                {
                    // Locate the active row by the current change-log index.
                    Transform activeRow = null;
                    if (board.ChangeLogIndex == 0)
                    {
                        activeRow = _contentRT.Find("InitialState");
                    }
                    else
                    {
                        var activeGroup = groups.Find(g => board.ChangeLogIndex > g.StartIndex
                                                        && board.ChangeLogIndex <= g.EndIndex);
                        if (activeGroup != null)
                            activeRow = _contentRT.Find($"Group_{activeGroup.GroupId}");
                    }

                    if (activeRow is RectTransform activeRT)
                    {
                        float contentH   = _contentRT.rect.height;
                        float viewportH  = scrollRect.viewport != null
                            ? scrollRect.viewport.rect.height
                            : scrollRect.GetComponent<RectTransform>().rect.height;
                        float scrollable = contentH - viewportH;

                        if (scrollable > 0f)
                        {
                            // anchoredPosition.y is negative (rows go downward from the content top).
                            float rowTop       = -activeRT.anchoredPosition.y;
                            float targetOffset = rowTop - (viewportH - activeRT.rect.height) * 0.5f;
                            targetOffset = Mathf.Clamp(targetOffset, 0f, scrollable);
                            scrollRect.verticalNormalizedPosition = 1f - (targetOffset / scrollable);
                        }
                        else
                        {
                            scrollRect.verticalNormalizedPosition = 1f;
                        }
                    }
                    else
                    {
                        scrollRect.verticalNormalizedPosition = 1f; // fallback: scroll to top
                    }
                }

                
            }

            // Update highlight state for each group row without recreating objects
            // Highlight the Initial State row when no changes have been applied
            var initialStateRow = _contentRT.Find("InitialState");
            if (initialStateRow != null)
            {
                var lbl = initialStateRow.Find("Label")?.GetComponent<Text>();
                if (lbl != null) lbl.color = board.ChangeLogIndex == 0 ? Color.yellow : Color.white;
            }

            for (int i = 0; i < _contentRT.childCount; i++)
            {
                var child = _contentRT.GetChild(i);
                if (!child.name.StartsWith("Group_")) continue;
                var label = child.Find("Label");
                if (label == null) continue;
                // parse group id from name
                var parts = child.name.Split('_');
                if (parts.Length < 2) continue;
                if (!int.TryParse(parts[1], out int gid)) continue;
                // find the group
                var g = groups.Find(x => x.GroupId == gid);
                if (g == null) continue;
                // EndIndex is exclusive, so use > StartIndex (not >=) to avoid
                // simultaneously matching both this group and the one before it.
                bool active = (board.ChangeLogIndex > g.StartIndex && board.ChangeLogIndex <= g.EndIndex);
                var lblTxt = label.GetComponent<Text>();
                if (lblTxt != null) lblTxt.color = active ? Color.yellow : Color.white;
            }
        }

        private float _lastRefreshTime = 0f;
        private const float _refreshInterval = 0.5f; // seconds

        /**
         * Seeks the board back to the index saved when the hover started, restores the
         * appropriate rule-result highlights, and clears the hover flag. Safe to call
         * even when no hover is active (early-returns immediately).
         *
         * @param runner The active SolverRunner.
         */
        private void RestoreFromHover(SolverRunner runner)
        {
            if (!_isHovering) return;
            _isHovering = false;
            if (runner?.CurrentBoard == null || _savedIndexBeforeHover < 0) return;
            runner.CurrentBoard.SeekChangeLogIndex(_savedIndexBeforeHover);
            // Restore highlight to match the committed board state
            var gs = runner.CurrentBoard.GetChangeLogSummary();
            var committed = gs.Find(g => runner.CurrentBoard.ChangeLogIndex > g.StartIndex
                                      && runner.CurrentBoard.ChangeLogIndex <= g.EndIndex);
            if (committed != null)
                runner.SetLastRuleResultFromChangeLogRange(committed.StartIndex, committed.EndIndex);
            else
                runner.SetLastRuleResultFromChangeLogRange(0, 0);
            runner.ClearPreview();
        }

        private void Update()
        {
            if (_panelGO == null || !_panelGO.activeSelf) return;
            if (_isHovering) return; // don't rebuild rows while a hover seek is active
            if (Time.unscaledTime - _lastRefreshTime < _refreshInterval) return;
            _lastRefreshTime = Time.unscaledTime;
            Refresh();
        }
    }
}
