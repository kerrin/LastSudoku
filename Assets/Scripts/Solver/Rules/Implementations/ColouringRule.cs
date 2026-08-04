using System.Collections.Generic;
using System.Linq;
using Sudoku.Models;
using Sudoku.UI.Config;
using Board = Sudoku.Models.Board;

namespace Sudoku.Solver.Rules
{
    /**
     * Colouring (also known as Single Chains) is a technique used to eliminate candidates by coloring cells in a way that reveals contradictions.
     * Importantly, we can only make links between cells in the deduction if their box has 1 or 2 candidates of that digit.
     * 
     * Start by picking a candidate in a cell and coloring it in one colour.
     * Then look at all cells that sees (column, row, box) that candidate, for each row, column, and box where the digit only appear 1 or 2 times,
     * we can chain to those candidates, setting them all to the oposite colour.
     * If the row, column or box has more than 2 candidates of that digit, we cannot chain to those candidates and they are not bi-location links.
     * 
     * When checking bi-location candidates in the chain, if the candidate is already the same colour as another candidate in the same unit (row, column, or box), the same-colour contradiction exists, so that entire colour is false.
     * 
     * The strategy is all about recognising that one of the colours will be the solution and the other not.
     * 
     * We follow the chain of bi-location candidates, and if we find a candidate that sees it's own colour, we can eliminate all candidates of that colour.
     *
     * This rule should only be applied if colouring is enabled and has at least two colours enabled.
     * Example board: PexgJNCqJyeY81q73gz6Ptf1PxzMPJNMpvWpWFq0jwDy3r
     * Starting in the top left box, the 5s in cell (0,1) or (1,0) can be the start of the chain.
     * See https://www.sudokuwiki.org/Print_Simple_Colouring
     */
    public class ColouringRule : ISudokuRule
    {
        private const string TargetATag = "TargetA";
        private const string TargetBTag = "TargetB";
        private const string DeductionTag = "Deduction";
        private const string FailureTag = "Failure";

        private sealed class ChainNode
        {
            public int Row;
            public int Column;
            public int Box;
        }

        private sealed class EliminationPlan
        {
            public int Digit;
            public List<ChainNode> ComponentNodes;
            public Dictionary<int, int> NodeColours;
            public List<ChainNode> ContradictionNodes;
            public List<ChainNode> RemovalNodes;
            public string Description;

            public EliminationPlan(
                int digit,
                List<ChainNode> componentNodes,
                Dictionary<int, int> nodeColours,
                List<ChainNode> contradictionNodes,
                List<ChainNode> removalNodes,
                string description)
            {
                Digit = digit;
                ComponentNodes = componentNodes;
                NodeColours = nodeColours;
                ContradictionNodes = contradictionNodes;
                RemovalNodes = removalNodes;
                Description = description;
            }
        }

        public string Name => "Colouring";

        public Difficulty Difficulty => Difficulty.Expert;

        /**
         * Determine whether the Colouring rule can be applied to the current board.
         *
         * @param board Current puzzle board.
         * @returns True when the board is valid, colour prerequisites are met, and a colouring elimination exists.
         */
        public bool CanApply(Board board)
        {
            if (board == null) return false;
            if (!board.IsValid()) return false;
            if (ColourSettings.GetEnabledColourCount() < 2) return false;

            var plan = FindEliminationPlan(board);
            return plan != null;
        }

        /**
         * Calculate candidate removals produced by Single Chains colouring.
         *
         * @param board Current puzzle board.
         * @returns RuleResult containing candidate removals and the evidence chain.
         */
        public RuleResult CalculateChanges(Board board)
        {
            return RuleCalculationCache.GetOrCalculate(this, board, () => CalculateChangesInternal(board));
        }

