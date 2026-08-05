using System.Collections.Generic;
using System.Linq;
using Cell = Sudoku.Models.Cell;
using Board = Sudoku.Models.Board;

namespace Sudoku.Solver.Rules
{
    /**
     * Hidden Single (also called Single Position) — for a given digit in a unit
     * (row/column/box), if only one cell can contain that digit according to candidates,
     * place it there.
     */
    public class HiddenSingleRule : HiddenRuleBase
    {
        public override string Name => "Hidden Single";

        public override Difficulty Difficulty => Difficulty.Easy;

        private class HiddenSingleResult
        {
            public Cell Cell { get; set; }
            public int Digit { get; set; }
            public List<Cell> UnitCells { get; set; }
        }

        public override bool CanApply(Board board)
        {
            return FindAny(board) != null;
        }

        /**
         * Find the first hidden single in rows, columns, then boxes.
         *
         * @param board Board to inspect.
         * @returns First hidden single hit or null.
         */
        private HiddenSingleResult FindAny(Board board)
        {
            foreach (var unit in EnumerateUnitsInSolverOrder(board))
            {
                int size = board.Size;
                for (int digit = 1; digit <= size; digit++)
                {
                    var candidates = new List<Cell>();
                    bool digitAlreadyPlaced = false;

                    foreach (var cell in unit.cells)
                    {
                        if (cell.Value.HasValue && cell.Value.Value == digit)
                        {
                            digitAlreadyPlaced = true;
                            break;
                        }

                        if (!cell.Value.HasValue && cell.Candidates.Contains(digit))
                        {
                            candidates.Add(cell);
                            if (candidates.Count > 1)
                            {
                                break;
                            }
                        }
                    }

                    if (!digitAlreadyPlaced && candidates.Count == 1)
                    {
                        return new HiddenSingleResult
                        {
                            Cell = candidates[0],
                            Digit = digit,
                            UnitCells = unit.cells.ToList()
                        };
                    }
                }
            }

            return null;
        }

        protected override RuleResult CalculateChangesInternal(Board board)
        {
            var result = new RuleResult();
            HiddenSingleResult found = FindAny(board);
            if (found == null)
            {
                result.Apply = false;
                return result;
            }
            Cell cell = found.Cell;
            int digit = found.Digit;
            CellChange change = new CellChange { Row = cell.Row, Column = cell.Column, NewValue = digit, RemovedCandidates = RuleExtensions.AllCandidatesExcept(cell, digit) };

            // Highlight the whole unit (row/column/box) that contained the single candidate.
            foreach (Cell u in found.UnitCells)
            {
                if (!result.UsedCells.Exists(x => x.Row == u.Row && x.Column == u.Column && x.Candidate == digit))
                    result.UsedCells.Add(new UsedCell { Row = u.Row, Column = u.Column, Candidate = digit });
            }

            // Record the placement and peer candidate removals as consequences of the single deduction.
            result.Changes.Add(change);
            if (!result.UsedCells.Exists(u => u.Row == cell.Row && u.Column == cell.Column && u.Candidate == digit))
                result.UsedCells.Add(new UsedCell { Row = cell.Row, Column = cell.Column, Candidate = digit });

            foreach (Cell peer in board.GetPeers(cell))
            {
                if (peer.Candidates.Contains(digit))
                {
                    var peerChange = new CellChange { Row = peer.Row, Column = peer.Column };
                    peerChange.RemovedCandidates.Add(digit);
                    result.Changes.Add(peerChange);
                }
            }

            result.Apply = true;
            result.Description = $"Placed {digit} at ({cell.Row},{cell.Column}) via Hidden Single";
            return result;
        }
    }
}

