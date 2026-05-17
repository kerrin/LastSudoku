using UnityEngine;
using Sudoku.Solver;

/** 
 * Small helper that wires scene components at runtime after scripts compile.
 * It assigns the first found `SolverRunner` to any `BoardVisualizer` and
 * `RuleTogglePanel` instances that don't already have a runner set.
 */
public class SceneWiring : MonoBehaviour
{
    void Awake()
    {
        var runner = FindAnyObjectByType<SolverRunner>();
        if (runner == null) return;

        var visu = FindAnyObjectByType<BoardVisualizer>();
        if (visu != null && visu.Runner == null) visu.Runner = runner;

        var panel = FindAnyObjectByType<RuleTogglePanel>();
        if (panel != null && panel.Runner == null) panel.Runner = runner;
    }
}
