using NUnit.Framework;
using Sudoku.Models;
using Sudoku.Solver;
using UnityEngine;

namespace Sudoku.Tests.Editor
{
    public class BoardVisualizerHoldInteractionTests
    {
        [Test]
        public void TryGetCellFromScreenPosition_MapsPointerToCorrectCell()
        {
            var visualizer = CreateVisualizer(out var runner, out var root);
            try
            {
                Assert.IsTrue(visualizer.TryGetCellFromScreenPosition(new Vector2(60f, 100f), out var row, out var column));
                Assert.AreEqual(2, row);
                Assert.AreEqual(1, column);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void HoldLifecycle_ArmsAfterThreshold_AndCancelsOnLeaveOrEarlyRelease()
        {
            var visualizer = CreateVisualizer(out var runner, out var root);
            try
            {
                visualizer.HoldOpenThresholdSeconds = 0f;

                Assert.IsTrue(visualizer.IsBoardInteractionAvailable());
                visualizer.BeginCellHold(1, 1, new Vector2(50f, 50f));
                Assert.AreEqual(BoardVisualizer.HoldPhase.Holding, visualizer.CurrentHoldPhase);
                Assert.AreEqual(1, visualizer.SelectedHoldRow);
                Assert.AreEqual(1, visualizer.SelectedHoldColumn);

                visualizer.UpdateCellHold(new Vector2(50f, 50f));
                Assert.AreEqual(BoardVisualizer.HoldPhase.Armed, visualizer.CurrentHoldPhase);
                Assert.IsTrue(visualizer.RadialOpenIntentRequested);

                visualizer.ReleaseCellHold();
                Assert.AreEqual(BoardVisualizer.HoldPhase.Idle, visualizer.CurrentHoldPhase);
                Assert.AreEqual(-1, visualizer.SelectedHoldRow);
                Assert.AreEqual(-1, visualizer.SelectedHoldColumn);
                Assert.IsTrue(visualizer.RadialOpenIntentRequested, "Armed intent should remain available until consumed.");

                Assert.IsTrue(visualizer.ConsumeRadialOpenIntent());
                Assert.IsFalse(visualizer.RadialOpenIntentRequested);

                visualizer.BeginCellHold(3, 3, new Vector2(150f, 150f));
                visualizer.HoldOpenThresholdSeconds = 999f;
                visualizer.UpdateCellHold(new Vector2(500f, 500f));
                Assert.AreEqual(BoardVisualizer.HoldPhase.Idle, visualizer.CurrentHoldPhase);
                Assert.IsFalse(visualizer.RadialOpenIntentRequested);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MissingBoard_DisablesInteraction()
        {
            var root = new GameObject("VisualizerRoot_MissingBoard");
            var visualizer = root.AddComponent<BoardVisualizer>();
            try
            {
                Assert.IsFalse(visualizer.IsBoardInteractionAvailable());
                visualizer.BeginCellHold(0, 0, Vector2.zero);
                Assert.AreEqual(BoardVisualizer.HoldPhase.Idle, visualizer.CurrentHoldPhase);
                Assert.IsFalse(visualizer.TryGetCellFromScreenPosition(Vector2.zero, out _, out _));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static BoardVisualizer CreateVisualizer(out SolverRunner runner, out GameObject root)
        {
            root = new GameObject("VisualizerRoot");
            runner = root.AddComponent<SolverRunner>();
            runner.PuzzleRows = new[]
            {
                ".........",
                ".........",
                ".........",
                ".........",
                ".........",
                ".........",
                ".........",
                ".........",
                "........."
            };
            runner.LoadBoardFromRows();

            var visualizer = root.AddComponent<BoardVisualizer>();
            visualizer.Runner = runner;
            visualizer.CellSize = 40;
            visualizer.FitToScreenHeight = false;
            visualizer.Offset = Vector2.zero;
            return visualizer;
        }
    }
}
