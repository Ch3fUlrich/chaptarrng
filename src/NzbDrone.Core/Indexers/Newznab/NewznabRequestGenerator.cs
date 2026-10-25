using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.IndexerSearch.Definitions;

namespace NzbDrone.Core.Indexers.Newznab
{
    public class NewznabRequestGenerator : IIndexerRequestGenerator
    {
        protected readonly INewznabCapabilitiesProvider _capabilitiesProvider;
        private readonly Logger _logger;
        private readonly string _indexerName;
        public int MaxPages { get; set; }
        public int PageSize { get; set; }
        public NewznabSettings Settings { get; set; }
        public int? ProxyId { get; set; }

        public NewznabRequestGenerator(INewznabCapabilitiesProvider capabilitiesProvider, Logger logger, string indexerName)
        {
            _capabilitiesProvider = capabilitiesProvider;
            _logger = logger;
            _indexerName = indexerName.IsNullOrWhiteSpace() ? "Newznab" : indexerName;

            MaxPages = 30;
            PageSize = 100;
        }

        private bool SupportsSearch
        {
            get
            {
                var capabilities = _capabilitiesProvider.GetCapabilities(Settings, ProxyId);

                return capabilities.SupportedSearchParameters != null &&
                       capabilities.SupportedSearchParameters.Contains("q");
            }
        }

        protected virtual bool SupportsBookSearch
        {
            get
            {
                var capabilities = _capabilitiesProvider.GetCapabilities(Settings, ProxyId);

                return capabilities.SupportedBookSearchParameters != null &&
                       capabilities.SupportedBookSearchParameters.Contains("author") &&
                       capabilities.SupportedBookSearchParameters.Contains("title");
            }
        }

        public virtual IndexerPageableRequestChain GetRecentRequests()
        {
            var pageableRequests = new IndexerPageableRequestChain();

            var capabilities = _capabilitiesProvider.GetCapabilities(Settings, ProxyId);

            if (capabilities.SupportedBookSearchParameters != null)
            {
                pageableRequests.Add(GetPagedRequests(MaxPages, Settings.Categories, "book", ""));
            }
            else if (capabilities.SupportedSearchParameters != null)
            {
                pageableRequests.Add(GetPagedRequests(MaxPages, Settings.Categories, "search", ""));
            }

            return pageableRequests;
        }

