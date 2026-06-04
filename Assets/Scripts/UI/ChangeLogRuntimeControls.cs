using UnityEngine;
using UnityEngine.UI;
using Sudoku.Solver;
using Sudoku.Solver.Rules;

namespace Sudoku.Scripts.UI
{
    /**
     * At runtime (Designer/Play mode) injects three buttons into the SidePanel
     * at the bottom center: Undo, View Changes, Redo.
     * Currently they log clicks; wiring to board functionality will be added later.
     */
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public class ChangeLogRuntimeControls : MonoBehaviour
    {
        [Tooltip("Optional reference to a parent RectTransform (e.g. SidePanel). If null, script will try to find a GameObject named 'SidePanel' or the Scene Canvas.")]
        public RectTransform ParentPanel;

        private GameObject _containerGO;

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
