using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentValidation.Results;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.RemotePathMappings;
using NzbDrone.Core.Soulseek;

namespace NzbDrone.Core.Download.Clients.Slskd
{
    public class SlskdDownloadClient : DownloadClientBase<SlskdDownloadClientSettings>
    {
        internal const string FolderPrefix = "chaptarr-";
        private const string ExternalIdPrefix = "chaptarr|";
        private static readonly Version MinimumVersion = new(0, 26, 0);

        // Batch id -> external id (null = not ours). Batches are immutable, so this never goes stale.
        private static readonly ConcurrentDictionary<string, string> BatchOwners = new();

        private readonly ISlskdApiClient _apiClient;

        public SlskdDownloadClient(ISlskdApiClient apiClient,
                                   IConfigService configService,
                                   IDiskProvider diskProvider,
                                   IRemotePathMappingService remotePathMappingService,
                                   Logger logger)
            : base(configService, diskProvider, remotePathMappingService, logger)
        {
            _apiClient = apiClient;
        }

        public override string Name => "slskd";

        public override DownloadProtocol Protocol => DownloadProtocol.Soulseek;

        private SlskdConnection Connection => new() { BaseUrl = Settings.BaseUrl, ApiKey = Settings.ApiKey };

        public override async Task<string> Download(RemoteBook remoteBook, IIndexer indexer)
        {
            var release = remoteBook?.Release;
            if (release == null || release.DownloadProtocol != DownloadProtocol.Soulseek)
            {
                throw new DownloadClientException("slskd download client only accepts Soulseek releases.");
            }

            if (!SlskdDownloadToken.TryParse(release.DownloadUrl, out var token))
            {
                throw new ReleaseDownloadException(release, "Soulseek release has an invalid or unsafe download token.");
            }

            var id = Guid.NewGuid();
            var request = new SlskdBatchRequest
            {
                Id = id.ToString(),
                Username = token.Username,
                Files = token.Files.Select(f => new SlskdBatchFile { Filename = f.Filename, Size = f.Size }).ToList(),
                Options = new SlskdBatchOptions
                {
                    Destination = FolderPrefix + id.ToString("N"),
                    ExternalId = $"{ExternalIdPrefix}{Definition.Id}|{release.Title}"
                }
            };

            SlskdBatchResponse response;
            try
            {
                response = await _apiClient.EnqueueBatchAsync(Connection, request);
            }
            catch (SlskdApiException ex)
            {
                throw new ReleaseDownloadException(release, $"slskd refused the download: {ex.Message}");
            }

            if (response?.Failures is { Count: > 0 })
            {
                await TryCancelAsync(id);
                throw new ReleaseDownloadException(release, $"slskd could not queue {response.Failures.Count} of {token.Files.Count} files: {response.Failures[0].Message}");
            }

            return id.ToString();
        }

        public override IEnumerable<DownloadClientItem> GetItems()
        {
            var users = _apiClient.GetDownloadsAsync(Connection).GetAwaiter().GetResult();
            var transfers = users
                .SelectMany(u => u.Directories ?? new List<SlskdDirectoryTransfers>())
                .SelectMany(d => d.Files ?? new List<SlskdTransfer>())
                .Where(t => t.BatchId.HasValue && !t.Removed)
                .ToList();

            var items = new List<DownloadClientItem>();
            foreach (var group in transfers.GroupBy(t => t.BatchId.Value))
            {
                var title = OwnedTitle(group.Key);
                if (title == null)
                {
                    continue;
                }

                items.Add(BuildItem(group.Key, title, group.ToList()));
            }

            return items;
        }

        public override DownloadClientItem GetImportItem(DownloadClientItem item, DownloadClientItem previousImportAttempt)
        {
            var result = item.Clone();
            var folder = item.OutputPath.FullPath;

            if (item.Status == DownloadItemStatus.Completed && folder.IsNotNullOrWhiteSpace() && _diskProvider.FolderExists(folder))
            {
                result.FilePaths = _diskProvider.GetFiles(folder, true).OrderBy(f => f, StringComparer.Ordinal).ToList();
                result.FileListConfidence = DownloadClientFileListConfidence.Authoritative;
            }

            return result;
        }

        public override void RemoveItem(DownloadClientItem item, bool deleteData)
        {
            if (item == null || !Guid.TryParse(item.DownloadId, out var id))
            {
                return;
            }

            var batch = _apiClient.GetBatchAsync(Connection, id).GetAwaiter().GetResult();
            foreach (var transfer in batch?.Transfers ?? new List<SlskdTransfer>())
            {
                _apiClient.CancelDownloadAsync(Connection, batch.Username, transfer.Id, true).GetAwaiter().GetResult();
            }

            BatchOwners.TryRemove(CacheKey(id), out _);

            if (deleteData)
            {
                DeleteOwnFolder(FolderFor(id));
            }
        }

        public override DownloadClientInfo GetStatus()
        {
            return new DownloadClientInfo
            {
                IsLocalhost = false,
                OutputRootFolders = Settings.DownloadDirectory.IsNotNullOrWhiteSpace()
                    ? new List<OsPath> { new(Settings.DownloadDirectory) }
                    : new List<OsPath>()
            };
        }

        public override void MarkItemAsImported(DownloadClientItem downloadClientItem)
        {
            // Nothing to mark: slskd keeps no import state, completed batches are removed through RemoveItem.
        }

