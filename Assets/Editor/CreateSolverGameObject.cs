using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Sudoku.Solver;

/// <summary>
/// Editor helper to create and wire a Solver GameObject with `SolverRunner` and `BoardVisualizer`.
/// </summary>
public static class CreateSolverGameObject
{
    [MenuItem("Tools/Sudoku/Create Solver GameObject (Easy)")]
    public static void CreateEasy()
    {
        var go = new GameObject("Solver (Easy)");
        Undo.RegisterCreatedObjectUndo(go, "Create Solver GameObject (Easy)");

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

        [MenuItem("Tools/Sudoku/Create Solver GameObject (Hard)")]
    public static void CreateHard()
    {
        var go = new GameObject("Solver (Hard)");
        Undo.RegisterCreatedObjectUndo(go, "Create Solver GameObject (Hard)");

        var runner = Undo.AddComponent<SolverRunner>(go);
        var visual = Undo.AddComponent<BoardVisualizer>(go);
        visual.Runner = runner;

        // Provide a common sample puzzle (use '.' for empty cells)
        runner.PuzzleRows = new string[]
        {
            "8...7....",
            ".753.8...",
            "6.9.1....",
            ".....14.8",
            ".........",
            "..7...3.2",
            "24....5..",
            "9..4.76..",
            "....36..."
        };

        // Ensure scene is marked dirty so the user can save
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Selection.activeGameObject = go;
    }
}