        private RuleResult CalculateChangesInternal(Board board)
        {
            var result = new RuleResult();

            if (board == null || !board.IsValid() || ColourSettings.GetEnabledColourCount() < 2)
            {
                result.Apply = false;
                return result;
            }

            var plan = FindEliminationPlan(board);
            if (plan == null)
            {
                result.Apply = false;
                return result;
            }

            for (int i = 0; i < plan.ComponentNodes.Count; i++)
            {
                var node = plan.ComponentNodes[i];
                int nodeKey = ToNodeKey(board, node.Row, node.Column);
                int colour = 0;
                if (plan.NodeColours != null && plan.NodeColours.TryGetValue(nodeKey, out var storedColour))
                {
                    colour = storedColour;
                }

                AddUsedCell(result.UsedCells, node.Row, node.Column, plan.Digit, colour == 0 ? TargetATag : TargetBTag);
            }

            for (int i = 0; i < plan.ContradictionNodes.Count; i++)
            {
                var node = plan.ContradictionNodes[i];
                AddUsedCell(result.UsedCells, node.Row, node.Column, plan.Digit, FailureTag);
            }

            for (int i = 0; i < plan.RemovalNodes.Count; i++)
            {
                var node = plan.RemovalNodes[i];
                if (board.Cells[node.Row, node.Column].Value.HasValue)
                {
                    continue;
                }

                if (!board.Cells[node.Row, node.Column].Candidates.Contains(plan.Digit))
                {
                    continue;
                }

                var change = new CellChange { Row = node.Row, Column = node.Column };
                change.RemovedCandidates.Add(plan.Digit);
                result.Changes.Add(change);

                AddUsedCell(result.UsedCells, node.Row, node.Column, plan.Digit, DeductionTag);
            }

            result.Description = plan.Description;
            result.Apply = result.Changes.Count > 0;
            return result;
        }

        /**
         * Find the first deterministic colouring elimination plan.
         *
         * @param board Current puzzle board.
         * @returns Elimination plan, or null when none exists.
         */
        private static EliminationPlan FindEliminationPlan(Board board)
        {
            EliminationPlan bestPlan = null;

            for (int digit = 1; digit <= board.Size; digit++)
            {
                var graph = BuildStrongLinkGraph(board, digit);
                if (graph.Count == 0)
                {
                    continue;
                }

                var sortedKeys = graph.Keys.OrderBy(k => k).ToList();
                var visited = new HashSet<int>();

                for (int keyIndex = 0; keyIndex < sortedKeys.Count; keyIndex++)
                {
                    int startKey = sortedKeys[keyIndex];
                    if (visited.Contains(startKey))
                    {
                        continue;
                    }

                    var componentKeys = new List<int>();
                    var colours = new Dictionary<int, int>();
                    var queue = new Queue<int>();
                    queue.Enqueue(startKey);
                    colours[startKey] = 0;
                    visited.Add(startKey);

                    while (queue.Count > 0)
                    {
                        int current = queue.Dequeue();
                        componentKeys.Add(current);

                        var neighbors = graph[current];
                        for (int i = 0; i < neighbors.Count; i++)
                        {
                            int next = neighbors[i];
                            if (!colours.ContainsKey(next))
                            {
                                colours[next] = 1 - colours[current];
                            }

                            if (!visited.Contains(next))
                            {
                                visited.Add(next);
                                queue.Enqueue(next);
                            }
                        }
                    }

                    if (componentKeys.Count < 2)
                    {
                        continue;
                    }

                    var componentGraph = BuildComponentGraph(graph, componentKeys);
                    componentKeys.Sort();
                    var componentColours = BuildAlternatingColoursFromComponent(componentKeys, componentGraph);
                    if (componentColours.Count == 0)
                    {
                        continue;
                    }

                    if (!IsValidChainComponent(componentGraph, componentKeys, componentColours))
                    {
                        continue;
                    }

                    var componentNodes = new List<ChainNode>(componentKeys.Count);
                    for (int i = 0; i < componentKeys.Count; i++)
                    {
                        componentNodes.Add(DecodeNode(board, componentKeys[i]));
                    }

                    var contradictionPlan = BuildContradictionElimination(board, digit, componentKeys, componentColours, componentNodes, componentColours);
                    if (IsBetterPlan(contradictionPlan, bestPlan))
                    {
                        bestPlan = contradictionPlan;
                    }

                    var twoColourPlan = BuildTwoColourElimination(board, digit, componentKeys, componentColours, componentNodes);
                    if (IsBetterPlan(twoColourPlan, bestPlan))
                    {
                        bestPlan = twoColourPlan;
                    }

                }
            }

            return bestPlan;
        }

