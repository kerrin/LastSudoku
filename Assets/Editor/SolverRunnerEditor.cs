using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Sudoku.Solver;

/// <summary>
/// Inspector UI for SolverRunner providing editor buttons to trigger solver actions
/// and a small readout of the current board and last-applied rule/result.
/// </summary>
[CustomEditor(typeof(SolverRunner))]
[CanEditMultipleObjects]
public class SolverRunnerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var runner = (SolverRunner)target;
        EditorGUILayout.Space();

        EditorGUILayout.LabelField("SolverRunner Tools", EditorStyles.boldLabel);

        if (GUILayout.Button("Load Board From Rows"))
        {
            Undo.RecordObject(runner, "Load Board From Rows");
            runner.LoadBoardFromRows();
            EditorUtility.SetDirty(runner);
            MarkSceneDirtyIfNeeded(runner);
        }

        if (GUILayout.Button("Initialise Candidates"))
        {
            Undo.RecordObject(runner, "Initialise Candidates");
            runner.InitialiseCandidates();
            EditorUtility.SetDirty(runner);
            MarkSceneDirtyIfNeeded(runner);
        }

        if (GUILayout.Button("Run Next Rule Step"))
        {
            Undo.RecordObject(runner, "Run Next Rule Step");
            runner.RunNextStep();
            EditorUtility.SetDirty(runner);
            MarkSceneDirtyIfNeeded(runner);
        }

        if (GUILayout.Button("Run Solve"))
        {
            Undo.RecordObject(runner, "Run Solve");
            runner.RunSolve();
            EditorUtility.SetDirty(runner);
            MarkSceneDirtyIfNeeded(runner);
        }

        if (GUILayout.Button("Reset Candidates"))
        {
            Undo.RecordObject(runner, "Reset Candidates");
            runner.ResetCandidates();
            EditorUtility.SetDirty(runner);
            MarkSceneDirtyIfNeeded(runner);
        }

        if (GUILayout.Button("Select Runner GameObject"))
        {
            Selection.activeGameObject = runner.gameObject;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Current Board", EditorStyles.boldLabel);
        try
        {
            var board = runner.CurrentBoard;
            if (board == null)
            {
                EditorGUILayout.LabelField("(no board loaded)");
            }
            else
            {
                var sb = new System.Text.StringBuilder();
                for (int r = 0; r < board.Size; r++)
                {
                    for (int c = 0; c < board.Size; c++)
                    {
                        var v = board.Cells[r, c].Value;
                        sb.Append(v.HasValue ? (char)('0' + v.Value) : '.');
                    }
                    if (r < board.Size - 1) sb.AppendLine();
                }
                EditorGUILayout.TextArea(sb.ToString());
            }
        }
        catch (System.Exception ex)
        {
            EditorGUILayout.LabelField("(error rendering board) " + ex.Message);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Last Rule", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Rule:", runner.LastAppliedRule != null ? runner.LastAppliedRule.Name : "(none)");
        EditorGUILayout.LabelField("Result:", runner.LastRuleResult != null ? runner.LastRuleResult.Description : "(none)");
    }

    private void MarkSceneDirtyIfNeeded(SolverRunner runner)
    {
        // Do not mark the scene dirty during play mode — this is an Editor-only action
        // and is not safe or necessary while the Editor is playing.
        if (runner == null || runner.gameObject == null) return;
        if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
        EditorSceneManager.MarkSceneDirty(runner.gameObject.scene);
    }
}
