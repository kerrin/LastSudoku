using UnityEngine;
using UnityEngine.UI;
using Sudoku.Solver;
using Sudoku.Solver.Rules;
using Sudoku.UI.Learning;

namespace Sudoku.UI.Panels
{
    /**
     * Adds the change-history controls at the bottom of the SidePanel and the
     * Learn / Wiki entry just above them.
     * Currently they log clicks; wiring to board functionality will be added later.
     */
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public class ChangeLogRuntimeControls : MonoBehaviour
    {
        [Tooltip("Optional reference to a parent RectTransform (e.g. SidePanel). If null, script will try to find a GameObject named 'SidePanel' or the Scene Canvas.")]
        public RectTransform ParentPanel;

        private GameObject _containerGO;
        private GameObject _learningButtonGO;
        private RectTransform _sidePanelRect;

        private void OnEnable()
        {
            // In edit mode and play mode we want the UI present. Defer to SetupControls which is idempotent.
            SetupControls();
        }

        private void OnDisable()
        {
            // Intentionally do not destroy created UI on disable to avoid
            // errors during playmode transitions and activation/deactivation
            // cycles in the Editor. Designers can remove the GameObject
            // manually if they don't want it in the scene.
        }

        private void Update()
        {
            if (_containerGO == null)
                SetupControls();

            AttachControlsToSidePanel();
        }

        private void SetupControls()
        {
            // Avoid creating multiple times
            if (_containerGO != null) return;

            // Determine parent for the controls
            RectTransform parent = ParentPanel;
            if (parent == null)
            {
                var side = GameObject.Find("SidePanel");
                if (side != null) parent = side.transform as RectTransform;
            }

            if (parent == null)
            {
                var canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
                if (canvas != null) parent = canvas.transform as RectTransform;
            }

            if (parent == null)
            {
                Debug.LogWarning("ChangeLogRuntimeControls: No SidePanel or Canvas found to attach controls to.");
                return;
            }

            // Reuse existing container if present under parent
            var found = parent.Find("ChangeLogControls");
            if (found != null)
            {
                _containerGO = found.gameObject;
            }

            // Create a container at the bottom center if none found
            if (_containerGO == null)
            {
                _containerGO = new GameObject("ChangeLogControls", typeof(RectTransform));
            }

            _containerGO.transform.SetParent(parent, false);
            var containerRT = _containerGO.GetComponent<RectTransform>();
            containerRT.anchorMin = new Vector2(0.5f, 0f);
            containerRT.anchorMax = new Vector2(0.5f, 0f);
            containerRT.pivot = new Vector2(0.5f, 0f);
            containerRT.sizeDelta = new Vector2(360, 48);
            containerRT.anchoredPosition = new Vector2(0, 8); // 8 px above bottom

            EnsureLearningButton(parent);

            var layout = _containerGO.GetComponent<HorizontalLayoutGroup>();
            if (layout == null) layout = _containerGO.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 8;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;

            // Ensure buttons exist (idempotent) so edit-mode and play-mode both show them
            EnsureButtonExists(_containerGO.transform, "UndoButton", "Undo", 80, () => {
                var runner = UnityEngine.Object.FindAnyObjectByType<SolverRunner>();
                if (runner != null && runner.CurrentBoard != null)
                {
                    runner.CurrentBoard.UndoLast();
                    runner.SyncCandidatesForCurrentBoard();
                    UpdateDisplayFromCurrentState(runner);
                    RefreshPanels();
                }
            });
            EnsureButtonExists(_containerGO.transform, "ViewChangesButton", "View Changes", 140, () => {
                ChangeLogPanelRuntime.TogglePanel();
            });
            EnsureButtonExists(_containerGO.transform, "RedoButton", "Redo", 80, () => {
                var runner = UnityEngine.Object.FindAnyObjectByType<SolverRunner>();
                if (runner != null && runner.CurrentBoard != null)
                {
                    runner.CurrentBoard.RedoNext();
                    runner.SyncCandidatesForCurrentBoard();
                    UpdateDisplayFromCurrentState(runner);
                    RefreshPanels();
                }
            });

            // Set initial button state now that controls exist
            UpdateButtonStates();
        }

        private void EnsureLearningButton(RectTransform parent)
        {
            const string buttonName = "LearnWikiButton";
            var existing = _learningButtonGO != null ? _learningButtonGO.transform : parent.Find(buttonName);
            if (existing == null)
            {
                var canvases = UnityEngine.Object.FindObjectsByType<Canvas>();
                foreach (var canvas in canvases)
                {
                    if (canvas.transform == parent)
                        continue;

                    existing = canvas.transform.Find(buttonName);
                    if (existing != null)
                        break;
                }
            }

            if (existing != null)
            {
                _learningButtonGO = existing.gameObject;
                var existingButton = existing.GetComponent<Button>();
                if (existingButton != null)
                {
                    existingButton.onClick.RemoveAllListeners();
                    existingButton.onClick.AddListener(OpenLearningHub);
                }
            }
            else
            {
                _learningButtonGO = new GameObject(buttonName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                _learningButtonGO.transform.SetParent(parent, false);
            }

            var buttonObject = _learningButtonGO;
            if (buttonObject.transform.parent != parent)
                buttonObject.transform.SetParent(parent, false);
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(142f, 34f);
            rect.anchoredPosition = new Vector2(0f, 60f);

            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.20f, 0.24f, 0.30f, 1f);
            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OpenLearningHub);

            var textObject = buttonObject.transform.Find("Text")?.gameObject;
            if (textObject == null)
            {
                textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                textObject.transform.SetParent(buttonObject.transform, false);
            }
            var textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var label = textObject.GetComponent<Text>();
            label.text = "Learn / Wiki";
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (label.font == null)
                label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            label.fontSize = 14;
            label.raycastTarget = false;
        }

