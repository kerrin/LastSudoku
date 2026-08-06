using UnityEngine;
using UnityEngine.UI;
using Sudoku.Solver;

namespace Sudoku.UI.Panels
{
    /**
     * Solve-mode side panel controls for directional-link tool selection.
     */
    [DisallowMultipleComponent]
    public class DirectionalLinkModePanel : MonoBehaviour
    {
        public SolverRunner Runner;
        public BoardVisualizer BoardVisualizer;

        private enum LinkToggleIconKind
        {
            Off = 0,
            Strong = 1,
            Weak = 2,
        }

        private Toggle _offToggle;
        private Toggle _strongToggle;
        private Toggle _weakToggle;
        private bool _isUpdatingUi;

        private Texture2D _offIconTexture;
        private Texture2D _strongIconTexture;
        private Texture2D _weakIconTexture;

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            EnsureBuilt();
            SyncFromVisualizer();
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (BoardVisualizer == null)
            {
                BoardVisualizer = FindAnyObjectByType<BoardVisualizer>();
            }

            if (Runner == null)
            {
                Runner = FindAnyObjectByType<SolverRunner>();
            }

            SyncFromVisualizer();
        }

        private void EnsureBuilt()
        {
            var rootRect = transform as RectTransform;
            if (rootRect == null)
            {
                return;
            }

            var csf = gameObject.GetComponent<ContentSizeFitter>();
            if (csf == null)
            {
                csf = gameObject.AddComponent<ContentSizeFitter>();
            }

            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var vlg = gameObject.GetComponent<VerticalLayoutGroup>();
            if (vlg == null)
            {
                vlg = gameObject.AddComponent<VerticalLayoutGroup>();
            }

            vlg.padding = new RectOffset(4, 4, 4, 4);
            vlg.spacing = 4f;
            vlg.childControlWidth = false;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;

            var rootLayout = gameObject.GetComponent<LayoutElement>();
            if (rootLayout == null)
            {
                rootLayout = gameObject.AddComponent<LayoutElement>();
            }

            rootLayout.preferredWidth = 48f;
            rootLayout.minWidth = 48f;

            EnsureLabel();
            EnsureToggleRow();
        }

        private void EnsureLabel()
        {
            var label = transform.Find("Title")?.GetComponent<Text>();
            if (label == null)
            {
                var go = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                go.transform.SetParent(transform, false);
                label = go.GetComponent<Text>();
            }

            label.text = "Directional Links";
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.fontSize = 11;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (label.font == null)
            {
                label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            label.raycastTarget = false;

            var layout = label.GetComponent<LayoutElement>();
            if (layout == null)
            {
                layout = label.gameObject.AddComponent<LayoutElement>();
            }

            layout.preferredHeight = 16f;
            layout.minHeight = 16f;
        }

        private void EnsureToggleRow()
        {
            var row = transform.Find("ToggleRow");
            if (row == null)
            {
                var rowGo = new GameObject("ToggleRow", typeof(RectTransform), typeof(CanvasRenderer));
                rowGo.transform.SetParent(transform, false);
                row = rowGo.transform;
            }

            var existingHorizontal = row.GetComponent<HorizontalLayoutGroup>();
            if (existingHorizontal != null)
            {
                Destroy(existingHorizontal);
            }

            var vlg = row.GetComponent<VerticalLayoutGroup>();
            if (vlg == null)
            {
                vlg = row.gameObject.AddComponent<VerticalLayoutGroup>();
            }

            vlg.spacing = 3f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(0, 0, 0, 0);

            var rowLayout = row.GetComponent<LayoutElement>();
            if (rowLayout == null)
            {
                rowLayout = row.gameObject.AddComponent<LayoutElement>();
            }

            rowLayout.preferredWidth = 40f;
            rowLayout.minWidth = 40f;

            var group = row.GetComponent<ToggleGroup>();
            if (group == null)
            {
                group = row.gameObject.AddComponent<ToggleGroup>();
            }

            group.allowSwitchOff = false;

            EnsureIconTextures();
            _offToggle = EnsureToggle(row, group, "OffToggle", LinkToggleIconKind.Off, _offIconTexture, OnOffToggleChanged);
            _strongToggle = EnsureToggle(row, group, "StrongToggle", LinkToggleIconKind.Strong, _strongIconTexture, OnStrongToggleChanged);
            _weakToggle = EnsureToggle(row, group, "WeakToggle", LinkToggleIconKind.Weak, _weakIconTexture, OnWeakToggleChanged);
        }

        private static Toggle EnsureToggle(
            Transform parent,
            ToggleGroup group,
            string name,
            LinkToggleIconKind iconKind,
            Texture2D iconTexture,
            UnityEngine.Events.UnityAction<bool> callback)
        {
            var existing = parent.Find(name);
            GameObject host;
            if (existing == null)
            {
                host = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Toggle));
                host.transform.SetParent(parent, false);
            }
            else
            {
                host = existing.gameObject;
            }

