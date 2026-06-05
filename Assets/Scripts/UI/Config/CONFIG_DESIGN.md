# Configuration Panel System Design

## Overview
The configuration panel is a tabbed interface that allows users to customize various game settings. It's designed to be extensible, supporting the addition of new configuration tabs (Audio, Graphics, etc.) without modifying the core panel structure.

## Architecture

### Component Hierarchy

```
ConfigPanelManager
├── Creates and manages the config panel lifecycle
├── Opens/closes the panel as a modal overlay
└── Manages canvas and background elements

ConfigPanel
├── Main tabbed panel component
├── Manages tab switching
├── Creates tab button bar
├── Creates content area with scroll view
└── Delegates content creation to active tab

ConfigTab (Abstract Base Class)
├── Base class for all configuration tabs
├── Implements PopulateContent(contentRoot) for subclasses
├── Provides OnTabActivated/OnTabDeactivated lifecycle hooks
└── Manages tab-specific state

RulesConfigTab (Concrete Implementation)
├── Implements rule toggle UI
├── Manages rule registry integration
├── Creates toggle for each registered rule
└── Updates ApplyRulePanel on state changes
```

### UI Layout Structure

```
ConfigPanelCanvas (if needed)
└── ConfigPanel (Modal overlay container)
    ├── Background (Semi-transparent black overlay, clickable to close)
    ├── PanelContainer (Centered content container)
    │   └── Panel (Main panel with tab system)
    │       ├── TabButtonBar (Top: horizontal layout with tab buttons)
    │       │   ├── Tab_RulesButton
    │       │   ├── Tab_AudioButton (future)
    │       │   └── Tab_GraphicsButton (future)
    │       ├── ContentArea (Bottom: scrollable content)
    │       │   └── ScrollView
    │       │       └── Viewport
    │       │           └── Content (Populated by active tab)
    │       └── CloseButton (Top-right corner)
```

## Core Classes

### ConfigTab
**Location:** `Assets/Scripts/UI/Config/ConfigTab.cs`

Base class for all configuration tabs.

```csharp
public abstract class ConfigTab : MonoBehaviour
{
    public string TabName { get; set; }
    
    public abstract void PopulateContent(RectTransform contentRoot);
    public virtual void OnTabActivated() { }
    public virtual void OnTabDeactivated() { }
}
```

**Usage:** Inherit from `ConfigTab` to create new configuration tabs.

### ConfigPanel
**Location:** `Assets/Scripts/UI/Config/ConfigPanel.cs`

Main panel component that manages tabs.

```csharp
public class ConfigPanel : MonoBehaviour
{
    public void RegisterTab(ConfigTab tab);
    public void BuildPanel();
    public void SelectTab(int tabIndex);
}
```

**Key Features:**
- Creates tab button bar with one button per registered tab
- Creates scrollable content area
- Handles tab switching and content population
- Uses layout groups for responsive sizing

### RulesConfigTab
**Location:** `Assets/Scripts/UI/Config/Tabs/RulesConfigTab.cs`

Configuration tab for managing Sudoku rule toggles.

```csharp
public class RulesConfigTab : ConfigTab
{
    public SolverRunner Runner { get; set; }
    
    public override void PopulateContent(RectTransform contentRoot);
    public override void OnTabActivated();
    public override void OnTabDeactivated();
}
```

**Features:**
- Automatically discovers rules from SolverRunner's RuleRegistry
- Creates a toggle row for each rule
- Updates rule enabled state through RuleRegistry
- Refreshes ApplyRulePanel on state changes

### ConfigPanelManager
**Location:** `Assets/Scripts/UI/Config/ConfigPanelManager.cs`

Singleton manager for opening/closing the configuration panel.

```csharp
public class ConfigPanelManager : MonoBehaviour
{
    public void OpenConfigPanel();
    public void CloseConfigPanel();
}
```

**Integration Points:**
- Called from `MainMenuFlowController.OpenConfigurationStub()`
- Creates its own canvas if none exists
- Automatically initializes the RulesConfigTab
- Handles keyboard input (Escape) for closing

