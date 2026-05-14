using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Sudoku.Solver;

/// <summary>
/// Editor helper to create and wire a Solver GameObject with `SolverRunner` and `BoardVisualizer`.
/// </summary>
public static class CreateSolverGameObject
{
    [MenuItem("Tools/Sudoku/Create Solver GameObject")]
    public static void Create()
    {
        var go = new GameObject("Solver");
        Undo.RegisterCreatedObjectUndo(go, "Create Solver GameObject");

        var runner = Undo.AddComponent<SolverRunner>(go);
        var visual = Undo.AddComponent<BoardVisualizer>(go);
        visual.Runner = runner;

        // Provide a common sample puzzle (use '.' for empty cells)
        runner.PuzzleRows = new string[]
        {
            "53..7....",
            "6..195...",
            ".98....6.",
            "8...6...3",
            "4..8.3..1",
            "7...2...6",
            ".6....28.",
            "...419..5",
            "....8..79"
        };

        // Ensure scene is marked dirty so the user can save
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Selection.activeGameObject = go;
    }
}
