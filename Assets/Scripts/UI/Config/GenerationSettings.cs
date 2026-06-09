namespace Sudoku.UI.Config
{
    /**
     * Runtime configuration flags for puzzle generation options.
     */
    public static class GenerationSettings
    {
        /**
         * When true, generated puzzles keep 180-degree rotational clue symmetry.
         */
        public static bool UseRotationalSymmetry { get; set; } = true;
    }
}