        /**
         * Compare two elimination plans and decide whether candidate should replace current.
         *
         * @param candidate Candidate plan to evaluate.
         * @param current Current best plan.
         * @returns True when candidate is a better plan.
         */
        private static bool IsBetterPlan(EliminationPlan candidate, EliminationPlan current)
        {
            if (candidate == null)
            {
                return false;
            }

            if (current == null)
            {
                return true;
            }

            bool candidateHasContradiction = candidate.ContradictionNodes != null && candidate.ContradictionNodes.Count > 0;
            bool currentHasContradiction = current.ContradictionNodes != null && current.ContradictionNodes.Count > 0;
            if (candidateHasContradiction != currentHasContradiction)
            {
                return candidateHasContradiction;
            }

            int candidateRemovalCount = candidate.RemovalNodes != null ? candidate.RemovalNodes.Count : 0;
            int currentRemovalCount = current.RemovalNodes != null ? current.RemovalNodes.Count : 0;
            if (candidateRemovalCount != currentRemovalCount)
            {
                return candidateRemovalCount > currentRemovalCount;
            }

            int candidateComponentSize = candidate.ComponentNodes != null ? candidate.ComponentNodes.Count : 0;
            int currentComponentSize = current.ComponentNodes != null ? current.ComponentNodes.Count : 0;
            if (candidateComponentSize != currentComponentSize)
            {
                return candidateComponentSize > currentComponentSize;
            }

            if (candidate.Digit != current.Digit)
            {
                return candidate.Digit < current.Digit;
            }

            return false;
        }

        /**
         * Build adjacency map limited to nodes within one connected component.
         *
         * @param graph Full strong-link graph.
         * @param componentKeys Node keys in the connected component.
         * @returns Component-scoped adjacency map.
         */
        private static Dictionary<int, List<int>> BuildComponentGraph(Dictionary<int, List<int>> graph, List<int> componentKeys)
        {
            var componentSet = new HashSet<int>(componentKeys);
            var result = new Dictionary<int, List<int>>();

            for (int i = 0; i < componentKeys.Count; i++)
            {
                int key = componentKeys[i];
                var neighbors = new List<int>();
                if (graph.TryGetValue(key, out var raw))
                {
                    for (int n = 0; n < raw.Count; n++)
                    {
                        int next = raw[n];
                        if (componentSet.Contains(next))
                        {
                            neighbors.Add(next);
                        }
                    }
                }

                neighbors.Sort();
                result[key] = neighbors;
            }

            return result;
        }

        /**
         * Validate whether a component can be represented by a consistent two-colour assignment.
         *
         * @param componentGraph Component adjacency map.
         * @param componentKeys Node keys inside this component.
         * @returns True when every component node has a valid two-colour assignment.
         */
        private static bool IsValidChainComponent(
            Dictionary<int, List<int>> componentGraph,
            List<int> componentKeys,
            Dictionary<int, int> componentColours)
        {
            for (int i = 0; i < componentKeys.Count; i++)
            {
                int key = componentKeys[i];
                if (!componentGraph.TryGetValue(key, out var neighbors))
                {
                    return false;
                }

                if (!componentColours.ContainsKey(key))
                {
                    return false;
                }
            }

            return true;
        }

        /**
         * Build alternating colours for a valid non-branching component.
         *
         * @param componentKeys Node keys in the component.
         * @param componentGraph Component adjacency map.
         * @returns Node-to-colour map (0 or 1).
         */
        private static Dictionary<int, int> BuildAlternatingColoursFromComponent(List<int> componentKeys, Dictionary<int, List<int>> componentGraph)
        {
            var colours = new Dictionary<int, int>();

            // Prefer an endpoint when present so chain orientation is deterministic.
            int start = componentKeys
                .Where(k => componentGraph.TryGetValue(k, out var neighbors) && neighbors.Count == 1)
                .OrderBy(k => k)
                .FirstOrDefault();

            if (start == 0 && !componentKeys.Contains(0))
            {
                start = componentKeys.OrderBy(k => k).First();
            }

            var queue = new Queue<int>();
            colours[start] = 0;
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                if (!componentGraph.TryGetValue(current, out var neighbors))
                {
                    continue;
                }

                for (int i = 0; i < neighbors.Count; i++)
                {
                    int next = neighbors[i];
                    if (!colours.ContainsKey(next))
                    {
                        colours[next] = 1 - colours[current];
                        queue.Enqueue(next);
                        continue;
                    }

                    // If a strong-link edge connects same-colour nodes, this component cannot be
                    // represented as a valid two-colour chain under the rule prerequisites.
                    if (colours[next] == colours[current])
                    {
                        return new Dictionary<int, int>();
                    }
                }
            }

