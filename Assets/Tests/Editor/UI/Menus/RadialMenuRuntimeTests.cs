using NUnit.Framework;
using Sudoku.Models;
using Sudoku.UI.Config;
using Sudoku.UI.Menus;
using UnityEngine;
using UnityEngine.UI;

namespace Sudoku.Tests.Editor
{
    public class RadialMenuRuntimeTests
    {
        [Test]
        public void Open_BuildsExpectedSegmentLayout_AndUpdatesCenterLabel()
        {
            var canvas = CreateCanvas("RadialMenuCanvas_A");
            try
            {
                var menu = CreateMenu(canvas);
                menu.Open(new Vector2(200f, 200f), "Dynamic Smart");

                Assert.IsTrue(menu.IsOpen);
                Assert.AreEqual(11, menu.SegmentOrder.Count);
                Assert.AreEqual(10, menu.OuterSegmentOrder.Count);
                Assert.AreEqual("No Action", menu.CenterLabel);
                Assert.AreEqual("No Action", menu.GetLabel(RadialMenuSegmentId.SmartCenter));
                Assert.AreEqual("No Action", menu.GetLabel(RadialMenuSegmentId.TopNoAction));
            }
            finally
            {
                Object.DestroyImmediate(canvas.gameObject);
            }
        }

        [Test]
        public void ReleasePointer_OnSubActionButtons_ReturnsExpectedDigitActionType()
        {
            var canvas = CreateCanvas("RadialMenuCanvas_E");
            try
            {
                var menu = CreateMenu(canvas);
                menu.Open(new Vector2(200f, 200f), "Smart");
                menu.SetCellContext(null, new[] { 1 });

                Assert.IsTrue(menu.TryGetDigitActionButtonPosition(RadialMenuSegmentId.Digit1, RadialDigitActionType.RemoveCandidate, out var removePointer));
                Assert.IsTrue(menu.TryGetDigitActionButtonPosition(RadialMenuSegmentId.Digit1, RadialDigitActionType.AddCandidate, out var addPointer));

                var removeSelection = menu.ReleasePointer(removePointer);
                Assert.AreEqual(RadialMenuSegmentId.Digit1, removeSelection.SegmentId);
                Assert.AreEqual(RadialDigitActionType.RemoveCandidate, removeSelection.DigitActionType);

                menu.Open(new Vector2(200f, 200f), "Smart");
                menu.SetCellContext(null, new[] { 1 });
                var addSelection = menu.ReleasePointer(addPointer);
                Assert.AreEqual(RadialMenuSegmentId.None, addSelection.SegmentId, "Disabled sub-action should commit no selection.");
                Assert.AreEqual(RadialDigitActionType.DefaultDigit, addSelection.DigitActionType, "Add sub-action should be disabled when candidate already exists.");
            }
            finally
            {
                Object.DestroyImmediate(canvas.gameObject);
            }
        }

        [Test]
        public void DigitSubActionButtons_AreAlignedOutwardFromCenter()
        {
            var canvas = CreateCanvas("RadialMenuCanvas_F");
            try
            {
                var menu = CreateMenu(canvas);
                menu.Open(new Vector2(220f, 220f), "Smart");

                Assert.IsTrue(menu.TryGetSegmentScreenPosition(RadialMenuSegmentId.Digit1, out var digitCenter));
                Assert.IsTrue(menu.TryGetDigitActionButtonPosition(RadialMenuSegmentId.Digit1, RadialDigitActionType.RemoveCandidate, out var removePos));
                Assert.IsTrue(menu.TryGetDigitActionButtonPosition(RadialMenuSegmentId.Digit1, RadialDigitActionType.AddCandidate, out var addPos));

                var outward = (digitCenter - menu.DisplayGuiPosition).normalized;
                var removeVec = (removePos - digitCenter);
                var addVec = (addPos - digitCenter);

                Assert.Greater(Vector2.Dot(removeVec.normalized, outward), 0.95f);
                Assert.Greater(Vector2.Dot(addVec.normalized, outward), 0.95f);
                Assert.Greater(removeVec.magnitude, addVec.magnitude);
            }
            finally
            {
                Object.DestroyImmediate(canvas.gameObject);
            }
        }