        protected override void Test(List<ValidationFailure> failures)
        {
            var validation = Settings.Validate();
            if (!validation.IsValid)
            {
                failures.AddRange(validation.Errors);
                return;
            }

            try
            {
                var state = _apiClient.GetApplicationAsync(Connection).GetAwaiter().GetResult();
                if (Version.TryParse(state?.Version?.Current, out var version) && version < MinimumVersion)
                {
                    failures.Add(new ValidationFailure(nameof(Settings.BaseUrl), $"slskd {version} is too old, 0.26.0 or newer is required (download batches)."));
                }

                if (state?.Server != null && !state.Server.IsLoggedIn)
                {
                    failures.Add(new ValidationFailure(nameof(Settings.BaseUrl), $"slskd is reachable but not logged in to the Soulseek network (state: {state.Server.State})."));
                }
            }
            catch (SlskdApiException ex)
            {
                failures.Add(new ValidationFailure(ex.StatusCode == 401 || ex.StatusCode == 403 ? nameof(Settings.ApiKey) : nameof(Settings.BaseUrl), ex.Message));
                return;
            }
            catch (HttpException ex)
            {
                failures.Add(new ValidationFailure(nameof(Settings.BaseUrl), $"Unable to reach slskd: {ex.Message}"));
                return;
            }

            failures.AddIfNotNull(TestFolder(Settings.DownloadDirectory, nameof(Settings.DownloadDirectory), false));
        }

        private DownloadClientItem BuildItem(Guid id, string title, List<SlskdTransfer> transfers)
        {
            var (status, message) = SlskdTransferState.Aggregate(transfers);
            var folder = FolderFor(id);

            if (status == DownloadItemStatus.Completed && !_diskProvider.FolderExists(folder))
            {
                status = DownloadItemStatus.Warning;
                message = $"slskd finished, but '{folder}' does not exist. Check the Download Directory setting.";
            }

            var total = transfers.Sum(t => t.Size);
            var remaining = transfers.Sum(t => Math.Max(0, t.Size - t.BytesTransferred));
            var speed = transfers.Where(t => SlskdTransferState.Parse(t.State) == SlskdTransferPhase.InProgress).Sum(t => t.AverageSpeed);
            var definition = (DownloadClientDefinition)Definition;

            return new DownloadClientItem
            {
                DownloadClientInfo = DownloadClientItemClientInfo.FromDownloadClient(this, false),
                DownloadId = id.ToString(),
                Title = title,
                TotalSize = total,
                RemainingSize = status == DownloadItemStatus.Completed ? 0 : remaining,
                RemainingTime = speed > 0 ? TimeSpan.FromSeconds(remaining / speed) : null,
                OutputPath = new OsPath(folder),
                Status = status,
                Message = message,
                CanMoveFiles = false,
                CanBeRemoved = status == DownloadItemStatus.Completed ? definition.RemoveCompletedDownloads : status == DownloadItemStatus.Failed && definition.RemoveFailedDownloads,
                FileListConfidence = status == DownloadItemStatus.Completed ? DownloadClientFileListConfidence.Authoritative : DownloadClientFileListConfidence.Pending
            };
        }

        // Title when the batch was created by this client definition, otherwise null.
        private string OwnedTitle(Guid batchId)
        {
            var key = CacheKey(batchId);
            if (!BatchOwners.TryGetValue(key, out var externalId))
            {
                var batch = _apiClient.GetBatchAsync(Connection, batchId).GetAwaiter().GetResult();
                externalId = batch?.Options?.ExternalId ?? string.Empty;
                BatchOwners[key] = externalId;
            }

            var prefix = $"{ExternalIdPrefix}{Definition.Id}|";
            return externalId.StartsWith(prefix, StringComparison.Ordinal) ? externalId.Substring(prefix.Length) : null;
        }

        private string CacheKey(Guid id) => $"{Settings.BaseUrl}|{id}";

        private string FolderFor(Guid id) => Path.Combine(Settings.DownloadDirectory ?? string.Empty, FolderPrefix + id.ToString("N"));

        private async Task TryCancelAsync(Guid id)
        {
            try
            {
                var batch = await _apiClient.GetBatchAsync(Connection, id);
                foreach (var transfer in batch?.Transfers ?? new List<SlskdTransfer>())
                {
                    await _apiClient.CancelDownloadAsync(Connection, batch.Username, transfer.Id, true);
                }
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Could not cancel partially queued slskd batch {0}", id);
            }
        }

        // Only ever deletes chaptarr-<id> folders directly inside the configured download directory.
        private void DeleteOwnFolder(string folder)
        {
            try
            {
                var root = Path.GetFullPath(Settings.DownloadDirectory ?? string.Empty);
                var full = Path.GetFullPath(folder);
                if (Path.GetDirectoryName(full.TrimEnd(Path.DirectorySeparatorChar)) != root.TrimEnd(Path.DirectorySeparatorChar) ||
                    !Path.GetFileName(full).StartsWith(FolderPrefix, StringComparison.Ordinal))
                {
                    _logger.Warn("Refusing to delete '{0}': not a Chaptarr folder inside the slskd download directory.", folder);
                    return;
                }

                if (_diskProvider.FolderExists(full))
                {
                    _diskProvider.DeleteFolder(full, true);
                }
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Could not delete slskd download folder '{0}'", folder);
            }
        }
    }
}
