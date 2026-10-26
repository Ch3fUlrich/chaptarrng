using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NLog;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Cache;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.Clients;
using NzbDrone.Core.Download.Clients.Sabnzbd;
using NzbDrone.Core.Download.Clients.Sabnzbd.Responses;
using NzbDrone.Core.RemotePathMappings;

namespace Chaptarr.Core.Test.Download
{
    [TestFixture]
    public class SabnzbdFileSnapshotFallbackFixture
    {
        private class TestSabnzbdProxy : ISabnzbdProxy
        {
            public SabnzbdHistory History { get; set; } = new() { Items = new List<SabnzbdHistoryItem>() };
            public SabnzbdQueue Queue { get; set; } = new() { Items = new List<SabnzbdQueueItem>() };

            public string GetBaseUrl(SabnzbdSettings settings, string relativePath = null) => "http://localhost:8080/";
            public SabnzbdAddResponse DownloadNzb(byte[] nzbData, string filename, string category, int priority, SabnzbdSettings settings) => throw new NotImplementedException();
            public void RemoveFromQueue(string id, bool deleteData, SabnzbdSettings settings) => throw new NotImplementedException();
            public void RemoveFromHistory(string id, bool deleteData, bool deletePermanently, SabnzbdSettings settings) => throw new NotImplementedException();
            public string GetVersion(SabnzbdSettings settings) => "5.0.0";
            public SabnzbdConfig GetConfig(SabnzbdSettings settings) => throw new NotImplementedException();
            public SabnzbdFullStatus GetFullStatus(SabnzbdSettings settings) => throw new NotImplementedException();
            public SabnzbdQueue GetQueue(int start, int limit, SabnzbdSettings settings) => Queue;
            public SabnzbdHistory GetHistory(int start, int limit, SabnzbdSettings settings) => History;
            public string RetryDownload(string id, SabnzbdSettings settings) => throw new NotImplementedException();
        }

        private class PassthroughRemotePathMappingService : IRemotePathMappingService
        {
            public List<RemotePathMapping> All() => throw new NotImplementedException();
            public RemotePathMapping Add(RemotePathMapping mapping) => throw new NotImplementedException();
            public void Remove(int id) => throw new NotImplementedException();
            public RemotePathMapping Get(int id) => throw new NotImplementedException();
            public RemotePathMapping Update(RemotePathMapping mapping) => throw new NotImplementedException();
            public OsPath RemapRemoteToLocal(string host, OsPath remotePath) => remotePath;
            public OsPath RemapLocalToRemote(string host, OsPath localPath) => localPath;
            public OsPath RemapRemoteToLocal(int downloadClientId, string host, OsPath remotePath) => remotePath;
            public OsPath RemapLocalToRemote(int downloadClientId, string host, OsPath localPath) => localPath;
            public RemotePathMappingTestResult Test(RemotePathMapping mapping) => throw new NotImplementedException();
        }

        private class ConfigServiceProxy : DispatchProxy
        {
            public int DownloadClientHistoryLimit { get; set; } = 60;

            protected override object Invoke(MethodInfo targetMethod, object[] args)
            {
                if (targetMethod?.Name == "get_DownloadClientHistoryLimit")
                {
                    return DownloadClientHistoryLimit;
                }

                throw new NotImplementedException($"Test proxy does not implement IConfigService.{targetMethod?.Name}");
            }
        }

        [Test]
        public void should_leave_completed_history_file_list_unset_for_disk_snapshot_fallback()
        {
            var proxy = new TestSabnzbdProxy
            {
                History = new SabnzbdHistory
                {
                    Items = new List<SabnzbdHistoryItem>
                    {
                        new()
                        {
                            Id = "SABnzbd_nzo_1",
                            Title = "Example Book",
                            Category = "audiobooks",
                            Status = SabnzbdDownloadStatus.Completed,
                            Storage = "/downloads/Example Book",
                            Size = 1234
                        }
                    }
                }
            };

            var client = new Sabnzbd(
                proxy,
                httpClient: null,
                configService: CreateConfigService(),
                diskProvider: null,
                remotePathMappingService: new PassthroughRemotePathMappingService(),
                nzbValidationService: null,
                logger: LogManager.GetCurrentClassLogger())
            {
                Definition = new DownloadClientDefinition
                {
                    Id = 1,
                    Name = "SABnzbd",
                    Settings = new SabnzbdSettings
                    {
                        Host = "localhost",
                        Port = 8080,
                        AudiobookCategory = "audiobooks",
                        EbookCategory = "ebooks"
                    }
                }
            };

            var items = client.GetItems().ToList();

            Assert.That(items, Has.Count.EqualTo(1));
            Assert.That(items[0].Status, Is.EqualTo(DownloadItemStatus.Completed));
            Assert.That(items[0].OutputPath.FullPath, Is.EqualTo("/downloads/Example Book"));
            Assert.That(items[0].FilePaths, Is.Null);
            Assert.That(items[0].FileListConfidence, Is.Null);
        }

