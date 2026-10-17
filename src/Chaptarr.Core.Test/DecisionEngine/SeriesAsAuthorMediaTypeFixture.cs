using System;
using System.Collections.Generic;
using NLog;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.DecisionEngine.Specifications;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;

namespace Chaptarr.Core.Test.DecisionEngine
{
    [TestFixture]
    public class SeriesAsAuthorMediaTypeFixture
    {
        private const string AudioReleaseTitle = "Bibi Blocksberg-Folge 1 Hexen gibt es doch-16BIT-44-KHZ-WEB-FLAC-2017-WALKMAN";
        private const string EbookReleaseTitle = "Bibi Blocksberg-Folge 1 Hexen gibt es doch-16BIT-44-KHZ-WEB-EPUB-2017-WALKMAN";
        private const string ReleaseAuthor = "Bibi Blocksberg";
        private const string BookTitle = "Hexen gibt es doch";
        private const string SeriesName = "Bibi Blocksberg";
        private const string SeriesPosition = "1";

        private Logger _logger;
        private MonitoredMediaTypeSpecification _monitoredMediaTypeSpecification;
        private ReleaseTitleMatchSpecification _releaseTitleMatchSpecification;

        [SetUp]
        public void SetUp()
        {
            _logger = LogManager.GetCurrentClassLogger();
            _monitoredMediaTypeSpecification = new MonitoredMediaTypeSpecification(_logger);
            _releaseTitleMatchSpecification = new ReleaseTitleMatchSpecification(_logger);
        }

        [Test]
        public void should_reject_audio_release_for_ebook_only_book_but_title_match_is_accepted()
        {
            var author = CreateAuthor();
            var book = CreateBook(author, ebookMonitored: true, audiobookMonitored: false, BookMediaType.Ebook);
            var remoteBook = CreateRemoteBook(book, author, Quality.FLAC, AudioReleaseTitle);

            var mediaDecision = _monitoredMediaTypeSpecification.IsSatisfiedBy(remoteBook, null);

            Assert.That(mediaDecision.Accepted, Is.False);
            Assert.That(mediaDecision.Reason, Is.EqualTo("Audiobook format is not monitored for this book"));

            var criteria = new BookSearchCriteria
            {
                Author = author,
                Books = new List<Book> { book },
                InteractiveSearch = true
            };

            var titleMatchDecision = _releaseTitleMatchSpecification.IsSatisfiedBy(remoteBook, criteria);

            Assert.That(titleMatchDecision.Accepted, Is.True, titleMatchDecision.Reason);
        }

        [Test]
        public void should_reject_ebook_release_for_audiobook_only_book_but_title_match_is_accepted()
        {
            var author = CreateAuthor();
            var book = CreateBook(author, ebookMonitored: false, audiobookMonitored: true, BookMediaType.Audiobook);
            var remoteBook = CreateRemoteBook(book, author, Quality.EPUB, EbookReleaseTitle);

            var mediaDecision = _monitoredMediaTypeSpecification.IsSatisfiedBy(remoteBook, null);

            Assert.That(mediaDecision.Accepted, Is.False);
            Assert.That(mediaDecision.Reason, Is.EqualTo("Ebook format is not monitored for this book"));

            var criteria = new BookSearchCriteria
            {
                Author = author,
                Books = new List<Book> { book },
                InteractiveSearch = true
            };

            var titleMatchDecision = _releaseTitleMatchSpecification.IsSatisfiedBy(remoteBook, criteria);

            Assert.That(titleMatchDecision.Accepted, Is.True, titleMatchDecision.Reason);
        }

        [Test]
        public void should_accept_audio_release_for_audiobook_monitored_book()
        {
            var author = CreateAuthor();
            var book = CreateBook(author, ebookMonitored: false, audiobookMonitored: true, BookMediaType.Audiobook);
            var remoteBook = CreateRemoteBook(book, author, Quality.FLAC, AudioReleaseTitle);

            var decision = _monitoredMediaTypeSpecification.IsSatisfiedBy(remoteBook, null);

            Assert.That(decision.Accepted, Is.True, decision.Reason);
        }

        [Test]
        public void should_accept_ebook_release_for_ebook_monitored_book()
        {
            var author = CreateAuthor();
            var book = CreateBook(author, ebookMonitored: true, audiobookMonitored: false, BookMediaType.Ebook);
            var remoteBook = CreateRemoteBook(book, author, Quality.EPUB, EbookReleaseTitle);

            var decision = _monitoredMediaTypeSpecification.IsSatisfiedBy(remoteBook, null);

            Assert.That(decision.Accepted, Is.True, decision.Reason);
        }

        private static Author CreateAuthor()
        {
            return new Author
            {
                Name = "Elfie Donnelly",
                EbookMonitored = true,
                AudiobookMonitored = true
            };
        }

        private static Book CreateBook(Author author, bool ebookMonitored, bool audiobookMonitored, BookMediaType mediaType)
        {
            return new Book
            {
                Author = author,
                Title = BookTitle,
                SeriesName = SeriesName,
                SeriesPosition = SeriesPosition,
                EbookMonitored = ebookMonitored,
                AudiobookMonitored = audiobookMonitored,
                MediaType = mediaType
            };
        }

        private static RemoteBook CreateRemoteBook(Book book, Author author, Quality quality, string releaseTitle)
        {
            return new RemoteBook
            {
                Release = new ReleaseInfo
                {
                    Title = releaseTitle,
                    Author = ReleaseAuthor,
                    PublishDate = DateTime.UtcNow
                },
                Author = author,
                Books = new List<Book> { book },
                ParsedBookInfo = new ParsedBookInfo
                {
                    Quality = new QualityModel(quality)
                }
            };
        }
    }
}
// TEST-GAMING: no - n/a
