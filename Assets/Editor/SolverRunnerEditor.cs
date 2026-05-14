using UnityEditor;
using UnityEngine;
using Sudoku.Solver;

/// <summary>
/// Inspector UI for SolverRunner providing editor buttons to trigger solver actions.
/// </summary>
[CustomEditor(typeof(SolverRunner))]
public class SolverRunnerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var runner = (SolverRunner)target;
        EditorGUILayout.Space();

        if (GUILayout.Button("Load Board From Rows"))
        {
            runner.LoadBoardFromRows();
            EditorUtility.SetDirty(runner);
        }

        if (GUILayout.Button("Initialise Candidates"))
        {
            runner.InitialiseCandidates();
            EditorUtility.SetDirty(runner);
        }

        if (GUILayout.Button("Run Next Rule Step"))
        {
            runner.RunNextStep();
            EditorUtility.SetDirty(runner);
        }

        if (GUILayout.Button("Run Solve"))
        {
            runner.RunSolve();
            EditorUtility.SetDirty(runner);
        }

        if (GUILayout.Button("Reset Candidates"))
        {
            runner.ResetCandidates();
            EditorUtility.SetDirty(runner);
        }

        if (GUILayout.Button("Select Runner GameObject"))
        {
            Selection.activeGameObject = runner.gameObject;
        }
    }
}
