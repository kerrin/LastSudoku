namespace Sudoku.Solver.Rules
{
    /**
     * Hidden Triple: three digits are collectively confined to three cells in one unit,
     * so all non-triple candidates can be removed from those three cells.
     */
    public class HiddenTripleRule : HiddenSubsetRuleBase
    {
        public override string Name => "Hidden Triple";

        public override Difficulty Difficulty => Difficulty.Hard;

        protected override int SubsetSize => 3;

        protected override string SubsetLabel => "Triple";
    }

}

