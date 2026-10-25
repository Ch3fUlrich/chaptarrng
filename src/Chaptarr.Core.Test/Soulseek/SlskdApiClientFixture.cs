using System;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using NzbDrone.Core.Soulseek;

namespace Chaptarr.Core.Test.Soulseek
{
    [TestFixture]
    public class SlskdApiClientFixture
    {
        private readonly SlskdConnection _connection = new() { BaseUrl = "http://slskd.example:5030/", ApiKey = "test-key" };

        [Test]
        public async Task should_post_search_with_api_key_and_camel_case_body()
        {
            var http = new FakeSlskdHttpClient { Handler = _ => (HttpStatusCode.OK, SlskdFixtures.Load("search-ebook.json")) };
            var client = new SlskdApiClient(http);
            var id = Guid.NewGuid();

            var search = await client.StartSearchAsync(_connection, new SlskdSearchRequest { Id = id, SearchText = "dune", SearchTimeout = 15000, FilterResponses = true });

            var request = http.Requests.Single();
            Assert.That(request.Url.FullUri, Is.EqualTo("http://slskd.example:5030/api/v0/searches"));
            Assert.That(request.Method, Is.EqualTo(System.Net.Http.HttpMethod.Post));
            Assert.That(request.Headers.Get("X-API-Key"), Is.EqualTo("test-key"));
            using var body = JsonDocument.Parse(request.ContentData);
            Assert.That(body.RootElement.GetProperty("searchText").GetString(), Is.EqualTo("dune"));
            Assert.That(body.RootElement.GetProperty("searchTimeout").GetInt32(), Is.EqualTo(15000));
            Assert.That(body.RootElement.GetProperty("id").GetGuid(), Is.EqualTo(id));
            Assert.That(search.IsComplete, Is.True);
            Assert.That(search.Responses, Has.Count.EqualTo(3));
        }

        [Test]
        public async Task should_read_responses_and_application_state()
        {
            var http = new FakeSlskdHttpClient
            {
                Handler = request => request.Url.FullUri.EndsWith("/application")
                    ? (HttpStatusCode.OK, SlskdFixtures.Load("application.json"))
                    : (HttpStatusCode.OK, JsonSerializer.Serialize(SlskdFixtures.Search("search-audiobook.json").Responses))
            };
            var client = new SlskdApiClient(http);

            var responses = await client.GetSearchResponsesAsync(_connection, Guid.NewGuid());
            var state = await client.GetApplicationAsync(_connection);

            Assert.That(responses, Has.Count.EqualTo(4));
            Assert.That(responses[0].Files[0].Filename, Does.Contain("Eschbach"));
            Assert.That(state.Version.Current, Is.EqualTo("0.26.2.0"));
            Assert.That(state.Server.IsLoggedIn, Is.True);
        }

        [TestCase(HttpStatusCode.TooManyRequests)]
        [TestCase(HttpStatusCode.Unauthorized)]
        [TestCase(HttpStatusCode.Conflict)]
        public void should_raise_api_exception_with_status_for_http_errors(HttpStatusCode status)
        {
            var http = new FakeSlskdHttpClient { Handler = _ => (status, "nope") };
            var client = new SlskdApiClient(http);

            var ex = Assert.ThrowsAsync<SlskdApiException>(() => client.GetSearchAsync(_connection, Guid.NewGuid()));

            Assert.That(ex.StatusCode, Is.EqualTo((int)status));
        }

        [Test]
        public void should_report_unreadable_json_and_missing_url()
        {
            var client = new SlskdApiClient(new FakeSlskdHttpClient { Handler = _ => (HttpStatusCode.OK, "<html>") });

            Assert.ThrowsAsync<SlskdApiException>(() => client.GetApplicationAsync(_connection));
            Assert.ThrowsAsync<SlskdApiException>(() => client.GetApplicationAsync(new SlskdConnection()));
        }

        [Test]
        public async Task should_ignore_404_when_deleting_a_search()
        {
            var client = new SlskdApiClient(new FakeSlskdHttpClient { Handler = _ => (HttpStatusCode.NotFound, string.Empty) });

            await client.DeleteSearchAsync(_connection, Guid.NewGuid());
        }
    }
}
