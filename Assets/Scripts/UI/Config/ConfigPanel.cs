using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Sudoku.UI.Config
{
    /**
     * Tabbed configuration panel that manages multiple configuration tabs.
     *
     * Layout (all driven by VerticalLayoutGroup — no absolute positioning inside):
     *   Panel root (VLG vertical, Image background)
     *   ├── Header (HLG horizontal, fixed height)
     *   │   ├── TabsArea (HLG, flex width — one button per tab)
     *   │   └── CloseButton (fixed square, shown when OnCloseRequested is set)
     *   └── ContentArea (ScrollRect, fills remaining height)
     *       └── Viewport (RectMask2D) → Content (ContentSizeFitter)
     *
     * To add a new tab: call RegisterTab() before BuildPanel().
     * To add Audio/Graphics tabs: create a class that inherits ConfigTab.
     */
    [DisallowMultipleComponent]
    public class ConfigPanel : MonoBehaviour
    {
        [SerializeField] private Color _tabButtonBackgroundInactive = new Color(1f, 1f, 1f, 0.1f);
        [SerializeField] private Color _tabButtonBackgroundActive   = new Color(1f, 1f, 1f, 0.25f);
        [SerializeField] private Color _panelBackground             = new Color(0.13f, 0.13f, 0.13f, 0.97f);
        [SerializeField] private Color _headerBackground            = new Color(0.07f, 0.07f, 0.07f, 1f);
        [SerializeField] private float _tabButtonHeight = 40f;
        [SerializeField] private float _contentPadding  = 8f;

        /** Assign before calling BuildPanel() to wire up the close (×) button. */
        public System.Action OnCloseRequested;

        private List<ConfigTab> _tabs       = new List<ConfigTab>();
        private int             _activeTabIndex = -1;
        private RectTransform   _contentRoot;
        private List<Button>    _tabButtons = new List<Button>();

        /**
         * Register a configuration tab. Must be called before BuildPanel().
         * 
         * @param tab The ConfigTab to register.
         */
        public void RegisterTab(ConfigTab tab)
        {
            _tabs.Add(tab);
        }

        /**
         * Build the panel UI with all registered tabs.
         * Creates the tab button bar and content area.
         */
        public void BuildPanel()
        {
            if (_tabs.Count == 0)
            {
                Debug.LogWarning("ConfigPanel: No tabs registered. Cannot build panel.");
                return;
            }

            // Ensure a RectTransform is present.
            if (GetComponent<RectTransform>() == null)
                gameObject.AddComponent<RectTransform>();

            // Dark background for the whole panel.
            var bg = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            bg.color = _panelBackground;

            // Root VLG stacks Header → ContentArea vertically.
            var vlg = GetComponent<VerticalLayoutGroup>() ?? gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childControlHeight     = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth      = true;
            vlg.childForceExpandWidth  = true;
            vlg.spacing  = 0f;
            vlg.padding  = new RectOffset(0, 0, 0, 0);

            // Header row: tabs + optional close button (all inside HLG, no absolute positions).
            CreateHeader(transform);

            // Scrollable content area fills the rest.
            CreateContentArea(transform);

            // Activate the first tab.
            SelectTab(0);
        }

        /**
         * Switch to the tab at the given index.
         * 
         * @param tabIndex The index of the tab to activate.
         */
        public void SelectTab(int tabIndex)
        {
            if (tabIndex < 0 || tabIndex >= _tabs.Count)
            {
                Debug.LogWarning($"ConfigPanel: Invalid tab index {tabIndex}");
                return;
            }

            // Deactivate previous tab.
            if (_activeTabIndex >= 0 && _activeTabIndex < _tabs.Count)
                _tabs[_activeTabIndex].OnTabDeactivated();

            _activeTabIndex = tabIndex;

            // Update button tints.
            for (int i = 0; i < _tabButtons.Count; i++)
            {
                var img = _tabButtons[i].GetComponent<Image>();
                if (img != null)
                    img.color = (i == _activeTabIndex)
                        ? _tabButtonBackgroundActive
                        : _tabButtonBackgroundInactive;
            }

            // Clear and repopulate content.
            if (_contentRoot == null) return;

            for (int i = _contentRoot.childCount - 1; i >= 0; --i)
                Destroy(_contentRoot.GetChild(i).gameObject);

            _tabs[_activeTabIndex].PopulateContent(_contentRoot);
            _tabs[_activeTabIndex].OnTabActivated();
        }

        /**
         * Create the header row containing tab buttons and the close button.
         * Uses a HorizontalLayoutGroup so no absolute positioning is needed.
         * 
         * @param parent The panel root transform.
         */
        private void CreateHeader(Transform parent)
        {
            var headerGO = new GameObject("Header", typeof(RectTransform));
            headerGO.transform.SetParent(parent, false);

            // Fixed-height row driven by the parent VLG.
            var headerLE = headerGO.AddComponent<LayoutElement>();
            headerLE.preferredHeight = _tabButtonHeight;
            headerLE.minHeight       = _tabButtonHeight;
            headerLE.flexibleHeight  = 0f;
            headerLE.flexibleWidth   = 1f;

            var headerHLG = headerGO.AddComponent<HorizontalLayoutGroup>();
            headerHLG.childControlWidth      = true;
            headerHLG.childForceExpandWidth  = false;
            headerHLG.childControlHeight     = true;
            headerHLG.childForceExpandHeight = true;
            headerHLG.spacing  = 0f;
            headerHLG.padding  = new RectOffset(0, 0, 0, 0);

            var headerBg = headerGO.AddComponent<Image>();
            headerBg.color = _headerBackground;

            // Tab buttons container — grows to fill available width.
            var tabsAreaGO = new GameObject("TabsArea", typeof(RectTransform));
            tabsAreaGO.transform.SetParent(headerGO.transform, false);

            var tabsLE = tabsAreaGO.AddComponent<LayoutElement>();
            tabsLE.flexibleWidth = 1f;

            var tabsHLG = tabsAreaGO.AddComponent<HorizontalLayoutGroup>();
            tabsHLG.childControlWidth      = true;
            tabsHLG.childForceExpandWidth  = true;
            tabsHLG.childControlHeight     = true;
            tabsHLG.childForceExpandHeight = true;
            tabsHLG.spacing = 2f;
            tabsHLG.padding = new RectOffset(2, 2, 2, 2);

            for (int i = 0; i < _tabs.Count; i++)
                CreateTabButton(tabsAreaGO.transform, _tabs[i].TabName, i);

            // Close button sits right of the tabs, same height, square.
            if (OnCloseRequested != null)
                CreateCloseButtonInHeader(headerGO.transform);
        }

        /**
         * Create a single tab button inside the tabs area HLG.
         * 
         * @param parent    The TabsArea HLG transform.
         * @param tabName   Display name for the tab.
         * @param tabIndex  Index used by SelectTab.
         */
        private void CreateTabButton(Transform parent, string tabName, int tabIndex)
        {
            var btnGO = new GameObject($"Tab_{tabName}", typeof(RectTransform));
            btnGO.transform.SetParent(parent, false);

            var btnLE = btnGO.AddComponent<LayoutElement>();
            btnLE.flexibleWidth = 1f;

            var btnImage = btnGO.AddComponent<Image>();
            btnImage.color = _tabButtonBackgroundInactive;

            var btn = btnGO.AddComponent<Button>();
            btn.targetGraphic = btnImage;
            btn.transition    = Selectable.Transition.None;

            // Label — fills the button via anchors (safe: no VLG inside this button).
            var txtGO  = new GameObject("Label", typeof(RectTransform));
            txtGO.transform.SetParent(btnGO.transform, false);

            var txtRT  = txtGO.GetComponent<RectTransform>();
            txtRT.anchorMin  = Vector2.zero;
            txtRT.anchorMax  = Vector2.one;
            txtRT.offsetMin  = Vector2.zero;
            txtRT.offsetMax  = Vector2.zero;

            var txt = txtGO.AddComponent<Text>();
            txt.text      = tabName;
            txt.font      = GetSafeFont();
            txt.fontSize  = 14;
            txt.fontStyle = FontStyle.Bold;
            txt.color     = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.raycastTarget = false;

            _tabButtons.Add(btn);
            int idx = tabIndex;
            btn.onClick.AddListener(() => SelectTab(idx));
        }

        /**
         * Create the close (×) button as the last child in the header HLG.
         * It is a fixed-width square so it never stretches.
         * 
         * @param parent The Header HLG transform.
         */
        private void CreateCloseButtonInHeader(Transform parent)
        {
            var btnGO = new GameObject("CloseButton", typeof(RectTransform));
            btnGO.transform.SetParent(parent, false);

            // Fixed square — does not grow.
            var le = btnGO.AddComponent<LayoutElement>();
            le.preferredWidth  = _tabButtonHeight;
            le.minWidth        = _tabButtonHeight;
            le.flexibleWidth   = 0f;

            var img = btnGO.AddComponent<Image>();
            img.color = new Color(0.65f, 0.12f, 0.12f, 1f);

            var btn = btnGO.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition    = Selectable.Transition.ColorTint;
            btn.colors = new ColorBlock
            {
                normalColor      = new Color(0.65f, 0.12f, 0.12f, 1f),
                highlightedColor = new Color(0.85f, 0.20f, 0.20f, 1f),
                pressedColor     = new Color(0.45f, 0.08f, 0.08f, 1f),
                disabledColor    = new Color(0.50f, 0.50f, 0.50f, 0.5f),
                colorMultiplier  = 1f,
                fadeDuration     = 0.08f
            };
            btn.onClick.AddListener(() => OnCloseRequested?.Invoke());

            // × label — anchored inside the button (no outer layout group here).
            var txtGO = new GameObject("X", typeof(RectTransform));
            txtGO.transform.SetParent(btnGO.transform, false);

            var txtRT = txtGO.GetComponent<RectTransform>();
            txtRT.anchorMin = Vector2.zero;
            txtRT.anchorMax = Vector2.one;
            txtRT.offsetMin = Vector2.zero;
            txtRT.offsetMax = Vector2.zero;

            var txt = txtGO.AddComponent<Text>();
            txt.text      = "×";
            txt.font      = GetSafeFont();
            txt.fontSize  = 22;
            txt.fontStyle = FontStyle.Bold;
            txt.color     = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.raycastTarget = false;
        }

        /**
         * Create the scrollable content area. Uses a LayoutElement with flexibleHeight=1
         * so the VLG gives it all remaining space after the fixed-height header.
         * 
         * @param parent The panel root transform.
         */
        private void CreateContentArea(Transform parent)
        {
            // The scroll rect itself IS the content area GO.
            var areaGO = new GameObject("ContentArea", typeof(RectTransform));
            areaGO.transform.SetParent(parent, false);

            // Takes all remaining vertical space from the parent VLG.
            var areaLE = areaGO.AddComponent<LayoutElement>();
            areaLE.flexibleHeight = 1f;
            areaLE.flexibleWidth  = 1f;

            var scroll = areaGO.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical   = true;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            // Viewport — fills the scroll rect via anchors (no layout conflict as
            // ScrollRect is not a layout group).
            var viewportGO = new GameObject("Viewport", typeof(RectTransform));
            viewportGO.transform.SetParent(areaGO.transform, false);

            var viewportRT = viewportGO.GetComponent<RectTransform>();
            viewportRT.anchorMin = Vector2.zero;
            viewportRT.anchorMax = Vector2.one;
            viewportRT.offsetMin = Vector2.zero;
            viewportRT.offsetMax = Vector2.zero;

            viewportGO.AddComponent<RectMask2D>();
            scroll.viewport = viewportRT;

            // Content — anchored to top of viewport, grows downward via ContentSizeFitter.
            var contentGO = new GameObject("Content", typeof(RectTransform));
            contentGO.transform.SetParent(viewportGO.transform, false);

            _contentRoot            = contentGO.GetComponent<RectTransform>();
            _contentRoot.anchorMin  = new Vector2(0f, 1f);
            _contentRoot.anchorMax  = new Vector2(1f, 1f);
            _contentRoot.pivot      = new Vector2(0.5f, 1f);
            _contentRoot.sizeDelta  = Vector2.zero;

            var contentVLG = contentGO.AddComponent<VerticalLayoutGroup>();
            contentVLG.childControlHeight     = true;
            contentVLG.childForceExpandHeight = false;
            contentVLG.childControlWidth      = true;
            contentVLG.childForceExpandWidth  = true;
            contentVLG.spacing = 2f;
            contentVLG.padding = new RectOffset(
                (int)_contentPadding, (int)_contentPadding,
                (int)_contentPadding, (int)_contentPadding);

            // Height grows with content; width matches viewport.
            var csf = contentGO.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.content = _contentRoot;
            // Scroll to top after content is wired up.
            scroll.verticalNormalizedPosition = 1f;
        }

        /**
         * Return a builtin font with fallback to LegacyRuntime when Arial is unavailable.
         */
        private static Font GetSafeFont()
        {
            Font f = null;
            try   { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
            if (f == null)
                try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
            return f;
        }
    }
}
