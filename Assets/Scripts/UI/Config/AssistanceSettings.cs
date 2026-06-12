namespace Sudoku.UI.Config
{
    /**
     * Runtime configuration flags for Assistance options shown in the config panel.
     *
     * Values are intentionally static so gameplay/UI systems can read the latest
     * toggle state without needing scene references.
     */
    public static class AssistanceSettings
    {
        /**
         * When true, puzzle start fills every unsolved cell with all candidates.
         */
        public static bool AutoFillAllCandidatesOnPuzzleStart { get; set; }

        /**
         * When true and auto-fill is enabled, puzzle start removes illegal
         * candidates from each unsolved cell after the full fill pass.
         */
        public static bool AutoInitialiseCandidatesOnPuzzleStart { get; set; }

        /**
         * When true, the ApplyRulePanel is hidden even in puzzle mode.
         */
        public static bool HideApplyRules { get; set; }

        /**
         * When true, setting a value applies candidate cleanup to peers.
         * When false, set-value behaves as value-only placement.
         */
        public static bool AutoCandidateOnSetValue { get; set; } = true;
    }
}