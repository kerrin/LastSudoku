#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;

public static class ChangeLogTools
{
    [MenuItem("Tools/Spawn Change Log Controls")]
    public static void SpawnChangeLogControls()
    {
        var existing = GameObject.Find("ChangeLogRuntimeControls");
        if (existing != null)
        {
            Debug.Log("ChangeLogTools: Found existing ChangeLogRuntimeControls in scene. Selecting it.");
            Selection.activeGameObject = existing;
            return;
        }

        var go = new GameObject("ChangeLogRuntimeControls");
        go.AddComponent<Sudoku.Scripts.UI.ChangeLogRuntimeControls>();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("ChangeLogTools: Spawned ChangeLogRuntimeControls. Save the scene to persist.");
    }
}
#endif
