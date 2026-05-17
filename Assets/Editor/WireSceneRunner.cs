using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Sudoku.Solver;

public static class WireSceneRunner
{
    [MenuItem("Tools/Sudoku/Wire Scene Runner")] 
    public static void Wire()
    {
        var runner = Object.FindAnyObjectByType<SolverRunner>();
        if (runner == null)
        {
            Debug.LogWarning("WireSceneRunner: No SolverRunner found in the scene.");
            return;
        }

        var vis = Object.FindAnyObjectByType<BoardVisualizer>();
        if (vis != null)
        {
            if (vis.Runner == null)
            {
                Undo.RecordObject(vis, "Wire BoardVisualizer");
                vis.Runner = runner;
                EditorUtility.SetDirty(vis);
            }
        }

        var panel = Object.FindAnyObjectByType<RuleTogglePanel>();
        if (panel != null)
        {
            if (panel.Runner == null)
            {
                Undo.RecordObject(panel, "Wire RuleTogglePanel");
                panel.Runner = runner;
                EditorUtility.SetDirty(panel);
            }
        }

        // Ensure a GameObject named 'Board' exists and has the components
        var boardGO = GameObject.Find("Board");
        if (boardGO != null)
        {
            if (boardGO.GetComponent<SolverRunner>() == null)
            {
                Undo.AddComponent<SolverRunner>(boardGO);
            }
            if (boardGO.GetComponent<BoardVisualizer>() == null)
            {
                Undo.AddComponent<BoardVisualizer>(boardGO);
            }
            var bv = boardGO.GetComponent<BoardVisualizer>();
            if (bv != null && bv.Runner == null)
            {
                Undo.RecordObject(bv, "Wire BoardVisualizer on Board GO");
                bv.Runner = runner;
                EditorUtility.SetDirty(bv);
            }
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("WireSceneRunner: wired SolverRunner to BoardVisualizer and RuleTogglePanel.");
    }
}
