namespace Sudoku.Solver.Rules
{
    /**
     * Implements the Naked Triples technique:
     * If three cells in a unit (row, column, or block) have exactly the same three candidates,
     * those candidates can be removed from all other cells in that unit.
     */
    public class NakedTripleRule : NakedSubsetRuleBase
    {
        /** Rule display name. */
        public override string Name => "Naked Triple";

        /** Difficulty classification for this rule. */
        public override Difficulty Difficulty => Difficulty.Hard;

        protected override int SubsetSize => 3;

        protected override string SubsetLabel => "Triple";
    }
}

