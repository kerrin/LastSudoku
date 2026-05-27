using UnityEditor;
using UnityEngine;

public static class RuleTogglePanelEditor
{
    [MenuItem("Tools/Rule Toggle Panel/Apply Scene Update to All Panels")]
    public static void ApplySceneUpdateToAll()
    {
        var panels = Object.FindObjectsByType<RuleTogglePanel>();
        if (panels == null || panels.Length == 0)
        {
            return;
        }

        int updated = 0;
        foreach (var p in panels)
        {
            if (ApplyToPanel(p)) updated++;
        }
    }

    [MenuItem("CONTEXT/RuleTogglePanel/Apply Scene Update")]
    public static void ApplySceneUpdateToContext(MenuCommand command)
    {
        var panel = command.context as RuleTogglePanel;
        if (panel == null) return;
    }

    private static bool ApplyToPanel(RuleTogglePanel panel)
    {
        return false;
    }
}