            return colours;
        }

        /**
         * Build a graph of strong links for one candidate digit.
         *
         * @param board Current puzzle board.
         * @param digit Candidate digit.
         * @returns Adjacency map of node key to strongly-linked node keys.
         */
        private static Dictionary<int, List<int>> BuildStrongLinkGraph(Board board, int digit)
        {
            var graph = new Dictionary<int, List<int>>();

            for (int row = 0; row < board.Size; row++)
            {
                var candidates = new List<int>();
                for (int column = 0; column < board.Size; column++)
                {
                    var cell = board.Cells[row, column];
                    if (!cell.Value.HasValue &&
                        cell.Candidates.Contains(digit))
                    {
                        candidates.Add(ToNodeKey(board, row, column));
                    }
                }

                AddStrongLink(graph, candidates);
            }

            for (int column = 0; column < board.Size; column++)
            {
                var candidates = new List<int>();
                for (int row = 0; row < board.Size; row++)
                {
                    var cell = board.Cells[row, column];
                    if (!cell.Value.HasValue &&
                        cell.Candidates.Contains(digit))
                    {
                        candidates.Add(ToNodeKey(board, row, column));
                    }
                }

                AddStrongLink(graph, candidates);
            }

            for (int box = 0; box < board.Size; box++)
            {
                var candidates = new List<int>();
                foreach (var cell in board.GetBox(box))
                {
                    if (!cell.Value.HasValue &&
                        cell.Candidates.Contains(digit))
                    {
                        candidates.Add(ToNodeKey(board, cell.Row, cell.Column));
                    }
                }

                AddStrongLink(graph, candidates);
            }

            return graph;
        }

        /**
         * Add an undirected strong link when a unit has exactly two candidate locations.
         *
         * @param graph Adjacency map to modify.
         * @param candidates Candidate node keys found in one unit.
         */
        private static void AddStrongLink(Dictionary<int, List<int>> graph, List<int> candidates)
        {
            if (candidates == null || candidates.Count != 2)
            {
                return;
            }

            int a = candidates[0];
            int b = candidates[1];

            EnsureNode(graph, a);
            EnsureNode(graph, b);

            if (!graph[a].Contains(b)) graph[a].Add(b);
            if (!graph[b].Contains(a)) graph[b].Add(a);
        }

        /**
         * Build elimination from a same-colour contradiction found inside one unit.
         *
         * @param board Current puzzle board.
         * @param digit Candidate digit.
         * @param componentKeys Node keys in this connected component.
         * @param colours Two-colour assignment for each key.
         * @param componentNodes Decoded component nodes.
         * @returns Elimination plan or null when no contradiction elimination exists.
         */
        private static EliminationPlan BuildContradictionElimination(
            Board board,
            int digit,
            List<int> componentKeys,
            Dictionary<int, int> colours,
            List<ChainNode> componentNodes,
            Dictionary<int, int> componentColours)
        {
            for (int colour = 0; colour <= 1; colour++)
            {
                for (int row = 0; row < board.Size; row++)
                {
                    var sameColour = GetNodesInRow(board, componentKeys, colours, colour, row);
                    if (sameColour.Count >= 2)
                    {
                        return BuildContradictionResult(board, digit, componentKeys, colours, componentNodes, componentColours, colour, sameColour, "row", row + 1);
                    }
                }

                for (int column = 0; column < board.Size; column++)
                {
                    var sameColour = GetNodesInColumn(board, componentKeys, colours, colour, column);
                    if (sameColour.Count >= 2)
                    {
                        return BuildContradictionResult(board, digit, componentKeys, colours, componentNodes, componentColours, colour, sameColour, "column", column + 1);
                    }
                }

                for (int box = 0; box < board.Size; box++)
                {
                    var sameColour = GetNodesInBox(board, componentKeys, colours, colour, box);
                    if (sameColour.Count >= 2)
                    {
                        return BuildContradictionResult(board, digit, componentKeys, colours, componentNodes, componentColours, colour, sameColour, "box", box + 1);
                    }
                }
            }

            return null;
        }

