namespace Sudoku.Solver.Rules
{
    /**
     * Implements the Naked Pairs technique:
     * If two cells in a unit (row, column, or block) have exactly the same two candidates,
     * those candidates can be removed from all other cells in that unit.
     */
    public class NakedPairRule : NakedSubsetRuleBase
    {
        /** Rule display name. */
        public override string Name => "Naked Pair";

        /** Difficulty classification for this rule. */
        public override Difficulty Difficulty => Difficulty.Medium;

        protected override int SubsetSize => 2;

        protected override string SubsetLabel => "Pair";
    }
}

