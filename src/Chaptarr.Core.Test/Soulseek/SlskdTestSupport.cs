using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
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
}
