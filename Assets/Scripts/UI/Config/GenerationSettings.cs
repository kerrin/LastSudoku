using UnityEngine;

namespace Sudoku.UI.Config
{
    /**
     * Runtime configuration flags for puzzle generation options.
     */
    public static class GenerationSettings
    {
        public const int MinAllowedSolutionsWhenNonUnique = 2;
        public const int MaxAllowedSolutionsWhenNonUniqueLimit = 64;

        private static int _maxAllowedSolutionsWhenNonUnique = 8;

        /**
         * When true, generated puzzles keep 180-degree rotational clue symmetry.
         */
        public static bool UseRotationalSymmetry { get; set; } = true;

        /**
         * When true, generated puzzles must remain uniquely solvable.
         * When false, puzzles may be solvable with advanced rules or guessing.
         */
        public static bool GenerateUniqueSolvable { get; set; } = true;

        /**
         * Maximum number of valid solutions accepted when unique solvability is disabled.
         */
        public static int MaxAllowedSolutionsWhenNonUnique
        {
            get => _maxAllowedSolutionsWhenNonUnique;
            set => _maxAllowedSolutionsWhenNonUnique = Mathf.Clamp(
                value,
                MinAllowedSolutionsWhenNonUnique,
                MaxAllowedSolutionsWhenNonUniqueLimit);
        }
    }
}
