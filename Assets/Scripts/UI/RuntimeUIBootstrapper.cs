using UnityEngine;

namespace Sudoku.Scripts.UI
{
    public static class RuntimeUIBootstrapper
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimeUiControllers()
        {
            // Don't run in editor-only contexts where play mode isn't active
            if (!Application.isPlaying) return;

            // Ensure ChangeLog controls host exists.
            var changeLogExisting = Object.FindAnyObjectByType<ChangeLogRuntimeControls>();
            if (changeLogExisting == null)
            {
                var changeLogGo = new GameObject("ChangeLogRuntimeControls");
                Object.DontDestroyOnLoad(changeLogGo);
                changeLogGo.AddComponent<ChangeLogRuntimeControls>();
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
