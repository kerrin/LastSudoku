using UnityEngine;
using Sudoku.Solver;
using Sudoku.UI.Panels;

namespace Sudoku.Core
{

/** 
 * Small helper that wires scene components at runtime after scripts compile.
 * It assigns the first found `SolverRunner` to any `BoardVisualizer` and
 * `RuleTogglePanel` instances that don't already have a runner set.
 */
public class SceneWiring : MonoBehaviour
{
    void Awake()
    {
        var runner = Object.FindAnyObjectByType<SolverRunner>();
        if (runner == null) return;

        var visu = Object.FindAnyObjectByType<BoardVisualizer>();
        if (visu != null && visu.Runner == null) visu.Runner = runner;

        var panel = Object.FindAnyObjectByType<RuleTogglePanel>();
        if (panel != null && panel.Runner == null) panel.Runner = runner;
    }
}
}
