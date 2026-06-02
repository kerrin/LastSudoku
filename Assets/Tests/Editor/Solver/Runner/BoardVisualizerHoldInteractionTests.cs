using NUnit.Framework;
using Sudoku.Models;
using Sudoku.Solver;
using Sudoku.Solver.Rules;
using Sudoku.Scripts.UI;
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
        public void HoldLifecycle_ArmsAndOpensRadialMenu_WithDynamicCenterLabel()
        {
            var visualizer = CreateVisualizer(out var runner, out var root);
            try
            {
                visualizer.HoldOpenThresholdSeconds = 0f;

                var boardCenter = new Vector2(60f, 60f);
                visualizer.BeginCellHold(1, 1, boardCenter);
                visualizer.UpdateCellHold(boardCenter);

                Assert.AreEqual(BoardVisualizer.HoldPhase.Armed, visualizer.CurrentHoldPhase);
                Assert.IsNotNull(visualizer.RadialMenu);
                Assert.IsTrue(visualizer.RadialMenu.IsOpen);
                Assert.IsNotEmpty(visualizer.RadialMenu.CenterLabel);
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

        [Test]
        public void ApplyRadialSelection_Digit_ExecutesSetRemoveAndAddOperations()
        {
            var visualizer = CreateVisualizer(out var runner, out var root);
            try
            {
                visualizer.BeginCellHold(0, 0, Vector2.zero);

                visualizer.ApplyRadialSelection(new RadialMenuSelection
                {
                    SegmentId = RadialMenuSegmentId.Digit5,
                    Digit = 5,
                    Label = "5"
                }, EventModifiers.None);

                Assert.AreEqual(5, runner.CurrentBoard.Cells[0, 0].Value);
                Assert.IsTrue(runner.LastRuleResult != null && runner.LastRuleResult.Apply);

                visualizer.ReleaseCellHold();
                runner.CurrentBoard.UndoLast();

                visualizer.BeginCellHold(0, 1, Vector2.zero);
                Assert.IsTrue(runner.CurrentBoard.Cells[0, 1].Candidates.Contains(5));

                visualizer.ApplyRadialSelection(new RadialMenuSelection
                {
                    SegmentId = RadialMenuSegmentId.Digit5,
                    Digit = 5,
                    Label = "5"
                }, EventModifiers.Shift);

                Assert.IsFalse(runner.CurrentBoard.Cells[0, 1].Candidates.Contains(5));
                Assert.IsTrue(runner.LastRuleResult != null && runner.LastRuleResult.Apply);

                visualizer.ApplyRadialSelection(new RadialMenuSelection
                {
                    SegmentId = RadialMenuSegmentId.Digit5,
                    Digit = 5,
                    Label = "5"
                }, EventModifiers.Control);

                Assert.IsTrue(runner.CurrentBoard.Cells[0, 1].Candidates.Contains(5));
                Assert.IsTrue(runner.LastRuleResult != null && runner.LastRuleResult.Apply);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ApplyRadialSelection_GivenCell_ProducesClearNoOp()
        {
            var root = new GameObject("VisualizerRoot_Given");
            var runner = root.AddComponent<SolverRunner>();
            runner.PuzzleRows = new[]
            {
                "1........",
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

            try
            {
                visualizer.BeginCellHold(0, 0, Vector2.zero);
                visualizer.ApplyRadialSelection(new RadialMenuSelection
                {
                    SegmentId = RadialMenuSegmentId.Digit1,
                    Digit = 1,
                    Label = "1"
                }, EventModifiers.None);

                Assert.IsNotNull(runner.LastRuleResult);
                Assert.IsFalse(runner.LastRuleResult.Apply);
                StringAssert.Contains("Given cells only allow removing seen-cell candidates", runner.LastRuleResult.Description);
                StringAssert.Contains("Given cells only allow removing seen-cell candidates", visualizer.LastRadialOutcomeMessage);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ApplyRadialSelection_SmartCenter_KeepsPlaceholderNoOp()
        {
            var visualizer = CreateVisualizer(out var runner, out var root);
            try
            {
                visualizer.BeginCellHold(0, 0, Vector2.zero);
                visualizer.ApplyRadialSelection(new RadialMenuSelection
                {
                    SegmentId = RadialMenuSegmentId.SmartCenter,
                    Label = "No Action"
                }, EventModifiers.None);

                Assert.IsNotNull(runner.LastRuleResult);
                Assert.IsFalse(runner.LastRuleResult.Apply);
                StringAssert.Contains("No action selected.", runner.LastRuleResult.Description);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ResolveDigitOperation_ModifierDriven_UsesExpectedMappings()
        {
            var visualizer = CreateVisualizer(out _, out var root);
            try
            {
                visualizer.DigitActionMode = BoardVisualizer.NumericRadialActionMode.ModifierDriven;

                Assert.AreEqual(ManualCellEditOperation.SetValue, visualizer.ResolveDigitOperation(EventModifiers.None));
                Assert.AreEqual(ManualCellEditOperation.RemoveCandidate, visualizer.ResolveDigitOperation(EventModifiers.Shift));
                Assert.AreEqual(ManualCellEditOperation.AddCandidate, visualizer.ResolveDigitOperation(EventModifiers.Control));
                Assert.AreEqual(ManualCellEditOperation.AddCandidate, visualizer.ResolveDigitOperation(EventModifiers.Command));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ApplyRadialSelection_UnitCandidateAction_RespectsShiftForAddElseRemove()
        {
            var visualizer = CreateVisualizer(out var runner, out var root);
            try
            {
                visualizer.BeginCellHold(4, 4, Vector2.zero);
                var selection = new RadialMenuSelection
                {
                    SegmentId = RadialMenuSegmentId.Digit3,
                    Digit = 3,
                    DigitActionType = RadialDigitActionType.UnitCandidateAction,
                    Label = "3"
                };

                visualizer.ApplyRadialSelection(selection, EventModifiers.None);
                Assert.IsFalse(runner.CurrentBoard.Cells[4, 0].Candidates.Contains(3));

                visualizer.ApplyRadialSelection(selection, EventModifiers.Shift);
                Assert.IsTrue(runner.CurrentBoard.Cells[4, 0].Candidates.Contains(3));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ApplyRadialSelection_AddCandidateSubAction_ClearsExistingValue()
        {
            var visualizer = CreateVisualizer(out var runner, out var root);
            try
            {
                var cell = runner.CurrentBoard.Cells[0, 0];
                runner.CurrentBoard.SetValue(cell, 6);

                visualizer.BeginCellHold(0, 0, Vector2.zero);
                visualizer.ApplyRadialSelection(new RadialMenuSelection
                {
                    SegmentId = RadialMenuSegmentId.Digit6,
                    Digit = 6,
                    DigitActionType = RadialDigitActionType.AddCandidate,
                    Label = "6"
                }, EventModifiers.None);

                Assert.IsNull(cell.Value);
                Assert.IsTrue(cell.Candidates.Contains(6));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ApplyRadialSelection_CurrentValueDigit_ClearsValueAndRestoresCandidates()
        {
            var visualizer = CreateVisualizer(out var runner, out var root);
            try
            {
                var cell = runner.CurrentBoard.Cells[0, 0];
                runner.CurrentBoard.SetValue(cell, 6);
                Assert.IsFalse(runner.CurrentBoard.Cells[0, 1].Candidates.Contains(6));

                visualizer.BeginCellHold(0, 0, Vector2.zero);
                visualizer.ApplyRadialSelection(new RadialMenuSelection
                {
                    SegmentId = RadialMenuSegmentId.Digit6,
                    Digit = 6,
                    DigitActionType = RadialDigitActionType.ClearValue,
                    Label = "Clear"
                }, EventModifiers.None);

                Assert.IsNull(cell.Value);
                Assert.IsTrue(cell.Candidates.Contains(6));
                Assert.IsTrue(runner.CurrentBoard.Cells[0, 1].Candidates.Contains(6));
                Assert.IsTrue(runner.LastRuleResult != null && runner.LastRuleResult.Apply);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ManualAction_RefreshesUndoButtonState_AfterFirstEdit()
        {
            var gameRoot = new GameObject("GameRoot");
            var uiCanvasGO = new GameObject("UICanvas", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            var uiCanvas = uiCanvasGO.GetComponent<Canvas>();
            uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var sidePanelGO = new GameObject("SidePanel", typeof(RectTransform));
            sidePanelGO.transform.SetParent(uiCanvasGO.transform, false);

            var runner = gameRoot.AddComponent<SolverRunner>();
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

            var visualizer = gameRoot.AddComponent<BoardVisualizer>();
            visualizer.Runner = runner;
            visualizer.CellSize = 40;
            visualizer.FitToScreenHeight = false;
            visualizer.Offset = Vector2.zero;

            var controls = sidePanelGO.AddComponent<ChangeLogRuntimeControls>();
            controls.ParentPanel = sidePanelGO.GetComponent<RectTransform>();

            try
            {
                var undoButton = sidePanelGO.transform.Find("ChangeLogControls/UndoButton")?.GetComponent<UnityEngine.UI.Button>();
                Assert.IsNotNull(undoButton);
                Assert.IsFalse(undoButton.interactable, "Undo should start disabled before any manual edit.");

                visualizer.BeginCellHold(0, 0, Vector2.zero);
                visualizer.ApplyRadialSelection(new RadialMenuSelection
                {
                    SegmentId = RadialMenuSegmentId.Digit5,
                    Digit = 5,
                    Label = "5"
                }, EventModifiers.None);

                Assert.IsTrue(undoButton.interactable, "Undo should enable immediately after the first manual edit.");
            }
            finally
            {
                Object.DestroyImmediate(gameRoot);
                Object.DestroyImmediate(uiCanvasGO);
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