        private static IConfigService CreateConfigService()
        {
            return DispatchProxy.Create<IConfigService, ConfigServiceProxy>();
        }

        [TestCase(SabnzbdDownloadStatus.Queued, DownloadItemStatus.Queued)]
        [TestCase(SabnzbdDownloadStatus.Downloading, DownloadItemStatus.Downloading)]
        public void should_not_synthesize_output_path_for_queue_item_without_storage(SabnzbdDownloadStatus status, DownloadItemStatus expected)
        {
            var repository = DispatchProxy.Create<IRemotePathMappingRepository, MappingRepositoryProxy>();
            ((MappingRepositoryProxy)(object)repository).Mappings = new List<RemotePathMapping>
            {
                new() { DownloadClientId = 0, Host = "sabhost", RemotePath = "/remote/usenet/completed/", LocalPath = "/downloads/" }
            };
            var definition = new DownloadClientDefinition
            {
                Id = 1,
                Name = "SABnzbd",
                Settings = new SabnzbdSettings { Host = "sabhost", AudiobookCategory = "audiobooks", EbookCategory = "ebooks" }
            };
            var clientRepository = DispatchProxy.Create<IDownloadClientRepository, MappingRepositoryProxy>();
            ((MappingRepositoryProxy)(object)clientRepository).Clients = new List<DownloadClientDefinition> { definition };
            var mappings = new RemotePathMappingService(clientRepository, repository, null, new CacheManager(), LogManager.GetCurrentClassLogger());
            var proxy = new TestSabnzbdProxy
            {
                Queue = new SabnzbdQueue
                {
                    Items = new List<SabnzbdQueueItem>
                    {
                        new() { Id = "SABnzbd_nzo_1", Title = "Author - Title", Category = "audiobooks", Status = status }
                    }
                }
            };
            var client = new Sabnzbd(proxy, null, CreateConfigService(), null, mappings, null, LogManager.GetCurrentClassLogger())
            {
                Definition = definition
            };

            var item = client.GetItems().Single();

            Assert.That(item.Status, Is.EqualTo(expected));
            Assert.That(item.OutputPath.IsEmpty, Is.True);
        }

        private class MappingRepositoryProxy : DispatchProxy
        {
            public List<RemotePathMapping> Mappings { get; set; }
            public List<DownloadClientDefinition> Clients { get; set; }

            protected override object Invoke(MethodInfo targetMethod, object[] args)
            {
                if (targetMethod.Name == "All")
                {
                    return Mappings;
                }

                if (targetMethod.Name == "Find")
                {
                    return Clients.SingleOrDefault(c => c.Id == (int)args[0]);
                }

                throw new NotImplementedException(targetMethod.Name);
            }
        }

        public class ImportHandoffProxy : DispatchProxy
        {
            public Func<MethodInfo, object[], object> Handler { get; set; }

            protected override object Invoke(MethodInfo targetMethod, object[] args) => Handler(targetMethod, args);
        }

        private static T CreateHandoffProxy<T>(Func<MethodInfo, object[], object> handler) where T : class
        {
            var proxy = DispatchProxy.Create<T, ImportHandoffProxy>();
            ((ImportHandoffProxy)(object)proxy).Handler = handler;
            return proxy;
        }

