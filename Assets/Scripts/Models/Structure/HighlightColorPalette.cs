using UnityEngine;

namespace Sudoku.Models
{
    /**
     * Pastel colour definitions and helpers for the HighlightColor enum.
     *
     * Centralises all colour-to-RGBA conversions so rendering code doesn't
     * need to repeat colour constants.
     */
    public static class HighlightColorPalette
    {
        // Pastel base colours used for annotations.
        public static readonly Color Green = new Color(0.59f, 0.93f, 0.59f, 1f);
        public static readonly Color Amber = new Color(0.98f, 0.84f, 0.47f, 1f);
        public static readonly Color Red   = new Color(0.98f, 0.58f, 0.58f, 1f);
        public static readonly Color Blue  = new Color(0.54f, 0.74f, 0.98f, 1f);

        /**
         * Convert a HighlightColor to its UnityEngine Color.
         *
         * @param highlight The enum value to convert.
         * @returns The matching pastel RGBA colour, or Color.clear for None.
         */
        public static Color ToColor(HighlightColor highlight)
        {
            return highlight switch
            {
                HighlightColor.Green => Green,
                HighlightColor.Amber => Amber,
                HighlightColor.Red   => Red,
                HighlightColor.Blue  => Blue,
                _                   => Color.clear,
            };
        }

        /**
         * Short single-letter label for use in compact UI (e.g. radial menu).
         *
         * @param highlight The enum value to label.
         * @returns A single character label string.
         */
        public static string ToLabel(HighlightColor highlight)
        {
            return highlight switch
            {
                HighlightColor.Green => "G",
                HighlightColor.Amber => "A",
                HighlightColor.Red   => "R",
                HighlightColor.Blue  => "B",
                _                   => "?",
            };
        }

        /**
         * Human-readable name for the colour.
         *
         * @param highlight The enum value to name.
         * @returns The colour's full name, or "None".
         */
        public static string ToFullName(HighlightColor highlight)
        {
            return highlight switch
            {
                HighlightColor.Green => "Green",
                HighlightColor.Amber => "Amber",
                HighlightColor.Red   => "Red",
                HighlightColor.Blue  => "Blue",
                _                   => "None",
            };
        }
    }
}
