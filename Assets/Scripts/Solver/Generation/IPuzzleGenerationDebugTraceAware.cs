namespace Sudoku.Solver.Unsolver
{
    /**
     * Optional capability for unsolve handlers that can emit nested generation-debug
     * events while a top-level PuzzleGenerator step is executing.
     */
    public interface IPuzzleGenerationDebugTraceAware
    {
        void SetDebugTracer(IPuzzleGenerationDebugTracer debugTracer);
    }
}
