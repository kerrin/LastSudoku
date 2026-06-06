namespace Sudoku.Solver.Unsolver
{
    /**
     * Describes the outcome of an <see cref="IUnsolveHandler.TryUnsolve"/> call.
     */
    public enum UnsolveResult
    {
        /** A value was successfully removed; the corresponding rule can reinstate it. */
        Success,

        /** No suitable cell was found in the current board state for this handler. */
        NoApplicableMove,

        /** This handler does not support value removal (e.g. candidate-only rules). */
        NotSupported,
    }
}
