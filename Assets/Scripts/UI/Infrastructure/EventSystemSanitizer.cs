using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Sudoku.UI.Infrastructure
{

/// <summary>
/// Ensures EventSystems use the new Input System UI module when available,
/// or disables legacy StandaloneInputModule to avoid UnityEngine.Input exceptions
/// when the project is configured to use the Input System package.
/// Runs before scenes load so modules won't poll legacy Input at startup.
/// </summary>
public static class EventSystemSanitizer
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureCorrectInputModules()
    {
        Type inputModuleType = null;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            inputModuleType = asm.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule");
            if (inputModuleType != null) break;
        }

        // Find all StandaloneInputModule instances across loaded objects (scenes, prefabs, editor UI)
        var standalones = Resources.FindObjectsOfTypeAll<StandaloneInputModule>();
        foreach (var standalone in standalones)
        {
            if (standalone == null) continue;
            var go = standalone.gameObject;

            if (inputModuleType != null)
            {
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(standalone);
                    go.AddComponent(inputModuleType);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(standalone);
                    go.AddComponent(inputModuleType);
                }
            }
            else
            {
                standalone.enabled = false;
                Debug.LogWarning($"EventSystemSanitizer: Disabled StandaloneInputModule on '{go.name}' to avoid legacy Input calls.");
            }
        }
    }
#if UNITY_EDITOR
    [InitializeOnLoadMethod]
    private static void EnsureEditorSanitizer()
    {
        EnsureCorrectInputModules();
    }
#endif
}

}
