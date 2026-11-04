using System;
using System.Linq;
using NUnit.Framework;
using Sudoku.UI.Learning;

namespace Sudoku.Tests.UI
{
    public class LearningContentTests
    {
        [Test]
        public void TutorialsStartWithBasicsAndProgressThroughEasyRules()
        {
            var titles = LearningContent.GetTutorials().Select(item => item.Title).ToArray();

            Assert.AreEqual("Sudoku basics", titles[0]);
            Assert.AreEqual("Using the app", titles[1]);
            Assert.Less(System.Array.IndexOf(titles, "Naked Single"), System.Array.IndexOf(titles, "Naked Pair"));
            Assert.Less(System.Array.IndexOf(titles, "Hidden Single"), System.Array.IndexOf(titles, "Naked Pair"));
        }

        [Test]
        public void InteractiveExercisesRequireCorrectCellDigitAndAction()
        {
            var exercise = LearningContent.GetTutorials().Single(item => item.Id == "naked-pair").Exercise;

            Assert.IsTrue(exercise.IsCorrectAction(4, 7, 2, LearningAction.RemoveCandidate));
            Assert.IsFalse(exercise.IsCorrectAction(4, 7, 5, LearningAction.RemoveCandidate));
            Assert.IsFalse(exercise.IsCorrectAction(4, 7, 2, LearningAction.SetValue));
            Assert.IsFalse(exercise.IsCorrectAction(4, 6, 2, LearningAction.RemoveCandidate));
        }

        [Test]
        public void NonBasicsExercisePromptsExplainHoldAndReleaseInteraction()
        {
            foreach (var tutorial in LearningContent.GetTutorials().Where(item => item.Id != "sudoku-basics"))
            {
                StringAssert.Contains("hold", tutorial.Exercise.Prompt,
                    $"Tutorial '{tutorial.Id}' should explain holding the mouse button.");
                StringAssert.Contains("release", tutorial.Exercise.Prompt,
                    $"Tutorial '{tutorial.Id}' should explain releasing the mouse button.");
            }
        }

        [Test]
        public void SudokuBasicsDoesNotShowItsAnswerBeforeTheExercise()
        {
            var basics = LearningContent.GetTutorials().Single(item => item.Id == "sudoku-basics");

            StringAssert.DoesNotContain("9", basics.Exercise.Prompt);
            StringAssert.DoesNotContain("9", basics.Exercise.SuccessMessage);
            StringAssert.DoesNotContain("missing 9", basics.Exercise.SuccessMessage);
        }

        [Test]
        public void WikiIncludesTheRegisteredAdvancedTechniques()
        {
            var articleTitles = LearningContent.GetWikiArticles().Select(item => item.Title).ToArray();

            CollectionAssert.Contains(articleTitles, "Forcing Chains");
            CollectionAssert.Contains(articleTitles, "X-Wing");
            CollectionAssert.Contains(articleTitles, "Bivalue Universal Grave");
        }

        [Test]
        public void WikiGroupsSubjectsAndEveryLinkTargetsAnArticle()
        {
            var articles = LearningContent.GetWikiArticles();
            var articleIds = articles.Select(article => article.Id).ToArray();
            var forcingChains = articles.Single(article => article.Id == "forcing-chain");

            Assert.AreEqual("Rules", forcingChains.Category);
            Assert.AreEqual("Chains", forcingChains.Subsection);
            CollectionAssert.AreEquivalent(new[] { "weak-links", "strong-links", "candidates" },
                forcingChains.Links.Select(link => link.ArticleId).ToArray());
            Assert.IsTrue(articles.SelectMany(article => article.Links).All(link => articleIds.Contains(link.ArticleId)));
        }

        [Test]
        public void WikiLinksAreEmbeddedInTheirArticleExplanations()
        {
            foreach (var article in LearningContent.GetWikiArticles())
            {
                foreach (var link in article.Links)
                    Assert.IsTrue(
                        article.Explanation.IndexOf(link.Label, StringComparison.OrdinalIgnoreCase) >= 0,
                        $"Article '{article.Id}' should mention its inline link '{link.Label}'.");
            }
        }
    }
}