        public virtual IndexerPageableRequestChain GetSearchRequests(BookSearchCriteria searchCriteria)
        {
            var pageableRequests = new IndexerPageableRequestChain();
            var requestedMediaType = SearchMediaTypeHelper.GetRequestedMediaType(searchCriteria);
            var configuredCategories = (Settings.Categories ?? Enumerable.Empty<int>()).Distinct().ToList();
            var searchCategories = SearchMediaTypeHelper.FilterCategoriesForMediaType(configuredCategories, requestedMediaType);

            if (requestedMediaType.HasValue && configuredCategories.Count > 0 && searchCategories.Count == 0)
            {
                _logger.Warn("Newznab indexer '{0}' has no {1} categories selected; this search will send no indexer requests",
                    _indexerName,
                    requestedMediaType.Value.ToString().ToLowerInvariant());
            }

            var mainTitle = BookSearchCriteria.GetMainSearchTitle(searchCriteria.BookTitle, searchCriteria.Author?.Name);
            var hasBookTitle = !string.IsNullOrWhiteSpace(mainTitle);

            // BookQuery throws on an empty title, so only evaluate the queries when there is one.
            var authorQuery = hasBookTitle ? searchCriteria.AuthorQuery : null;
            var bookQuery = hasBookTitle ? searchCriteria.BookQuery : null;
            var transliteratedAuthor = hasBookTitle ? searchCriteria.AuthorQueryTransliterated ?? authorQuery : null;
            var transliteratedBook = hasBookTitle ? searchCriteria.BookQueryTransliterated ?? bookQuery : null;

            if (SupportsBookSearch && hasBookTitle)
            {
                pageableRequests.AddTier();
                AddVariant(pageableRequests,
                    searchCategories,
                    "book",
                    $"&author={NewsnabifyTitle(authorQuery)}&title={NewsnabifyTitle(bookQuery)}",
                    $"&author={NewsnabifyTitle(transliteratedAuthor)}&title={NewsnabifyTitle(transliteratedBook)}");

                pageableRequests.AddTier();
                AddVariant(pageableRequests,
                    searchCategories,
                    "book",
                    $"&title={NewsnabifyTitle(bookQuery)}",
                    $"&title={NewsnabifyTitle(transliteratedBook)}");
            }

            if (SupportsSearch && hasBookTitle)
            {
                pageableRequests.AddTier();

                AddVariant(pageableRequests,
                    searchCategories,
                    "search",
                    $"&q={NewsnabifyTitle(bookQuery)}+{NewsnabifyTitle(authorQuery)}",
                    $"&q={NewsnabifyTitle(transliteratedBook)}+{NewsnabifyTitle(transliteratedAuthor)}");

                AddVariant(pageableRequests,
                    searchCategories,
                    "search",
                    $"&q={NewsnabifyTitle(authorQuery)}+{NewsnabifyTitle(bookQuery)}",
                    $"&q={NewsnabifyTitle(transliteratedAuthor)}+{NewsnabifyTitle(transliteratedBook)}");

                pageableRequests.AddTier();

                AddVariant(pageableRequests,
                    searchCategories,
                    "search",
                    $"&q={NewsnabifyTitle(bookQuery)}",
                    $"&q={NewsnabifyTitle(transliteratedBook)}");
            }

            // Interactive search: a title like "..., Book 4" rarely appears in a release name, so every
            // tier above can come back empty for a book the indexer does have. Tiers are tried in order
            // until one returns results, so this only runs when the precise queries found nothing.
            // MyAnonaMouse has the same last-resort tier.
            var queryWithoutBookNumber = searchCriteria.InteractiveSearch ? searchCriteria.BookQueryWithoutBookNumber : null;
            if (queryWithoutBookNumber != null)
            {
                pageableRequests.AddTier();

                if (SupportsBookSearch)
                {
                    pageableRequests.Add(GetPagedRequests(MaxPages,
                        searchCategories,
                        "book",
                        $"&title={NewsnabifyTitle(queryWithoutBookNumber)}"));
                }

                if (SupportsSearch)
                {
                    pageableRequests.Add(GetPagedRequests(MaxPages,
                        searchCategories,
                        "search",
                        $"&q={NewsnabifyTitle(queryWithoutBookNumber)}"));
                }
            }

            if (searchCriteria.SeriesName.IsNotNullOrWhiteSpace())
            {
                var supportsBookSearch = SupportsBookSearch;
                var supportsSearch = SupportsSearch;
                var includeTitleVariant = searchCriteria.SeriesTitleQuery.IsNotNullOrWhiteSpace() && !TitleQueryContainsSeries(searchCriteria);
                var includePositionVariant = searchCriteria.SeriesPositionQuery.IsNotNullOrWhiteSpace();

                if ((supportsBookSearch || supportsSearch) && (includeTitleVariant || includePositionVariant))
                {
                    pageableRequests.AddTier();

                    if (supportsBookSearch)
                    {
                        if (includeTitleVariant)
                        {
                            pageableRequests.Add(GetPagedRequests(MaxPages,
                                searchCategories,
                                "book",
                                $"&title={NewsnabifyTitle(searchCriteria.SeriesTitleQuery)}"));
                        }

                        if (includePositionVariant)
                        {
                            pageableRequests.Add(GetPagedRequests(MaxPages,
                                searchCategories,
                                "book",
                                $"&title={NewsnabifyTitle(searchCriteria.SeriesPositionQuery)}"));
                        }
                    }

                    if (supportsSearch)
                    {
                        if (includeTitleVariant)
                        {
                            pageableRequests.Add(GetPagedRequests(MaxPages,
                                searchCategories,
                                "search",
                                $"&q={NewsnabifyTitle(searchCriteria.SeriesTitleQuery)}"));
                        }

                        if (includePositionVariant)
                        {
                            pageableRequests.Add(GetPagedRequests(MaxPages,
                                searchCategories,
                                "search",
                                $"&q={NewsnabifyTitle(searchCriteria.SeriesPositionQuery)}"));
                        }
                    }
                    }
                }
            }

