using UnityEngine;

namespace Sudoku.UI.Config
{
    /**
     * Base class for configuration panel tabs. Tabs are content containers
     * that can be shown/hidden and are managed by a parent ConfigPanel.
     *
     * Subclasses should override PopulateContent() to create their UI elements.
     */
    public abstract class ConfigTab : MonoBehaviour
    {
        [SerializeField] private string _tabName = "Tab";

        /** Display name for this tab (shown on the tab button). */
        public string TabName
        {
            get { return _tabName; }
            set { _tabName = value; }
        }

        /** 
         * Create the UI content for this tab. The contentRoot RectTransform is empty
         * and ready for content to be added as children.
         * 
         * @param contentRoot The RectTransform container where content should be placed.
         */
        public abstract void PopulateContent(RectTransform contentRoot);

        /**
         * Called when this tab is activated (made visible).
         * Override to perform any refresh logic needed when switching to this tab.
         */
        public virtual void OnTabActivated()
        {
        }

        /**
         * Called when this tab is deactivated (hidden).
         * Override to perform any cleanup logic when switching away from this tab.
         */
        public virtual void OnTabDeactivated()
        {
        }
    }
}
