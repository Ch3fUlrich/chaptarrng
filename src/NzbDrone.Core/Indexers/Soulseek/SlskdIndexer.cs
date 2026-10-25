using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation.Results;
using NLog;
using NzbDrone.Common.Http;
using NzbDrone.Core.Books;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Soulseek;
using NzbDrone.Core.ThingiProvider;

namespace NzbDrone.Core.Indexers.Soulseek
{
    public class SlskdIndexer : IndexerBase<SlskdIndexerSettings>
    {
        // slskd accepts only one search request at a time (HTTP 429 otherwise).
        private static readonly SemaphoreSlim SearchGate = new(1, 1);
        internal static TimeSpan PollInterval = TimeSpan.FromSeconds(1);

        private readonly ISlskdApiClient _apiClient;

        public SlskdIndexer(ISlskdApiClient apiClient,
                            IIndexerStatusService indexerStatusService,
                            IConfigService configService,
                            IParsingService parsingService,
                            Logger logger)
            : base(indexerStatusService, configService, parsingService, logger)
        {
            _apiClient = apiClient;
        }

        public override string Name => "Soulseek (slskd)";

        public override DownloadProtocol Protocol => DownloadProtocol.Soulseek;

        public override bool SupportsRss => false;

        public override bool SupportsSearch => true;

        public override ProviderMessage Message => new(
            "Searches Soulseek through your slskd instance. Give it a lower priority than your Usenet indexers so it is only used as a fallback.",
            ProviderMessageType.Info);

        public override Task<IList<ReleaseInfo>> FetchRecent()
        {
            return Task.FromResult<IList<ReleaseInfo>>(Array.Empty<ReleaseInfo>());
        }

        public override async Task<IList<ReleaseInfo>> Fetch(BookSearchCriteria searchCriteria)
        {
            var mediaType = SearchMediaTypeHelper.GetRequestedMediaType(searchCriteria);
            var title = searchCriteria?.BookTitle;

            // Not BookQuery: that strips diacritics, and the umlaut spellings are searched here.
            var query = BookSearchCriteria.GetMainSearchTitle(searchCriteria?.SearchTitleOverride ?? title, searchCriteria?.Author?.Name);

            return CleanupReleases(await SearchAsync(searchCriteria?.Author?.Name, title, query, mediaType)).ToList();
        }

        public override async Task<IList<ReleaseInfo>> Fetch(AuthorSearchCriteria searchCriteria)
        {
            var releases = new List<ReleaseInfo>();

            foreach (var book in (searchCriteria?.Books ?? new List<Book>()).Where(b => b != null))
            {
                var edition = book.Editions?.Where(e => e != null && e.Monitored).OrderBy(e => e.Id).FirstOrDefault();
                var title = NzbDrone.Core.IndexerSearch.ReleaseSearchService.GetSearchBookTitle(book, edition);
                releases.AddRange(await SearchAsync(searchCriteria.Author?.Name, title, title, book.MediaType));
            }

            return CleanupReleases(releases).ToList();
        }

        public override HttpRequest GetDownloadRequest(string link)
        {
            // Never fetched over HTTP: the Soulseek download client interprets the slskd:// token.
            return new HttpRequest(link);
        }

        protected override async Task Test(List<ValidationFailure> failures)
        {
            var validation = Settings.Validate();
            if (!validation.IsValid)
            {
                failures.AddRange(validation.Errors);
                return;
            }

            try
            {
                var state = await _apiClient.GetApplicationAsync(Connection);
                if (state?.Server == null)
                {
                    failures.Add(new ValidationFailure(nameof(Settings.BaseUrl), "Unable to read the slskd application state; is this a slskd instance?"));
                }
                else if (!state.Server.IsLoggedIn)
                {
                    failures.Add(new ValidationFailure(nameof(Settings.BaseUrl), $"slskd is reachable but not logged in to the Soulseek network (state: {state.Server.State})."));
                }
            }
            catch (SlskdApiException ex)
            {
                failures.Add(new ValidationFailure(ex.StatusCode == 401 || ex.StatusCode == 403 ? nameof(Settings.ApiKey) : nameof(Settings.BaseUrl), ex.Message));
            }
            catch (HttpException ex)
            {
                failures.Add(new ValidationFailure(nameof(Settings.BaseUrl), $"Unable to reach slskd: {ex.Message}"));
            }
        }