        private void AttachControlsToSidePanel()
        {
            var sidePanel = ResolveSidePanel();
            if (sidePanel == null)
                return;

            bool movedControls = _containerGO != null && _containerGO.transform.parent != sidePanel;
            if (movedControls)
            {
                _containerGO.transform.SetParent(sidePanel, false);
                var controlsRect = _containerGO.GetComponent<RectTransform>();
                controlsRect.anchorMin = new Vector2(0.5f, 0f);
                controlsRect.anchorMax = new Vector2(0.5f, 0f);
                controlsRect.pivot = new Vector2(0.5f, 0f);
                controlsRect.sizeDelta = new Vector2(360f, 48f);
                controlsRect.anchoredPosition = new Vector2(0f, 8f);
            }

            if (_learningButtonGO == null || _learningButtonGO.transform.parent != sidePanel)
                EnsureLearningButton(sidePanel);

            PositionLearningButton(sidePanel);
        }

        private void PositionLearningButton(RectTransform sidePanel)
        {
            if (_learningButtonGO == null)
                return;

            const float undoRowTop = 56f;
            const float buttonHeight = 34f;
            const float gap = 8f;
            float bottomOffset = undoRowTop + gap;

            var directionalPanel = sidePanel.GetComponentInChildren<DirectionalLinkModePanel>(true);
            if (directionalPanel != null && directionalPanel.transform is RectTransform directionalRect)
            {
                var corners = new Vector3[4];
                directionalRect.GetWorldCorners(corners);
                float lowestWorldY = Mathf.Min(corners[0].y, corners[3].y);
                float lowestLocalY = sidePanel.InverseTransformPoint(new Vector3(corners[0].x, lowestWorldY, corners[0].z)).y;
                float panelBottom = sidePanel.rect.yMin;
                float directionalBottomOffset = lowestLocalY - panelBottom;
                float highestAvailableBottom = directionalBottomOffset - buttonHeight - gap;
                if (highestAvailableBottom >= bottomOffset)
                    bottomOffset = Mathf.Min(bottomOffset, highestAvailableBottom);
            }

            var buttonRect = _learningButtonGO.GetComponent<RectTransform>();
            buttonRect.anchoredPosition = new Vector2(0f, bottomOffset);
        }

        private RectTransform ResolveSidePanel()
        {
            if (_sidePanelRect != null)
                return _sidePanelRect;

            if (ParentPanel != null && ParentPanel.name == "SidePanel")
                return _sidePanelRect = ParentPanel;

            var sidePanel = GameObject.Find("SidePanel");
            if (sidePanel != null && sidePanel.TryGetComponent(out RectTransform sidePanelRect))
                return _sidePanelRect = sidePanelRect;

            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>();
            foreach (var canvas in canvases)
            {
                var child = canvas.transform.Find("SidePanel") as RectTransform;
                if (child != null)
                    return _sidePanelRect = child;
            }
            return null;
        }

        private static void OpenLearningHub()
        {
            var hub = UnityEngine.Object.FindAnyObjectByType<LearningHubPanel>();
            if (hub == null)
            {
                Debug.LogError("ChangeLogRuntimeControls: The learning hub is unavailable.");
                return;
            }
            hub.OpenTutorials();
        }

