using Sudoku.Solver.Rules;

namespace Sudoku.Solver.Unsolver
{
    /**
     * Maps solver rules to their corresponding <see cref="IUnsolveHandler"/> implementations.
     *
    * Supported rules (Naked Single, Hidden Single, Hidden Pair, Hidden Triple, Right Angle) get dedicated handlers.
     * All other rules receive a <see cref="CandidateOnlyUnsolveHandler"/> stub that
     * returns <see cref="UnsolveResult.NotSupported"/> without mutating the board.
     *
     * The mapping is keyed by rule type name to stay decoupled from rule internals.
    * New rules with dedicated unsolve behavior should be registered here when added.
     */
    public static class UnsolveHandlerRegistry
    {
        /**
         * Return the appropriate <see cref="IUnsolveHandler"/> for the given rule.
         *
         * @param rule The solver rule to map.
         * @returns A handler capable of unsolving for that rule (or a NotSupported stub).
         */
        public static IUnsolveHandler GetHandler(ISudokuRule rule)
        {
            switch (rule.GetType().Name)
            {
                case nameof(NakedSingleRule):
                    return new NakedSingleUnsolveHandler();
                case nameof(HiddenSingleRule):
                    return new HiddenSingleUnsolveHandler();
                case nameof(HiddenPairRule):
                    return new HiddenPairUnsolveHandler();
                case nameof(HiddenTripleRule):
                    return new HiddenTripleUnsolveHandler();
                case nameof(RightAngleRule):
                    return new RightAngleUnsolveHandler();
                default:
                    return new CandidateOnlyUnsolveHandler(rule.Name);
            }
        }
    }
}
