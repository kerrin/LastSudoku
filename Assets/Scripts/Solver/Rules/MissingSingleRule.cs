using System.Collections.Generic;
using System.Linq;
using Sudoku.Models;

namespace Sudoku.Solver.Rules
{
    /**
     * Also called "Single Position" — when a digit can only occupy one cell
     * within a unit (row, column, or box), it must be placed there.
     */
    public class MissingSingleRule : ISudokuRule
    {
        private enum UnitKind { Row, Column, Box }
        /** Rule display name. */
        public string Name => "Missing Single";

        /** Difficulty classification for this rule. */
        public Difficulty Difficulty => Difficulty.Easy;

        private class MissingSingleResult
        {
            public Cell Cell { get; set; }
            public int Digit { get; set; }
            public UnitKind Unit { get; set; }
            public int UnitIndex { get; set; }
        }

        /** Return true if any unit contains a digit that has only one candidate position. */
        public bool CanApply(Board board)
        {
            return FindAny(board) != null;
        }

        /**
         * Find the first (cell,digit) pair where the digit is the only candidate
         * within its row, column, or box. Returns null when none found.
         */
        private MissingSingleResult FindAny(Board board)
        {
            int size = board.Size;
            for (int digit = 1; digit <= size; digit++)
            {
                /** rows */
                for (int r = 0; r < size; r++)
                {
                    var candidates = new List<Cell>();
                    foreach (Cell cell in board.GetRow(r))
                    {
                        if (!cell.Value.HasValue && cell.Candidates.Contains(digit)) candidates.Add(cell);
                    }
                    if (candidates.Count == 1) return new MissingSingleResult { Cell = candidates[0], Digit = digit, Unit = UnitKind.Row, UnitIndex = r };
                }

                /** columns */
                for (int c = 0; c < size; c++)
                {
                    var candidates = new List<Cell>();
                    foreach (Cell cell in board.GetColumn(c))
                    {
                        if (!cell.Value.HasValue && cell.Candidates.Contains(digit)) candidates.Add(cell);
                    }
                    if (candidates.Count == 1) return new MissingSingleResult { Cell = candidates[0], Digit = digit, Unit = UnitKind.Column, UnitIndex = c };
                }

                /** boxes */
                for (int b = 0; b < size; b++)
                {
                    var candidates = new List<Cell>();
                    foreach (Cell cell in board.GetBox(b))
                    {
                        if (!cell.Value.HasValue && cell.Candidates.Contains(digit)) candidates.Add(cell);
                    }
                    if (candidates.Count == 1) return new MissingSingleResult { Cell = candidates[0], Digit = digit, Unit = UnitKind.Box, UnitIndex = b };
                }
            }
            return null;
        }

        /**
         * Apply the Missing Single found by <see cref="FindAny"/>: set the digit
         * into the located cell and remove it from peer candidates.
         */
        public RuleResult CalculateChanges(Board board)
        {
            var result = new RuleResult();
            MissingSingleResult found = FindAny(board);
            if (found == null)
            {
                    result.Apply = false;
                return result;
            }
            Cell cell = found.Cell;
            int digit = found.Digit;
            UnitKind unit = found.Unit;
            int unitIndex = found.UnitIndex;
            // highlight all cells in the unit used for deduction
            IEnumerable<Cell> unitCells = unit == UnitKind.Row ? board.GetRow(unitIndex)
                                        : unit == UnitKind.Column ? board.GetColumn(unitIndex)
                                        : board.GetBox(unitIndex);
            foreach (var uc in unitCells)
            {
                if (!result.UsedCells.Exists(u => u.Row == uc.Row && u.Column == uc.Column && u.Candidate == digit))
                    result.UsedCells.Add(new UsedCell { Row = uc.Row, Column = uc.Column, Candidate = digit });
            }
            var change = new CellChange { Row = cell.Row, Column = cell.Column, OldValue = cell.Value, NewValue = digit };
            // mark peers with values as used
            foreach (Cell peer in board.GetPeers(cell))
            {
                if (peer.Value.HasValue && !result.UsedCells.Exists(u => u.Row == peer.Row && u.Column == peer.Column))
                    result.UsedCells.Add(new UsedCell { Row = peer.Row, Column = peer.Column });
            }

            // Record placement and peer candidate removals (do not modify board here)
            result.Changes.Add(change);
            foreach (Cell peer in board.GetPeers(cell))
            {
                if (peer.Candidates.Contains(digit))
                {
                    var peerChange = new CellChange { Row = peer.Row, Column = peer.Column };
                    peerChange.RemovedCandidates.Add(digit);
                    result.Changes.Add(peerChange);
                    if (!result.UsedCells.Exists(u => u.Row == peer.Row && u.Column == peer.Column && u.Candidate == digit))
                        result.UsedCells.Add(new UsedCell { Row = peer.Row, Column = peer.Column, Candidate = digit });
                }
            }
                result.Apply = true;
            result.Description = $"Placed {digit} at ({cell.Row},{cell.Column}) via Missing Single";
            
            return result;
        }
    }
}
