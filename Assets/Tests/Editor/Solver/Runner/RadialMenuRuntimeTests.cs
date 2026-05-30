using NUnit.Framework;
using Sudoku.Scripts.UI;
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
                Assert.AreEqual("Dynamic Smart", menu.CenterLabel);
                Assert.AreEqual("Dynamic Smart", menu.GetLabel(RadialMenuSegmentId.SmartCenter));
                Assert.AreEqual("No Action", menu.GetLabel(RadialMenuSegmentId.TopNoAction));
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

                Assert.AreEqual(RadialMenuSegmentId.SmartCenter, menu.ResolveSegment(new Vector2(200f, 200f)));
                Assert.AreEqual(RadialMenuSegmentId.TopNoAction, menu.ResolveSegment(new Vector2(200f, 120f)));
                Assert.AreEqual(RadialMenuSegmentId.Digit1, menu.ResolveSegment(new Vector2(255f, 145f)));
                Assert.AreEqual(RadialMenuSegmentId.Digit3, menu.ResolveSegment(new Vector2(290f, 200f)));
                Assert.AreEqual(RadialMenuSegmentId.None, menu.ResolveSegment(new Vector2(200f, 20f)));
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

                menu.UpdatePointer(new Vector2(255f, 145f));
                Assert.AreEqual(RadialMenuSegmentId.Digit1, menu.HoveredSegmentId);

                var selection = menu.ReleasePointer(new Vector2(255f, 145f));
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
