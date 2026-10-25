using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using NLog;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.Clients;
using NzbDrone.Core.Download.Clients.Slskd;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Soulseek;

namespace Chaptarr.Core.Test.Soulseek
{
    [TestFixture]
    public class SlskdDownloadClientFixture
    {
        private const string BatchA = "11111111-1111-4111-8111-111111111111";
        private const string BatchB = "22222222-2222-4222-8222-222222222222";
        private const string BatchD = "44444444-4444-4444-8444-444444444444";
        private const string BatchE = "55555555-5555-4555-8555-555555555555";

        private string _dir;
        private FakeSlskdApi _api;
        private SlskdDownloadClient _client;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "slskd-client-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _api = new FakeSlskdApi
            {
                Downloads = JsonSerializer.Deserialize<List<SlskdUserTransfers>>(SlskdFixtures.Load("downloads.json"), SlskdApiClient.JsonOptions),
                State = JsonSerializer.Deserialize<SlskdApplicationState>(SlskdFixtures.Load("application.json"), SlskdApiClient.JsonOptions)
            };

            foreach (var pair in JsonSerializer.Deserialize<Dictionary<string, SlskdBatch>>(SlskdFixtures.Load("batches.json"), SlskdApiClient.JsonOptions))
            {
                _api.Batches[Guid.Parse(pair.Key)] = pair.Value;
            }