            var background = host.GetComponent<Image>();
            background.color = new Color(0.16f, 0.16f, 0.2f, 0.97f);

            var toggle = host.GetComponent<Toggle>();
            toggle.group = group;
            toggle.targetGraphic = background;

            var selection = host.transform.Find("Selection") as RectTransform;
            if (selection == null)
            {
                var selectionGo = new GameObject("Selection", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                selectionGo.transform.SetParent(host.transform, false);
                selection = selectionGo.GetComponent<RectTransform>();
            }

            selection.anchorMin = Vector2.zero;
            selection.anchorMax = Vector2.one;
            selection.offsetMin = Vector2.zero;
            selection.offsetMax = Vector2.zero;

            var selectionImage = selection.GetComponent<Image>();
            selectionImage.color = new Color(0.95f, 0.67f, 0.22f, 0.3f);

            toggle.graphic = selectionImage;

            var labelTransform = host.transform.Find("Label") as RectTransform;
            if (labelTransform != null)
            {
                labelTransform.gameObject.SetActive(false);
            }

            var iconTransform = host.transform.Find("Icon") as RectTransform;
            if (iconTransform == null)
            {
                var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                iconGo.transform.SetParent(host.transform, false);
                iconTransform = iconGo.GetComponent<RectTransform>();
            }

            iconTransform.anchorMin = new Vector2(0.5f, 0.5f);
            iconTransform.anchorMax = new Vector2(0.5f, 0.5f);
            iconTransform.pivot = new Vector2(0.5f, 0.5f);
            iconTransform.sizeDelta = iconKind == LinkToggleIconKind.Off
                ? new Vector2(18f, 18f)
                : new Vector2(20f, 14f);
            iconTransform.anchoredPosition = Vector2.zero;

            var iconImage = iconTransform.GetComponent<RawImage>();
            iconImage.texture = iconTexture;
            iconImage.color = Color.white;
            iconImage.raycastTarget = false;

            var layout = host.GetComponent<LayoutElement>();
            if (layout == null)
            {
                layout = host.AddComponent<LayoutElement>();
            }

            layout.preferredHeight = 34f;
            layout.minHeight = 34f;
            layout.preferredWidth = 36f;
            layout.minWidth = 36f;

            toggle.onValueChanged.RemoveAllListeners();
            toggle.onValueChanged.AddListener(callback);
            return toggle;
        }

        private void EnsureIconTextures()
        {
            if (_offIconTexture == null)
            {
                _offIconTexture = BuildOffIconTexture();
            }

            if (_strongIconTexture == null)
            {
                _strongIconTexture = BuildArrowIconTexture(dotted: false, new Color(0.96f, 0.28f, 0.28f, 1f));
            }

            if (_weakIconTexture == null)
            {
                _weakIconTexture = BuildArrowIconTexture(dotted: true, new Color(0.14f, 0.82f, 0.28f, 1f));
            }
        }

        private static Texture2D BuildOffIconTexture()
        {
            const int size = 24;
            var pixels = AllocateTransparentPixels(size);
            DrawIconLine(pixels, size, 4, 4, 19, 19, Color.white, 2);
            DrawIconLine(pixels, size, 19, 4, 4, 19, Color.white, 2);
            return BuildTexture(size, pixels);
        }

