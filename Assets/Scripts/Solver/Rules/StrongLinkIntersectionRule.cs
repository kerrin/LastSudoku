using System.Collections.Generic;
using System.Linq;
using Sudoku.Models;

namespace Sudoku.Solver.Rules
{
    /**
     * Strong-link intersection rule.
     *
     * For a digit d, find an endpoint A inside box B1 such that within B1 there
     * are exactly two candidates for d in A.Row (a strong link). Find an endpoint
     * B inside a different box B2 such that within B2 there are exactly two
     * candidates for d in B.Column (a strong link). If A and B do not share row
     * or column, then any cell that sees both A and B cannot contain d.
     */
    public class StrongLinkIntersectionRule : ISudokuRule
    {
        public string Name => "Strong Link Intersection";

        public Difficulty Difficulty => Difficulty.Medium;

        public bool CanApply(Board board)
        {
            // Disabled: this rule is not currently supported/active.
            return false;
        }

        private (Cell a, Cell b, int digit, List<Cell> removals)? FindElimination(Board board)
        {
            int size = board.Size;
            for (int digit = 1; digit <= size; digit++)
            {
                var candidates = new List<Cell>();
                for (int r = 0; r < size; r++) for (int c = 0; c < size; c++)
                {
                    var cell = board.Cells[r, c];
                    if (!cell.Value.HasValue && cell.Candidates.Contains(digit)) candidates.Add(cell);
                }

                foreach (Cell a in candidates)
                {
                    var boxRowCells = board.GetBox(a.Box).Where(x => x.Row == a.Row && !x.Value.HasValue && x.Candidates.Contains(digit)).ToList();
                    if (boxRowCells.Count != 2) continue;

                    foreach (Cell b in candidates)
                    {
                        if (ReferenceEquals(a, b)) continue;
                        if (a.Box == b.Box) continue;
                        if (a.Row == b.Row || a.Column == b.Column) continue;

                        var boxColCells = board.GetBox(b.Box).Where(x => x.Column == b.Column && !x.Value.HasValue && x.Candidates.Contains(digit)).ToList();
                        if (boxColCells.Count != 2) continue;

                        var peersA = new HashSet<Cell>(board.GetPeers(a));
                        var peersB = new HashSet<Cell>(board.GetPeers(b));
                        peersA.IntersectWith(peersB);

                        var removals = new List<Cell>();
                        foreach (Cell p in peersA)
                        {
                            if (p == a || p == b) continue;
                            if (!p.Value.HasValue && p.Candidates.Contains(digit)) removals.Add(p);
                        }

                        if (removals.Count > 0) return (a, b, digit, removals);
                    }
                }
            }
            return null;
        }

        public RuleResult Apply(Board board)
        {
            var found = FindElimination(board);
            var r = new RuleResult();
            if (found == null)
            {
                r.Applied = false;
                return r;
            }
            var (a, b, digit, removals) = found.Value;
            if (!r.UsedCells.Exists(u => u.Row == a.Row && u.Column == a.Column)) r.UsedCells.Add(new UsedCell { Row = a.Row, Column = a.Column });
            if (!r.UsedCells.Exists(u => u.Row == b.Row && u.Column == b.Column)) r.UsedCells.Add(new UsedCell { Row = b.Row, Column = b.Column });
            foreach (Cell p in removals)
            {
                if (p.Candidates.Remove(digit))
                {
                    var change = new CellChange { Row = p.Row, Column = p.Column };
                    change.RemovedCandidates.Add(digit);
                    r.Changes.Add(change);
                    if (!r.UsedCells.Exists(u => u.Row == p.Row && u.Column == p.Column)) r.UsedCells.Add(new UsedCell { Row = p.Row, Column = p.Column });
                }
            }
            r.Applied = r.Changes.Count > 0;
            if (r.Applied) r.Description = $"Strong link intersection removed {digit} from {r.Changes.Count} cell(s)";
            return r;
        }

        public RuleResult ApplyOnlyCandidates(Board board)
        {
            return new RuleResult { Applied = false };
        }
    }
}