        [TestCase(1, "sabhost", "sabhost", "")]
        [TestCase(1, "sabhost", "sabhost", "/Author, Name - Title.epub")]
        [TestCase(1, "sabhost", "sabhost", "/Author, Name - Title.pdf")]
        [TestCase(5, "sabhost", "sabhost", "/")]
        [TestCase(1, "SABHOST", "sabhost", "/")]
        [TestCase(5, "sabhost", "SABHOST", "")]
        [TestCase(1, "sabhost:8080", "sabhost", "/")]
        [TestCase(5, "sabhost", "sabhost:8080", "/")]
        public void should_map_completed_history_with_all_client_host_mappings(int clientId, string clientHost, string mappingHost, string trailingSlash)
        {
            var clients = Enumerable.Range(1, 5).Select(id => new DownloadClientDefinition
            {
                Id = id,
                Name = "SABnzbd",
                Settings = new SabnzbdSettings { Host = clientHost, AudiobookCategory = "audiobooks", EbookCategory = "ebooks" }
            }).ToList();
            var repository = DispatchProxy.Create<IRemotePathMappingRepository, MappingRepositoryProxy>();
            ((MappingRepositoryProxy)(object)repository).Mappings = new List<RemotePathMapping>
            {
                new() { Id = 1, DownloadClientId = 0, Host = mappingHost, RemotePath = "/remote/usenet/completed/", LocalPath = "/downloads/" },
                new() { Id = 2, DownloadClientId = 0, Host = mappingHost, RemotePath = "/remote/usenet/other-test/", LocalPath = "/test/" }
            };
            var clientRepository = DispatchProxy.Create<IDownloadClientRepository, MappingRepositoryProxy>();
            ((MappingRepositoryProxy)(object)clientRepository).Clients = clients;
            var mappings = new RemotePathMappingService(clientRepository, repository, null, new CacheManager(), LogManager.GetCurrentClassLogger());
            var proxy = new TestSabnzbdProxy
            {
                History = new SabnzbdHistory
                {
                    Items = new List<SabnzbdHistoryItem>
                    {
                        new() { Id = "SABnzbd_nzo_1", Title = "Author,.Name.-.Title", Category = "audiobooks", Status = SabnzbdDownloadStatus.Completed,
                            Storage = "/remote/usenet/completed/audiobooks/Author,.Name.-.Title" + trailingSlash }
                    }
                }
            };
            var client = new Sabnzbd(proxy, null, CreateConfigService(), null, mappings, null, LogManager.GetCurrentClassLogger())
            {
                Definition = clients.Single(c => c.Id == clientId)
            };

            var item = client.GetItems().Single();

            Assert.That(item.OutputPath.FullPath.TrimEnd('/'), Is.EqualTo("/downloads/audiobooks/Author,.Name.-.Title"));

            string importedPath = null;
            var provider = CreateHandoffProxy<IProvideDownloadClient>((method, args) => client);
            var importer = CreateHandoffProxy<NzbDrone.Core.MediaFiles.IDownloadedBooksImportService>((method, args) =>
            {
                Assert.That(method.Name, Is.EqualTo("ProcessPath"));
                importedPath = (string)args[0];
                return new List<NzbDrone.Core.MediaFiles.BookImport.ImportResult>();
            });
            var history = CreateHandoffProxy<NzbDrone.Core.History.IHistoryService>((method, args) => new List<NzbDrone.Core.History.EntityHistory>());
            string incompleteOutputPath = null;
            var events = CreateHandoffProxy<NzbDrone.Core.Messaging.Events.IEventAggregator>((method, args) =>
            {
                if (args[0] is NzbDrone.Core.MediaFiles.Events.BookImportIncompleteEvent incomplete)
                {
                    // DownloadHistoryService uses this DownloadItem path as SourceTitle.
                    incompleteOutputPath = incomplete.TrackedDownload.DownloadItem.OutputPath.ToString();
                }

                return null;
            });
            var resolver = CreateHandoffProxy<IDownloadImportModeResolver>((method, args) => args[0]);
            var completed = new CompletedDownloadService(events, history, new ProvideImportItemService(provider), importer,
                resolver, null, NoopFailedDownloadService.Instance, LogManager.GetCurrentClassLogger(), NoopDownloadClientFileSnapshotService.Instance);
            var tracked = new NzbDrone.Core.Download.TrackedDownloads.TrackedDownload
            {
                DownloadItem = item,
                DownloadClient = clientId,
                State = NzbDrone.Core.Download.TrackedDownloads.TrackedDownloadState.ImportPending
            };

            completed.Import(tracked);

            Assert.That(importedPath, Is.EqualTo(item.OutputPath.FullPath));
            Assert.That(importedPath, Does.StartWith("/downloads/").And.Not.Contain("/remote/"));
            Assert.That(incompleteOutputPath, Is.EqualTo(importedPath));
        }
    }
}