        [Test]
        public void ResolveSegment_MapsCenterAndOuterSegments_InClockwiseOrder()
        {
            var canvas = CreateCanvas("RadialMenuCanvas_B");
            try
            {
                var menu = CreateMenu(canvas);
                menu.Open(new Vector2(200f, 200f), "Smart");

                var center = menu.DisplayGuiPosition;
                Assert.AreEqual(RadialMenuSegmentId.SmartCenter, menu.ResolveSegment(center));
                Assert.AreEqual(RadialMenuSegmentId.TopNoAction, menu.ResolveSegment(center + new Vector2(0f, -menu.OuterSegmentRadius)));

                Assert.IsTrue(menu.TryGetSegmentScreenPosition(RadialMenuSegmentId.Digit1, out var digit1Pos));
                Assert.IsTrue(menu.TryGetSegmentScreenPosition(RadialMenuSegmentId.Digit3, out var digit3Pos));
                Assert.AreEqual(RadialMenuSegmentId.Digit1, menu.ResolveSegment(digit1Pos));
                Assert.AreEqual(RadialMenuSegmentId.Digit3, menu.ResolveSegment(digit3Pos));

                Assert.AreEqual(RadialMenuSegmentId.None, menu.ResolveSegment(center + new Vector2(0f, -(menu.OuterSegmentRadius + menu.OuterSegmentDiameter * 1.25f))));
            }
            finally
            {
                Object.DestroyImmediate(canvas.gameObject);
            }
        }

        [Test]
        public void Open_ClampsMenuInsideScreen_AndKeepsTopSegmentAtTop()
        {
            var canvas = CreateCanvas("RadialMenuCanvas_D");
            try
            {
                var menu = CreateMenu(canvas);
                var requested = new Vector2(4f, 4f);
                menu.Open(requested, "Smart");

                Assert.Greater(menu.DisplayGuiPosition.x, requested.x);
                Assert.Greater(menu.DisplayGuiPosition.y, requested.y);
                Assert.AreNotEqual(requested, menu.DisplayGuiPosition);
                Assert.AreEqual(RadialMenuSegmentId.TopNoAction, menu.ResolveSegment(menu.DisplayGuiPosition + new Vector2(0f, -menu.OuterSegmentRadius)));

                Assert.IsTrue(menu.TryGetSegmentScreenPosition(RadialMenuSegmentId.TopNoAction, out var topPos));
                Assert.Less(topPos.y, menu.DisplayGuiPosition.y, "Top segment must render above center in IMGUI coordinates.");
            }
            finally
            {
                Object.DestroyImmediate(canvas.gameObject);
            }
        }

        [Test]
        public void HoverAndRelease_ReturnSelectedSegmentWithoutSideEffects()
        {
            var canvas = CreateCanvas("RadialMenuCanvas_C");
            try
            {
                var menu = CreateMenu(canvas);
                menu.Open(new Vector2(200f, 200f), "Smart");

                Assert.IsTrue(menu.TryGetSegmentScreenPosition(RadialMenuSegmentId.Digit1, out var digit1Pos));

                menu.UpdatePointer(digit1Pos);
                Assert.AreEqual(RadialMenuSegmentId.Digit1, menu.HoveredSegmentId);

                var selection = menu.ReleasePointer(digit1Pos);
                Assert.IsNotNull(selection);
                Assert.AreEqual(RadialMenuSegmentId.Digit1, selection.SegmentId);
                Assert.AreEqual(1, selection.Digit);
                Assert.AreEqual("1", selection.Label);
                Assert.IsFalse(menu.IsOpen);
                Assert.AreEqual(RadialMenuSegmentId.None, menu.HoveredSegmentId);
            }
            finally
            {
                Object.DestroyImmediate(canvas.gameObject);
            }
        }

