using System;
using System.Collections.Generic;
using System.Linq;

namespace Sudoku.UI.Learning
{
    public enum LearningAction
    {
        SetValue,
        AddCandidate,
        RemoveCandidate
    }

    public sealed class LearningBoardCell
    {
        public int Row { get; }
        public int Column { get; }
        public int? Value { get; }
        public IReadOnlyList<int> Candidates { get; }

        public LearningBoardCell(int row, int column, int? value = null, params int[] candidates)
        {
            Row = row;
            Column = column;
            Value = value;
            Candidates = candidates ?? Array.Empty<int>();
        }
    }

    public sealed class LearningExercise
    {
        public string Prompt { get; }
        public IReadOnlyList<LearningBoardCell> Cells { get; }
        public int TargetRow { get; }
        public int TargetColumn { get; }
        public int TargetDigit { get; }
        public LearningAction ExpectedAction { get; }
        public string SuccessMessage { get; }

        public LearningExercise(
            string prompt,
            IEnumerable<LearningBoardCell> cells,
            int targetRow,
            int targetColumn,
            int targetDigit,
            LearningAction expectedAction,
            string successMessage)
        {
            Prompt = prompt ?? string.Empty;
            Cells = (cells ?? Enumerable.Empty<LearningBoardCell>()).ToArray();
            TargetRow = targetRow;
            TargetColumn = targetColumn;
            TargetDigit = targetDigit;
            ExpectedAction = expectedAction;
            SuccessMessage = successMessage ?? string.Empty;

            if (targetRow < 0 || targetRow > 8 || targetColumn < 0 || targetColumn > 8)
                throw new ArgumentOutOfRangeException(nameof(targetRow), "The exercise target must be inside a standard 9 by 9 Sudoku board.");
            if (targetDigit < 1 || targetDigit > 9)
                throw new ArgumentOutOfRangeException(nameof(targetDigit), "The exercise digit must be between 1 and 9.");
            if (Cells.Any(cell => cell == null || cell.Row < 0 || cell.Row > 8 || cell.Column < 0 || cell.Column > 8))
                throw new ArgumentException("Every example cell must be inside a standard 9 by 9 Sudoku board.", nameof(cells));
        }

        public bool IsCorrectAction(int row, int column, int digit, LearningAction action)
        {
            return row == TargetRow && column == TargetColumn && digit == TargetDigit && action == ExpectedAction;
        }

        public LearningBoardCell FindCell(int row, int column)
        {
            return Cells.FirstOrDefault(cell => cell.Row == row && cell.Column == column);
        }
    }

    public sealed class WikiLink
    {
        public string Label { get; }
        public string ArticleId { get; }

        public WikiLink(string label, string articleId)
        {
            Label = label ?? throw new ArgumentNullException(nameof(label));
            ArticleId = articleId ?? throw new ArgumentNullException(nameof(articleId));
        }
    }

    public sealed class LearningItem
    {
        public string Id { get; }
        public string Title { get; }
        public string Category { get; }
        public string Subsection { get; }
        public string Explanation { get; }
        public IReadOnlyList<WikiLink> Links { get; }
        public LearningExercise Exercise { get; }
        public bool IsTutorial { get; }

