using System.Collections.Generic;
using System.Linq;
using Sudoku.Models;

namespace Sudoku.Solver.Rules
{
    internal enum ForcingChainUnitType
    {
        Row = 0,
        Column = 1,
        Box = 2
    }

    internal sealed class ForcingChainLinkModel
    {
        public int Size;
        public List<int> Candidates = new List<int>();
        public Dictionary<int, int> CandidateIndexByKey = new Dictionary<int, int>();
        public Dictionary<int, List<int>> WeakLinksByKey = new Dictionary<int, List<int>>();
        public Dictionary<int, List<int>> StrongLinksByKey = new Dictionary<int, List<int>>();
        public Dictionary<int, List<int>> CellGroupsByIndex = new Dictionary<int, List<int>>();
        public Dictionary<int, List<int>> UnitDigitGroupsByIndex = new Dictionary<int, List<int>>();

        public static ForcingChainLinkModel Build(Board board)
        {
            var model = new ForcingChainLinkModel { Size = board.Size };
            int size = board.Size;

            for (int row = 0; row < size; row++)
            {
                for (int column = 0; column < size; column++)
                {
                    var cell = board.Cells[row, column];
                    if (cell == null || cell.Value.HasValue || cell.Candidates == null || cell.Candidates.Count == 0)
                    {
                        continue;
                    }

                    foreach (int digit in cell.Candidates.OrderBy(value => value))
                    {
                        int key = MakeCandidateKey(board, row, column, digit);
                        model.CandidateIndexByKey[key] = model.Candidates.Count;
                        model.Candidates.Add(key);
                    }
                }
            }

            if (model.Candidates.Count == 0)
            {
                return model;
            }

            for (int row = 0; row < size; row++)
            {
                for (int column = 0; column < size; column++)
                {
                    var cell = board.Cells[row, column];
                    if (cell == null || cell.Value.HasValue || cell.Candidates == null)
                    {
                        continue;
                    }

                    if (cell.Candidates.Count == 2)
                    {
                        var pair = cell.Candidates.OrderBy(value => value).ToList();
                        AddLink(
                            model.StrongLinksByKey,
                            MakeCandidateKey(board, row, column, pair[0]),
                            MakeCandidateKey(board, row, column, pair[1]));
                    }

                    if (cell.Candidates.Count > 0)
                    {
                        int cellIndex = row * size + column;
                        model.CellGroupsByIndex[cellIndex] = cell.Candidates
                            .Select(digit => MakeCandidateKey(board, row, column, digit))
                            .ToList();
                    }
                }
            }

            for (int digit = 1; digit <= size; digit++)
            {
                for (int row = 0; row < size; row++)
                {
                    var group = new List<int>();
                    for (int column = 0; column < size; column++)
                    {
                        var cell = board.Cells[row, column];
                        if (cell != null && !cell.Value.HasValue && cell.Candidates != null && cell.Candidates.Contains(digit))
                        {
                            group.Add(MakeCandidateKey(board, row, column, digit));
                        }
                    }

                    AddUnitDigitGroup(model, board, group, ForcingChainUnitType.Row, row, digit);
                }

                for (int column = 0; column < size; column++)
                {
                    var group = new List<int>();
                    for (int row = 0; row < size; row++)
                    {
                        var cell = board.Cells[row, column];
                        if (cell != null && !cell.Value.HasValue && cell.Candidates != null && cell.Candidates.Contains(digit))
                        {
                            group.Add(MakeCandidateKey(board, row, column, digit));
                        }
                    }

                    AddUnitDigitGroup(model, board, group, ForcingChainUnitType.Column, column, digit);
                }

                for (int box = 0; box < size; box++)
                {
                    var group = new List<int>();
                    foreach (var cell in board.GetBox(box))
                    {
                        if (cell != null && !cell.Value.HasValue && cell.Candidates != null && cell.Candidates.Contains(digit))
                        {
                            group.Add(MakeCandidateKey(board, cell.Row, cell.Column, digit));
                        }
                    }

                    AddUnitDigitGroup(model, board, group, ForcingChainUnitType.Box, box, digit);
                }
            }

            foreach (var group in model.CellGroupsByIndex.Values)
            {
                AddGroupLinks(model.WeakLinksByKey, group);
            }

            foreach (var group in model.UnitDigitGroupsByIndex.Values)
            {
                AddGroupLinks(model.WeakLinksByKey, group);
            }

            return model;
        }

        public bool IsStrongLink(int fromCandidate, int toCandidate)
        {
            return StrongLinksByKey.TryGetValue(fromCandidate, out var linked)
                && linked != null
                && linked.Contains(toCandidate);
        }

        public static int MakeCandidateKey(Board board, int row, int column, int digit)
        {
            return ((row * board.Size) + column) * (board.Size + 1) + digit;
        }

        public static int GetRow(Board board, int candidate)
        {
            int packedCell = candidate / (board.Size + 1);
            return packedCell / board.Size;
        }

        public static int GetColumn(Board board, int candidate)
        {
            int packedCell = candidate / (board.Size + 1);
            return packedCell % board.Size;
        }

        public static int GetDigit(Board board, int candidate)
        {
            return candidate % (board.Size + 1);
        }

        public static int MakeUnitDigitGroupIndex(Board board, ForcingChainUnitType unitType, int unitIndex, int digit)
        {
            return ((((int)unitType) * board.Size) + unitIndex) * (board.Size + 1) + digit;
        }

        private static void AddUnitDigitGroup(
            ForcingChainLinkModel model,
            Board board,
            List<int> group,
            ForcingChainUnitType unitType,
            int unitIndex,
            int digit)
        {
            if (group.Count == 0)
            {
                return;
            }

            int groupIndex = MakeUnitDigitGroupIndex(board, unitType, unitIndex, digit);
            model.UnitDigitGroupsByIndex[groupIndex] = group;
            if (group.Count == 2)
            {
                AddLink(model.StrongLinksByKey, group[0], group[1]);
            }
        }

        private static void AddGroupLinks(Dictionary<int, List<int>> links, List<int> group)
        {
            if (group == null || group.Count < 2)
            {
                return;
            }

            var orderedCandidates = group.OrderBy(candidate => candidate).ToList();
            for (int i = 0; i < orderedCandidates.Count; i++)
            {
                for (int j = i + 1; j < orderedCandidates.Count; j++)
                {
                    AddLink(links, orderedCandidates[i], orderedCandidates[j]);
                }
            }
        }

        private static void AddLink(Dictionary<int, List<int>> links, int a, int b)
        {
            if (a == b)
            {
                return;
            }

            if (!links.TryGetValue(a, out var listA))
            {
                listA = new List<int>();
                links[a] = listA;
            }

            if (!listA.Contains(b))
            {
                listA.Add(b);
            }

            if (!links.TryGetValue(b, out var listB))
            {
                listB = new List<int>();
                links[b] = listB;
            }

            if (!listB.Contains(a))
            {
                listB.Add(a);
            }
        }
    }
}
