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
            Selection.activeGameObject = existing;
            return;
        }

        var go = new GameObject("ChangeLogRuntimeControls");
        go.AddComponent<Sudoku.Scripts.UI.ChangeLogRuntimeControls>();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }
}
#endif
