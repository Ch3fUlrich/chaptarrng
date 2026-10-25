using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using FluentValidation.Results;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;
using NzbDrone.Common.Http;

namespace NzbDrone.Core.Notifications.BookLore
{
    public interface IBookLoreProxy
    {
        ValidationFailure Test(BookLoreSettings settings);

        /// <summary>Uploads the files; throws <see cref="AggregateException"/> for ANY failure (login or upload) after
        /// every file was attempted, so the caller's notification status records it. Source files are never touched.</summary>
        void UploadFiles(BookLoreSettings settings, IEnumerable<string> filePaths);
    }

    public class BookLoreProxy : IBookLoreProxy
    {
        private readonly IHttpClient _httpClient;
        private readonly Logger _logger;

        public BookLoreProxy(IHttpClient httpClient, Logger logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public ValidationFailure Test(BookLoreSettings settings)
        {
            try
            {
                GetAccessToken(settings);
                return null;
            }
            catch (HttpException ex) when (IsCredentialRejection(ex))
            {
                _logger.Warn(ex, "BookLore rejected the configured credentials at {0}", settings.BaseUrl);
                return new ValidationFailure(nameof(settings.Password), "BookLore rejected the username or password");
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Unable to connect to BookLore at {0}", settings.BaseUrl);
                return new ValidationFailure(string.Empty, "Unable to connect to BookLore. Check the URL, credentials, and logs.");
            }
        }

        public void UploadFiles(BookLoreSettings settings, IEnumerable<string> filePaths)
        {
            var paths = filePaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (paths.Count == 0)
            {
                return;
            }

            string accessToken;
            try
            {
                accessToken = GetAccessToken(settings);
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "BookLore login failed; skipping {0} upload(s)", paths.Count);
                throw new AggregateException("BookLore login failed; nothing was uploaded.", ex);
            }

            // One bad file must not stop the remaining uploads. Failures are
            // collected and thrown together so the notification status (and the
            // health check) still records the failure.
            var failures = new List<Exception>();

            // After one file failed twice the server is evidently down: later files get a single attempt without
            // a delay, so a failing BookLore cannot stall the import thread for (files x RetryDelay).
            var retriesAllowed = true;

            foreach (var path in paths)
            {
                try
                {
                    UploadWithRetry(settings, ref accessToken, path, ref retriesAllowed);
                }
                catch (Exception ex)
                {
                    _logger.Warn(ex, "BookLore upload failed for {0}", Path.GetFileName(path));
                    failures.Add(ex);
                }
            }

            if (failures.Count > 0)
            {
                throw new AggregateException($"BookLore upload failed for {failures.Count} of {paths.Count} file(s).", failures);
            }
        }

        private void UploadWithRetry(BookLoreSettings settings, ref string accessToken, string path, ref bool retriesAllowed)
        {
            try
            {
                UploadFile(settings, accessToken, path);
                return;
            }
            catch (Exception ex) when (retriesAllowed && IsRetryableUploadFailure(ex))
            {
                _logger.Warn("BookLore upload of {0} failed ({1}); retrying once", Path.GetFileName(path), ex.Message);

                if (IsUnauthorized(ex))
                {
                    // The token expired during a long batch: log in again and use the new token for the rest.
                    accessToken = GetAccessToken(settings);
                }
                else if (RetryDelay > TimeSpan.Zero)
                {
                    Thread.Sleep(RetryDelay);
                }
            }

            try
            {
                UploadFile(settings, accessToken, path);
            }
            catch
            {
                retriesAllowed = false;
                throw;
            }
        }

        /// <summary>Pause before the single retry of a failed upload.</summary>
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(2);

        private static bool IsUnauthorized(Exception ex)
        {
            return ex is HttpException httpException && httpException.Response?.StatusCode == HttpStatusCode.Unauthorized;
        }

