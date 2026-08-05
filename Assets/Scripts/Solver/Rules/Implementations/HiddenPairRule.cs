namespace Sudoku.Solver.Rules
{
    /**
     * Hidden Pair: two digits are collectively confined to two cells in one unit,
     * so all non-pair candidates can be removed from those two cells.
     */
    public class HiddenPairRule : HiddenSubsetRuleBase
    {
        public override string Name => "Hidden Pair";

        public override Difficulty Difficulty => Difficulty.Hard;

        protected override int SubsetSize => 2;

        protected override string SubsetLabel => "Pair";
    }

}

