using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NLog;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.GrabBudget;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Queue;
using QueueItem = NzbDrone.Core.Queue.Queue;

namespace Chaptarr.Core.Test.IndexerSearch
{
    [TestFixture]
    public class ScheduledMissingSearchFixture
    {
        private Mock<ISearchForReleases> _search;
        private Mock<IBookService> _books;
        private Mock<IQueueService> _queue;
        private Mock<IProcessDownloadDecisions> _process;
        private Mock<IGrabBudgetService> _budget;
        private Mock<IConfigService> _config;
        private Mock<IMissingSearchAttemptRepository> _attempts;
        private List<Book> _library;
        private List<int> _searched;
        private ScheduledMissingSearch _subject;

        private static Author Author(int id) => new() { Id = id, AudiobookQualityProfileId = 1, EbookQualityProfileId = 1 };

        [SetUp]
        public void Setup()
        {
            _search = new Mock<ISearchForReleases>();
            _books = new Mock<IBookService>();
            _queue = new Mock<IQueueService>();
            _process = new Mock<IProcessDownloadDecisions>();
            _budget = new Mock<IGrabBudgetService>();
            _config = new Mock<IConfigService>();
            _attempts = new Mock<IMissingSearchAttemptRepository>();
            _searched = new List<int>();

            // Three authors; author 10 holds a base row and a German variant row.
            _library = new List<Book>
            {
                new() { Id = 1, AuthorId = 10, Author = Author(10), MediaType = BookMediaType.Audiobook },
                new() { Id = 2, AuthorId = 10, Author = Author(10), MediaType = BookMediaType.Audiobook },
                new() { Id = 3, AuthorId = 20, Author = Author(20), MediaType = BookMediaType.Ebook },
                new() { Id = 4, AuthorId = 30, Author = Author(30), MediaType = BookMediaType.Ebook }
            };

            _books.Setup(b => b.GetMissingBookSearchTargets(It.IsAny<BookMediaType?>(), It.IsAny<int?>()))
                .Returns(() => _library.Select(b => new BookSearchTarget { BookId = b.Id, AuthorId = b.AuthorId }).ToList());
            _books.Setup(b => b.GetBooks(It.IsAny<IEnumerable<int>>()))
                .Returns((IEnumerable<int> ids) => _library.Where(b => ids.Contains(b.Id)).ToList());
            _books.Setup(b => b.GetBooksByAuthor(It.IsAny<int>())).Returns(new List<Book>());
            _queue.Setup(q => q.GetQueue()).Returns(new List<QueueItem>());
            _attempts.Setup(a => a.ForBooks(It.IsAny<IEnumerable<int>>())).Returns(new List<MissingSearchAttempt>());

            _search.Setup(s => s.BookSearch(It.IsAny<Book>(), It.IsAny<List<Book>>(), false, false, false, false))
                .Callback((Book b, List<Book> c, bool m, bool u, bool i, bool a) => _searched.Add(b.Id))
                .Returns(Task.FromResult(new List<DownloadDecision>()));
            _process.Setup(p => p.ProcessDecisions(It.IsAny<List<DownloadDecision>>()))
                .Returns(Task.FromResult(new ProcessedDecisions(new List<DownloadDecision> { new DownloadDecision(null) }, new List<DownloadDecision>(), new List<DownloadDecision>())));

            _config.SetupGet(c => c.GrabBudgetEnabled).Returns(true);
            _config.SetupGet(c => c.MissingSearchMaxRows).Returns(10);
            _config.SetupGet(c => c.MissingSearchCooldownHours).Returns(24);
            _config.SetupGet(c => c.MissingSearchMaxAttempts).Returns(5);
            _budget.Setup(b => b.CheckBudget(It.IsAny<int>(), It.IsAny<int>(), null)).Returns(GrabBudgetResult.Allow());

            _subject = new ScheduledMissingSearch(_search.Object, _books.Object, _queue.Object, _process.Object,
                _budget.Object, _config.Object, _attempts.Object, LogManager.GetLogger("ScheduledMissingSearchFixture"));
        }

        [Test]
        public void does_not_run_while_the_grab_budget_is_disabled()
        {
            _config.SetupGet(c => c.GrabBudgetEnabled).Returns(false);

            var result = _subject.Run(null);

            Assert.That(result.Ran, Is.False);
            Assert.That(_searched, Is.Empty);
            _books.Verify(b => b.GetMissingBookSearchTargets(It.IsAny<BookMediaType?>(), It.IsAny<int?>()), Times.Never);
        }

