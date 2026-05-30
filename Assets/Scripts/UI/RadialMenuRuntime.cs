using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Sudoku.Scripts.UI
{
    public enum RadialMenuSegmentId
    {
        None = 0,
        TopNoAction = 1,
        Digit1 = 2,
        Digit2 = 3,
        Digit3 = 4,
        Digit4 = 5,
        Digit5 = 6,
        Digit6 = 7,
        Digit7 = 8,
        Digit8 = 9,
        Digit9 = 10,
        SmartCenter = 11
    }

    public class RadialMenuSelection
    {
        public RadialMenuSegmentId SegmentId;
        public int? Digit;
        public string Label;
    }

    [DisallowMultipleComponent]
    [ExecuteAlways]
    [DefaultExecutionOrder(10000)]
    public class RadialMenuRuntime : MonoBehaviour
    {
        private static readonly RadialMenuSegmentId[] OuterOrder =
        {
            RadialMenuSegmentId.TopNoAction,
            RadialMenuSegmentId.Digit1,
            RadialMenuSegmentId.Digit2,
            RadialMenuSegmentId.Digit3,
            RadialMenuSegmentId.Digit4,
            RadialMenuSegmentId.Digit5,
            RadialMenuSegmentId.Digit6,
            RadialMenuSegmentId.Digit7,
            RadialMenuSegmentId.Digit8,
            RadialMenuSegmentId.Digit9
        };

        private static readonly RadialMenuSegmentId[] DisplayOrder =
        {
            RadialMenuSegmentId.TopNoAction,
            RadialMenuSegmentId.Digit1,
            RadialMenuSegmentId.Digit2,
            RadialMenuSegmentId.Digit3,
            RadialMenuSegmentId.Digit4,
            RadialMenuSegmentId.Digit5,
            RadialMenuSegmentId.Digit6,
            RadialMenuSegmentId.Digit7,
            RadialMenuSegmentId.Digit8,
            RadialMenuSegmentId.Digit9,
            RadialMenuSegmentId.SmartCenter
        };

        [Tooltip("Diameter of the radial shell in pixels")]
        public float ShellDiameter = 250f;

        [Tooltip("Diameter of the center Smart segment")]
        public float CenterDiameter = 88f;

        [Tooltip("Diameter of each outer segment")]
        public float OuterSegmentDiameter = 52f;

        [Tooltip("Radius from center to outer segment centers")]
        public float OuterSegmentRadius = 88f;

        public Color BackgroundColor = new Color(0.12f, 0.13f, 0.16f, 0.94f);
        public Color SegmentColor = new Color(0.22f, 0.24f, 0.29f, 0.96f);
        public Color HoverColor = new Color(0.85f, 0.76f, 0.28f, 1f);
        public Color CenterColor = new Color(0.16f, 0.36f, 0.54f, 0.98f);
        public Color TextColor = Color.white;
        public Color TextHoverColor = Color.black;

        public bool IsOpen { get; private set; }
        public RadialMenuSegmentId HoveredSegmentId { get; private set; } = RadialMenuSegmentId.None;
        public RadialMenuSegmentId SelectedSegmentId { get; private set; } = RadialMenuSegmentId.None;
        public Vector2 OpenScreenPosition { get; private set; }
        public Vector2 DisplayScreenPosition { get; private set; }
        public Vector2 DisplayGuiPosition { get; private set; }
        public string CenterLabel { get; private set; } = "Smart";
        public IReadOnlyList<RadialMenuSegmentId> SegmentOrder => DisplayOrder;
        public IReadOnlyList<RadialMenuSegmentId> OuterSegmentOrder => OuterOrder;

        public event Action<RadialMenuSelection> SelectionCommitted;

        private Canvas _canvas;
        private RectTransform _root;
        private CanvasGroup _canvasGroup;
        private Image _background;
        private readonly Dictionary<RadialMenuSegmentId, Image> _segmentImages = new Dictionary<RadialMenuSegmentId, Image>();
        private readonly Dictionary<RadialMenuSegmentId, Text> _segmentLabels = new Dictionary<RadialMenuSegmentId, Text>();
        private readonly Dictionary<RadialMenuSegmentId, RectTransform> _segmentRects = new Dictionary<RadialMenuSegmentId, RectTransform>();
        private bool _built;
        private RadialMenuSegmentId _lastLoggedHover = RadialMenuSegmentId.None;
        private Texture2D _pixelTexture;

        private void OnEnable()
        {
            EnsureBuilt();
            ApplyVisibility(false);
        }

        private void OnValidate()
        {
            if (_built)
            {
                RefreshVisuals();
            }
        }

        private void OnGUI()
        {
            // Rendering is owned by BoardVisualizer.OnGUI so the shell can always be drawn
            // after the board in a single IMGUI pass.
        }

        public void RenderOverlayOnGUI()
        {
            if (!IsOpen) return;
            if (Event.current == null) return;

            int previousDepth = GUI.depth;
            GUI.depth = -10000;
            RenderOverlay();
            GUI.depth = previousDepth;
        }

        public void Open(Vector2 screenPosition, string centerLabel)
        {
            EnsureBuilt();
            OpenScreenPosition = screenPosition;
            CenterLabel = string.IsNullOrWhiteSpace(centerLabel) ? "Smart" : centerLabel;
            IsOpen = true;
            HoveredSegmentId = RadialMenuSegmentId.None;
            SelectedSegmentId = RadialMenuSegmentId.None;
            UpdateDisplayPosition(screenPosition);
            PositionRoot(DisplayScreenPosition);
            UpdateCenterLabel();
            ApplyVisibility(true);
            RefreshVisuals();
            bool clamped = DisplayGuiPosition != screenPosition;
            Debug.Log($"[RadialMenuRuntime] Open requested={screenPosition} displayGui={DisplayGuiPosition} displayScreen={DisplayScreenPosition} clamped={clamped} centerLabel='{CenterLabel}'", this);
        }

        public void Close()
        {
            IsOpen = false;
            HoveredSegmentId = RadialMenuSegmentId.None;
            SelectedSegmentId = RadialMenuSegmentId.None;
            ApplyVisibility(false);
            RefreshVisuals();
            _lastLoggedHover = RadialMenuSegmentId.None;
        }

        public void UpdatePointer(Vector2 screenPosition)
        {
            if (!IsOpen) return;
            HoveredSegmentId = ResolveSegment(screenPosition);
            if (HoveredSegmentId != _lastLoggedHover)
            {
                Debug.Log($"[RadialMenuRuntime] Hover segment={HoveredSegmentId} pointer={screenPosition}", this);
                _lastLoggedHover = HoveredSegmentId;
            }
            RefreshVisuals();
        }

        public RadialMenuSelection ReleasePointer(Vector2 screenPosition)
        {
            if (!IsOpen) return new RadialMenuSelection { SegmentId = RadialMenuSegmentId.None, Label = string.Empty };

            HoveredSegmentId = ResolveSegment(screenPosition);
            SelectedSegmentId = HoveredSegmentId;
            var result = BuildSelection(SelectedSegmentId);
            SelectionCommitted?.Invoke(result);
            Debug.Log($"[RadialMenuRuntime] Release segment={result.SegmentId} digit={(result.Digit.HasValue ? result.Digit.Value.ToString() : "(none)")} label='{result.Label}' pointer={screenPosition}", this);
            Close();
            return result;
        }

        public RadialMenuSegmentId ResolveSegment(Vector2 screenPosition)
        {
            var delta = screenPosition - DisplayGuiPosition;
            float distance = delta.magnitude;

            float centerRadius = CenterDiameter * 0.5f;
            if (distance <= centerRadius)
            {
                return RadialMenuSegmentId.SmartCenter;
            }

            float outerRadius = OuterSegmentRadius + OuterSegmentDiameter * 0.5f;
            if (distance > outerRadius)
            {
                return RadialMenuSegmentId.None;
            }

            float angle = GetClockwiseAngleFromTop(delta);
            if (angle < 18f || angle >= 342f)
            {
                return RadialMenuSegmentId.TopNoAction;
            }

            int outerIndex = Mathf.FloorToInt((angle - 18f) / 36f) + 1;
            outerIndex = Mathf.Clamp(outerIndex, 1, 9);
            return OuterOrder[outerIndex];
        }

        public bool TryGetSegmentScreenPosition(RadialMenuSegmentId segmentId, out Vector2 position)
        {
            position = default;
            if (!IsOpen) return false;

            if (segmentId == RadialMenuSegmentId.SmartCenter)
            {
                position = DisplayGuiPosition;
                return true;
            }

            int index = Array.IndexOf(DisplayOrder, segmentId);
            if (index < 0 || index >= 10) return false;

            float angle = 90f - index * 36f;
            float radians = angle * Mathf.Deg2Rad;
            // IMGUI uses Y-down coordinates, so invert the mathematical Y component.
            position = DisplayGuiPosition + new Vector2(Mathf.Cos(radians), -Mathf.Sin(radians)) * OuterSegmentRadius;
            return true;
        }

        public RadialMenuSelection BuildSelection(RadialMenuSegmentId segmentId)
        {
            return new RadialMenuSelection
            {
                SegmentId = segmentId,
                Digit = SegmentIdToDigit(segmentId),
                Label = GetLabel(segmentId)
            };
        }

        public string GetLabel(RadialMenuSegmentId segmentId)
        {
            switch (segmentId)
            {
                case RadialMenuSegmentId.TopNoAction:
                    return "No Action";
                case RadialMenuSegmentId.Digit1:
                    return "1";
                case RadialMenuSegmentId.Digit2:
                    return "2";
                case RadialMenuSegmentId.Digit3:
                    return "3";
                case RadialMenuSegmentId.Digit4:
                    return "4";
                case RadialMenuSegmentId.Digit5:
                    return "5";
                case RadialMenuSegmentId.Digit6:
                    return "6";
                case RadialMenuSegmentId.Digit7:
                    return "7";
                case RadialMenuSegmentId.Digit8:
                    return "8";
                case RadialMenuSegmentId.Digit9:
                    return "9";
                case RadialMenuSegmentId.SmartCenter:
                    return CenterLabel;
                default:
                    return string.Empty;
            }
        }

        private void EnsureBuilt()
        {
            if (_built) return;

            _canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
            if (_canvas == null)
            {
                var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                _canvas = canvasGO.GetComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            var host = new GameObject("RadialMenu", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            host.transform.SetParent(_canvas.transform, false);
            _root = host.GetComponent<RectTransform>();
            _root.anchorMin = new Vector2(0.5f, 0.5f);
            _root.anchorMax = new Vector2(0.5f, 0.5f);
            _root.pivot = new Vector2(0.5f, 0.5f);
            _root.sizeDelta = new Vector2(ShellDiameter, ShellDiameter);

            var nestedCanvas = host.GetComponent<Canvas>();
            nestedCanvas.overrideSorting = true;
            nestedCanvas.sortingOrder = short.MaxValue;
            nestedCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

            _canvasGroup = host.GetComponent<CanvasGroup>();
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.alpha = 0f;

            _background = CreateImageChild(_root, "Background", new Vector2(ShellDiameter, ShellDiameter), BackgroundColor);
            _background.raycastTarget = false;

            foreach (var segmentId in DisplayOrder)
            {
                CreateSegment(segmentId);
            }

            _built = true;
            RefreshVisuals();
        }

        private void RenderOverlay()
        {
            EnsurePixelTexture();

            foreach (var segmentId in DisplayOrder)
            {
                if (!TryGetSegmentScreenPosition(segmentId, out var position)) continue;

                float diameter = segmentId == RadialMenuSegmentId.SmartCenter ? CenterDiameter : OuterSegmentDiameter;
                var rect = new Rect(position.x - diameter * 0.5f, position.y - diameter * 0.5f, diameter, diameter);

                bool hovered = HoveredSegmentId == segmentId;
                var fill = GetBaseColor(segmentId, hovered);
                var labelColor = hovered ? TextHoverColor : TextColor;
                DrawRoundedSegment(rect, fill);

                var label = GetLabel(segmentId);
                var style = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = labelColor },
                    fontSize = segmentId == RadialMenuSegmentId.SmartCenter ? 16 : 14
                };
                GUI.Label(rect, label, style);
            }
        }

        private void EnsurePixelTexture()
        {
            if (_pixelTexture != null) return;
            _pixelTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _pixelTexture.SetPixel(0, 0, Color.white);
            _pixelTexture.Apply();
        }

        private void DrawRoundedSegment(Rect rect, Color color)
        {
            if (_pixelTexture == null) return;
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, _pixelTexture);
            GUI.color = previous;
        }

        private void CreateSegment(RadialMenuSegmentId segmentId)
        {
            var go = new GameObject(segmentId.ToString(), typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(_root, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            var image = go.GetComponent<Image>();
            image.sprite = null;
            image.color = segmentId == RadialMenuSegmentId.SmartCenter ? CenterColor : SegmentColor;
            image.raycastTarget = false;

            var textGO = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer));
            textGO.transform.SetParent(go.transform, false);
            var textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var label = textGO.AddComponent<Text>();
            label.alignment = TextAnchor.MiddleCenter;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (label.font == null) label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            label.fontSize = segmentId == RadialMenuSegmentId.SmartCenter ? 16 : 14;
            label.color = TextColor;
            label.text = GetLabel(segmentId);
            label.raycastTarget = false;

            _segmentImages[segmentId] = image;
            _segmentLabels[segmentId] = label;
            _segmentRects[segmentId] = rect;
        }

        private void UpdateCenterLabel()
        {
            if (_segmentLabels.TryGetValue(RadialMenuSegmentId.SmartCenter, out var label))
            {
                label.text = CenterLabel;
            }
        }

        private void PositionRoot(Vector2 screenPosition)
        {
            if (_root == null) return;
            var canvasRect = _root.parent as RectTransform;
            if (canvasRect != null)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, null, out var localPoint);
                _root.anchoredPosition = localPoint;
            }
            else
            {
                _root.anchoredPosition = screenPosition;
            }
        }

        private void UpdateDisplayPosition(Vector2 requestedGuiPosition)
        {
            float radius = Mathf.Max(ShellDiameter * 0.5f, OuterSegmentRadius + OuterSegmentDiameter * 0.5f);
            float screenX = requestedGuiPosition.x;
            float screenY = Screen.height - requestedGuiPosition.y;

            screenX = Mathf.Clamp(screenX, radius, Mathf.Max(radius, Screen.width - radius));
            screenY = Mathf.Clamp(screenY, radius, Mathf.Max(radius, Screen.height - radius));

            DisplayScreenPosition = new Vector2(screenX, screenY);
            DisplayGuiPosition = new Vector2(screenX, Screen.height - screenY);
        }

        private void ApplyVisibility(bool visible)
        {
            if (_canvasGroup == null) return;
            // The shell is rendered by the IMGUI overlay path so the canvas copy stays hidden.
            _canvasGroup.alpha = 0f;
        }

        private void RefreshVisuals()
        {
            if (!_built) return;

            foreach (var kvp in _segmentImages)
            {
                bool hovered = HoveredSegmentId == kvp.Key;
                kvp.Value.color = GetBaseColor(kvp.Key, hovered);
            }

            if (_segmentLabels.TryGetValue(RadialMenuSegmentId.SmartCenter, out var centerLabel))
            {
                centerLabel.text = CenterLabel;
            }
        }

        private Color GetBaseColor(RadialMenuSegmentId segmentId, bool hovered)
        {
            if (segmentId == RadialMenuSegmentId.SmartCenter)
            {
                return hovered ? HoverColor : CenterColor;
            }

            return hovered ? HoverColor : SegmentColor;
        }

        private static float GetClockwiseAngleFromTop(Vector2 delta)
        {
            float angle = Mathf.Atan2(delta.x, -delta.y) * Mathf.Rad2Deg;
            if (angle < 0f) angle += 360f;
            return angle;
        }

        private static int? SegmentIdToDigit(RadialMenuSegmentId segmentId)
        {
            switch (segmentId)
            {
                case RadialMenuSegmentId.Digit1: return 1;
                case RadialMenuSegmentId.Digit2: return 2;
                case RadialMenuSegmentId.Digit3: return 3;
                case RadialMenuSegmentId.Digit4: return 4;
                case RadialMenuSegmentId.Digit5: return 5;
                case RadialMenuSegmentId.Digit6: return 6;
                case RadialMenuSegmentId.Digit7: return 7;
                case RadialMenuSegmentId.Digit8: return 8;
                case RadialMenuSegmentId.Digit9: return 9;
                default: return null;
            }
        }

        private static Image CreateImageChild(RectTransform parent, string name, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            var image = go.GetComponent<Image>();
            image.sprite = null;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }
    }
}
