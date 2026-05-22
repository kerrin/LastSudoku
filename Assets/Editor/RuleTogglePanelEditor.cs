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
            Debug.Log("RuleTogglePanelEditor: No RuleTogglePanel instances found in open scenes.");
            return;
        }

        int updated = 0;
        foreach (var p in panels)
        {
            if (ApplyToPanel(p)) updated++;
        }

        Debug.Log($"RuleTogglePanelEditor: Updated {updated} panel(s).");
    }

    [MenuItem("CONTEXT/RuleTogglePanel/Apply Scene Update")]
    public static void ApplySceneUpdateToContext(MenuCommand command)
    {
        var panel = command.context as RuleTogglePanel;
        if (panel == null) return;
        if (ApplyToPanel(panel)) Debug.Log("RuleTogglePanelEditor: Panel updated.");
    }

    private static bool ApplyToPanel(RuleTogglePanel panel)
    {
        return false;
    }
}
