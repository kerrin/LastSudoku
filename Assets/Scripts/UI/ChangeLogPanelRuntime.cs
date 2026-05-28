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
            scrollRT.offsetMax = new Vector2(-12, -12);

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
            // Do not add a Mask here — an Image with alpha=0 combined with Mask
            // would create an empty mask (invisible). Make the viewport image
            // non-raycastable so pointer events reach child rows instead.
            vpImg.raycastTarget = false;

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

        private GameObject CreateButton(Transform parent, string text, float width, Action onClick)
        {
            var go = new GameObject(text + "Btn", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(width, 28);
            var img = go.GetComponent<Image>(); img.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            var btn = go.GetComponent<Button>();
            if (onClick != null) btn.onClick.AddListener(() => onClick());
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

                // TODO: Debug Remove..Show a header with current index
                CreateTextChild(_contentRT, "Header", $"ChangeLog: {board.ChangeLog.Count} entries - Index: {board.ChangeLogIndex}", 14, TextAnchor.MiddleCenter);

                for (int i = 0; i < groups.Count; i++)
                {
                    var g = groups[i];
                    var gRow = new GameObject($"Group_{g.GroupId}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    gRow.transform.SetParent(_contentRT, false);
                    var img = gRow.GetComponent<Image>(); img.color = new Color(0.15f, 0.15f, 0.15f, 0.6f);
                    var layoutElement = gRow.AddComponent<LayoutElement>();
                    layoutElement.preferredHeight = 44;
                    var h = gRow.AddComponent<HorizontalLayoutGroup>();
                    h.childForceExpandHeight = true; h.childForceExpandWidth = true; h.spacing = 8; h.childControlWidth = true; h.childControlHeight = true;

                    var label = CreateTextChild(gRow.transform, "Label", $"[{g.GroupId}] {g.RuleName} ({g.ChangesCount} changes)", 14, TextAnchor.MiddleLeft);
                    var labelLE = label.AddComponent<LayoutElement>(); labelLE.flexibleWidth = 1;
                    var jump = CreateButton(gRow.transform, "Jump", 80, () => {
                        // Jump the board to the group's end index
                        if (runner.CurrentBoard == null) return;
                        int targetIndex = g.EndIndex;
                        int delta = targetIndex - runner.CurrentBoard.ChangeLogIndex;
                        if (delta > 0) runner.CurrentBoard.RedoSteps(delta);
                        else if (delta < 0) runner.CurrentBoard.UndoSteps(-delta);
                        Refresh();
                    });

                    // Show a short description
                    var desc = CreateTextChild(gRow.transform, "Desc", g.Description ?? "", 12, TextAnchor.UpperLeft);
                    var descLE = desc.AddComponent<LayoutElement>(); descLE.flexibleWidth = 1; desc.GetComponent<RectTransform>().sizeDelta = new Vector2(300, 24);
                }

                _lastChangeLogCount = board.ChangeLog.Count;
                _lastGroupsHash = groupsHash;

                // Force layout rebuild so the VerticalLayoutGroup / ContentSizeFitter compute sizes immediately
                UnityEngine.Canvas.ForceUpdateCanvases();
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(_contentRT);
                // Reset scroll to top so new content is visible
                var scrollRect = _contentRT.parent != null && _contentRT.parent.parent != null ? _contentRT.parent.parent.GetComponent<ScrollRect>() : null;
                if (scrollRect != null) scrollRect.verticalNormalizedPosition = 1f;

                // Diagnostic logging to help trace invisibility issues
                try
                {
                    Debug.Log($"ChangeLogPanelRuntime.Diag: panelCanvas={(_panelCanvas!=null)} renderMode={_panelCanvas?.renderMode} sortingOrder={_panelCanvas?.sortingOrder}");
                    var panelRT = _panelGO.GetComponent<RectTransform>();
                    Debug.Log($"ChangeLogPanelRuntime.Diag: panel rect={panelRT.rect} anchoredPosition={panelRT.anchoredPosition} localScale={panelRT.localScale}");
                    var scrollGO = _contentRT.parent != null ? _contentRT.parent.parent : null;
                    if (scrollGO != null)
                    {
                        var srt = scrollGO.GetComponent<RectTransform>();
                        Debug.Log($"ChangeLogPanelRuntime.Diag: scroll rect={srt.rect} anchoredPosition={srt.anchoredPosition} localScale={srt.localScale}");
                    }
                    var vp = _contentRT.parent; // viewport
                    if (vp != null)
                    {
                        var vpRT = vp.GetComponent<RectTransform>();
                        Debug.Log($"ChangeLogPanelRuntime.Diag: viewport rect={vpRT.rect} anchoredPosition={vpRT.anchoredPosition} localScale={vpRT.localScale}");
                    }
                    Debug.Log($"ChangeLogPanelRuntime.Diag: content childCount={_contentRT.childCount} content rect={_contentRT.rect} anchoredPosition={_contentRT.anchoredPosition} localScale={_contentRT.localScale}");
                    for (int ci = 0; ci < _contentRT.childCount; ci++)
                    {
                        var c = _contentRT.GetChild(ci) as RectTransform;
                        if (c == null) continue;
                        Debug.Log($"ChangeLogPanelRuntime.Diag: child[{ci}] name={c.name} rect={c.rect} anchoredPos={c.anchoredPosition} sizeDelta={c.sizeDelta} localScale={c.localScale}");
                        var img = c.GetComponent<Image>();
                        if (img != null) Debug.Log($"ChangeLogPanelRuntime.Diag: child[{ci}] image color={img.color} enabled={(img.enabled)}");
                        var txt = c.GetComponentInChildren<Text>();
                        if (txt != null) Debug.Log($"ChangeLogPanelRuntime.Diag: child[{ci}] text='{txt.text}' color={txt.color} fontSize={txt.fontSize}");
                    }
                    // log ancestor scales
                    Transform t = _panelGO.transform;
                    int depth = 0;
                    while (t != null && depth < 10)
                    {
                        Debug.Log($"ChangeLogPanelRuntime.Diag: ancestor[{depth}] name={t.name} localScale={t.localScale}");
                        t = t.parent;
                        depth++;
                    }
                }
                catch (Exception ex)
                {
                    Debug.Log($"ChangeLogPanelRuntime.Diag: exception {ex.Message}");
                }
            }

            // Ensure header text reflects current index even if we didn't rebuild
            var header = _contentRT.Find("Header");
            if (header != null)
            {
                var t = header.GetComponent<Text>();
                if (t != null) t.text = $"ChangeLog: {board.ChangeLog.Count} entries - Index: {board.ChangeLogIndex}";
            }

            // Update highlight state for each group row without recreating objects
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
                bool active = (board.ChangeLogIndex >= g.StartIndex && board.ChangeLogIndex <= g.EndIndex);
                var lblTxt = label.GetComponent<Text>();
                if (lblTxt != null) lblTxt.color = active ? Color.yellow : Color.white;
            }
        }

        private float _lastRefreshTime = 0f;
        private const float _refreshInterval = 0.5f; // seconds

        private void Update()
        {
            if (_panelGO == null || !_panelGO.activeSelf) return;
            if (Time.unscaledTime - _lastRefreshTime < _refreshInterval) return;
            _lastRefreshTime = Time.unscaledTime;
            Refresh();
        }
    }
}