            _client = Build(_api, _dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, true);
            }
        }

        private static SlskdDownloadClient Build(FakeSlskdApi api, string dir)
        {
            return new SlskdDownloadClient(api, null, new SlskdTestDiskProvider(), null, LogManager.CreateNullLogger())
            {
                Definition = new DownloadClientDefinition
                {
                    Id = 5,
                    Name = "slskd",
                    Protocol = DownloadProtocol.Soulseek,
                    RemoveCompletedDownloads = true,
                    Settings = new SlskdDownloadClientSettings { BaseUrl = "http://" + Guid.NewGuid().ToString("N") + ".example:5030", DownloadDirectory = dir }
                }
            };
        }

        private static RemoteBook Release(string url, DownloadProtocol protocol = DownloadProtocol.Soulseek) => new()
        {
            Release = new ReleaseInfo { Title = "Frank Herbert - Dune [epub]", DownloadUrl = url, DownloadProtocol = protocol }
        };

        private static string TokenUrl(params string[] names)
        {
            return SlskdDownloadToken.ToUrl(new SlskdDownloadToken
            {
                Username = "peer_two",
                Directory = "@@x\\Book",
                Files = names.Select(n => new SlskdTokenFile { Filename = "@@x\\Book\\" + n, Size = 1000 }).ToList()
            });
        }

        [Test]
        public async Task should_enqueue_one_batch_with_deterministic_destination_and_owner_marker()
        {
            var id = await _client.Download(Release(TokenUrl("Dune.epub")), null);

            var request = _api.Enqueued.Single();
            Assert.That(Guid.Parse(id), Is.EqualTo(Guid.Parse(request.Id)));
            Assert.That(request.Username, Is.EqualTo("peer_two"));
            Assert.That(request.Files.Single().Filename, Is.EqualTo("@@x\\Book\\Dune.epub"));
            Assert.That(request.Options.Destination, Is.EqualTo("chaptarr-" + Guid.Parse(id).ToString("N")));
            Assert.That(request.Options.ExternalId, Is.EqualTo("chaptarr|5|Frank Herbert - Dune [epub]"));
        }

        [Test]
        public void should_refuse_foreign_protocol_and_unsafe_tokens()
        {
            Assert.ThrowsAsync<DownloadClientException>(() => _client.Download(Release(TokenUrl("a.epub"), DownloadProtocol.Usenet), null));
            Assert.ThrowsAsync<ReleaseDownloadException>(() => _client.Download(Release("https://example.invalid/x"), null));
            Assert.ThrowsAsync<ReleaseDownloadException>(() => _client.Download(Release(TokenUrl("..\\..\\evil.epub")), null));
            Assert.That(_api.Enqueued, Is.Empty);
        }

        [Test]
        public void should_fail_and_cancel_when_slskd_cannot_queue_every_file()
        {
            _api.EnqueueResponse = JsonSerializer.Deserialize<SlskdBatchResponse>(SlskdFixtures.Load("enqueue-partial.json"), SlskdApiClient.JsonOptions);

            var ex = Assert.ThrowsAsync<ReleaseDownloadException>(() => _client.Download(Release(TokenUrl("Dune.epub")), null));

            Assert.That(ex.Message, Does.Contain("File not shared"));
            Assert.That(_api.Cancelled, Is.Not.Empty.And.All.Matches<(string User, Guid Id, bool Remove)>(c => c.Remove));
        }

        [Test]
        public void should_translate_api_errors_into_release_download_exceptions()
        {
            _api.EnqueueError = new SlskdApiException(429, "busy");

            var ex = Assert.ThrowsAsync<ReleaseDownloadException>(() => _client.Download(Release(TokenUrl("Dune.epub")), null));

            Assert.That(ex.Message, Does.Contain("busy"));
        }

        [Test]
        public void should_report_only_our_batches_with_aggregated_state()
        {
            Directory.CreateDirectory(Path.Combine(_dir, "chaptarr-" + BatchB.Replace("-", string.Empty)));

            var items = _client.GetItems().ToDictionary(i => i.DownloadId);

            Assert.That(items.Keys, Is.EquivalentTo(new[] { BatchA, BatchB, BatchD, BatchE }));
            Assert.That(items[BatchA].Status, Is.EqualTo(DownloadItemStatus.Downloading));
            Assert.That(items[BatchA].Title, Is.EqualTo("Author - Title [mp3]"));
            Assert.That(items[BatchA].TotalSize, Is.EqualTo(60000000));
            Assert.That(items[BatchA].RemainingSize, Is.EqualTo(20000000));
            Assert.That(items[BatchA].RemainingTime, Is.EqualTo(TimeSpan.FromSeconds(20)));
            Assert.That(items[BatchB].Status, Is.EqualTo(DownloadItemStatus.Completed));
            Assert.That(items[BatchB].OutputPath.FullPath, Is.EqualTo(Path.Combine(_dir, "chaptarr-" + BatchB.Replace("-", string.Empty))));
            Assert.That(items[BatchB].CanBeRemoved, Is.True);
            Assert.That(items[BatchB].CanMoveFiles, Is.False);
            Assert.That(items[BatchD].Status, Is.EqualTo(DownloadItemStatus.Failed));
            Assert.That(items[BatchD].Message, Does.Contain("timed out"));
            Assert.That(items[BatchE].Status, Is.EqualTo(DownloadItemStatus.Queued));
            Assert.That(items[BatchE].Message, Does.Contain("7"));
        }

        [Test]
        public void should_warn_instead_of_completing_when_the_download_folder_is_missing()
        {
            var items = _client.GetItems().ToDictionary(i => i.DownloadId);

            Assert.That(items[BatchB].Status, Is.EqualTo(DownloadItemStatus.Warning));
            Assert.That(items[BatchB].Message, Does.Contain("Download Directory"));
        }

        [Test]
        public void should_resolve_each_batch_owner_once()
        {
            _client.GetItems().ToList();
            var lookups = _api.BatchLookups;
            _client.GetItems().ToList();

            Assert.That(lookups, Is.EqualTo(5));
            Assert.That(_api.BatchLookups, Is.EqualTo(lookups));
        }

        [Test]
        public void should_list_imported_files_authoritatively()
        {
            var folder = Path.Combine(_dir, "chaptarr-" + BatchB.Replace("-", string.Empty));
            Directory.CreateDirectory(Path.Combine(folder, "CD1"));
            File.WriteAllText(Path.Combine(folder, "CD1", "a.mp3"), "x");
            File.WriteAllText(Path.Combine(folder, "b.epub"), "y");

            var item = _client.GetItems().Single(i => i.DownloadId == BatchB);
            var import = _client.GetImportItem(item, null);

            Assert.That(import.FilePaths, Is.EqualTo(new[] { Path.Combine(folder, "CD1", "a.mp3"), Path.Combine(folder, "b.epub") }));
            Assert.That(import.FileListConfidence, Is.EqualTo(DownloadClientFileListConfidence.Authoritative));
        }

        [Test]
        public void should_cancel_remove_transfers_and_delete_only_its_own_folder()
        {
            var own = Path.Combine(_dir, "chaptarr-" + BatchB.Replace("-", string.Empty));
            var foreign = Path.Combine(_dir, "someone-elses-folder");
            Directory.CreateDirectory(own);
            Directory.CreateDirectory(foreign);
            File.WriteAllText(Path.Combine(own, "a.epub"), "x");

            var item = _client.GetItems().Single(i => i.DownloadId == BatchB);
            _client.RemoveItem(item, true);

            Assert.That(_api.Cancelled.Select(c => (c.User, c.Remove)), Is.EqualTo(new[] { ("peer_two", true) }));
            Assert.That(Directory.Exists(own), Is.False);
            Assert.That(Directory.Exists(foreign), Is.True);
        }

        [Test]
        public void should_keep_data_when_not_asked_to_delete()
        {
            var own = Path.Combine(_dir, "chaptarr-" + BatchB.Replace("-", string.Empty));
            Directory.CreateDirectory(own);

            _client.RemoveItem(new DownloadClientItem { DownloadId = BatchB }, false);

            Assert.That(Directory.Exists(own), Is.True);
            Assert.That(_api.Cancelled, Has.Count.EqualTo(1));
        }

        [Test]
        public void should_validate_version_login_and_directory_in_test()
        {
            Assert.That(_client.Test().IsValid, Is.True);

            _api.State.Version.Current = "0.25.1.0";
            Assert.That(_client.Test().Errors.Single().ErrorMessage, Does.Contain("0.26.0"));

            _api.State.Version.Current = "0.26.2.0";
            _api.State.Server.State = "Disconnected";
            Assert.That(_client.Test().Errors.Single().ErrorMessage, Does.Contain("not logged in"));

            _api.State.Server.State = "Connected, LoggedIn";
            Directory.Delete(_dir, true);
            Assert.That(_client.Test().Errors.Single().PropertyName, Is.EqualTo(nameof(SlskdDownloadClientSettings.DownloadDirectory)));
        }

        [Test]
        public void should_blame_the_api_key_for_401()
        {
            _api.ApplicationError = new SlskdApiException(401, "unauthorized");

            Assert.That(_client.Test().Errors.Single().PropertyName, Is.EqualTo(nameof(SlskdDownloadClientSettings.ApiKey)));
        }

        [TestCase("Completed, Succeeded", SlskdTransferPhase.Succeeded)]
        [TestCase("Completed, Cancelled", SlskdTransferPhase.Failed)]
        [TestCase("Completed, TimedOut", SlskdTransferPhase.Failed)]
        [TestCase("Completed, Errored", SlskdTransferPhase.Failed)]
        [TestCase("Completed, Rejected", SlskdTransferPhase.Failed)]
        [TestCase("InProgress", SlskdTransferPhase.InProgress)]
        [TestCase("Queued, Remotely", SlskdTransferPhase.Queued)]
        [TestCase("Queued, Locally", SlskdTransferPhase.Queued)]
        [TestCase("Requested", SlskdTransferPhase.Queued)]
        [TestCase("Initializing", SlskdTransferPhase.Queued)]
        [TestCase("", SlskdTransferPhase.Queued)]
        [TestCase(null, SlskdTransferPhase.Queued)]
        public void should_map_transfer_states(string state, SlskdTransferPhase expected)
        {
            Assert.That(SlskdTransferState.Parse(state), Is.EqualTo(expected));
        }

        [Test]
        public void should_warn_for_batches_without_transfers_and_fail_on_any_failed_file()
        {
            Assert.That(SlskdTransferState.Aggregate(new List<SlskdTransfer>()).Status, Is.EqualTo(DownloadItemStatus.Warning));

            var mixed = new List<SlskdTransfer>
            {
                new() { State = "Completed, Succeeded" },
                new() { State = "Completed, Rejected", Exception = "peer said no" }
            };

            Assert.That(SlskdTransferState.Aggregate(mixed).Status, Is.EqualTo(DownloadItemStatus.Failed));
        }
    }
}