            return pageableRequests;
        }

        public virtual IndexerPageableRequestChain GetSearchRequests(AuthorSearchCriteria searchCriteria)
        {
            var pageableRequests = new IndexerPageableRequestChain();

            if (SupportsBookSearch)
            {
                AddBookPageableRequests(pageableRequests,
                    Settings.Categories,
                    $"&author={NewsnabifyTitle(searchCriteria.AuthorQuery)}");

                if (searchCriteria.AuthorQueryTransliterated != null)
                {
                    pageableRequests.Add(GetPagedRequests(MaxPages,
                        Settings.Categories,
                        "book",
                        $"&author={NewsnabifyTitle(searchCriteria.AuthorQueryTransliterated)}"));
                }
            }

            if (SupportsSearch)
            {
                pageableRequests.AddTier();

                pageableRequests.Add(GetPagedRequests(MaxPages,
                    Settings.Categories,
                    "search",
                    $"&q={NewsnabifyTitle(searchCriteria.AuthorQuery)}"));

                if (searchCriteria.AuthorQueryTransliterated != null)
                {
                    pageableRequests.Add(GetPagedRequests(MaxPages,
                        Settings.Categories,
                        "search",
                        $"&q={NewsnabifyTitle(searchCriteria.AuthorQueryTransliterated)}"));
                }
            }

            return pageableRequests;
        }

        private void AddBookPageableRequests(IndexerPageableRequestChain chain, IEnumerable<int> categories, string parameters)
        {
            chain.AddTier();

            chain.Add(GetPagedRequests(MaxPages, categories, "book", $"{parameters}"));
        }

        private void AddVariant(IndexerPageableRequestChain chain, IEnumerable<int> categories, string type, string original, string transliterated)
        {
            chain.Add(GetPagedRequests(MaxPages, categories, type, original));

            if (transliterated != original)
            {
                chain.Add(GetPagedRequests(MaxPages, categories, type, transliterated));
            }
        }

        private IEnumerable<IndexerRequest> GetPagedRequests(int maxPages, IEnumerable<int> categories, string searchType, string parameters)
        {
            if (categories.Empty())
            {
                yield break;
            }

            var categoriesQuery = string.Join(",", categories.Distinct());

            var baseUrl =
                $"{Settings.BaseUrl.TrimEnd('/')}{Settings.ApiPath.TrimEnd('/')}?t={searchType}&cat={categoriesQuery}&extended=1{Settings.AdditionalParameters}";

            if (Settings.ApiKey.IsNotNullOrWhiteSpace())
            {
                baseUrl += "&apikey=" + Settings.ApiKey;
            }

            if (PageSize == 0)
            {
                yield return new IndexerRequest($"{baseUrl}{parameters}", HttpAccept.Rss);
            }
            else
            {
                for (var page = 0; page < maxPages; page++)
                {
                    yield return new IndexerRequest($"{baseUrl}&offset={page * PageSize}&limit={PageSize}{parameters}",
                        HttpAccept.Rss);
                }
            }
        }

        private static string NewsnabifyTitle(string title)
        {
            title = title.Replace("+", " ");
            return Uri.EscapeDataString(title);
        }

        private static bool TitleQueryContainsSeries(BookSearchCriteria searchCriteria)
        {
            if (string.IsNullOrWhiteSpace(searchCriteria.SeriesName))
            {
                return false;
            }

            var mainTitle = BookSearchCriteria.GetMainSearchTitle(searchCriteria.BookTitle, searchCriteria.Author?.Name);
            if (string.IsNullOrWhiteSpace(mainTitle))
            {
                return false;
            }

            var seriesQuery = searchCriteria.SeriesQuery;
            if (string.IsNullOrWhiteSpace(seriesQuery))
            {
                return false;
            }

            var normalizedBookQuery = searchCriteria.BookQuery.Replace('+', ' ').ToLowerInvariant();
            var normalizedSeriesQuery = seriesQuery.Replace('+', ' ').ToLowerInvariant();

            return normalizedBookQuery.Contains(normalizedSeriesQuery);
        }
    }
}
