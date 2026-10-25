using System;
using System.Collections.Generic;
using NLog;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.DecisionEngine.Specifications;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;

namespace Chaptarr.Core.Test.DecisionEngine
{
    [TestFixture]
    public class SeriesAsAuthorTitleMatchFixture
    {
        [TestCase("Bibi Blocksberg-Folge 1 Hexen gibt es doch-16BIT-44-KHZ-WEB-FLAC-2017-WALKMAN", "Bibi Blocksberg", true, null)]
        [TestCase("Bibi Blocksberg-Folge 1 Hexen gibt es doch-16BIT-44-KHZ-WEB-FLAC-1980-WALKMAN", "Bibi Blocksberg", true, null)]
        [TestCase("Bibi Blocksberg - 001 - Bibi Hexen gibt es doch", "", true, null)]
        [TestCase("Helen Harper-Hexen gibt es doch - Hex Files, Band 1 (Ungekuerzt)-16BIT-44-KHZ-WEB-FLAC-2020-WALKMAN", "Helen Harper", false, "Title/Author mismatch")]
        [TestCase("Helen Harper - Hex Files - Hexen gibt es doch Band 1 Ungekuerzt-Audiobook-WEB-DE-2020-AUDiAL", "Helen Harper", false, null)]
        [TestCase("Helen Harper - Hex Files 01 - Hexen gibt es doch", "", false, null)]
        [TestCase("Helen Harper - Hex Files - Band 01 - Hexen gibt es doch", "", false, null)]
        public void should_evaluate_series_as_author_or_leading_series_episode(
            string releaseTitle,
            string releaseAuthor,
            bool expectedAccepted,
            string expectedReason)
        {
            var criteria = CreateCriteria();

            var release = new ReleaseInfo
            {
                Title = releaseTitle,
                Author = releaseAuthor,
                PublishDate = DateTime.UtcNow
            };

            var remoteBook = new RemoteBook
            {
                Release = release
            };

            var logger = LogManager.GetCurrentClassLogger();
            var spec = new ReleaseTitleMatchSpecification(logger);

            var decision = spec.IsSatisfiedBy(remoteBook, criteria);

            Assert.That(decision.Accepted, Is.EqualTo(expectedAccepted), decision.Reason);

            if (expectedReason != null)
            {
                Assert.That(decision.Reason, Is.EqualTo(expectedReason));
            }
        }

        [TestCase("The Witcher", "The Witcher - 001 - Hexen gibt es doch", true)]
        [TestCase("Diary of a Wimpy Kid", "Diary of a Wimpy Kid - 001 - Hexen gibt es doch", true)]
        [TestCase("Babylon 5", "Babylon 5 - 001 - Hexen gibt es doch", true)]
        [TestCase("Babylon 5", "Babylon 5 - Hexen gibt es doch", false)]
        public void leading_series_episode_uses_raw_series_tokens(string seriesName, string releaseTitle, bool expectedAccepted)
        {
            var targetBook = new Book { Title = "Hexen gibt es doch", SeriesName = seriesName, SeriesPosition = "1", MediaType = BookMediaType.Audiobook };
            var criteria = new BookSearchCriteria
            {
                Author = new Author { Name = "Elfie Donnelly" },
                Books = new List<Book> { targetBook },
                InteractiveSearch = true
            };
            var remoteBook = new RemoteBook { Release = new ReleaseInfo { Title = releaseTitle, Author = "", PublishDate = DateTime.UtcNow } };

            var decision = new ReleaseTitleMatchSpecification(LogManager.GetCurrentClassLogger()).IsSatisfiedBy(remoteBook, criteria);

            Assert.That(decision.Accepted, Is.EqualTo(expectedAccepted), decision.Reason);
        }

        [Test]
        public void should_treat_series_author_as_positive_identity_evidence()
        {
            var expectedAuthor = new Author { Name = "Elfie Donnelly" };
            var targetBook = new Book
            {
                Title = "Hexen gibt es doch",
                SeriesName = "Bibi Blocksberg",
                SeriesPosition = "1",
                MediaType = BookMediaType.Audiobook
            };

            var release = new ReleaseInfo
            {
                Title = "Bibi Blocksberg-Folge 1 Hexen gibt es doch-16BIT-44-KHZ-WEB-FLAC-2017-WALKMAN",
                Author = "Bibi Blocksberg",
                PublishDate = DateTime.UtcNow
            };

            var titleMatch = new TitleMatchResult();

            var evidence = ReleaseIdentityEvidence.Analyze(release, expectedAuthor, targetBook, titleMatch);

            Assert.That(evidence.HasPositiveIdentityEvidence, Is.True);
            Assert.That(evidence.HasStructuredAuthorMismatch, Is.False);
        }

        private static BookSearchCriteria CreateCriteria()
        {
            var targetBook = new Book
            {
                Title = "Hexen gibt es doch",
                SeriesName = "Bibi Blocksberg",
                SeriesPosition = "1",
                MediaType = BookMediaType.Audiobook
            };

            return new BookSearchCriteria
            {
                Author = new Author { Name = "Elfie Donnelly" },
                Books = new List<Book> { targetBook },
                InteractiveSearch = true
            };
        }
    }
}