## How to Add a New Configuration Tab

### Step 1: Create a New Tab Class

```csharp
// Assets/Scripts/UI/Config/Tabs/AudioConfigTab.cs
namespace Sudoku.Scripts.UI.Config
{
    public class AudioConfigTab : ConfigTab
    {
        public override void PopulateContent(RectTransform contentRoot)
        {
            // Create audio configuration UI here
            // Example: Volume slider, mute toggle, etc.
        }

        public override void OnTabActivated()
        {
            // Called when this tab becomes visible
            // Refresh UI state if needed
        }

        public override void OnTabDeactivated()
        {
            // Called when this tab is hidden
            // Cleanup if needed
        }
    }
}
```

### Step 2: Register the Tab in ConfigPanelManager

In `ConfigPanelManager.CreateConfigPanel()`, add the new tab:

```csharp
// Create the audio tab
var audioTab = panelGO.AddComponent<AudioConfigTab>();
audioTab.TabName = "Audio";
_configPanel.RegisterTab(audioTab);
```

### Step 3: Order Matters

Tabs are displayed in the order they are registered, so register them in the desired order:

```csharp
// Register in order: Rules, Audio, Graphics, etc.
var rulesTab = panelGO.AddComponent<RulesConfigTab>();
_configPanel.RegisterTab(rulesTab);

var audioTab = panelGO.AddComponent<AudioConfigTab>();
_configPanel.RegisterTab(audioTab);

var graphicsTab = panelGO.AddComponent<GraphicsConfigTab>();
_configPanel.RegisterTab(graphicsTab);
```

## Future Enhancements

### For Rule Explanations (Phase 2)
- Add a help/info icon next to each rule toggle
- Clicking opens a modal with rule explanation and tutorial link
- Store rule descriptions in metadata or localization system

### For Hint/Solve Features (Phase 2)
- Add a new tab or section for Solve Mode settings
- Include toggles for:
  - Auto-apply hints
  - Difficulty levels
  - Hint types (candidates only, solving techniques, etc.)
  - Solve mode analytics

### For Audio/Graphics Configs (Phase 3)
- **AudioConfigTab**: Volume sliders, sound effect toggles
- **GraphicsConfigTab**: Quality presets, particle effects, board appearance

## Integration Points

1. **MainMenuFlowController**
   - `OpenConfigurationStub()` creates ConfigPanelManager and opens panel
   - Can be called from main menu "Open Configuration" button

2. **RuleRegistry**
   - RulesConfigTab queries current rule states
   - Updates rule enabled state on toggle change
   - Used by SolverRunner to apply rule changes

3. **ApplyRulePanel**
   - Refreshed when rules are toggled
   - Ensures UI reflects current rule state

4. **RuntimeUIBootstrapper**
   - Could auto-create ConfigPanelManager if desired

## Testing

Unit tests are provided in `Assets/Tests/Editor/UI/Config/ConfigPanelTests.cs`:
- `ConfigPanelManager_CanBeCreated()` - Verifies manager creation
- `ConfigPanel_CanBuildWithRulesTab()` - Verifies tab structure
- `ConfigTab_PopulateContent_CanBeOverridden()` - Verifies inheritance

## Known Limitations

1. No persistence: Configuration changes are not saved between sessions (can be added later)
2. No validation: No input validation for configuration values (add as needed)
3. No descriptions: Rule descriptions hardcoded in UI (can be moved to metadata)
4. No localization: All text is in English (add localization system if needed)

## File Locations

- `Assets/Scripts/UI/Config/ConfigTab.cs` - Base class
- `Assets/Scripts/UI/Config/ConfigPanel.cs` - Main panel
- `Assets/Scripts/UI/Config/ConfigPanelManager.cs` - Manager/entry point
- `Assets/Scripts/UI/Config/Tabs/RulesConfigTab.cs` - Rules implementation
- `Assets/Tests/Editor/UI/Config/ConfigPanelTests.cs` - Unit tests