        public LearningItem(
            string id,
            string title,
            string category,
            string explanation,
            LearningExercise exercise,
            bool isTutorial,
            string subsection = "",
            IEnumerable<WikiLink> links = null)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Title = title ?? throw new ArgumentNullException(nameof(title));
            Category = category ?? string.Empty;
            Subsection = subsection ?? string.Empty;
            Explanation = explanation ?? string.Empty;
            Exercise = exercise;
            IsTutorial = isTutorial;
            Links = (links ?? Enumerable.Empty<WikiLink>()).ToArray();
        }
    }

    public static class LearningContent
    {
        private static LearningBoardCell Value(int row, int column, int value)
        {
            return new LearningBoardCell(row, column, value);
        }

        private static LearningBoardCell Candidates(int row, int column, params int[] candidates)
        {
            return new LearningBoardCell(row, column, null, candidates);
        }

        private static LearningExercise Exercise(
            string prompt,
            int targetRow,
            int targetColumn,
            int targetDigit,
            LearningAction action,
            string success,
            params LearningBoardCell[] cells)
        {
            return new LearningExercise(prompt, cells, targetRow, targetColumn, targetDigit, action, success);
        }

        private static WikiLink Link(string label, string articleId)
        {
            return new WikiLink(label, articleId);
        }

        private static readonly IReadOnlyList<LearningItem> Tutorials = new[]
        {
            new LearningItem(
                "sudoku-basics",
                "Sudoku basics",
                "Start here",
                "A standard Sudoku is a 9 by 9 grid divided into nine 3 by 3 boxes. Every row, every column, and every box must contain each digit from 1 to 9 exactly once. A blank cell can only contain a digit that does not already appear in its row, column, or box. The puzzle is solved when every cell is filled without breaking any of these restrictions.",
                Exercise(
                    "The highlighted cell is empty. Choose a digit that follows the row, column, and box restrictions, then set its value.",
                    0, 8, 9, LearningAction.SetValue,
                    "Correct. The chosen value follows the Sudoku restrictions for this cell.",
                    Value(0, 0, 1), Value(0, 1, 2), Value(0, 2, 3), Value(0, 3, 4),
                    Value(0, 4, 5), Value(0, 5, 6), Value(0, 6, 7), Value(0, 7, 8)),
                true),
            new LearningItem(
                "app-controls",
                "Using the app",
                "App guide",
                "In solve mode, press and hold a cell to open its radial menu. Keep holding while you move to an action, then release to choose it. Choosing a digit sets the cell value; + adds a candidate and - removes one. To make pencil mark 5, hold the cell, move to 5 and +, then release.\n\nThe Toggle Rules and Apply Rules panels live in the board's help area. Open Config and use the Assistance setting \"Hide Apply Rules\" to hide or show solver help; in solve mode this setting also hides Toggle Rules. Toggle Rules remains available while creating a puzzle. View Changes opens the change history; use Undo and Redo to move backwards and forwards through recorded changes.",
                Exercise(
                    "Press and hold the highlighted cell until its radial menu opens. Keep holding while you move to 5 and +, then release to add the candidate.",
                    2, 4, 5, LearningAction.AddCandidate,
                    "That's it. The + action adds the candidate without setting a cell value.",
                    Candidates(2, 4, 2, 8)),
                true),
            new LearningItem(
                "candidate-basics",
                "Candidates",
                "Start here",
                "Candidates (also called pencil marks) are the digits that could still fit in an empty cell. Remove a candidate when a placed value rules it out. A candidate list must never include a digit already present in the cell's row, column, or box. Candidate-based techniques rely on the candidate marks being accurate.",
                Exercise(
                    "The highlighted cell is in the same column as a placed 6. Press and hold it until the radial menu opens, keep holding while you move to 6 and -, then release.",
                    2, 3, 6, LearningAction.RemoveCandidate,
                    "Correct. Cells in the same column cannot both contain 6.",
                    Value(0, 3, 6), Candidates(2, 3, 1, 3, 6)),
                true),
            new LearningItem(
                "naked-single",
                "Naked Single",
                "Easy rules",
                "If an empty cell has only one candidate left, that digit must go in the cell. The digit is forced by the cell itself: all other digits are ruled out by values already placed in its row, column, or box.",
                Exercise(
                    "The highlighted cell has only candidate 4. Press and hold it until the radial menu opens, keep holding while you move to 4, then release to set the value.",
                    1, 3, 4, LearningAction.SetValue,
                    "Correct. Since 4 is the only candidate in R2C4, place 4 there.",
                    Candidates(1, 3, 4)),
                true),
            new LearningItem(
                "hidden-single",
                "Hidden Single",
                "Easy rules",
                "A digit can be forced by a unit even when its cell has other candidates. If a digit has only one possible location in a row, column, or box, it must go in that location. The digit may be hidden among the cell's other candidates.",
                Exercise(
                    "In row 4, 7 appears only in the highlighted cell. That cell also has candidate 2. Press and hold it until the radial menu opens, keep holding while you move to 7, then release to place it.",
                    3, 4, 7, LearningAction.SetValue,
                    "Correct. Row 4 has only one place for 7, even though its cell has another candidate.",
                    Candidates(3, 0, 1, 3), Candidates(3, 1, 3, 6), Candidates(3, 2, 1, 6),
                    Candidates(3, 3, 2, 4), Candidates(3, 4, 2, 7), Candidates(3, 5, 3, 4),
                    Candidates(3, 6, 5, 8), Candidates(3, 7, 1, 8), Candidates(3, 8, 4, 9)),
                true),
            new LearningItem(
                "naked-pair",
                "Naked Pair",
                "Medium rules",
                "If two cells in the same row, column, or box contain exactly the same two candidates, those two digits must occupy those two cells in some order. They can therefore be removed from every other empty cell in that unit.",
                Exercise(
                    "The first two cells in row 5 form a {2, 7} pair. Press and hold the highlighted cell until the radial menu opens, keep holding while you move to 2 and -, then release.",
                    4, 7, 2, LearningAction.RemoveCandidate,
                    "Correct. The pair reserves 2 and 7 for the first two cells, so 2 can be removed from this cell.",
                    Candidates(4, 0, 2, 7), Candidates(4, 1, 2, 7), Candidates(4, 7, 2, 5, 7)),
                true),
            new LearningItem(
                "pointing-pair",
                "Pointing Pair",
                "Medium rules",
                "When all candidates for a digit in a box lie in one row or column, that digit must occupy one of those cells inside the box. It can be removed from the rest of that row or column outside the box. This is also known as a pointing pair when there are two positions.",
                Exercise(
                    "In the top-left box, the only positions for 6 are in row 1. Press and hold the highlighted cell until the radial menu opens, keep holding while you move to 6 and -, then release.",
                    0, 4, 6, LearningAction.RemoveCandidate,
                    "Correct. The 6 in the box must be in row 1, so no other cell in row 1 can contain 6.",
                    Candidates(0, 0, 1, 6), Candidates(0, 2, 3, 6), Candidates(0, 4, 1, 6)),
                true)
        };

        private static readonly IReadOnlyList<LearningItem> WikiArticles = new[]
        {
            Article("sudoku-basics", "Sudoku rules", "Basics", "Overview",
                "A standard Sudoku has 9 rows, 9 columns, and nine 3 by 3 boxes. Each row, column, and box contains every digit 1 through 9 exactly once. A value in a cell must satisfy all three units. Candidates are the possible values that remain for an empty cell.",
                Link("Candidates", "candidates")),
            Article("candidates", "Candidates and pencil marks", "Basics", "Notation",
                "Candidates are the possible values for an unsolved cell after considering the Sudoku rules for its row, column, and box. Naked Single and Hidden Single are basic techniques that use candidates in different ways.",
                Link("Sudoku rules", "sudoku-basics"), Link("Naked Single", "naked-single"), Link("Hidden Single", "hidden-single")),
            Article("app-controls", "Using the app", "App Guide", "Board controls",
                "Press and hold a cell to open its radial menu. Choose a digit to set its value; + adds a candidate and - removes one. Candidates are possible values for an empty cell. View Changes opens the change history. Undo and Redo move through the recorded history. Config > Assistance > Hide Apply Rules controls solver-help visibility (and also hides Toggle Rules in solve mode).",
                Link("Change history", "change-history"), Link("Candidates", "candidates")),
            Article("change-history", "Change history", "App Guide", "Board controls",
                "Using the app, open View Changes to see recorded groups of edits. Use Undo to step back and Redo to step forward. Selecting a history entry lets you inspect the associated changes and restore the board to that point.",
                Link("Using the app", "app-controls")),
            Article("naked-single", "Naked Single", "Rules", "Simple",
                "A cell with exactly one candidate must contain that digit. This is the simplest candidate-based placement. Candidates also help reveal a Hidden Single, a basic placement technique.",
                Link("Candidates", "candidates"), Link("Hidden Single", "hidden-single")),
            Article("hidden-single", "Hidden Single", "Rules", "Simple",
                "If a digit has only one candidate location in a row, column, or box, place it there, even if the cell has other candidates. Naked Single is the cell-based counterpart.",
                Link("Candidates", "candidates"), Link("Naked Single", "naked-single")),
            Article("naked-pair", "Naked Pair", "Rules", "Subsets",
                "Two cells in one unit with the same two candidates reserve those digits. Remove both digits from other cells in that unit. Hidden Pair is the complementary pattern; Naked Triple extends the same idea.",
                Link("Hidden Pair", "hidden-pair"), Link("Naked Triple", "naked-triple")),
            Article("hidden-pair", "Hidden Pair", "Rules", "Subsets",
                "If two digits can appear in the same two cells of a unit and nowhere else in that unit, remove all other candidates from those two cells. Naked Pair is the complementary pattern, and Hidden Triple extends it.",
                Link("Naked Pair", "naked-pair"), Link("Hidden Triple", "hidden-triple")),
            Article("naked-triple", "Naked Triple", "Rules", "Subsets",
                "Three cells in one unit whose combined candidates are exactly three digits reserve those digits. Remove them from all other cells in the unit. This generalizes Naked Pair and leads to Naked Quad.",
                Link("Naked Pair", "naked-pair"), Link("Naked Quad", "naked-quad")),
            Article("hidden-triple", "Hidden Triple", "Rules", "Subsets",
                "If three digits are confined to the same three cells of a unit, remove every other candidate from those cells. This extends Hidden Pair and leads to Hidden Quad.",
                Link("Hidden Pair", "hidden-pair"), Link("Hidden Quad", "hidden-quad")),
            Article("naked-quad", "Naked Quad", "Rules", "Subsets",
                "Four cells in a unit with a combined set of exactly four candidates reserve those digits; remove them from the other cells in that unit. This extends Naked Triple.",
                Link("Naked Triple", "naked-triple")),
            Article("hidden-quad", "Hidden Quad", "Rules", "Subsets",
                "If four digits can occur only in the same four cells of a unit, remove other candidates from those cells. It extends Hidden Triple.",
                Link("Hidden Triple", "hidden-triple")),
            Article("box-line", "Box-Line Reduction", "Rules", "Intersections",
                "If all candidates for a digit in a row or column fall inside one box, remove that digit from the rest of the box. Pointing Pair / Triple is the complementary box-to-line form.",
                Link("Pointing Pair / Triple", "pointing-pair")),
            Article("pointing-pair", "Pointing Pair / Triple", "Rules", "Intersections",
                "When all positions for a digit in a box lie on one row or column, remove that digit from the rest of that line outside the box. Box-Line Reduction applies the reverse direction.",
                Link("Box-Line Reduction", "box-line")),
            Article("skyscraper", "Skyscraper", "Rules", "Fish and line patterns",
                "Two rows (or columns) each contain a candidate in the same two columns (or rows). When one pair of endpoints is linked by one of the Strong Links, cells that see both other endpoints cannot contain that digit through Weak Links.",
                Link("Strong Links", "strong-links"), Link("Weak Links", "weak-links")),
            Article("right-angle", "Right Angle", "Rules", "Fish and line patterns",
                "This pattern uses aligned candidate positions across a box and a line to prove that a candidate can be removed from a shared peer. Follow the Strong Links and Weak Links formed by the marked row, column, and box relationships carefully.",
                Link("Strong Links", "strong-links"), Link("Weak Links", "weak-links")),
            Article("x-wing", "X-Wing", "Rules", "Fish and line patterns",
                "For one digit, if it appears in exactly the same two columns in each of two rows, those four corners form an X-Wing. Remove the digit from those columns in other rows. Swordfish extends this pattern, and the transposed row/column pattern also works.",
                Link("Swordfish", "swordfish")),
            Article("swordfish", "Swordfish", "Rules", "Fish and line patterns",
                "A Swordfish extends the X-Wing idea across three rows and three columns. If a digit's candidate positions in three base rows are confined to the same three cover columns, eliminate it from those columns in other rows. Jellyfish extends the pattern to four rows.",
                Link("X-Wing", "x-wing")),
            Article("y-wing", "Y-Wing", "Rules", "Wings",
                "Three bivalue cells form a pivot and two pincers. Their Candidates share one pivot digit each, and the pincer cells share a third digit; remove that third digit from cells that see both pincers. XYZ-Wing is a related three-cell pattern.",
                Link("Candidates", "candidates"), Link("XYZ-Wing", "xyz-wing")),
            Article("w-wing", "W-Wing", "Rules", "Wings",
                "Two cells with the same pair are connected by a Strong Link on one candidate. The other candidate can then be removed from any cell that sees both endpoints through Weak Links. This is a Strong Links pattern.",
                Link("Strong Links", "strong-links"), Link("Weak Links", "weak-links")),
            Article("xyz-wing", "XYZ-Wing", "Rules", "Wings",
                "A three-candidate pivot and two bivalue pincers form an XYZ-Wing. Candidates shared by all three pattern cells can be eliminated from cells that see them. Y-Wing is a related pattern with a bivalue pivot.",
                Link("Y-Wing", "y-wing"), Link("Candidates", "candidates")),
            Article("strong-links", "Strong Links", "Rules", "Links",
                "A strong link connects two candidates for the same digit in one unit when those are the only two possible positions for that digit there. At least one endpoint must be true. Chains often alternate strong links with weak links, as in Forcing Chains.",
                Link("Weak Links", "weak-links"), Link("Forcing Chains", "forcing-chain")),
            Article("weak-links", "Weak Links", "Rules", "Links",
                "A Weak Link connects two candidates that cannot both be true, usually because they are the same digit in one row, column, or box. At least one endpoint must be false. Strong Links are stronger relationships: at least one endpoint must be true. Forcing Chains combine these relationships.",
                Link("Strong Links", "strong-links"), Link("Forcing Chains", "forcing-chain")),
            Article("colouring", "Simple Colouring", "Rules", "Chains",
                "Strong Links for one digit form alternating colour chains connected by Weak Links. If two same-colour candidates see each other, that colour is impossible; candidates seeing both colours can also be eliminated.",
                Link("Strong Links", "strong-links"), Link("Weak Links", "weak-links")),
            Article("forcing-chain", "Forcing Chains", "Rules", "Chains",
                "Assume a candidate is true or false and follow the consequences along alternating Weak Links and Strong Links. If every branch leads to the same placement or elimination, that conclusion is forced. Candidates are the starting possibilities for each chain. Longer chains are best taught with curated diagrams.",
                Link("Weak Links", "weak-links"), Link("Strong Links", "strong-links"), Link("Candidates", "candidates")),
            Article("bug", "Bivalue Universal Grave", "Rules", "Uniqueness",
                "A BUG pattern has every unsolved cell bivalue except one trivalue cell. Under the uniqueness assumption for a valid Sudoku, the extra candidate in that trivalue cell is the solution. Candidates describe the possible values for each unsolved cell.",
                Link("Candidates", "candidates"))
        };

        public static IReadOnlyList<LearningItem> GetTutorials()
        {
            return Tutorials;
        }

        public static IReadOnlyList<LearningItem> GetWikiArticles()
        {
            return WikiArticles;
        }

        private static LearningItem Article(
            string id,
            string title,
            string section,
            string subsection,
            string explanation,
            params WikiLink[] links)
        {
            return new LearningItem(id, title, section, explanation, null, false, subsection, links);
        }
    }
}
