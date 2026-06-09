using System;
using System.Collections.Generic;
using Sudoku.Models;
using Sudoku.Solver.Rules;

namespace Sudoku.Solver.Unsolver
{
    /**
     * Unsolve handler for the Hidden Triple rule.
     *
     * Since Hidden Triple is a candidate-elimination technique, this handler only
     * removes a value when the resulting board state creates a direct Hidden Triple
     * deduction that trims the removed cell.
     */
    public class HiddenTripleUnsolveHandler : IUnsolveHandler
    {
        private readonly HiddenTripleRule _rule = new HiddenTripleRule();

        public string RuleName => nameof(HiddenTripleRule);

        public UnsolveResult TryUnsolve(Board board, Random random)
        {
            var candidates = BuildCandidateList(board);
            if (candidates.Count == 0)
            {
                return UnsolveResult.NoApplicableMove;
            }

            var chosen = candidates[random.Next(candidates.Count)];
            chosen.Value = null;
            chosen.IsGiven = false;
            RecomputeCandidates(board);
            return UnsolveResult.Success;
        }

        /**
         * Find removable cells that would make Hidden Triple applicable.
         */
        public List<Cell> BuildCandidateList(Board board)
        {
            var result = new List<Cell>();
            if (board == null || board.Cells == null)
            {
                return result;
            }

            for (int row = 0; row < board.Size; row++)
            {
                for (int column = 0; column < board.Size; column++)
                {
                    var cell = board.Cells[row, column];
                    if (cell == null || !cell.Value.HasValue)
                    {
                        continue;
                    }

                    var trial = PuzzleGenerator.CloneBoard(board);
                    var trialCell = trial.Cells[row, column];
                    int removedValue = trialCell.Value.Value;
                    trialCell.Value = null;
                    trialCell.IsGiven = false;
                    RecomputeCandidates(trial);

                    var changes = _rule.CalculateChanges(trial);
                    if (changes == null || !changes.Apply)
                    {
                        continue;
                    }

                    if (!HasCandidateRemovalAtCell(changes, row, column))
                    {
                        continue;
                    }

                    changes.EnactCandidates(trial);
                    var narrowedTarget = trial.Cells[row, column];
                    if (!narrowedTarget.Candidates.Contains(removedValue)
                        || narrowedTarget.Candidates.Count > 3)
                    {
                        continue;
                    }

                    result.Add(cell);
                }
            }

            return result;
        }

        private static bool HasCandidateRemovalAtCell(RuleResult changes, int row, int column)
        {
            if (changes == null || changes.Changes == null)
            {
                return false;
            }

            for (int i = 0; i < changes.Changes.Count; i++)
            {
                var change = changes.Changes[i];
                if (change.Row == row
                    && change.Column == column
                    && change.RemovedCandidates != null
                    && change.RemovedCandidates.Count > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static void RecomputeCandidates(Board board)
        {
            if (board == null || board.Cells == null)
            {
                return;
            }

            for (int row = 0; row < board.Size; row++)
            {
                for (int column = 0; column < board.Size; column++)
                {
                    var cell = board.Cells[row, column];
                    cell.Candidates.Clear();
                    if (cell.Value.HasValue)
                    {
                        continue;
                    }

                    for (int value = 1; value <= board.Size; value++)
                    {
                        cell.Candidates.Add(value);
                    }
                }
            }

            for (int row = 0; row < board.Size; row++)
            {
                for (int column = 0; column < board.Size; column++)
                {
                    var cell = board.Cells[row, column];
                    if (cell.Value.HasValue)
                    {
                        continue;
                    }

                    foreach (var peer in board.GetPeers(cell))
                    {
                        if (peer.Value.HasValue)
                        {
                            cell.Candidates.Remove(peer.Value.Value);
                        }
                    }
                }
            }
        }
    }
}