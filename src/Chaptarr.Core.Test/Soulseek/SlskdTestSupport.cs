using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Http;
using NzbDrone.Core.Soulseek;

namespace Chaptarr.Core.Test.Soulseek
{
    internal static class SlskdFixtures
    {
        public static string Load(string name)
        {
            var assembly = typeof(SlskdFixtures).Assembly;
            var resource = assembly.GetManifestResourceNames().Single(n => n.EndsWith("." + name, StringComparison.Ordinal));
            using var reader = new StreamReader(assembly.GetManifestResourceStream(resource));
            return reader.ReadToEnd();
        }

        public static SlskdSearch Search(string name)
        {
            return JsonSerializer.Deserialize<SlskdSearch>(Load(name), SlskdApiClient.JsonOptions);
        }
    }

    internal class FakeSlskdHttpClient : IHttpClient
    {
        public List<HttpRequest> Requests { get; } = new();
        public Func<HttpRequest, (HttpStatusCode Status, string Body)> Handler { get; set; } = _ => (HttpStatusCode.OK, "{}");

        public Task<HttpResponse> ExecuteAsync(HttpRequest request)
        {
            Requests.Add(request);
            var (status, body) = Handler(request);
            return Task.FromResult(new HttpResponse(request, new HttpHeader(), body, status));
        }

        public HttpResponse Execute(HttpRequest request) => throw new NotSupportedException();
        public void DownloadFile(string url, string fileName, string userAgent = null) => throw new NotSupportedException();
        public HttpResponse Get(HttpRequest request) => throw new NotSupportedException();
        public HttpResponse<T> Get<T>(HttpRequest request)
            where T : new() => throw new NotSupportedException();
        public HttpResponse Head(HttpRequest request) => throw new NotSupportedException();
        public HttpResponse Post(HttpRequest request) => throw new NotSupportedException();
        public HttpResponse<T> Post<T>(HttpRequest request)
            where T : new() => throw new NotSupportedException();
        public Task DownloadFileAsync(string url, string fileName, string userAgent = null) => throw new NotSupportedException();
        public Task<HttpResponse> GetAsync(HttpRequest request) => throw new NotSupportedException();
        public Task<HttpResponse<T>> GetAsync<T>(HttpRequest request)
            where T : new() => throw new NotSupportedException();
        public Task<HttpResponse> HeadAsync(HttpRequest request) => throw new NotSupportedException();
        public Task<HttpResponse> PostAsync(HttpRequest request) => throw new NotSupportedException();
        public Task<HttpResponse<T>> PostAsync<T>(HttpRequest request)
            where T : new() => throw new NotSupportedException();
    }

    internal class FakeSlskdApi : ISlskdApiClient
    {
        public List<SlskdSearchRequest> Started { get; } = new();
        public List<Guid> Deleted { get; } = new();
        public Func<SlskdSearchRequest, SlskdSearch> Result { get; set; } = _ => SlskdFixtures.Search("search-ebook.json");
        public Exception StartError { get; set; }
        public SlskdApplicationState State { get; set; }
        public Exception ApplicationError { get; set; }

        public List<SlskdBatchRequest> Enqueued { get; } = new();
        public SlskdBatchResponse EnqueueResponse { get; set; } = new();
        public Exception EnqueueError { get; set; }
        public List<SlskdUserTransfers> Downloads { get; set; } = new();
        public Dictionary<Guid, SlskdBatch> Batches { get; } = new();
        public int BatchLookups { get; private set; }
        public List<(string User, Guid Id, bool Remove)> Cancelled { get; } = new();

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

        public Task<SlskdApplicationState> GetApplicationAsync(SlskdConnection connection)
        {
            if (ApplicationError != null)
            {
                throw ApplicationError;
            }

            return Task.FromResult(State);
        }

        public Task<SlskdBatchResponse> EnqueueBatchAsync(SlskdConnection connection, SlskdBatchRequest request)
        {
            if (EnqueueError != null)
            {
                throw EnqueueError;
            }

            Enqueued.Add(request);
            var batch = new SlskdBatch
            {
                Id = Guid.Parse(request.Id),
                Username = request.Username,
                Options = request.Options,
                Transfers = request.Files.Select(f => new SlskdTransfer { Id = Guid.NewGuid(), Filename = f.Filename, Size = f.Size, State = "Queued, Remotely" }).ToList()
            };
            Batches[batch.Id] = batch;
            EnqueueResponse.Batch = batch;
            return Task.FromResult(EnqueueResponse);
        }

        public Task<SlskdBatch> GetBatchAsync(SlskdConnection connection, Guid id)
        {
            BatchLookups++;
            return Task.FromResult(Batches.TryGetValue(id, out var batch) ? batch : null);
        }

        public Task<List<SlskdUserTransfers>> GetDownloadsAsync(SlskdConnection connection) => Task.FromResult(Downloads);

        public Task CancelDownloadAsync(SlskdConnection connection, string username, Guid transferId, bool remove)
        {
            Cancelled.Add((username, transferId, remove));
            return Task.CompletedTask;
        }
    }

    internal sealed class SlskdTestDiskProvider : DiskProviderBase
    {
        public SlskdTestDiskProvider()
            : base(new System.IO.Abstractions.FileSystem())
        {
        }

        public override long? GetAvailableSpace(string path) => null;
        public override void InheritFolderPermissions(string filename) { }
        public override void SetEveryonePermissions(string filename) { }
        public override void SetFilePermissions(string path, string mask, string group) { }
        public override void SetPermissions(string path, string mask, string group) { }
        public override void CopyPermissions(string sourcePath, string targetPath) { }
        public override bool TryCreateHardLink(string source, string destination) => false;
        public override long? GetTotalSize(string path) => null;
    }
}