        [Test]
        public void searches_every_planned_row_round_robin_and_records_attempts()
        {
            var result = _subject.Run(null);

            Assert.That(result.Ran, Is.True);
            Assert.That(_searched, Is.EqualTo(new[] { 1, 3, 4, 2 }));
            _attempts.Verify(a => a.RecordAttempt(It.IsAny<int>(), It.IsAny<DateTime>(), false), Times.Exactly(4));
        }

        [Test]
        public void stops_early_and_logs_a_batch_when_the_per_run_budget_is_used_up()
        {
            // every search grabs one; the run limit is 2
            _budget.Setup(b => b.CheckBudget(It.IsAny<int>(), It.IsAny<int>(), null))
                .Returns((int grabbed, int queue, DateTime? now) => grabbed >= 2
                    ? GrabBudgetResult.Block(GrabBudgetStopReason.MaxPerRunReached, "limit")
                    : GrabBudgetResult.Allow());

            var result = _subject.Run(null);

            Assert.That(_searched, Is.EqualTo(new[] { 1, 3 }));
            Assert.That(result.Grabbed, Is.EqualTo(2));
            Assert.That(result.StopReason, Is.EqualTo(GrabBudgetStopReason.MaxPerRunReached));
            _budget.Verify(b => b.RecordBatch(2, 2, 0, GrabBudgetStopReason.MaxPerRunReached, It.IsAny<DateTime?>(), It.IsAny<string>()), Times.Once);
        }

        [Test]
        public void does_not_start_when_the_daily_budget_is_already_exhausted()
        {
            _budget.Setup(b => b.CheckBudget(0, 0, null))
                .Returns(GrabBudgetResult.Block(GrabBudgetStopReason.MaxPerDayReached, "day"));

            var result = _subject.Run(null);

            Assert.That(result.Ran, Is.False);
            Assert.That(_searched, Is.Empty);
            _budget.Verify(b => b.RecordBatch(0, 0, 0, GrabBudgetStopReason.MaxPerDayReached, It.IsAny<DateTime?>(), It.IsAny<string>()), Times.Once);
        }

        [Test]
        public void skips_rows_that_hit_the_retry_cap_and_rows_already_in_the_queue()
        {
            _attempts.Setup(a => a.ForBooks(It.IsAny<IEnumerable<int>>())).Returns(new List<MissingSearchAttempt>
            {
                new() { BookId = 1, Attempts = 5, LastAttemptAt = DateTime.UtcNow.AddDays(-2) }
            });
            _queue.Setup(q => q.GetQueue()).Returns(new List<QueueItem> { new() { Book = new Book { Id = 4 } } });

            _subject.Run(null);

            Assert.That(_searched, Is.EquivalentTo(new[] { 2, 3 }));
        }

        [Test]
        public void honours_max_rows_per_run()
        {
            _config.SetupGet(c => c.MissingSearchMaxRows).Returns(2);

            _subject.Run(null);

            Assert.That(_searched, Is.EqualTo(new[] { 1, 3 }));
        }

        [Test]
        public void search_errors_still_count_as_attempts_and_abort_after_three_in_a_row()
        {
            _search.Setup(s => s.BookSearch(It.IsAny<Book>(), It.IsAny<List<Book>>(), false, false, false, false))
                .Throws(new InvalidOperationException("indexer down"));

            _subject.Run(null);

            _attempts.Verify(a => a.RecordAttempt(It.IsAny<int>(), It.IsAny<DateTime>(), false), Times.Exactly(3));
        }

        [Test]
        public void book_search_service_delegates_only_whole_library_scheduled_runs()
        {
            var scheduled = new Mock<IScheduledMissingSearch>();
            scheduled.Setup(s => s.Run(It.IsAny<BookMediaType?>())).Returns(new ScheduledMissingSearchResult());
            var service = new BookSearchService(_search.Object, _books.Object, null, _queue.Object, _process.Object,
                LogManager.GetLogger("x"), scheduled.Object);

            service.Execute(new MissingBookSearchCommand { Trigger = CommandTrigger.Scheduled });
            scheduled.Verify(s => s.Run(It.IsAny<BookMediaType?>()), Times.Once);

            service.Execute(new MissingBookSearchCommand(10) { Trigger = CommandTrigger.Scheduled });
            service.Execute(new MissingBookSearchCommand { Trigger = CommandTrigger.Manual });
            scheduled.Verify(s => s.Run(It.IsAny<BookMediaType?>()), Times.Once);
        }
    }
}
