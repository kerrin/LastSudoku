using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Sudoku.Solver;
using Sudoku.Solver.Rules;

/// <summary>
/// Runtime UI panel that creates a toggle for each registered rule so the
/// user can enable/disable rules without modifying code. Attach this to a
/// GameObject in the scene; it will create a Canvas if none exists.
/// </summary>
public class RuleTogglePanel : MonoBehaviour
{
    public SolverRunner Runner;

    [Tooltip("Optional: width of the panel in pixels")]
    public float PanelWidth = 260f;

    [Tooltip("Optional: maximum height before the panel becomes scrollable")]
    public float MaxHeight = 400f;

    private RuleRegistry _registry;

    private void Start()
    {
        if (Runner == null) Runner = Object.FindAnyObjectByType<SolverRunner>();
        if (Runner == null)
        {
            Debug.LogWarning("RuleTogglePanel: No SolverRunner found in scene.");
            return;
        }

        _registry = Runner.Registry;
        if (_registry == null)
        {
            // Ensure the runner initializes a registry
            Runner.EnsureEngine();
            _registry = Runner.Registry;
            if (_registry == null)
            {
                Debug.LogWarning("RuleTogglePanel: Runner did not provide a RuleRegistry.");
                return;
            }
        }

        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        // If an existing Canvas is present but not ScreenSpaceOverlay (e.g. World Space),
        // create a default overlay canvas so the runtime UI is visible in the Game view.
        if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            canvas = CreateDefaultCanvas();
        }

        // Create panel
        GameObject panelGO = new GameObject("RuleTogglePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelGO.transform.SetParent(canvas.transform, false);
        var panelRect = panelGO.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.anchoredPosition = new Vector2(-10f, -10f);
        panelRect.sizeDelta = new Vector2(PanelWidth, Mathf.Min(MaxHeight, 200f));
        var img = panelGO.GetComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.6f);

        // Add layout
        var layout = panelGO.AddComponent<VerticalLayoutGroup>();
        layout.childForceExpandHeight = false;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.padding = new RectOffset(6, 6, 6, 6);
        layout.spacing = 4f;

        // Create header
        var headerGO = new GameObject("Header", typeof(RectTransform));
        headerGO.transform.SetParent(panelGO.transform, false);
        var headerText = headerGO.AddComponent<Text>();
        headerText.text = "Rules";
        headerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        headerText.fontSize = 18;
        headerText.color = Color.white;

        // Create content container (scrollable if many rules)
        GameObject contentGO = new GameObject("Content", typeof(RectTransform));
        contentGO.transform.SetParent(panelGO.transform, false);

        var rules = _registry.GetRulesWithStatus();
        foreach (var entry in rules)
        {
            CreateRuleToggle(contentGO.transform, entry.rule, entry.enabled);
        }
    }

    private void CreateRuleToggle(Transform parent, ISudokuRule rule, bool enabled)
    {
        var go = new GameObject(rule.GetType().Name + "_Toggle", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var toggle = go.AddComponent<Toggle>();

        // Background
        var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(go.transform, false);
        var bgImg = bg.GetComponent<Image>();
        bgImg.color = new Color(1f, 1f, 1f, 0.06f);

        // Checkmark
        var ck = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
        ck.transform.SetParent(bg.transform, false);
        var ckImg = ck.GetComponent<Image>();
        ckImg.color = Color.white;
        toggle.graphic = ckImg;

        // Label
        var labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(go.transform, false);
        var label = labelGO.AddComponent<Text>();
        label.text = rule.Name;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.color = Color.white;

        // Setup toggle initial state and callback
        toggle.isOn = enabled;
        string ruleTypeName = rule.GetType().Name;
        toggle.onValueChanged.AddListener((val) =>
        {
            _registry.SetEnabled(ruleTypeName, val);
            Debug.Log($"Rule '{ruleTypeName}' enabled={val}");
        });
    }

    private Canvas CreateDefaultCanvas()
    {
        var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // Ensure an EventSystem exists; prefer the new Input System UI module when available.
        if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            // Prefer the Input System UI module if it's available in any loaded assembly.
            System.Type inputModuleType = null;
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                inputModuleType = asm.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule");
                if (inputModuleType != null) break;
            }
            if (inputModuleType != null)
            {
                es.AddComponent(inputModuleType);
            }
            else
            {
                Debug.LogWarning("InputSystemUIInputModule not found in loaded assemblies. Not adding StandaloneInputModule to avoid Input System mismatch. Install the Input System package or change Player Settings.");
            }
        }

        return canvas;
    }
}
