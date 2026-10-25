using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Indexers.Soulseek;
using NzbDrone.Core.Soulseek;

namespace Chaptarr.Core.Test.Soulseek
{
    [TestFixture]
    public class SlskdIndexerFixture
    {
        private sealed class FakeApi : ISlskdApiClient
        {
            public List<SlskdSearchRequest> Started { get; } = new();
            public List<Guid> Deleted { get; } = new();
            public Func<SlskdSearchRequest, SlskdSearch> Result { get; set; } = _ => SlskdFixtures.Search("search-ebook.json");
            public Exception StartError { get; set; }
            public SlskdApplicationState State { get; set; }

            public Task<SlskdSearch> StartSearchAsync(SlskdConnection connection, SlskdSearchRequest request)
            {
                if (StartError != null)
                {
                    throw StartError;
                }

                Started.Add(request);
                return Task.FromResult(new SlskdSearch { Id = request.Id });
            }

            public Task<SlskdSearch> GetSearchAsync(SlskdConnection connection, Guid id) => Task.FromResult(new SlskdSearch { Id = id, IsComplete = true });

            public Task<List<SlskdSearchResponse>> GetSearchResponsesAsync(SlskdConnection connection, Guid id) => Task.FromResult(Result(Started.Last()).Responses);

            public Task DeleteSearchAsync(SlskdConnection connection, Guid id)
            {
                Deleted.Add(id);
                return Task.CompletedTask;
            }

            public Task<SlskdApplicationState> GetApplicationAsync(SlskdConnection connection) => Task.FromResult(State);
        }

        [SetUp]
        public void SetUp()
        {
            SlskdIndexer.PollInterval = TimeSpan.FromMilliseconds(1);
        }

        private static (SlskdIndexer Indexer, FakeApi Api) Build()
        {
            var api = new FakeApi();
            var indexer = new SlskdIndexer(api, null, null, null, LogManager.CreateNullLogger())
            {
                Definition = new IndexerDefinition
                {
                    Id = 9,
                    Name = "Soulseek",
                    Priority = 50,
                    Settings = new SlskdIndexerSettings { BaseUrl = "http://slskd.example:5030", ApiKey = "k" }
                }
            };
            return (indexer, api);
        }

        private static BookSearchCriteria Criteria(string author, string title, BookMediaType mediaType, string query = null) => new()
        {
            Author = new Author { Name = author },
            Books = new List<Book> { new() { Title = title, MediaType = mediaType } },
            BookTitle = title,
            SearchTitleOverride = query
        };

        [Test]
        public async Task should_search_and_stamp_indexer_fields()
        {
            var (indexer, api) = Build();

            var releases = await indexer.Fetch(Criteria("Frank Herbert", "Dune", BookMediaType.Ebook));

            Assert.That(api.Started.Single().SearchText, Is.EqualTo("dune"));
            Assert.That(api.Started.Single().SearchTimeout, Is.EqualTo(15000));
            Assert.That(releases, Has.Count.EqualTo(3));
            Assert.That(releases.Select(r => r.DownloadProtocol), Is.All.EqualTo(DownloadProtocol.Soulseek));
            Assert.That(releases.Select(r => r.IndexerId), Is.All.EqualTo(9));
            Assert.That(releases.Select(r => r.IndexerPriority), Is.All.EqualTo(50));
            Assert.That(api.Deleted, Is.EqualTo(new[] { api.Started.Single().Id }));
        }

        [Test]
        public async Task should_also_search_the_digraph_spelling_for_umlaut_titles()
        {
            var (indexer, api) = Build();
            api.Result = _ => SlskdFixtures.Search("search-audiobook.json");

            await indexer.Fetch(Criteria("Andreas Eschbach", "Das Jesus Video", BookMediaType.Audiobook));
            Assert.That(api.Started.Select(s => s.SearchText), Is.EqualTo(new[] { "jesus video" }));

            api.Started.Clear();
            await indexer.Fetch(Criteria("Hans Müller", "Märchen für Kinder", BookMediaType.Audiobook));
            Assert.That(api.Started.Select(s => s.SearchText), Is.EqualTo(new[] { "märchen für kinder", "maerchen fuer kinder" }));
        }

        [Test]
        public async Task should_return_nothing_and_still_clean_up_when_slskd_is_busy()
        {
            var (indexer, api) = Build();
            api.StartError = new SlskdApiException(429, "busy");

            var releases = await indexer.Fetch(Criteria("Frank Herbert", "Dune", BookMediaType.Ebook));

            Assert.That(releases, Is.Empty);
            Assert.That(api.Deleted, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task should_not_search_without_a_title_and_not_support_rss()
        {
            var (indexer, api) = Build();

            Assert.That(await indexer.Fetch(Criteria("Frank Herbert", " ", BookMediaType.Ebook)), Is.Empty);
            Assert.That(await indexer.FetchRecent(), Is.Empty);
            Assert.That(api.Started, Is.Empty);
            Assert.That(indexer.Protocol, Is.EqualTo(DownloadProtocol.Soulseek));
        }

        [Test]
        public void should_validate_settings()
        {
            Assert.That(new SlskdIndexerSettings { BaseUrl = "http://slskd:5030" }.Validate().IsValid, Is.True);
            Assert.That(new SlskdIndexerSettings { BaseUrl = "ftp://slskd" }.Validate().IsValid, Is.False);
            Assert.That(new SlskdIndexerSettings { BaseUrl = "http://user:pw@slskd" }.Validate().IsValid, Is.False);
            Assert.That(new SlskdIndexerSettings { BaseUrl = "http://slskd", SearchTimeoutSeconds = 1 }.Validate().IsValid, Is.False);
        }

        [Test]
        public void should_fail_test_when_not_logged_in_and_pass_when_logged_in()
        {
            var (indexer, api) = Build();
            api.State = new SlskdApplicationState { Server = new SlskdServerState { State = "Disconnected" } };
            Assert.That(indexer.Test().IsValid, Is.False);

            api.State = new SlskdApplicationState { Server = new SlskdServerState { State = "Connected, LoggedIn" } };
            Assert.That(indexer.Test().IsValid, Is.True);
        }
    }
}