        /**
         * Enables or disables (fades) Undo/Redo based on the board's current ChangeLog position.
         */
        private void UpdateButtonStates()
        {
            if (_containerGO == null) return;
            var runner = UnityEngine.Object.FindAnyObjectByType<SolverRunner>();
            var board = runner?.CurrentBoard;

            var undoBtn = _containerGO.transform.Find("UndoButton")?.GetComponent<Button>();
            if (undoBtn != null)
                undoBtn.interactable = board != null && board.ChangeLogIndex > 0;

            var redoBtn = _containerGO.transform.Find("RedoButton")?.GetComponent<Button>();
            if (redoBtn != null)
                redoBtn.interactable = board != null && board.ChangeLog != null && board.ChangeLogIndex < board.ChangeLog.Count;
        }

        /**
         * After an undo or redo, updates the board visualizer to show the highlights
         * that correspond to the group now at the current ChangeLog position.
         * Clears all highlights when at the initial state (ChangeLogIndex == 0).
         */
        private void UpdateDisplayFromCurrentState(SolverRunner runner)
        {
            if (runner?.CurrentBoard == null) return;
            var board = runner.CurrentBoard;
            var groups = board.GetChangeLogSummary();
            // Find the group whose applied range contains the current index
            var activeGroup = groups.Find(g => board.ChangeLogIndex > g.StartIndex && board.ChangeLogIndex <= g.EndIndex);
            if (activeGroup != null)
                runner.SetLastRuleResultFromChangeLogRange(activeGroup.StartIndex, activeGroup.EndIndex);
            else
            {
                // At initial state — clear all highlights
                runner.ClearPreview();
                runner.SetLastRuleResultFromChangeLogRange(0, 0);
            }
        }

        /**
         * Refreshes the ChangeLog panel (if open) and any Apply Rule panels after a state change.
         * Also updates Undo/Redo button states so they reflect the new ChangeLog position.
         */
        private void RefreshPanels()
        {
            var changeLogPanels = UnityEngine.Object.FindObjectsByType<ChangeLogPanelRuntime>();
            foreach (var p in changeLogPanels) p.Refresh();
            var applyPanels = UnityEngine.Object.FindObjectsByType<ApplyRulePanel>();
            foreach (var p in applyPanels) p.RefreshList();
            var statusPanels = UnityEngine.Object.FindObjectsByType<CreateModeStatusPanel>();
            foreach (var p in statusPanels) p.RefreshStatus();
            UpdateButtonStates();
        }

        /**
         * Static helper so external components (Jump buttons, status panels) can trigger
         * a button-state refresh without taking a direct reference to this instance.
         */
        public static void RefreshButtonStates()
        {
            foreach (var c in UnityEngine.Object.FindObjectsByType<ChangeLogRuntimeControls>())
                c.UpdateButtonStates();
        }

        private void CreateButton(Transform parent, string text, float width, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(text + "Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(width, 36);

            var img = go.GetComponent<Image>();
            img.color = new Color(0.9f, 0.9f, 0.9f, 1f);

            var btn = go.GetComponent<Button>();
            btn.onClick.AddListener(onClick);

            // Text child
            var textGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer));
            textGO.transform.SetParent(go.transform, false);
            var textRT = textGO.GetComponent<RectTransform>();
            textRT.anchorMin = new Vector2(0, 0);
            textRT.anchorMax = new Vector2(1, 1);
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;

            var txt = textGO.AddComponent<UnityEngine.UI.Text>();
            txt.text = text;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.black;
            // Use the legacy runtime font to avoid newer Unity builtin font changes
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.font = font;
            txt.fontSize = 14;
        }

        private void EnsureButtonExists(Transform parent, string childName, string text, float width, UnityEngine.Events.UnityAction onClick)
        {
            var existing = parent.Find(childName);
            if (existing != null)
            {
                // Ensure button has listener (remove previous to avoid duplicates)
                var button = existing.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(onClick);
                }
                // Ensure text matches
                var textComponent = existing.GetComponentInChildren<Text>();
                if (textComponent != null) textComponent.text = text;
                return;
            }

            var go = new GameObject(childName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(width, 36);

            var img = go.GetComponent<Image>();
            img.color = new Color(0.9f, 0.9f, 0.9f, 1f);

            var btn = go.GetComponent<Button>();
            btn.onClick.AddListener(onClick);

            // Text child
            var textGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer));
            textGO.transform.SetParent(go.transform, false);
            var textRT = textGO.GetComponent<RectTransform>();
            textRT.anchorMin = new Vector2(0, 0);
            textRT.anchorMax = new Vector2(1, 1);
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;

            var txt = textGO.AddComponent<Text>();
            txt.text = text;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.black;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.font = font;
            txt.fontSize = 14;
        }
    }
}
