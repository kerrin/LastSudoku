using UnityEngine;

namespace Sudoku.Scripts.UI
{
    public static class RuntimeUIBootstrapper
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureChangeLogControls()
        {
            // Don't run in editor-only contexts where play mode isn't active
            if (!Application.isPlaying) return;

            // If an instance already exists, do nothing
            var existing = Object.FindObjectOfType<ChangeLogRuntimeControls>();
            if (existing != null) return;

            // Create a new GameObject in the scene to host the controls
            var go = new GameObject("ChangeLogRuntimeControls");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<ChangeLogRuntimeControls>();
        }
    }
}