        private static bool IsRetryableUploadFailure(Exception ex)
        {
            if (ex is HttpException httpException)
            {
                // Response can be null for connection-level failures; those are transient too.
                return httpException.Response == null ||
                       (int)httpException.Response.StatusCode >= 500 ||
                       httpException.Response.StatusCode == HttpStatusCode.Unauthorized;
            }

            if (ex is FileNotFoundException || ex is DirectoryNotFoundException)
            {
                return false;
            }

            return ex is WebException || ex is IOException || ex is TimeoutException;
        }

        private string GetAccessToken(BookLoreSettings settings)
        {
            var request = new HttpRequestBuilder($"{settings.BaseUrl.TrimEnd('/')}/api/v1/auth/login")
                .Post()
                .SetHeader("Content-Type", "application/json")
                .Accept(HttpAccept.Json)
                .Build();

            request.SetContent(JsonConvert.SerializeObject(new
            {
                username = settings.Username,
                password = settings.Password
            }));
            request.RequestTimeout = TimeSpan.FromSeconds(30);
            request.AllowAutoRedirect = false;

            HttpResponse response;

            try
            {
                response = _httpClient.Execute(request);
            }
            catch (HttpException ex) when (IsTransientLoginFailure(ex))
            {
                // BookLore v2.4.0 can generate the same refresh token for two
                // logins in one second. Its unique-token constraint rejects the
                // second login with HTTP 400, so retry once after the token
                // timestamp has advanced.
                _logger.Warn("BookLore returned HTTP {0} during login; retrying once", (int?)ex.Response?.StatusCode);
                Thread.Sleep(TimeSpan.FromMilliseconds(1100));
                response = _httpClient.Execute(request);
            }

            var accessToken = JObject.Parse(response.Content)["accessToken"]?.Value<string>();

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                throw new InvalidDataException("BookLore login response did not include an access token.");
            }

            return accessToken;
        }

        private static bool IsCredentialRejection(HttpException exception)
        {
            if (exception.Response == null)
            {
                return false;
            }

            if (exception.Response.StatusCode == HttpStatusCode.Unauthorized || exception.Response.StatusCode == HttpStatusCode.Forbidden)
            {
                return true;
            }

            return exception.Response.StatusCode == HttpStatusCode.BadRequest &&
                   (exception.Response.Content ?? string.Empty).IndexOf("Invalid credentials", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsTransientLoginFailure(HttpException exception)
        {
            if (exception.Response == null)
            {
                return true;
            }

            if ((int)exception.Response.StatusCode >= 500)
            {
                return true;
            }

            return exception.Response.StatusCode == HttpStatusCode.BadRequest &&
                   (exception.Response.Content ?? string.Empty).IndexOf("A data conflict occurred", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void UploadFile(BookLoreSettings settings, string accessToken, string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("The imported book file no longer exists.", path);
            }

            using var fileStream = File.OpenRead(path);
            using var fileContent = new StreamContent(fileStream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            using var formContent = new MultipartFormDataContent();
            formContent.Add(fileContent, "file", Path.GetFileName(path));

            var request = new HttpRequestBuilder($"{settings.BaseUrl.TrimEnd('/')}/api/v1/files/upload/bookdrop")
                .Post()
                .SetHeader("Authorization", $"Bearer {accessToken}")
                .KeepAlive()
                .Build();

            request.AllowAutoRedirect = false;
            request.RequestTimeout = TimeSpan.FromHours(1);
            request.ContentSummary = $"file={Path.GetFileName(path)} ({fileStream.Length} bytes)";
            request.SetContent(formContent);

            var response = _httpClient.Execute(request);
            if ((int)response.StatusCode < 200 || (int)response.StatusCode >= 300)
            {
                throw new WebException("BookLore did not confirm the file upload.", WebExceptionStatus.ProtocolError);
            }

            _logger.Info("Uploaded {0} to BookLore BookDrop", Path.GetFileName(path));
        }
    }
}