        /**
         * Build a contradiction-elimination plan for one colour assignment.
         *
         * @param board Current puzzle board.
         * @param digit Candidate digit.
         * @param componentKeys Node keys in this connected component.
         * @param colours Two-colour assignment for each key.
         * @param componentNodes Decoded component nodes.
         * @param contradictionColour Colour index (0 or 1) proven false.
         * @param contradictionNodes Nodes that expose the contradiction.
         * @param unitName Human-readable unit type.
         * @param unitIndex One-based unit index for description.
         * @returns Elimination plan.
         */
        private static EliminationPlan BuildContradictionResult(
            Board board,
            int digit,
            List<int> componentKeys,
            Dictionary<int, int> colours,
            List<ChainNode> componentNodes,
            Dictionary<int, int> componentColours,
            int contradictionColour,
            List<ChainNode> contradictionNodes,
            string unitName,
            int unitIndex)
        {
            var removals = new List<ChainNode>();
            for (int i = 0; i < componentKeys.Count; i++)
            {
                int key = componentKeys[i];
                if (colours[key] != contradictionColour)
                {
                    continue;
                }

                var node = DecodeNode(board, key);
                var cell = board.Cells[node.Row, node.Column];
                if (!cell.Value.HasValue && cell.Candidates.Contains(digit))
                {
                    removals.Add(node);
                }
            }

            if (removals.Count == 0)
            {
                return null;
            }

            string colourName = contradictionColour == 0 ? "A" : "B";
            string description = $"Colouring removed {digit}: colour {colourName} contradicts itself in {unitName} {unitIndex}";
            return new EliminationPlan(digit, componentNodes, componentColours, contradictionNodes.Take(2).ToList(), removals, description);
        }

        /**
         * Build elimination from uncoloured cells that see both colours in one component.
         *
         * @param board Current puzzle board.
         * @param digit Candidate digit.
         * @param componentKeys Node keys in this connected component.
         * @param colours Two-colour assignment for each key.
         * @param componentNodes Decoded component nodes.
         * @returns Elimination plan or null when no two-colour elimination exists.
         */
        private static EliminationPlan BuildTwoColourElimination(
            Board board,
            int digit,
            List<int> componentKeys,
            Dictionary<int, int> colours,
            List<ChainNode> componentNodes)
        {
            var componentSet = new HashSet<int>(componentKeys);
            var removals = new List<ChainNode>();

            for (int row = 0; row < board.Size; row++)
            {
                for (int column = 0; column < board.Size; column++)
                {
                    var cell = board.Cells[row, column];
                    if (cell.Value.HasValue || !cell.Candidates.Contains(digit))
                    {
                        continue;
                    }

                    int cellKey = ToNodeKey(board, row, column);
                    if (componentSet.Contains(cellKey))
                    {
                        continue;
                    }

                    bool seesColourA = false;
                    bool seesColourB = false;
                    for (int i = 0; i < componentKeys.Count; i++)
                    {
                        int key = componentKeys[i];
                        var node = DecodeNode(board, key);
                        if (node.Row != row && node.Column != column && node.Box != cell.Box)
                        {
                            continue;
                        }

                        if (colours[key] == 0)
                        {
                            seesColourA = true;
                        }
                        else
                        {
                            seesColourB = true;
                        }

                        if (seesColourA && seesColourB)
                        {
                            removals.Add(new ChainNode { Row = row, Column = column, Box = cell.Box });
                            break;
                        }
                    }
                }
            }

            if (removals.Count == 0)
            {
                return null;
            }

            removals = removals
                .OrderBy(n => n.Row)
                .ThenBy(n => n.Column)
                .ToList();

            string description = $"Colouring removed {digit}: candidate sees both colours in the chain";
            return new EliminationPlan(digit, componentNodes, colours, new List<ChainNode>(), removals, description);
        }

