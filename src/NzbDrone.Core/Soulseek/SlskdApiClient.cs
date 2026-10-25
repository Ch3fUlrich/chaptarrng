using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using NzbDrone.Common.Http;

namespace NzbDrone.Core.Soulseek
{
    public interface ISlskdApiClient
    {
        Task<SlskdSearch> StartSearchAsync(SlskdConnection connection, SlskdSearchRequest request);
        Task<SlskdSearch> GetSearchAsync(SlskdConnection connection, Guid id);
        Task<List<SlskdSearchResponse>> GetSearchResponsesAsync(SlskdConnection connection, Guid id);
        Task DeleteSearchAsync(SlskdConnection connection, Guid id);
        Task<SlskdApplicationState> GetApplicationAsync(SlskdConnection connection);
        Task<SlskdBatchResponse> EnqueueBatchAsync(SlskdConnection connection, SlskdBatchRequest request);
        Task<SlskdBatch> GetBatchAsync(SlskdConnection connection, Guid id);
        Task<List<SlskdUserTransfers>> GetDownloadsAsync(SlskdConnection connection);
        Task CancelDownloadAsync(SlskdConnection connection, string username, Guid transferId, bool remove);
    }

    public class SlskdApiClient : ISlskdApiClient
    {
        internal static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly IHttpClient _httpClient;

        public SlskdApiClient(IHttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public Task<SlskdSearch> StartSearchAsync(SlskdConnection connection, SlskdSearchRequest request)
        {
            return SendAsync<SlskdSearch>(connection, HttpMethod.Post, "searches", JsonSerializer.Serialize(request, JsonOptions));
        }

        public Task<SlskdSearch> GetSearchAsync(SlskdConnection connection, Guid id)
        {
            return SendAsync<SlskdSearch>(connection, HttpMethod.Get, $"searches/{id}");
        }

        public Task<List<SlskdSearchResponse>> GetSearchResponsesAsync(SlskdConnection connection, Guid id)
        {
            return SendAsync<List<SlskdSearchResponse>>(connection, HttpMethod.Get, $"searches/{id}/responses");
        }

        public async Task DeleteSearchAsync(SlskdConnection connection, Guid id)
        {
            try
            {
                await SendAsync<object>(connection, HttpMethod.Delete, $"searches/{id}");
            }
            catch (SlskdApiException ex) when (ex.StatusCode == (int)HttpStatusCode.NotFound)
            {
            }
        }

        public Task<SlskdApplicationState> GetApplicationAsync(SlskdConnection connection)
        {
            return SendAsync<SlskdApplicationState>(connection, HttpMethod.Get, "application");
        }

        public Task<SlskdBatchResponse> EnqueueBatchAsync(SlskdConnection connection, SlskdBatchRequest request)
        {
            return SendAsync<SlskdBatchResponse>(connection, HttpMethod.Post, "transfers/downloads/batches", JsonSerializer.Serialize(request, JsonOptions));
        }

        public async Task<SlskdBatch> GetBatchAsync(SlskdConnection connection, Guid id)
        {
            try
            {
                return await SendAsync<SlskdBatch>(connection, HttpMethod.Get, $"transfers/downloads/batches/{id}");
            }
            catch (SlskdApiException ex) when (ex.StatusCode == (int)HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task<List<SlskdUserTransfers>> GetDownloadsAsync(SlskdConnection connection)
        {
            return await SendAsync<List<SlskdUserTransfers>>(connection, HttpMethod.Get, "transfers/downloads") ?? new List<SlskdUserTransfers>();
        }

        public async Task CancelDownloadAsync(SlskdConnection connection, string username, Guid transferId, bool remove)
        {
            try
            {
                await SendAsync<object>(connection, HttpMethod.Delete, $"transfers/downloads/{Uri.EscapeDataString(username)}/{transferId}?remove={(remove ? "true" : "false")}");
            }
            catch (SlskdApiException ex) when (ex.StatusCode == (int)HttpStatusCode.NotFound)
            {
            }
        }

        internal async Task<T> SendAsync<T>(SlskdConnection connection, HttpMethod method, string resource, string body = null)
        {
            var baseUrl = (connection?.BaseUrl ?? string.Empty).Trim().TrimEnd('/');
            if (baseUrl.Length == 0)
            {
                throw new SlskdApiException(0, "slskd URL is not configured.");
            }

            var request = new HttpRequest($"{baseUrl}/api/v0/{resource}", HttpAccept.Json)
            {
                Method = method,
                SuppressHttpError = true,
                RequestTimeout = TimeSpan.FromSeconds(30)
            };

            if (!string.IsNullOrWhiteSpace(connection.ApiKey))
            {
                request.Headers.Set("X-API-Key", connection.ApiKey.Trim());
            }

            if (body != null)
            {
                request.Headers.ContentType = "application/json";
                request.SetContent(body);
            }

            var response = await _httpClient.ExecuteAsync(request);

            if ((int)response.StatusCode >= 400)
            {
                var detail = response.Content;
                if (detail != null && detail.Length > 200)
                {
                    detail = detail.Substring(0, 200);
                }

                throw new SlskdApiException((int)response.StatusCode, $"slskd returned HTTP {(int)response.StatusCode}: {detail}");
            }

            if (typeof(T) == typeof(object) || string.IsNullOrWhiteSpace(response.Content))
            {
                return default;
            }

            try
            {
                return JsonSerializer.Deserialize<T>(response.Content, JsonOptions);
            }
            catch (JsonException ex)
            {
                throw new SlskdApiException((int)response.StatusCode, $"slskd returned an unreadable response: {ex.Message}");
            }
        }
    }
}