        [Test]
        public void CurrentValueDigit_ChangesLabelAndActionToClear()
        {
            var canvas = CreateCanvas("RadialMenuCanvas_H");
            try
            {
                var menu = CreateMenu(canvas);
                menu.Open(new Vector2(220f, 220f), "Smart");
                menu.SetCellContext(5, new[] { 1, 2, 3, 4, 5 });

                Assert.AreEqual("Clear", menu.GetLabel(RadialMenuSegmentId.Digit5));
                Assert.IsTrue(menu.TryGetSegmentScreenPosition(RadialMenuSegmentId.Digit5, out var digit5Pos));

                var selection = menu.ReleasePointer(digit5Pos);
                Assert.AreEqual(RadialMenuSegmentId.Digit5, selection.SegmentId);
                Assert.AreEqual(RadialDigitActionType.ClearValue, selection.DigitActionType);
                Assert.AreEqual("Clear", selection.Label);
            }
            finally
            {
                Object.DestroyImmediate(canvas.gameObject);
            }
        }

        [Test]
        public void ColourSubActions_AreAvailableForValueCells_AndClearRemovesColours()
        {
            var canvas = CreateCanvas("RadialMenuCanvas_I");
            try
            {
                var menu = CreateMenu(canvas);
                menu.Open(new Vector2(240f, 240f), "Smart");
                menu.SetCellContext(5, new[] { 1, 2, 3, 4, 5 });

                Assert.IsTrue(menu.TryGetDigitActionButtonPosition(RadialMenuSegmentId.Digit5, RadialDigitActionType.ColourGreen, out var greenPos));
                Assert.IsTrue(menu.TryGetDigitActionButtonPosition(RadialMenuSegmentId.Digit5, RadialDigitActionType.ColourClearAll, out var clearPos));

                var greenSelection = menu.ReleasePointer(greenPos);
                Assert.AreEqual(RadialMenuSegmentId.Digit5, greenSelection.SegmentId);
                Assert.AreEqual(RadialDigitActionType.ColourGreen, greenSelection.DigitActionType);
                Assert.AreEqual(5, greenSelection.Digit);
                Assert.AreEqual(HighlightColor.Green, greenSelection.SelectedColour);

                menu.Open(new Vector2(240f, 240f), "Smart");
                menu.SetCellContext(5, new[] { 1, 2, 3, 4, 5 });
                var clearSelection = menu.ReleasePointer(clearPos);
                Assert.AreEqual(RadialMenuSegmentId.Digit5, clearSelection.SegmentId);
                Assert.AreEqual(RadialDigitActionType.ColourClearAll, clearSelection.DigitActionType);
                Assert.IsTrue(clearSelection.ClearAllColours);
            }
            finally
            {
                Object.DestroyImmediate(canvas.gameObject);
            }
        }

        [Test]
        public void DisabledColourButtons_AreNotExposed()
        {
            bool previousGreen = ColourSettings.GreenEnabled;
            bool previousAmber = ColourSettings.AmberEnabled;
            bool previousRed = ColourSettings.RedEnabled;
            bool previousBlue = ColourSettings.BlueEnabled;

            var canvas = CreateCanvas("RadialMenuCanvas_J");
            try
            {
                ColourSettings.GreenEnabled = true;
                ColourSettings.AmberEnabled = true;
                ColourSettings.RedEnabled = true;
                ColourSettings.BlueEnabled = false;

                var menu = CreateMenu(canvas);
                menu.Open(new Vector2(240f, 240f), "Smart");
                menu.SetCellContext(5, new[] { 1, 2, 3, 4, 5 });

                Assert.IsTrue(menu.TryGetDigitActionButtonPosition(RadialMenuSegmentId.Digit5, RadialDigitActionType.ColourGreen, out _));
                Assert.IsFalse(menu.TryGetDigitActionButtonPosition(RadialMenuSegmentId.Digit5, RadialDigitActionType.ColourBlue, out _));
            }
            finally
            {
                ColourSettings.GreenEnabled = previousGreen;
                ColourSettings.AmberEnabled = previousAmber;
                ColourSettings.RedEnabled = previousRed;
                ColourSettings.BlueEnabled = previousBlue;
                Object.DestroyImmediate(canvas.gameObject);
            }
        }

        private static Canvas CreateCanvas(string name)
        {
            var canvasGO = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            return canvas;
        }

        private static RadialMenuRuntime CreateMenu(Canvas canvas)
        {
            var host = new GameObject("RadialMenuHost", typeof(RectTransform));
            host.transform.SetParent(canvas.transform, false);
            return host.AddComponent<RadialMenuRuntime>();
        }
    }
}
