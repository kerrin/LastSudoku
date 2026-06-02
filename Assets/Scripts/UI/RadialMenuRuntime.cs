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

    public enum RadialDigitActionType
    {
        DefaultDigit = 0,
        RemoveCandidate = 1,
        AddCandidate = 2,
        UnitCandidateAction = 3,
        ClearValue = 4
    }

    public class RadialMenuSelection
    {
        public RadialMenuSegmentId SegmentId;
        public int? Digit;
        public RadialDigitActionType DigitActionType = RadialDigitActionType.DefaultDigit;
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

        [Tooltip("Diameter of the center segment")]
        public float CenterDiameter = 104f;

        [Tooltip("Diameter of each outer segment")]
        public float OuterSegmentDiameter = 68f;

        [Tooltip("Radius from center to outer segment centers")]
        public float OuterSegmentRadius = 88f;

        public Color BackgroundColor = new Color(0.12f, 0.13f, 0.16f, 0.94f);
        public Color SegmentColor = new Color(0.22f, 0.24f, 0.29f, 0.96f);
        public Color DigitSegmentColor = new Color(0.08f, 0.09f, 0.12f, 0.98f);
        public Color HoverColor = new Color(0.85f, 0.76f, 0.28f, 1f);
        public Color ActiveDigitColor = new Color(0.08f, 0.09f, 0.12f, 0.98f);
        public Color DisabledSegmentColor = new Color(0.15f, 0.15f, 0.18f, 0.72f);
        public Color CenterColor = new Color(0.16f, 0.36f, 0.54f, 0.98f);
        public Color TextColor = Color.white;
        public Color TextHoverColor = Color.black;
        public Color SubActionColor = new Color(0.14f, 0.16f, 0.2f, 0.96f);
        public Color SubActionHoverColor = new Color(0.98f, 0.84f, 0.35f, 1f);
        public Color SubActionDisabledColor = new Color(0.24f, 0.26f, 0.3f, 1f);
        public Color SubActionTextColor = Color.white;
        public Color SubActionHoverTextColor = Color.black;
        public float SubActionButtonDiameter = 44f;
        // Keep sub-actions near the digit while avoiding overlap that steals main-button clicks.
        public float SubActionOutwardGap = 6f;
        public float SubActionLineSpacing = 2f;

        public bool IsOpen { get; private set; }
        public RadialMenuSegmentId HoveredSegmentId { get; private set; } = RadialMenuSegmentId.None;
        public RadialMenuSegmentId SelectedSegmentId { get; private set; } = RadialMenuSegmentId.None;
        public Vector2 OpenScreenPosition { get; private set; }
        public Vector2 DisplayScreenPosition { get; private set; }
        public Vector2 DisplayGuiPosition { get; private set; }
        public string CenterLabel { get; private set; } = "No Action";
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
        private readonly bool[] _digitCandidatePresent = new bool[10];
        private int? _currentCellValue;
        private bool _isGivenCell;
        private RadialDigitActionType _hoveredDigitActionType = RadialDigitActionType.DefaultDigit;

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
            // Center is intentionally static and acts as an explicit no-op action.
            CenterLabel = "No Action";
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
            _hoveredDigitActionType = RadialDigitActionType.DefaultDigit;
            ApplyVisibility(false);
            RefreshVisuals();
            _lastLoggedHover = RadialMenuSegmentId.None;
        }

        public void UpdatePointer(Vector2 screenPosition)
        {
            if (!IsOpen) return;
            if (TryResolveSubActionHit(screenPosition, out var subSegmentId, out var subActionType))
            {
                HoveredSegmentId = subSegmentId;
                _hoveredDigitActionType = subActionType;
            }
            else
            {
                HoveredSegmentId = ResolveSegment(screenPosition);
                _hoveredDigitActionType = ResolveDigitActionType(screenPosition, HoveredSegmentId);
            }
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

            if (TryResolveSubActionHit(screenPosition, out var subSegmentId, out var subActionType))
            {
                HoveredSegmentId = subSegmentId;
                _hoveredDigitActionType = subActionType;
            }
            else
            {
                HoveredSegmentId = ResolveSegment(screenPosition);
                _hoveredDigitActionType = ResolveDigitActionType(screenPosition, HoveredSegmentId);
            }
            SelectedSegmentId = HoveredSegmentId;
            var result = BuildSelection(SelectedSegmentId, _hoveredDigitActionType);
            SelectionCommitted?.Invoke(result);
            Debug.Log($"[RadialMenuRuntime] Release segment={result.SegmentId} digit={(result.Digit.HasValue ? result.Digit.Value.ToString() : "(none)")} action={result.DigitActionType} label='{result.Label}' pointer={screenPosition}", this);
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

            float outerRadius = GetOuterActionRadius();
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

            if (segmentId == RadialMenuSegmentId.TopNoAction)
            {
                position = DisplayGuiPosition + DirectionFromAngle(90f) * GetTopActionLabelRadius();
                return true;
            }

            if (IsDigitSegment(segmentId))
            {
                float centerAngle = GetSegmentCenterAngle(segmentId);
                position = DisplayGuiPosition + DirectionFromAngle(centerAngle) * GetDigitBandCenterRadius(0);
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

        public bool TryGetDigitActionButtonPosition(RadialMenuSegmentId segmentId, RadialDigitActionType actionType, out Vector2 position)
        {
            position = default;
            if (!IsOpen) return false;
            if (!IsDigitSegment(segmentId)) return false;
            if (!TryGetDigitActionCenter(segmentId, actionType, out position)) return false;
            return true;
        }

        public RadialMenuSelection BuildSelection(RadialMenuSegmentId segmentId, RadialDigitActionType actionType = RadialDigitActionType.DefaultDigit)
        {
            return new RadialMenuSelection
            {
                SegmentId = segmentId,
                Digit = SegmentIdToDigit(segmentId),
                DigitActionType = actionType,
                Label = GetLabel(segmentId)
            };
        }

        public void SetCellContext(int? cellValue, IReadOnlyCollection<int> candidates, bool isGivenCell = false)
        {
            _currentCellValue = cellValue;
            _isGivenCell = isGivenCell;
            for (int i = 0; i < _digitCandidatePresent.Length; i++) _digitCandidatePresent[i] = false;

            if (candidates == null)
            {
                return;
            }

            foreach (var candidate in candidates)
            {
                if (candidate >= 1 && candidate <= 9)
                {
                    _digitCandidatePresent[candidate] = true;
                }
            }
        }

        public string GetLabel(RadialMenuSegmentId segmentId)
        {
            if (IsDigitSegment(segmentId) && _currentCellValue.HasValue)
            {
                int? digit = SegmentIdToDigit(segmentId);
                if (digit.HasValue && digit.Value == _currentCellValue.Value)
                {
                    return "Clear";
                }
            }

            switch (segmentId)
            {
                case RadialMenuSegmentId.TopNoAction:
                    if (_currentCellValue.HasValue)
                    {
                        return $"Remove {_currentCellValue.Value}";
                    }
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
                    return "No Action";
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
                bool enabled = IsSegmentEnabled(segmentId);
                bool canHover = enabled && segmentId != RadialMenuSegmentId.SmartCenter;
                bool hovered = canHover && HoveredSegmentId == segmentId;
                if (segmentId == RadialMenuSegmentId.SmartCenter)
                {
                    if (!TryGetSegmentScreenPosition(segmentId, out var centerPosition)) continue;
                    var rect = new Rect(centerPosition.x - CenterDiameter * 0.5f, centerPosition.y - CenterDiameter * 0.5f, CenterDiameter, CenterDiameter);
                    var fill = GetBaseColor(segmentId, false);
                    DrawFilledCircle(centerPosition, CenterDiameter * 0.5f, fill, 40);
                    var style = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        normal = { textColor = TextColor },
                        fontSize = 18,
                        wordWrap = false,
                        clipping = TextClipping.Clip
                    };
                    GUI.Label(rect, GetLabel(segmentId), style);
                }
                else if (segmentId == RadialMenuSegmentId.TopNoAction)
                {
                    DrawTopActionWedge(enabled, hovered);
                }
                else if (IsDigitSegment(segmentId))
                {
                    DrawDigitWedge(segmentId, enabled);
                }
            }
        }

        private void DrawDigitWedge(RadialMenuSegmentId segmentId, bool enabled)
        {
            if (!IsDigitSegment(segmentId))
            {
                return;
            }

            float centerAngle = GetSegmentCenterAngle(segmentId);
            float startAngle = centerAngle + 18f - 2f;
            float endAngle = centerAngle - 18f + 2f;

            DrawDigitWedgeBand(segmentId, ResolvePrimaryDigitAction(segmentId), GetLabel(segmentId), 0, enabled, startAngle, endAngle);
            DrawDigitWedgeBand(segmentId, RadialDigitActionType.AddCandidate, "+", 1, enabled && IsDigitSubActionEnabled(segmentId, RadialDigitActionType.AddCandidate), startAngle, endAngle);
            DrawDigitWedgeBand(segmentId, RadialDigitActionType.RemoveCandidate, "-", 2, enabled && IsDigitSubActionEnabled(segmentId, RadialDigitActionType.RemoveCandidate), startAngle, endAngle);
        }

        private void DrawDigitWedgeBand(RadialMenuSegmentId segmentId, RadialDigitActionType actionType, string label, int slotIndex, bool enabled, float startAngle, float endAngle)
        {
            GetDigitBandRadii(slotIndex, out float innerRadius, out float outerRadius);

            bool hovered = enabled && HoveredSegmentId == segmentId && _hoveredDigitActionType == actionType;
            Color fill;
            Color labelColor;

            if (slotIndex == 0)
            {
                fill = enabled ? GetBaseColor(segmentId, hovered) : DisabledSegmentColor;
                labelColor = hovered ? TextHoverColor : TextColor;
            }
            else
            {
                fill = enabled ? (hovered ? SubActionHoverColor : SubActionColor) : SubActionDisabledColor;
                labelColor = hovered ? SubActionHoverTextColor : SubActionTextColor;
            }

            if (!enabled)
            {
                labelColor = new Color(labelColor.r, labelColor.g, labelColor.b, 0.75f);
            }

            DrawFilledSectorBand(innerRadius, outerRadius, startAngle, endAngle, fill, 28);

            if (!TryGetDigitActionCenter(segmentId, actionType, out var labelCenter))
            {
                return;
            }

            var labelRect = new Rect(labelCenter.x - 18f, labelCenter.y - 11f, 36f, 22f);

            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = labelColor },
                fontSize = slotIndex == 0 ? 13 : 10,
                wordWrap = false,
                clipping = TextClipping.Clip
            };
            GUI.Label(labelRect, label, style);
        }

        private bool TryGetDigitSubActionRect(RadialMenuSegmentId segmentId, Vector2 segmentPosition, int slotIndex, out Rect rect)
        {
            rect = default;
            if (!IsDigitSegment(segmentId)) return false;
            if (slotIndex < 0 || slotIndex > 2) return false;

            RadialDigitActionType actionType = slotIndex switch
            {
                0 => ResolvePrimaryDigitAction(segmentId),
                1 => RadialDigitActionType.AddCandidate,
                _ => RadialDigitActionType.RemoveCandidate
            };

            if (!TryGetDigitActionCenter(segmentId, actionType, out var center)) return false;

            float diameter = slotIndex == 0 ? 34f : 30f;

            rect = new Rect(
                center.x - diameter * 0.5f,
                center.y - diameter * 0.5f,
                diameter,
                diameter);
            return true;
        }

        private RadialDigitActionType ResolveDigitActionType(Vector2 screenPosition, RadialMenuSegmentId segmentId)
        {
            if (!IsDigitSegment(segmentId)) return RadialDigitActionType.DefaultDigit;
            if (_isGivenCell) return RadialDigitActionType.DefaultDigit;

            float radius = (screenPosition - DisplayGuiPosition).magnitude;
            GetDigitBandRadii(0, out float primaryInner, out float primaryOuter);
            if (radius >= primaryInner && radius <= primaryOuter)
            {
                return ResolvePrimaryDigitAction(segmentId);
            }

            return RadialDigitActionType.DefaultDigit;
        }

        private bool IsDigitSubActionEnabled(RadialMenuSegmentId segmentId, RadialDigitActionType actionType)
        {
            if (_isGivenCell) return false;

            var digit = SegmentIdToDigit(segmentId);
            if (!digit.HasValue) return false;

            switch (actionType)
            {
                case RadialDigitActionType.RemoveCandidate:
                    return _digitCandidatePresent[digit.Value];
                case RadialDigitActionType.AddCandidate:
                    return !_digitCandidatePresent[digit.Value] || _currentCellValue.HasValue;
                case RadialDigitActionType.UnitCandidateAction:
                    return false;
                default:
                    return true;
            }
        }

        private static bool IsDigitSegment(RadialMenuSegmentId segmentId)
        {
            return segmentId >= RadialMenuSegmentId.Digit1 && segmentId <= RadialMenuSegmentId.Digit9;
        }

        private bool TryResolveSubActionHit(Vector2 screenPosition, out RadialMenuSegmentId segmentId, out RadialDigitActionType actionType)
        {
            segmentId = RadialMenuSegmentId.None;
            actionType = RadialDigitActionType.DefaultDigit;

            for (int i = 0; i < OuterOrder.Length; i++)
            {
                var candidateSegment = OuterOrder[i];
                if (!IsDigitSegment(candidateSegment))
                {
                    continue;
                }

                if (!TryResolveDigitBand(screenPosition, candidateSegment, out var candidateAction, out var hitDisabledBand))
                {
                    continue;
                }

                if (hitDisabledBand)
                {
                    segmentId = RadialMenuSegmentId.None;
                    actionType = RadialDigitActionType.DefaultDigit;
                    return true;
                }

                segmentId = candidateSegment;
                actionType = candidateAction;
                return true;
            }

            return false;
        }

        private bool TryResolveDigitBand(Vector2 screenPosition, RadialMenuSegmentId segmentId, out RadialDigitActionType actionType, out bool hitDisabledBand)
        {
            actionType = RadialDigitActionType.DefaultDigit;
            hitDisabledBand = false;

            if (!IsDigitSegment(segmentId)) return false;
            if (ResolveSegment(screenPosition) != segmentId) return false;

            float radius = (screenPosition - DisplayGuiPosition).magnitude;
            GetDigitBandRadii(1, out float addInner, out float addOuter);
            GetDigitBandRadii(2, out float removeInner, out float removeOuter);

            if (radius >= addInner && radius <= addOuter)
            {
                actionType = RadialDigitActionType.AddCandidate;
                hitDisabledBand = !IsDigitSubActionEnabled(segmentId, actionType);
                return true;
            }

            if (radius >= removeInner && radius <= removeOuter)
            {
                actionType = RadialDigitActionType.RemoveCandidate;
                hitDisabledBand = !IsDigitSubActionEnabled(segmentId, actionType);
                return true;
            }

            return false;
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

        private void DrawTopActionWedge(bool enabled, bool hovered)
        {
            float startAngle = 90f + 18f - 2f;
            float endAngle = 90f - 18f + 2f;
            float innerRadius = GetInnerActionRadius();
            float outerRadius = GetOuterActionRadius();
            DrawFilledSectorBand(innerRadius, outerRadius, startAngle, endAngle, GetBaseColor(RadialMenuSegmentId.TopNoAction, hovered), 28);

            var labelCenter = DisplayGuiPosition + DirectionFromAngle(90f) * GetTopActionLabelRadius();
            var labelRect = new Rect(labelCenter.x - 38f, labelCenter.y - 12f, 76f, 24f);
            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = enabled ? (hovered ? TextHoverColor : TextColor) : new Color(TextColor.r, TextColor.g, TextColor.b, 0.65f) },
                fontSize = 12,
                wordWrap = false,
                clipping = TextClipping.Clip
            };
            GUI.Label(labelRect, GetLabel(RadialMenuSegmentId.TopNoAction), style);
        }

        private static Vector2 DirectionFromAngle(float angleDegrees)
        {
            float radians = angleDegrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), -Mathf.Sin(radians));
        }

        private void DrawFilledTriangle(Vector2 p0, Vector2 p1, Vector2 p2, Color color)
        {
            if (_pixelTexture == null) return;

            // Sort by Y ascending for scanline rasterization in IMGUI coordinates.
            if (p1.y < p0.y) Swap(ref p0, ref p1);
            if (p2.y < p0.y) Swap(ref p0, ref p2);
            if (p2.y < p1.y) Swap(ref p1, ref p2);

            float minY = p0.y;
            float maxY = p2.y;
            if (maxY - minY < 0.001f)
            {
                return;
            }

            Color previous = GUI.color;
            GUI.color = color;

            // Bottom-flat section: p0 -> p1 and p0 -> p2 until y == p1.y
            if (p1.y > p0.y)
            {
                DrawTriangleSection(p0, p1, p0, p2);
            }

            // Top-flat section: p1 -> p2 and p0 -> p2 from y == p1.y to p2.y
            if (p2.y > p1.y)
            {
                DrawTriangleSection(p1, p2, p0, p2);
            }

            GUI.color = previous;
        }

        private void DrawFilledSectorBand(float innerRadius, float outerRadius, float startAngle, float endAngle, Color color, int steps)
        {
            if (_pixelTexture == null) return;
            float radiusOverlap = 1.0f;
            float effectiveInnerRadius = Mathf.Max(0f, innerRadius - radiusOverlap);
            float effectiveOuterRadius = outerRadius + radiusOverlap;
            float averageRadius = (effectiveInnerRadius + effectiveOuterRadius) * 0.5f;

            int lineCount = Mathf.Max(steps * 3, 72);
            float angleStep = (endAngle - startAngle) / lineCount;
            float lineWidth = Mathf.Max(2f, Mathf.Abs(angleStep) * Mathf.Deg2Rad * averageRadius + 1.5f);

            for (int i = 0; i <= lineCount; i++)
            {
                float angle = startAngle + angleStep * i;
                Vector2 inner = DisplayGuiPosition + DirectionFromAngle(angle) * effectiveInnerRadius;
                Vector2 outer = DisplayGuiPosition + DirectionFromAngle(angle) * effectiveOuterRadius;
                DrawLineSegment(inner, outer, lineWidth, color);
            }
        }

        private void DrawFilledCircle(Vector2 center, float radius, Color color, int steps)
        {
            if (_pixelTexture == null) return;
            Color previous = GUI.color;
            GUI.color = color;

            for (int i = 0; i < steps; i++)
            {
                float angle0 = (360f / steps) * i;
                float angle1 = (360f / steps) * (i + 1);
                Vector2 p0 = center;
                Vector2 p1 = center + DirectionFromAngle(angle0) * radius;
                Vector2 p2 = center + DirectionFromAngle(angle1) * radius;
                DrawFilledTriangle(p0, p1, p2, color);
            }

            GUI.color = previous;
        }

        private void DrawTriangleSection(Vector2 leftStart, Vector2 leftEnd, Vector2 rightStart, Vector2 rightEnd)
        {
            float leftDeltaY = leftEnd.y - leftStart.y;
            float rightDeltaY = rightEnd.y - rightStart.y;
            if (leftDeltaY <= 0.0001f || rightDeltaY <= 0.0001f)
            {
                return;
            }

            float yStart = Mathf.Ceil(leftStart.y);
            float yEnd = Mathf.Floor(leftEnd.y);
            for (float y = yStart; y <= yEnd; y += 1f)
            {
                float tLeft = Mathf.Clamp01((y - leftStart.y) / leftDeltaY);
                float tRight = Mathf.Clamp01((y - rightStart.y) / rightDeltaY);
                float xLeft = Mathf.Lerp(leftStart.x, leftEnd.x, tLeft);
                float xRight = Mathf.Lerp(rightStart.x, rightEnd.x, tRight);

                float xMin = Mathf.Min(xLeft, xRight);
                float xMax = Mathf.Max(xLeft, xRight);
                float width = xMax - xMin;
                if (width > 0.01f)
                {
                    GUI.DrawTexture(new Rect(xMin, y, width, 1f), _pixelTexture);
                }
            }
        }

        private static void Swap(ref Vector2 a, ref Vector2 b)
        {
            var tmp = a;
            a = b;
            b = tmp;
        }

        private void DrawLineSegment(Vector2 start, Vector2 end, float width, Color color)
        {
            if (_pixelTexture == null) return;

            float angle = Mathf.Atan2(end.y - start.y, end.x - start.x) * Mathf.Rad2Deg;
            float length = Vector2.Distance(start, end);
            if (length <= 0.001f) return;

            Color previous = GUI.color;
            GUI.color = color;
            GUIUtility.RotateAroundPivot(angle, start);
            GUI.DrawTexture(new Rect(start.x, start.y - width * 0.5f, length, width), _pixelTexture);
            GUIUtility.RotateAroundPivot(-angle, start);
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
            float radius = GetMaxVisualRadius();
            float screenX = requestedGuiPosition.x;
            float screenY = Screen.height - requestedGuiPosition.y;

            screenX = Mathf.Clamp(screenX, radius, Mathf.Max(radius, Screen.width - radius));
            screenY = Mathf.Clamp(screenY, radius, Mathf.Max(radius, Screen.height - radius));

            DisplayScreenPosition = new Vector2(screenX, screenY);
            DisplayGuiPosition = new Vector2(screenX, Screen.height - screenY);
        }

        private float GetMaxVisualRadius()
        {
            float radius = Mathf.Max(ShellDiameter * 0.5f, GetOuterActionRadius());
            radius = Mathf.Max(radius, GetMaxSubActionExtentRadius());
            return radius;
        }

        private float GetMaxSubActionExtentRadius()
        {
            return GetOuterActionRadius();
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
                bool enabled = IsSegmentEnabled(kvp.Key);
                bool canHover = enabled && kvp.Key != RadialMenuSegmentId.SmartCenter;
                bool hovered = canHover && HoveredSegmentId == kvp.Key;
                kvp.Value.color = GetBaseColor(kvp.Key, hovered);
            }

            if (_segmentLabels.TryGetValue(RadialMenuSegmentId.SmartCenter, out var centerLabel))
            {
                centerLabel.text = CenterLabel;
            }
        }

        private Color GetBaseColor(RadialMenuSegmentId segmentId, bool hovered)
        {
            if (!IsSegmentEnabled(segmentId))
            {
                return DisabledSegmentColor;
            }

            if (segmentId == RadialMenuSegmentId.SmartCenter)
            {
                // Center No Action should remain neutral regardless of board state or hover.
                return DisabledSegmentColor;
            }

            if (segmentId == RadialMenuSegmentId.TopNoAction)
            {
                if (_currentCellValue.HasValue)
                {
                    return hovered ? HoverColor : DigitSegmentColor;
                }

                return DisabledSegmentColor;
            }

            // Current-value digit should look active, but only look hovered when actually hovered.
            if (IsDigitSegment(segmentId) && _currentCellValue.HasValue)
            {
                int? digit = SegmentIdToDigit(segmentId);
                if (digit.HasValue && digit.Value == _currentCellValue.Value)
                {
                    return hovered ? HoverColor : ActiveDigitColor;
                }
            }

            if (IsDigitSegment(segmentId))
            {
                return hovered ? HoverColor : DigitSegmentColor;
            }

            return hovered ? HoverColor : SegmentColor;
        }

        private static float GetSegmentCenterAngle(RadialMenuSegmentId segmentId)
        {
            int index = Array.IndexOf(DisplayOrder, segmentId);
            return 90f - index * 36f;
        }

        private float GetInnerActionRadius()
        {
            return CenterDiameter * 0.5f + 6f;
        }

        private float GetOuterActionRadius()
        {
            return OuterSegmentRadius + OuterSegmentDiameter * 0.5f;
        }

        private float GetDigitBandThickness()
        {
            return (GetOuterActionRadius() - GetInnerActionRadius()) / 3f;
        }

        private void GetDigitBandRadii(int slotIndex, out float innerRadius, out float outerRadius)
        {
            float bandThickness = GetDigitBandThickness();
            innerRadius = GetInnerActionRadius() + bandThickness * slotIndex;
            outerRadius = innerRadius + bandThickness;
        }

        private float GetDigitBandCenterRadius(int slotIndex)
        {
            GetDigitBandRadii(slotIndex, out float innerRadius, out float outerRadius);
            return (innerRadius + outerRadius) * 0.5f;
        }

        private float GetTopActionLabelRadius()
        {
            return (GetInnerActionRadius() + GetOuterActionRadius()) * 0.5f;
        }

        private RadialDigitActionType ResolvePrimaryDigitAction(RadialMenuSegmentId segmentId)
        {
            if (_currentCellValue.HasValue)
            {
                int? digit = SegmentIdToDigit(segmentId);
                if (digit.HasValue && digit.Value == _currentCellValue.Value)
                {
                    return RadialDigitActionType.ClearValue;
                }
            }

            return RadialDigitActionType.DefaultDigit;
        }

        private bool TryGetDigitActionCenter(RadialMenuSegmentId segmentId, RadialDigitActionType actionType, out Vector2 position)
        {
            position = default;
            if (!IsDigitSegment(segmentId)) return false;

            int slotIndex = actionType switch
            {
                RadialDigitActionType.AddCandidate => 1,
                RadialDigitActionType.RemoveCandidate => 2,
                _ => 0
            };

            float centerAngle = GetSegmentCenterAngle(segmentId);
            position = DisplayGuiPosition + DirectionFromAngle(centerAngle) * GetDigitBandCenterRadius(slotIndex);
            return true;
        }

        private bool IsSegmentEnabled(RadialMenuSegmentId segmentId)
        {
            if (segmentId == RadialMenuSegmentId.TopNoAction)
            {
                return _currentCellValue.HasValue;
            }

            if (!_isGivenCell) return true;

            return false;
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
