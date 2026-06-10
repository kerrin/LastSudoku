using UnityEngine;
using Sudoku.UI.Panels;
using Sudoku.UI.Controllers;
using Sudoku.UI.Config;

namespace Sudoku.UI.Infrastructure
{
    public static class RuntimeUIBootstrapper
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimeUiControllers()
        {
            // Don't run in editor-only contexts where play mode isn't active
            if (!Application.isPlaying) return;

            // Load persisted config first so all runtime systems start from saved values.
            RuntimeConfigService.EnsureLoaded();

            // Ensure ChangeLog controls host exists.
            var changeLogExisting = Object.FindAnyObjectByType<ChangeLogRuntimeControls>();
            if (changeLogExisting == null)
            {
                var changeLogGo = new GameObject("ChangeLogRuntimeControls");
                Object.DontDestroyOnLoad(changeLogGo);
                changeLogGo.AddComponent<ChangeLogRuntimeControls>();
            }

            var generationDebugExisting = Object.FindAnyObjectByType<PuzzleGenerationDebugRuntimePanel>();
            if (generationDebugExisting == null)
            {
                var generationDebugGo = new GameObject("PuzzleGenerationDebugRuntimePanel");
                Object.DontDestroyOnLoad(generationDebugGo);
                generationDebugGo.AddComponent<PuzzleGenerationDebugRuntimePanel>();
            }

            // Ensure main menu flow host exists.
            var flowExisting = Object.FindAnyObjectByType<MainMenuFlowController>();
            if (flowExisting == null)
            {
                var flowGo = new GameObject("MainMenuFlowController");
                Object.DontDestroyOnLoad(flowGo);
                flowGo.AddComponent<MainMenuFlowController>();
            }
        }
    }
}
