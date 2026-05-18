using UnityEngine;
using UnityEditor;

[InitializeOnLoad]
static class RuntimeUiAutoCleanup
{
    static RuntimeUiAutoCleanup()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        // When exiting edit mode (about to enter Play), remove leftover generated UI
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            CleanupGeneratedUiBeforePlay();
        }
    }

    private static void CleanupGeneratedUiBeforePlay()
    {
        var markers = Object.FindObjectsByType<GeneratedRuntimeUI>();
        int removed = 0;
        foreach (var m in markers)
        {
            if (m == null) continue;
            // Destroy immediately in editor
            Object.DestroyImmediate(m.gameObject);
            removed++;
        }

        if (removed > 0)
            Debug.Log($"Runtime UI auto-cleanup removed {removed} generated runtime UI object(s) before entering Play mode.");
    }
}
