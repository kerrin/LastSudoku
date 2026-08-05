namespace Sudoku.Solver.Rules
{
    /**
     * Hidden Quad: four digits are collectively confined to four cells in one unit,
     * so all non-quad candidates can be removed from those four cells.
     */
    public class HiddenQuadRule : HiddenSubsetRuleBase
    {
        public override string Name => "Hidden Quad";

        public override Difficulty Difficulty => Difficulty.Hard;

        protected override int SubsetSize => 4;

        protected override string SubsetLabel => "Quad";
    }

}

