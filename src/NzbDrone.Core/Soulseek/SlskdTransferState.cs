using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Download;

namespace NzbDrone.Core.Soulseek
{
    public enum SlskdTransferPhase
    {
        Queued,
        InProgress,
        Succeeded,
        Failed
    }

    // Maps slskd transfer states (Soulseek.NET flags rendered as "Completed, Succeeded") to download client
    // state, see the state table in docs/slskd.md.
    public static class SlskdTransferState
    {
        public static SlskdTransferPhase Parse(string state)
        {
            var flags = (state ?? string.Empty)
                .Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (flags.Contains("Completed"))
            {
                return flags.Contains("Succeeded") ? SlskdTransferPhase.Succeeded : SlskdTransferPhase.Failed;
            }

            return flags.Contains("InProgress") ? SlskdTransferPhase.InProgress : SlskdTransferPhase.Queued;
        }

        public static (DownloadItemStatus Status, string Message) Aggregate(IReadOnlyCollection<SlskdTransfer> transfers)
        {
            if (transfers == null || transfers.Count == 0)
            {
                return (DownloadItemStatus.Warning, "slskd reports no transfers for this download (removed in slskd?).");
            }

            var failed = transfers.FirstOrDefault(t => Parse(t.State) == SlskdTransferPhase.Failed);
            if (failed != null)
            {
                var reason = string.IsNullOrWhiteSpace(failed.Exception) ? failed.State : failed.Exception;
                return (DownloadItemStatus.Failed, $"Transfer failed ({reason}).");
            }

            if (transfers.All(t => Parse(t.State) == SlskdTransferPhase.Succeeded))
            {
                return (DownloadItemStatus.Completed, null);
            }

            if (transfers.Any(t => Parse(t.State) == SlskdTransferPhase.InProgress))
            {
                return (DownloadItemStatus.Downloading, null);
            }

            var position = transfers.Where(t => t.PlaceInQueue.HasValue).Select(t => t.PlaceInQueue.Value).DefaultIfEmpty(-1).Min();
            return (DownloadItemStatus.Queued, position >= 0 ? $"Queued at the peer (place {position})." : null);
        }
    }
}
