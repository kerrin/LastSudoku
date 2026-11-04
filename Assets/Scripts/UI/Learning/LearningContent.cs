using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;

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
        private const string ResourceName = "LearningContent";
        private static readonly Lazy<ContentData> Content = new Lazy<ContentData>(LoadContent);

        public static IReadOnlyList<LearningItem> GetTutorials()
        {
            return Content.Value.Tutorials;
        }

        public static IReadOnlyList<LearningItem> GetWikiArticles()
        {
            return Content.Value.WikiArticles;
        }

        private static ContentData LoadContent()
        {
            TextAsset asset = Resources.Load<TextAsset>(ResourceName);
            if (asset == null)
                throw new InvalidOperationException($"Learning content resource '{ResourceName}' was not found. Expected Assets/Resources/{ResourceName}.xml.");

            XDocument document;
            try
            {
                document = XDocument.Parse(asset.text, LoadOptions.SetLineInfo);
            }
            catch (Exception exception) when (exception is System.Xml.XmlException || exception is ArgumentException)
            {
                throw new InvalidOperationException($"Learning content resource '{ResourceName}' is not valid XML.", exception);
            }

            XElement root = document.Root;
            if (root == null || root.Name != "LearningContent")
                throw new InvalidOperationException($"Learning content resource '{ResourceName}' must have a LearningContent root element.");

            var tutorials = RequiredElement(root, "Tutorials").Elements("Tutorial")
                .Select(ParseTutorial)
                .ToArray();
            var wikiArticles = RequiredElement(root, "WikiArticles").Elements("Article")
                .Select(ParseArticle)
                .ToArray();

            ValidateUniqueIds(tutorials, "tutorial");
            ValidateUniqueIds(wikiArticles, "wiki article");
            ValidateWikiLinks(wikiArticles);
            return new ContentData(tutorials, wikiArticles);
        }

        private static LearningItem ParseTutorial(XElement element)
        {
            XElement exerciseElement = RequiredElement(element, "Exercise");
            var cells = RequiredElement(exerciseElement, "Cells").Elements("Cell")
                .Select(ParseCell)
                .ToArray();

            var exercise = new LearningExercise(
                RequiredElement(exerciseElement, "Prompt").Value,
                cells,
                RequiredIntAttribute(exerciseElement, "targetRow"),
                RequiredIntAttribute(exerciseElement, "targetColumn"),
                RequiredIntAttribute(exerciseElement, "targetDigit"),
                ParseAction(RequiredAttribute(exerciseElement, "expectedAction"), exerciseElement),
                RequiredElement(exerciseElement, "SuccessMessage").Value);

            return new LearningItem(
                RequiredAttribute(element, "id"),
                RequiredAttribute(element, "title"),
                RequiredAttribute(element, "category"),
                RequiredElement(element, "Explanation").Value,
                exercise,
                true);
        }

        private static LearningItem ParseArticle(XElement element)
        {
            var links = RequiredElement(element, "Links").Elements("Link")
                .Select(link => new WikiLink(
                    RequiredAttribute(link, "label"),
                    RequiredAttribute(link, "articleId")))
                .ToArray();

            return new LearningItem(
                RequiredAttribute(element, "id"),
                RequiredAttribute(element, "title"),
                RequiredAttribute(element, "category"),
                RequiredElement(element, "Explanation").Value,
                null,
                false,
                RequiredAttribute(element, "subsection"),
                links);
        }

        private static LearningBoardCell ParseCell(XElement element)
        {
            int row = RequiredIntAttribute(element, "row");
            int column = RequiredIntAttribute(element, "column");
            XAttribute valueAttribute = element.Attribute("value");
            int? value = valueAttribute == null ? (int?)null : ParseInt(valueAttribute.Value, "value", element);
            XAttribute candidatesAttribute = element.Attribute("candidates");
            int[] candidates = candidatesAttribute == null || string.IsNullOrWhiteSpace(candidatesAttribute.Value)
                ? Array.Empty<int>()
                : candidatesAttribute.Value.Split(',').Select(candidate => ParseInt(candidate.Trim(), "candidates", element)).ToArray();

            if (value.HasValue && candidates.Length > 0)
                throw ContentError(element, "A learning board cell cannot have both a value and candidates.");
            if (value.HasValue && (value.Value < 1 || value.Value > 9))
                throw ContentError(element, "Cell values must be digits from 1 through 9.");
            if (candidates.Any(candidate => candidate < 1 || candidate > 9))
                throw ContentError(element, "Cell candidates must be digits from 1 through 9.");
            if (candidates.Distinct().Count() != candidates.Length)
                throw ContentError(element, "A learning board cell cannot list the same candidate more than once.");

            return new LearningBoardCell(row, column, value, candidates);
        }

        private static LearningAction ParseAction(string action, XElement element)
        {
            if (Enum.TryParse(action, false, out LearningAction parsedAction) &&
                Enum.IsDefined(typeof(LearningAction), parsedAction))
                return parsedAction;
            throw ContentError(element, $"Unknown learning action '{action}'.");
        }

        private static int RequiredIntAttribute(XElement element, string name)
        {
            return ParseInt(RequiredAttribute(element, name), name, element);
        }

        private static int ParseInt(string value, string name, XElement element)
        {
            if (int.TryParse(value, out int result))
                return result;
            throw ContentError(element, $"Attribute or value '{name}' must be an integer.");
        }

        private static string RequiredAttribute(XElement element, string name)
        {
            string value = (string)element.Attribute(name);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
            throw ContentError(element, $"Required attribute '{name}' is missing or empty.");
        }

        private static XElement RequiredElement(XElement element, string name)
        {
            XElement child = element.Element(name);
            if (child != null)
                return child;
            throw ContentError(element, $"Required element '{name}' is missing.");
        }

        private static void ValidateUniqueIds(IEnumerable<LearningItem> items, string itemType)
        {
            var duplicateId = items.GroupBy(item => item.Id, StringComparer.Ordinal)
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicateId != null)
                throw new InvalidOperationException($"Learning content contains duplicate {itemType} id '{duplicateId.Key}'.");
        }

        private static void ValidateWikiLinks(IReadOnlyList<LearningItem> articles)
        {
            var articleIds = new HashSet<string>(articles.Select(article => article.Id), StringComparer.Ordinal);
            foreach (LearningItem article in articles)
            {
                foreach (WikiLink link in article.Links)
                {
                    if (!articleIds.Contains(link.ArticleId))
                        throw new InvalidOperationException(
                            $"Wiki article '{article.Id}' links to missing article '{link.ArticleId}'.");
                    if (article.Explanation.IndexOf(link.Label, StringComparison.OrdinalIgnoreCase) < 0)
                        throw new InvalidOperationException(
                            $"Wiki article '{article.Id}' has link label '{link.Label}' that is not present in its explanation.");
                }
            }
        }

        private static InvalidOperationException ContentError(XElement element, string message)
        {
            var lineInfo = (System.Xml.IXmlLineInfo)element;
            string location = lineInfo.HasLineInfo() ? $" at line {lineInfo.LineNumber}" : string.Empty;
            return new InvalidOperationException($"Invalid learning content{location}: {message}");
        }

        private sealed class ContentData
        {
            public IReadOnlyList<LearningItem> Tutorials { get; }
            public IReadOnlyList<LearningItem> WikiArticles { get; }

            public ContentData(LearningItem[] tutorials, LearningItem[] wikiArticles)
            {
                Tutorials = tutorials;
                WikiArticles = wikiArticles;
            }
        }
    }
}