        private SlskdConnection Connection => new() { BaseUrl = Settings.BaseUrl, ApiKey = Settings.ApiKey };

        private async Task<IList<ReleaseInfo>> SearchAsync(string author, string title, string query, BookMediaType? mediaType)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return Array.Empty<ReleaseInfo>();
            }

            // All query spellings feed ONE build/cap pass so MaximumResults and the per-language guarantee hold overall.
            var responses = new List<SlskdSearchResponse>();
            foreach (var searchText in BuildSearchTexts(query))
            {
                responses.AddRange(await SearchOnceAsync(searchText));
            }

            var context = new SlskdSearchContext { Author = author, Title = title, AlternateTitles = new List<string> { query }, MediaType = mediaType };
            return SlskdReleaseBuilder.Build(MergeResponses(responses), context, Settings);
        }

        private static IEnumerable<SlskdSearchResponse> MergeResponses(IEnumerable<SlskdSearchResponse> responses)
        {
            // The same peer answers both spellings: keep one response per user with the union of its files.
            return responses.Where(r => r?.Username != null).GroupBy(r => r.Username, StringComparer.Ordinal).Select(group =>
            {
                var first = group.First();
                return new SlskdSearchResponse
                {
                    Username = first.Username,
                    HasFreeUploadSlot = group.Any(r => r.HasFreeUploadSlot),
                    UploadSpeed = group.Max(r => r.UploadSpeed),
                    QueueLength = group.Min(r => r.QueueLength),
                    Files = group.SelectMany(r => r.Files ?? new List<SlskdFile>()).GroupBy(f => f.Filename, StringComparer.Ordinal).Select(g => g.First()).ToList()
                };
            });
        }

        private async Task<IList<SlskdSearchResponse>> SearchOnceAsync(string searchText)
        {
            var settings = Settings;
            var connection = Connection;
            var id = Guid.NewGuid();
            var timeout = TimeSpan.FromSeconds(settings.SearchTimeoutSeconds);

            await SearchGate.WaitAsync();
            try
            {
                await _apiClient.StartSearchAsync(connection, new SlskdSearchRequest
                {
                    Id = id,
                    SearchText = searchText,
                    SearchTimeout = (int)timeout.TotalMilliseconds,
                    FileLimit = 10000,
                    FilterResponses = true,
                    MinimumResponseFileCount = 1,
                    MaximumPeerQueueLength = settings.MaximumQueueLength > 0 ? settings.MaximumQueueLength : 1000000,
                    MinimumPeerUploadSpeed = settings.MinimumUploadSpeedKBps * 1024
                });

                var deadline = DateTime.UtcNow + timeout + TimeSpan.FromSeconds(10);
                SlskdSearch search;
                do
                {
                    await Task.Delay(PollInterval);
                    search = await _apiClient.GetSearchAsync(connection, id);
                }
                while (search != null && !search.IsComplete && DateTime.UtcNow < deadline);

                var responses = await _apiClient.GetSearchResponsesAsync(connection, id) ?? new List<SlskdSearchResponse>();

                return responses;
            }
            catch (SlskdApiException ex) when (ex.StatusCode is 400 or 404 or 409 or 429)
            {
                _logger.Warn("slskd search for '{0}' was not run: {1}", searchText, ex.Message);
                return Array.Empty<SlskdSearchResponse>();
            }
            finally
            {
                try
                {
                    await _apiClient.DeleteSearchAsync(connection, id);
                }
                catch (Exception ex)
                {
                    _logger.Debug(ex, "Could not delete slskd search {0}", id);
                }

                SearchGate.Release();
            }
        }

        // The query as written and, when it contains umlauts, the ae/oe/ue/ss spelling many shares use.
        internal static IList<string> BuildSearchTexts(string query)
        {
            var primary = BuildSearchText(query);
            var texts = new List<string>();
            if (!string.IsNullOrWhiteSpace(primary))
            {
                texts.Add(primary);
                var folded = SlskdTextMatcher.Fold(primary, true);
                if (folded != primary)
                {
                    texts.Add(folded);
                }
            }

            return texts;
        }

        // Significant words only (slskd matches every word), capped so long subtitles do not kill recall.
        internal static string BuildSearchText(string query)
        {
            var words = SlskdTextMatcher.TitleTokens(query).Take(8).ToList();
            return string.Join(" ", words);
        }
    }
}
