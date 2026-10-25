using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Core.Books;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.GrabBudget;
using NzbDrone.Core.Queue;

namespace NzbDrone.Core.IndexerSearch
{
    public class ScheduledMissingSearchResult
    {
        public bool Ran { get; set; }
        public int Planned { get; set; }
        public int Searched { get; set; }
        public int Grabbed { get; set; }
        public GrabBudgetStopReason StopReason { get; set; }
        public string Message { get; set; }
    }

    public interface IScheduledMissingSearch
    {
        ScheduledMissingSearchResult Run(BookMediaType? mediaType);
    }

    // The interval-driven whole-library missing search. Unlike a manual "search all", it only runs
    // while the grab budget is enabled, is capped per run, skips recently searched rows and rows that
    // used up their retries, and stops as soon as the budget says so.
    public class ScheduledMissingSearch : IScheduledMissingSearch
    {
        private const int MaxConsecutiveSearchErrors = 3;

        private readonly ISearchForReleases _releaseSearchService;
        private readonly IBookService _bookService;
        private readonly IQueueService _queueService;
        private readonly IProcessDownloadDecisions _processDownloadDecisions;
        private readonly IGrabBudgetService _grabBudgetService;
        private readonly IConfigService _configService;
        private readonly IMissingSearchAttemptRepository _attempts;
        private readonly Logger _logger;

        public ScheduledMissingSearch(ISearchForReleases releaseSearchService,
            IBookService bookService,
            IQueueService queueService,
            IProcessDownloadDecisions processDownloadDecisions,
            IGrabBudgetService grabBudgetService,
            IConfigService configService,
            IMissingSearchAttemptRepository attempts,
            Logger logger)
        {
            _releaseSearchService = releaseSearchService;
            _bookService = bookService;
            _queueService = queueService;
            _processDownloadDecisions = processDownloadDecisions;
            _grabBudgetService = grabBudgetService;
            _configService = configService;
            _attempts = attempts;
            _logger = logger;
        }

        public ScheduledMissingSearchResult Run(BookMediaType? mediaType)
        {
            var result = new ScheduledMissingSearchResult();
            var startedAt = DateTime.UtcNow;

            // Dry-run only has an effect while the budget is enabled, so "enabled" is the gate.
            if (!_configService.GrabBudgetEnabled)
            {
                result.Message = "Scheduled missing search skipped: the grab budget is disabled. Enable it (dry-run is fine) to allow scheduled searches.";
                _logger.Warn(result.Message);
                return result;
            }

            var queued = GetQueuedBookIds();
            var entry = _grabBudgetService.CheckBudget(0, queued.Count);
            if (!entry.Allowed)
            {
                return Stop(result, entry, startedAt);
            }

            result.Ran = true;

            var targets = _bookService.GetMissingBookSearchTargets(mediaType, null)
                .Where(t => !queued.Contains(t.BookId))
                .ToList();

            var books = _bookService.GetBooks(targets.Select(t => t.BookId)).ToDictionary(b => b.Id);
            var attempts = _attempts.ForBooks(targets.Select(t => t.BookId)).ToDictionary(a => a.BookId);
            _attempts.DeleteExcept(targets.Select(t => t.BookId).ToList());

            var candidates = targets
                .Where(t => books.ContainsKey(t.BookId))
                .Select(t => new MissingSearchCandidate
                {
                    BookId = t.BookId,
                    AuthorId = t.AuthorId,
                    LastSearchTime = books[t.BookId].LastSearchTime,
                    Attempts = attempts.TryGetValue(t.BookId, out var a) ? a.Attempts : 0,
                    LastAttemptAt = attempts.TryGetValue(t.BookId, out var b) ? b.LastAttemptAt : null
                })
                .ToList();

            var options = new MissingSearchPlanOptions
            {
                MaxRows = _configService.MissingSearchMaxRows,
                Cooldown = TimeSpan.FromHours(Math.Max(0, _configService.MissingSearchCooldownHours)),
                MaxAttempts = _configService.MissingSearchMaxAttempts
            };

            var plan = MissingSearchPlanner.Plan(candidates, startedAt, options);
            result.Planned = plan.Count;

            var catalogs = new Dictionary<int, List<Book>>();
            var errors = 0;
            var skippedBudget = 0;

            foreach (var item in plan)
            {
                var check = _grabBudgetService.CheckBudget(result.Grabbed, queued.Count + result.Grabbed);
                if (!check.Allowed)
                {
                    result.StopReason = check.StopReason;
                    result.Message = $"Scheduled missing search stopped early after {result.Searched} of {plan.Count} rows: {check.Reason}";
                    skippedBudget = plan.Count - result.Searched;
                    _logger.Info(result.Message);
                    break;
                }

                var book = books[item.BookId];

                if (book.Author != null && !ReleaseSearchService.HasConfiguredQualityProfileForMediaType(book.Author, book.MediaType))
                {
                    continue;
                }

                if (!catalogs.TryGetValue(item.AuthorId, out var catalog))
                {
                    catalog = _bookService.GetBooksByAuthor(item.AuthorId);
                    catalogs[item.AuthorId] = catalog;
                }

                // A capped row only gets here after the reset window passed: start a new count.
                var restart = options.MaxAttempts > 0 && item.Attempts >= options.MaxAttempts;
                result.Searched++;

                try
                {
                    var decisions = _releaseSearchService.BookSearch(book, catalog, false, false, false).GetAwaiter().GetResult();
                    var processed = _processDownloadDecisions.ProcessDecisions(decisions).GetAwaiter().GetResult();
                    result.Grabbed += processed.Grabbed.Count;
                    errors = 0;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Scheduled missing search failed for book: [{0}]", book);
                    errors++;
                }

                // Every attempt counts, grabbed or not: a grab that later fails and re-enters "missing"
                // is capped by the same counter, so a bad release cannot be re-grabbed forever.
                _attempts.RecordAttempt(item.BookId, DateTime.UtcNow, restart);

                if (errors >= MaxConsecutiveSearchErrors)
                {
                    result.Message = $"Scheduled missing search aborted after {errors} consecutive search errors.";
                    _logger.Warn(result.Message);
                    break;
                }
            }

            result.Message ??= $"Scheduled missing search finished: {result.Searched} of {result.Planned} planned rows searched, {result.Grabbed} grabbed.";
            _logger.Info(result.Message);

            if (result.StopReason != GrabBudgetStopReason.None)
            {
                _grabBudgetService.RecordBatch(result.Grabbed, skippedBudget, 0, result.StopReason, startedAt, result.Message);
            }

            return result;
        }

        private ScheduledMissingSearchResult Stop(ScheduledMissingSearchResult result, GrabBudgetResult budget, DateTime startedAt)
        {
            result.StopReason = budget.StopReason;
            result.Message = $"Scheduled missing search not started: {budget.Reason}";
            _logger.Info(result.Message);
            _grabBudgetService.RecordBatch(0, 0, 0, budget.StopReason, startedAt, result.Message);
            return result;
        }

        private HashSet<int> GetQueuedBookIds()
        {
            var ids = new HashSet<int>();

            foreach (var item in _queueService.GetQueue() ?? new List<Queue.Queue>())
            {
                var books = item.Book != null ? new List<Book> { item.Book } : item.RemoteBook?.Books ?? new List<Book>();
                foreach (var book in books.Where(b => b?.Id > 0))
                {
                    ids.Add(book.Id);
                }
            }

            return ids;
        }
    }
}