        /**
         * Collect component nodes of a specific colour inside one row.
         *
         * @param board Current puzzle board.
         * @param componentKeys Node keys in this component.
         * @param colours Two-colour assignment for each key.
         * @param colour Desired colour index.
         * @param row Desired row.
         * @returns Matching nodes ordered by column.
         */
        private static List<ChainNode> GetNodesInRow(Board board, List<int> componentKeys, Dictionary<int, int> colours, int colour, int row)
        {
            return componentKeys
                .Where(k => colours[k] == colour)
                .Select(k => DecodeNode(board, k))
                .Where(n => n.Row == row)
                .OrderBy(n => n.Column)
                .ToList();
        }

        /**
         * Collect component nodes of a specific colour inside one column.
         *
         * @param board Current puzzle board.
         * @param componentKeys Node keys in this component.
         * @param colours Two-colour assignment for each key.
         * @param colour Desired colour index.
         * @param column Desired column.
         * @returns Matching nodes ordered by row.
         */
        private static List<ChainNode> GetNodesInColumn(Board board, List<int> componentKeys, Dictionary<int, int> colours, int colour, int column)
        {
            return componentKeys
                .Where(k => colours[k] == colour)
                .Select(k => DecodeNode(board, k))
                .Where(n => n.Column == column)
                .OrderBy(n => n.Row)
                .ToList();
        }

        /**
         * Collect component nodes of a specific colour inside one box.
         *
         * @param board Current puzzle board.
         * @param componentKeys Node keys in this component.
         * @param colours Two-colour assignment for each key.
         * @param colour Desired colour index.
         * @param box Desired box index.
         * @returns Matching nodes ordered by row then column.
         */
        private static List<ChainNode> GetNodesInBox(Board board, List<int> componentKeys, Dictionary<int, int> colours, int colour, int box)
        {
            return componentKeys
                .Where(k => colours[k] == colour)
                .Select(k => DecodeNode(board, k))
                .Where(n => n.Box == box)
                .OrderBy(n => n.Row)
                .ThenBy(n => n.Column)
                .ToList();
        }

        /**
         * Add one evidence entry while preventing exact duplicates.
         *
         * @param usedCells Collection to update.
         * @param row Cell row.
         * @param column Cell column.
         * @param candidate Candidate digit.
         * @param tag Highlight semantic tag.
         */
        private static void AddUsedCell(List<UsedCell> usedCells, int row, int column, int candidate, string tag)
        {
            bool exists = usedCells.Exists(u =>
                u.Row == row &&
                u.Column == column &&
                u.Candidate == candidate &&
                u.HighlightTag == tag);
            if (exists)
            {
                return;
            }

            usedCells.Add(new UsedCell
            {
                Row = row,
                Column = column,
                Candidate = candidate,
                HighlightTag = tag
            });
        }

        /**
         * Ensure a node exists in the adjacency map.
         *
         * @param graph Graph map.
         * @param key Node key.
         */
        private static void EnsureNode(Dictionary<int, List<int>> graph, int key)
        {
            if (!graph.ContainsKey(key))
            {
                graph[key] = new List<int>();
            }
        }

        /**
         * Convert row/column coordinates to a deterministic integer node key.
         *
         * @param board Current puzzle board.
         * @param row Row index.
         * @param column Column index.
         * @returns Encoded key.
         */
        private static int ToNodeKey(Board board, int row, int column)
        {
            return row * board.Size + column;
        }

        /**
         * Decode a node key back to row/column/box coordinates.
         *
         * @param board Current puzzle board.
         * @param key Encoded node key.
         * @returns Decoded chain node.
         */
        private static ChainNode DecodeNode(Board board, int key)
        {
            int row = key / board.Size;
            int column = key % board.Size;
            return new ChainNode
            {
                Row = row,
                Column = column,
                Box = board.Cells[row, column].Box
            };
        }
    }

}

