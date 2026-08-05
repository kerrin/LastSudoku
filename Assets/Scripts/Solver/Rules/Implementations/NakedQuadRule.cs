namespace Sudoku.Solver.Rules
{
    /**
     * Implements the Naked Quads technique:
     * If four cells in a unit (row, column, or block) have exactly the same four candidates,
     * those candidates can be removed from all other cells in that unit.
     */
    public class NakedQuadRule : NakedSubsetRuleBase
    {
        /** Rule display name. */
        public override string Name => "Naked Quad";

        /** Difficulty classification for this rule. */
        public override Difficulty Difficulty => Difficulty.Hard;

        protected override int SubsetSize => 4;

        protected override string SubsetLabel => "Quad";
    }
}

