using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class RuleTogglePanelEditor
{
    [UnityEditor.InitializeOnLoadMethod]
    private static void AutoApplyOnScriptsReload()
    {
        // Register to run updates only when a scene is explicitly saved in the Editor.
        // This avoids making automatic scene changes during script reloads or play-mode transitions.
        EditorSceneManager.sceneSaved += (scene) =>
        {
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                ApplySceneUpdateToAll();
            }
            catch { }
        };
    }
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
        if (panel == null) return false;
        var root = panel.gameObject;
        Undo.RegisterFullObjectHierarchyUndo(root, "Update RuleTogglePanel");

        var rootLayout = root.GetComponent<VerticalLayoutGroup>();
        if (rootLayout == null) rootLayout = Undo.AddComponent<VerticalLayoutGroup>(root);
        rootLayout.childControlHeight = true;
        rootLayout.childControlWidth = true;
        // Do not force all children to expand vertically; header should keep its preferred height
        rootLayout.childForceExpandHeight = false;
        rootLayout.childForceExpandWidth = false;
        // Ensure children are aligned to the top so content doesn't get vertically centered
        rootLayout.childAlignment = TextAnchor.UpperLeft;
        rootLayout.spacing = 4;
        rootLayout.padding = new RectOffset(4,4,4,4);

        // Ensure the root has a LayoutElement so parent layout respects the panel size in the Editor
        var rootLE = root.GetComponent<LayoutElement>();
        if (rootLE == null) rootLE = Undo.AddComponent<LayoutElement>(root);
        // Use panel's configured MaxHeight/MaxWidth as editor preferred sizes so layout is readable in Editor
        var rtp = panel.GetComponent<RuleTogglePanel>();
        if (rtp != null)
        {
            rootLE.preferredHeight = rtp.MaxHeight;
            rootLE.preferredWidth = rtp.MaxWidth;
            rootLE.flexibleWidth = 0f;
        }

        // Header
        Transform headerT = root.transform.Find("Header");
        GameObject headerGO;
        TextMeshProUGUI headerTmp = null;
        Text headerText = null;
        if (headerT != null)
        {
            headerGO = headerT.gameObject;
            headerText = headerGO.GetComponent<Text>();
            headerTmp = headerGO.GetComponent<TextMeshProUGUI>();
            if (headerText == null && headerTmp == null) headerText = Undo.AddComponent<Text>(headerGO);
        }
        else
        {
            headerGO = new GameObject("Header", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(headerGO, "Create Header");
            headerGO.transform.SetParent(root.transform, false);
            headerText = Undo.AddComponent<Text>(headerGO);
        }

        if (headerText != null)
        {
            headerText.text = "Rules";
            headerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            headerText.fontSize = 16;
            headerText.color = Color.white;
            headerText.alignment = TextAnchor.MiddleCenter;
            headerText.horizontalOverflow = HorizontalWrapMode.Overflow;
            headerText.verticalOverflow = VerticalWrapMode.Truncate;
        }
        else if (headerTmp != null)
        {
            headerTmp.text = "Rules";
            headerTmp.alignment = TextAlignmentOptions.Center;
            headerTmp.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            headerTmp.enableAutoSizing = false;
            headerTmp.fontSize = 16;
            headerTmp.color = Color.white;
        }

        var headerLE = headerGO.GetComponent<LayoutElement>();
        if (headerLE == null) headerLE = Undo.AddComponent<LayoutElement>(headerGO);
        headerLE.preferredHeight = 26f;
        headerLE.flexibleWidth = 1f;

        // RuleToggles container
        Transform togglesT = root.transform.Find("RuleToggles");
        GameObject togglesGO;
        if (togglesT != null)
        {
            togglesGO = togglesT.gameObject;
        }
        else
        {
            togglesGO = new GameObject("RuleToggles", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(togglesGO, "Create RuleToggles");
            togglesGO.transform.SetParent(root.transform, false);
        }

        // If a designer accidentally added a ContentSizeFitter to the wrapper,
        // remove it in the Editor so the parent layout can control sizing.
        var wrapperCsf = togglesGO.GetComponent<ContentSizeFitter>();
        if (wrapperCsf != null)
        {
            Undo.RecordObject(togglesGO, "Remove ContentSizeFitter from RuleToggles");
            Undo.DestroyObjectImmediate(wrapperCsf);
            Debug.Log("RuleTogglePanelEditor: Removed ContentSizeFitter from RuleToggles wrapper.");
        }

        var togglesLE = togglesGO.GetComponent<LayoutElement>();
        if (togglesLE == null) togglesLE = Undo.AddComponent<LayoutElement>(togglesGO);
        togglesLE.flexibleHeight = 1f;

        // Create or reuse ScrollArea so toggles become scrollable in the Editor
        Transform scrollTrans = togglesGO.transform.Find("ScrollArea");
        GameObject scrollGO;
        Transform contentParent = null;
        if (scrollTrans != null)
        {
            scrollGO = scrollTrans.gameObject;
            contentParent = scrollGO.transform.Find("Viewport/Content");
            // Ensure ScrollArea has a LayoutElement so it can expand in the parent layout
            var existingScrollLE = scrollGO.GetComponent<LayoutElement>();
            if (existingScrollLE == null) existingScrollLE = Undo.AddComponent<LayoutElement>(scrollGO);
            Undo.RecordObject(existingScrollLE, "Configure ScrollArea LayoutElement");
            existingScrollLE.flexibleHeight = 1f;
            existingScrollLE.preferredHeight = 0f;
            existingScrollLE.minHeight = 0f;

            // Ensure the Viewport exists and has Image+Mask, and Content has layout+ContentSizeFitter
            var vp = scrollGO.transform.Find("Viewport");
            if (vp == null)
            {
                var viewportGO = new GameObject("Viewport", typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(viewportGO, "Create Viewport");
                viewportGO.transform.SetParent(scrollGO.transform, false);
                vp = viewportGO.transform;
            }
            var vpGOObj = vp.gameObject;
            var img = vpGOObj.GetComponent<Image>();
            if (img == null) img = Undo.AddComponent<Image>(vpGOObj);
            // Show a faint background in the Editor so the Viewport is visible while
            // debugging layout issues. Keep fully transparent at runtime/play.
            if (!Application.isPlaying)
            {
                img.color = new Color(0f, 0f, 0f, 0.12f);
            }
            else
            {
                img.color = new Color(0f, 0f, 0f, 0f);
            }
            img.raycastTarget = false;
            var mask = vpGOObj.GetComponent<Mask>();
            if (mask == null) mask = Undo.AddComponent<Mask>(vpGOObj);
            mask.showMaskGraphic = false;

            if (contentParent == null)
            {
                var contentGO = vp.Find("Content");
                if (contentGO == null)
                {
                    var createdContent = new GameObject("Content", typeof(RectTransform));
                    Undo.RegisterCreatedObjectUndo(createdContent, "Create Content");
                    createdContent.transform.SetParent(vp, false);
                    contentParent = createdContent.transform;
                }
                else contentParent = contentGO;
            }

            if (contentParent != null)
            {
                var contentGOObj = contentParent.gameObject;
                var vlg = contentGOObj.GetComponent<VerticalLayoutGroup>();
                if (vlg == null) vlg = Undo.AddComponent<VerticalLayoutGroup>(contentGOObj);
                vlg.childForceExpandHeight = false;
                vlg.childControlHeight = true;
                vlg.childControlWidth = true;
                vlg.childAlignment = TextAnchor.UpperLeft;
                vlg.spacing = 2f;
                vlg.padding = new RectOffset(0,0,0,0);

                var csf = contentGOObj.GetComponent<ContentSizeFitter>();
                if (csf == null) csf = Undo.AddComponent<ContentSizeFitter>(contentGOObj);
                csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }

            // Ensure ScrollRect references are correct
            var sr = scrollGO.GetComponent<ScrollRect>();
            if (sr == null) sr = Undo.AddComponent<ScrollRect>(scrollGO);
            sr.horizontal = false;
            if (sr.viewport == null && vp != null) sr.viewport = vp.GetComponent<RectTransform>();
            if (sr.content == null && contentParent != null) sr.content = contentParent.GetComponent<RectTransform>();
        }
        else
        {
            scrollGO = new GameObject("ScrollArea", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(scrollGO, "Create ScrollArea");
            scrollGO.transform.SetParent(togglesGO.transform, false);
            var scrollRect = Undo.AddComponent<ScrollRect>(scrollGO);
            scrollRect.horizontal = false;

            var viewportGO = new GameObject("Viewport", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(viewportGO, "Create Viewport");
            viewportGO.transform.SetParent(scrollGO.transform, false);
            var viewportImg = Undo.AddComponent<Image>(viewportGO);
            viewportImg.color = new Color(0f, 0f, 0f, 0f);
            Undo.AddComponent<Mask>(viewportGO).showMaskGraphic = false;

            var contentGO = new GameObject("Content", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(contentGO, "Create Content");
            contentGO.transform.SetParent(viewportGO.transform, false);

            scrollRect.viewport = viewportGO.GetComponent<RectTransform>();
            scrollRect.content = contentGO.GetComponent<RectTransform>();

            var contentVlg = Undo.AddComponent<VerticalLayoutGroup>(contentGO);
            contentVlg.childForceExpandHeight = false;
            contentVlg.childControlHeight = true;
            contentVlg.childControlWidth = true;
            contentVlg.childAlignment = TextAnchor.UpperLeft;
            contentVlg.spacing = 2f;
            contentVlg.padding = new RectOffset(0,0,0,0);

            var contentCsf = Undo.AddComponent<ContentSizeFitter>(contentGO);
            contentCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            contentParent = contentGO.transform;
            // Ensure the newly created ScrollArea has a LayoutElement so it expands
            var createdScrollLE = Undo.AddComponent<LayoutElement>(scrollGO);
            createdScrollLE.flexibleHeight = 1f;
            createdScrollLE.preferredHeight = 0f;
            createdScrollLE.minHeight = 0f;
        }

        // Ensure RectTransforms for scroll structure are stretched correctly in the Editor
        if (scrollGO != null)
        {
            var scrollRT = scrollGO.GetComponent<RectTransform>();
            if (scrollRT != null)
            {
                scrollRT.anchorMin = new Vector2(0f, 0f);
                scrollRT.anchorMax = new Vector2(1f, 1f);
                scrollRT.pivot = new Vector2(0.5f, 0.5f);
                scrollRT.anchoredPosition = Vector2.zero;
                scrollRT.sizeDelta = Vector2.zero;
            }

            var vp = scrollGO.transform.Find("Viewport");
            if (vp != null)
            {
                var vpRT = vp.GetComponent<RectTransform>();
                vpRT.anchorMin = new Vector2(0f, 0f);
                vpRT.anchorMax = new Vector2(1f, 1f);
                vpRT.pivot = new Vector2(0.5f, 0.5f);
                vpRT.anchoredPosition = Vector2.zero;
                vpRT.sizeDelta = Vector2.zero;
            }

            if (contentParent != null)
            {
                var contentRT = contentParent.GetComponent<RectTransform>();
                contentRT.anchorMin = new Vector2(0f, 1f);
                contentRT.anchorMax = new Vector2(1f, 1f);
                contentRT.pivot = new Vector2(0.5f, 1f);
                contentRT.anchoredPosition = Vector2.zero;
                contentRT.sizeDelta = Vector2.zero;
            }
        }

        // Move any legacy children (designer-placed toggles) into the content parent so they are visible in the viewport
        if (contentParent != null)
        {
            var legacyChildren = new System.Collections.Generic.List<Transform>();
            for (int i = togglesGO.transform.childCount - 1; i >= 0; --i)
            {
                var c = togglesGO.transform.GetChild(i);
                // Skip the ScrollArea itself
                if (c == scrollGO?.transform) continue;
                legacyChildren.Add(c);
            }
            for (int i = legacyChildren.Count - 1; i >= 0; --i)
            {
                var c = legacyChildren[i];
                if (c.name == "PlaceholderToggle") continue;
                Undo.SetTransformParent(c, contentParent, "Move Toggle to Scroll Content");
            }
        }

        Transform togglesParent = contentParent != null ? contentParent : togglesGO.transform;

        // Update existing toggles: ensure label and toggle sizes/overflow are correct
        for (int i = 0; i < togglesParent.childCount; ++i)
        {
            var child = togglesParent.GetChild(i).gameObject;
            // Skip placeholder
            if (child.name == "PlaceholderToggle") continue;

            // Ensure row height is 28px
            var rowLE = child.GetComponent<LayoutElement>();
            if (rowLE == null) rowLE = Undo.AddComponent<LayoutElement>(child);
            rowLE.preferredHeight = 28f;
            rowLE.minHeight = 28f;

            // Toggle element
            var toggleT = child.transform.Find("Toggle");
            if (toggleT != null)
            {
                var togGO = toggleT.gameObject;
                var toggleRect = togGO.GetComponent<RectTransform>();
                if (toggleRect != null) toggleRect.sizeDelta = new Vector2(26f, 26f);
                var tle = togGO.GetComponent<LayoutElement>();
                if (tle == null) tle = Undo.AddComponent<LayoutElement>(togGO);
                tle.preferredWidth = 26f;
                tle.preferredHeight = 26f;
            }

            // Label element
            var labelT = child.transform.Find("Label");
            if (labelT != null)
            {
                var labelGO = labelT.gameObject;
                var tmp = labelGO.GetComponent<TextMeshProUGUI>();
                var txt = labelGO.GetComponent<Text>();
                if (txt != null)
                {
                    txt.horizontalOverflow = HorizontalWrapMode.Overflow;
                    txt.verticalOverflow = VerticalWrapMode.Truncate;
                    txt.resizeTextForBestFit = true;
                    txt.resizeTextMinSize = 10;
                    txt.resizeTextMaxSize = 14;
                }
                else if (tmp != null)
                {
                    tmp.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
                    tmp.enableAutoSizing = true;
                    tmp.fontSizeMin = 10;
                    tmp.fontSizeMax = 14;
                }
                else
                {
                    txt = Undo.AddComponent<Text>(labelGO);
                    txt.horizontalOverflow = HorizontalWrapMode.Overflow;
                    txt.verticalOverflow = VerticalWrapMode.Truncate;
                    txt.resizeTextForBestFit = true;
                    txt.resizeTextMinSize = 10;
                    txt.resizeTextMaxSize = 14;
                }

                var lle = labelGO.GetComponent<LayoutElement>();
                if (lle == null) lle = Undo.AddComponent<LayoutElement>(labelGO);
                lle.flexibleWidth = 1f;
            }
        }

        // Mark scene dirty so changes persist
        EditorSceneManager.MarkSceneDirty(panel.gameObject.scene);
        return true;
    }
}
