using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Sudoku.Solver;

public static class PopulatePuzzleRows
{
    private static void SetRows(SolverRunner runner, string[] rows)
    {
        if (runner == null)
        {
            Debug.LogWarning("No SolverRunner found to populate.");
            return;
        }
        Undo.RecordObject(runner, "Populate PuzzleRows");
        runner.PuzzleRows = rows;
        EditorUtility.SetDirty(runner);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    private static SolverRunner GetTargetRunner()
    {
        // Prefer selected GameObject's runner
        if (Selection.activeGameObject != null)
        {
            var r = Selection.activeGameObject.GetComponent<SolverRunner>();
            if (r != null) return r;
        }
        // Fallback: first runner in the scene
        return Object.FindAnyObjectByType<SolverRunner>();
    }

    [MenuItem("Tools/Sudoku/Populate PuzzleRows (Easy)")]
    public static void PopulateEasy()
    {
        var rows = new string[]
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
        SetRows(GetTargetRunner(), rows);
    }

    [MenuItem("Tools/Sudoku/Populate PuzzleRows (Hard)")]
    public static void PopulateHard()
    {
        var rows = new string[]
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
        SetRows(GetTargetRunner(), rows);
    }

    [MenuItem("Tools/Sudoku/Populate PuzzleRows (Right Angle)")]
    public static void PopulateRightAngle()
    {
        var rows = new string[]
        {
            "8......56",
            "2....6...",
            "....23...",
            ".31......",
            "..7.4.3..",
            "......72.",
            "...83....",
            "...9....1",
            "46......5"
        };
        SetRows(GetTargetRunner(), rows);
    }

    [MenuItem("Tools/Sudoku/Populate PuzzleRows/Populate Selected Runner", priority = 51)]
    public static void PopulateSelectedRunner()
    {
        var runner = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<SolverRunner>() : null;
        if (runner == null)
        {
            EditorUtility.DisplayDialog("Populate PuzzleRows", "Select a GameObject that has a SolverRunner component.", "OK");
            return;
        }
        // Example quick fill: empty grid
        var rows = new string[9]
        {
            ".........",
            ".........",
            ".........",
            ".........",
            ".........",
            ".........",
            ".........",
            ".........",
            "........."
        };
        SetRows(runner, rows);
    }
}
