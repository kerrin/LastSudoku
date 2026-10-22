using System.Collections.Generic;
using Sudoku.Models;
using Board = Sudoku.Models.Board;

namespace Sudoku.Solver.Rules
{
    internal static class ForcingChainBranchGenerator
    {
        public static List<ForcingChainBranch> Generate(
            Board board,
            ForcingChainLinkModel model,
            IEnumerable<int> seedCandidates,
            bool assumeTrue,
            bool enableUnitCompletion)
        {
            var branches = new List<ForcingChainBranch>();
            foreach (int seedCandidate in seedCandidates)
            {
                branches.Add(new ForcingChainBranch
                {
                    SeedCandidate = seedCandidate,
                    State = ForcingChainPropagation.PropagateFromAssumption(
                        board,
                        model,
                        seedCandidate,
                        assumeTrue,
                        enableUnitCompletion)
                });
            }

            return branches;
        }
    }
}
