using System.Collections.Generic;
using Sudoku.Models;

namespace Sudoku.UI.Config
{
    /**
     * Runtime configuration flags for colour highlighting options.
     *
     * Controls which pastel highlight colours are available to the user
     * when annotating candidate digits and cell values on the board.
     */
    public static class ColourSettings
    {
        /** When true, the green highlight colour is available. */
        public static bool GreenEnabled { get; set; } = true;

        /** When true, the amber highlight colour is available. */
        public static bool AmberEnabled { get; set; } = true;

        /** When true, the red highlight colour is available. */
        public static bool RedEnabled { get; set; } = true;

        /** When true, the blue highlight colour is available. */
        public static bool BlueEnabled { get; set; } = false;

        /** True when at least one colour is enabled. */
        public static bool AnyEnabled =>
            GreenEnabled || AmberEnabled || RedEnabled || BlueEnabled;

        /**
         * Return the ordered list of currently enabled highlight colours.
         *
         * @returns Enabled colours in display order: Green, Amber, Red, Blue.
         */
        public static List<HighlightColor> GetEnabledColours()
        {
            var result = new List<HighlightColor>(4);
            if (GreenEnabled) result.Add(HighlightColor.Green);
            if (AmberEnabled) result.Add(HighlightColor.Amber);
            if (RedEnabled)   result.Add(HighlightColor.Red);
            if (BlueEnabled)  result.Add(HighlightColor.Blue);
            return result;
        }
    }
}