        private static Texture2D BuildArrowIconTexture(bool dotted, Color color)
        {
            const int size = 24;
            var pixels = AllocateTransparentPixels(size);

            if (dotted)
            {
                for (int x = 3; x <= 14; x += 3)
                {
                    DrawIconDot(pixels, size, x, 12, 1, color);
                }
            }
            else
            {
                DrawIconLine(pixels, size, 3, 12, 16, 12, color, 2);
            }

            DrawIconLine(pixels, size, 15, 12, 21, 12, color, 2);
            DrawIconLine(pixels, size, 16, 8, 21, 12, color, 2);
            DrawIconLine(pixels, size, 16, 16, 21, 12, color, 2);

            return BuildTexture(size, pixels);
        }

        private static Color[] AllocateTransparentPixels(int size)
        {
            var pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color(0f, 0f, 0f, 0f);
            }

            return pixels;
        }

        private static Texture2D BuildTexture(int size, Color[] pixels)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private static void DrawIconDot(Color[] pixels, int size, int cx, int cy, int radius, Color color)
        {
            for (int y = cy - radius; y <= cy + radius; y++)
            {
                if (y < 0 || y >= size)
                {
                    continue;
                }

                for (int x = cx - radius; x <= cx + radius; x++)
                {
                    if (x < 0 || x >= size)
                    {
                        continue;
                    }

                    int dx = x - cx;
                    int dy = y - cy;
                    if (dx * dx + dy * dy <= radius * radius)
                    {
                        pixels[y * size + x] = color;
                    }
                }
            }
        }

        private static void DrawIconLine(Color[] pixels, int size, int x0, int y0, int x1, int y1, Color color, int thickness)
        {
            int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
            if (steps <= 0)
            {
                return;
            }

            int half = Mathf.Max(0, thickness / 2);
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t));
                int y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));

                for (int oy = -half; oy <= half; oy++)
                {
                    int py = y + oy;
                    if (py < 0 || py >= size)
                    {
                        continue;
                    }

                    for (int ox = -half; ox <= half; ox++)
                    {
                        int px = x + ox;
                        if (px < 0 || px >= size)
                        {
                            continue;
                        }

                        pixels[py * size + px] = color;
                    }
                }
            }
        }

        private void OnOffToggleChanged(bool isOn)
        {
            if (!isOn || _isUpdatingUi)
            {
                return;
            }

            if (BoardVisualizer == null)
            {
                BoardVisualizer = FindAnyObjectByType<BoardVisualizer>();
            }

            BoardVisualizer?.SetDirectionalLinkToolMode(BoardVisualizer.DirectionalLinkToolMode.Off);
        }

        private void OnStrongToggleChanged(bool isOn)
        {
            if (!isOn || _isUpdatingUi)
            {
                return;
            }

            if (BoardVisualizer == null)
            {
                BoardVisualizer = FindAnyObjectByType<BoardVisualizer>();
            }

            BoardVisualizer?.SetDirectionalLinkToolMode(BoardVisualizer.DirectionalLinkToolMode.Strong);
        }

        private void OnWeakToggleChanged(bool isOn)
        {
            if (!isOn || _isUpdatingUi)
            {
                return;
            }

            if (BoardVisualizer == null)
            {
                BoardVisualizer = FindAnyObjectByType<BoardVisualizer>();
            }

            BoardVisualizer?.SetDirectionalLinkToolMode(BoardVisualizer.DirectionalLinkToolMode.Weak);
        }

        private void SyncFromVisualizer()
        {
            if (_offToggle == null || _strongToggle == null || _weakToggle == null)
            {
                return;
            }

            if (BoardVisualizer == null)
            {
                BoardVisualizer = FindAnyObjectByType<BoardVisualizer>();
            }

            var mode = BoardVisualizer != null
                ? BoardVisualizer.CurrentLinkToolMode
                : Sudoku.Solver.BoardVisualizer.DirectionalLinkToolMode.Off;

            _isUpdatingUi = true;
            _offToggle.SetIsOnWithoutNotify(mode == Sudoku.Solver.BoardVisualizer.DirectionalLinkToolMode.Off);
            _strongToggle.SetIsOnWithoutNotify(mode == Sudoku.Solver.BoardVisualizer.DirectionalLinkToolMode.Strong);
            _weakToggle.SetIsOnWithoutNotify(mode == Sudoku.Solver.BoardVisualizer.DirectionalLinkToolMode.Weak);
            _isUpdatingUi = false;
        }
    }
}
