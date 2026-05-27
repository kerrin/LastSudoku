using UnityEngine;
using UnityEditor;

public static class RuntimeUiCleanup
{
    [MenuItem("Tools/Runtime UI/Cleanup/Remove SidePanels")] 
    public static void RemoveSidePanels()
    {
        var panels = Object.FindObjectsByType<Transform>();
        int removed = 0;
        foreach (var t in panels)
        {
            if (t.name == "SidePanel")
            {
                Undo.DestroyObjectImmediate(t.gameObject);
                removed++;
            }
        }
    }

    [MenuItem("Tools/Runtime UI/Cleanup/Remove Generated UI")] 
    public static void RemoveGeneratedUI()
    {
        var markers = Object.FindObjectsByType<GeneratedRuntimeUI>();
        int removed = 0;
        foreach (var m in markers)
        {
            if (m != null)
            {
                Undo.DestroyObjectImmediate(m.gameObject);
                removed++;
            }
        }
    }

    [MenuItem("Tools/Runtime UI/Cleanup/Remove Empty Canvases")] 
    public static void RemoveEmptyCanvases()
    {
        var canvases = Object.FindObjectsByType<Canvas>();
        int removed = 0;
        foreach (var c in canvases)
        {
            if (c == null) continue;
            var go = c.gameObject;
            // Only remove default-named canvases that have no children
            if (go.name == "Canvas" && go.transform.childCount == 0)
            {
                Undo.DestroyObjectImmediate(go);
                removed++;
            }
        }
    }
}
