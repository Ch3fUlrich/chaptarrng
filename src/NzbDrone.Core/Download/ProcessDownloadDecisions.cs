using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download.Clients;
using NzbDrone.Core.Download.GrabBudget;
using NzbDrone.Core.Download.Pending;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Queue;

namespace NzbDrone.Core.Download
{
    public interface IProcessDownloadDecisions
    {
        Task<ProcessedDecisions> ProcessDecisions(List<DownloadDecision> decisions);
        Task<ProcessedDecisionResult> ProcessDecision(DownloadDecision decision, int? downloadClientId);
    }

    public class ProcessDownloadDecisions : IProcessDownloadDecisions
    {
        private readonly IDownloadService _downloadService;
        private readonly IPrioritizeDownloadDecision _prioritizeDownloadDecision;
        private readonly IPendingReleaseService _pendingReleaseService;
        private readonly Logger _logger;
        private readonly IConfigService _configService;
        private readonly IGrabBudgetService _grabBudgetService;
        private readonly IQueueService _queueService;
        private readonly IIndexerStatusService _indexerStatusService;

        public GrabBudgetResult GrabBudgetResult { get; private set; } = GrabBudgetResult.Allow();

        public ProcessDownloadDecisions(IDownloadService downloadService,
                                        IPrioritizeDownloadDecision prioritizeDownloadDecision,
                                        IPendingReleaseService pendingReleaseService,
                                        Logger logger,
                                        IConfigService configService = null,
                                        IGrabBudgetService grabBudgetService = null,
                                        IQueueService queueService = null,
                                        IIndexerStatusService indexerStatusService = null)
        {
            _downloadService = downloadService;
            _prioritizeDownloadDecision = prioritizeDownloadDecision;
            _pendingReleaseService = pendingReleaseService;
            _logger = logger;
            _configService = configService;
            _grabBudgetService = grabBudgetService;
            _queueService = queueService;
            _indexerStatusService = indexerStatusService;
        }

        public async Task<ProcessedDecisions> ProcessDecisions(List<DownloadDecision> decisions)
        {
            GrabBudgetResult = GrabBudgetResult.Allow();
            var runStartedAt = DateTime.UtcNow;

            var qualifiedReports = GetQualifiedReports(decisions);
            var prioritizedDecisions = _prioritizeDownloadDecision.PrioritizeDecisions(qualifiedReports);
            var grabbed = new List<DownloadDecision>();
            var pending = new List<DownloadDecision>();
            var rejected = decisions.Where(d => d.Rejected).ToList();

            var pendingAddQueue = new List<Tuple<DownloadDecision, PendingReleaseReason>>();

            var usenetFailed = false;
            var torrentFailed = false;

            var budgetEnabled = IsGrabBudgetEnabled();
            var queueSnapshot = budgetEnabled ? GetActiveQueueCount() : 0;
            var maxConsecutiveFailures = _configService?.GrabBudgetMaxConsecutiveFailures ?? 3;
            var consecutiveFailures = 0;
            var skippedCount = 0;
            var failedCount = 0;
            var indexerCooldownSkipped = false;
            var cooldownIndexerIds = budgetEnabled ? GetCooldownIndexerIds() : new HashSet<int>();

            var isDryRun = budgetEnabled && (_configService?.GrabBudgetDryRun ?? false);
            var wouldGrabCount = 0;
            var wouldGrabbed = isDryRun ? new List<DownloadDecision>() : null;
            var detailsLines = isDryRun ? new List<string>() : null;

            for (var index = 0; index < prioritizedDecisions.Count; index++)
            {
                var report = prioritizedDecisions[index];
                var downloadProtocol = report.RemoteBook.Release.DownloadProtocol;

                //Skip if already grabbed
                if (IsBookProcessed(isDryRun ? wouldGrabbed : grabbed, report))
                {
                    continue;
                }

                if (report.TemporarilyRejected)
                {
                    if (!isDryRun)
                    {
                        PreparePending(pendingAddQueue, grabbed, pending, report, PendingReleaseReason.Delay);
                    }
                    continue;
                }

                if ((downloadProtocol == DownloadProtocol.Usenet && usenetFailed) ||
                    (downloadProtocol == DownloadProtocol.Torrent && torrentFailed))
                {
                    if (!isDryRun)
                    {
                        PreparePending(pendingAddQueue, grabbed, pending, report, PendingReleaseReason.DownloadClientUnavailable);
                    }
                    continue;
                }

                if (budgetEnabled)
                {
                    var effectiveGrabbedCount = isDryRun ? wouldGrabCount : grabbed.Count;
                    var budget = _grabBudgetService.CheckBudget(effectiveGrabbedCount, queueSnapshot + effectiveGrabbedCount);

                    if (!budget.Allowed)
                    {
                        if (GrabBudgetResult.StopReason == GrabBudgetStopReason.None)
                        {
                            GrabBudgetResult = budget;
                        }

                        if (isDryRun)
                        {
                            skippedCount++;
                            if (detailsLines.Count < 50)
                            {
                                detailsLines.Add($"[skipped: {budget.StopReason}] {report.RemoteBook?.Release?.Title}");
                            }
                            continue;
                        }
                        else
                        {
                            _logger.Info("Grab budget exhausted ({0}): {1} Leaving {2} of {3} prioritized decision(s) un-grabbed.",
                                budget.StopReason,
                                budget.Reason,
                                prioritizedDecisions.Count - index,
                                prioritizedDecisions.Count);
                            skippedCount += prioritizedDecisions.Count - index;
                            break;
                        }
                    }

                    if (cooldownIndexerIds.Contains(report.RemoteBook?.Release?.IndexerId ?? 0))
                    {
                        skippedCount++;
                        indexerCooldownSkipped = true;
                        _logger.Debug("Skipping grab from Indexer {0} due to indexer cooldown.", report.RemoteBook?.Release?.IndexerId);

                        if (isDryRun)
                        {
                            if (detailsLines.Count < 50)
                            {
                                detailsLines.Add($"[skipped: {GrabBudgetStopReason.IndexerCooldown}] {report.RemoteBook?.Release?.Title}");
                            }
                        }

                        continue;
                    }
                }

                if (isDryRun)
                {
                    wouldGrabCount++;
                    wouldGrabbed.Add(report);

                    var authorId = report.RemoteBook?.Author?.Id ?? 0;
                    var bookIds = string.Join(",", report.RemoteBook?.Books?.Select(b => b.Id) ?? Enumerable.Empty<int>());
                    var runLimit = _configService?.GrabBudgetMaxPerRun ?? 0;

                    _logger.Info("[dry-run] would grab '{0}' (author {1}/book {2}, {3} of {4})",
                        report.RemoteBook?.Release?.Title,
                        authorId,
                        bookIds,
                        wouldGrabCount,
                        runLimit);

                    if (detailsLines.Count < 50)
                    {
                        detailsLines.Add($"[would grab] {report.RemoteBook?.Release?.Title}");
                    }

                    continue;
                }

                var result = await ProcessDecisionInternal(report);
                var stopRun = false;

                switch (result)
                {
                    case ProcessedDecisionResult.Grabbed:
                        {
                            grabbed.Add(report);
                            consecutiveFailures = 0;

                            if (budgetEnabled)
                            {
                                _grabBudgetService.RecordGrab();
                            }

                            break;
                        }

                    case ProcessedDecisionResult.Pending:
                        {
                            PreparePending(pendingAddQueue, grabbed, pending, report, PendingReleaseReason.Delay);
                            break;
                        }

                    case ProcessedDecisionResult.Rejected:
                        {
                            rejected.Add(report);
                            break;
                        }

                    case ProcessedDecisionResult.Failed:
                        {
                            failedCount++;
                            consecutiveFailures++;
                            PreparePending(pendingAddQueue, grabbed, pending, report, PendingReleaseReason.DownloadClientUnavailable);

                            if (downloadProtocol == DownloadProtocol.Usenet)
                            {
                                usenetFailed = true;
                            }
                            else if (downloadProtocol == DownloadProtocol.Torrent)
                            {
                                torrentFailed = true;
                            }

                            if (budgetEnabled && maxConsecutiveFailures > 0 && consecutiveFailures >= maxConsecutiveFailures)
                            {
                                GrabBudgetResult = GrabBudgetResult.Block(
                                    GrabBudgetStopReason.ConsecutiveFailures,
                                    $"Grab budget stopped: {consecutiveFailures} consecutive grab failure(s).");
                                _logger.Warn("Grab budget stopped ({0}): {1} Leaving {2} of {3} prioritized decision(s) un-grabbed.",
                                    GrabBudgetResult.StopReason,
                                    GrabBudgetResult.Reason,
                                    prioritizedDecisions.Count - index - 1,
                                    prioritizedDecisions.Count);
                                stopRun = true;
                            }

                            break;
                        }

                    case ProcessedDecisionResult.Skipped:
                        {
                            skippedCount++;
                            break;
                        }
                }

                if (stopRun)
                {
                    skippedCount += prioritizedDecisions.Count - 1 - index;
                    break;
                }
            }

            if (budgetEnabled && indexerCooldownSkipped && GrabBudgetResult.StopReason == GrabBudgetStopReason.None)
            {
                GrabBudgetResult = GrabBudgetResult.Block(
                    GrabBudgetStopReason.IndexerCooldown,
                    "One or more releases were skipped due to indexer cooldown.");
            }

            if (pendingAddQueue.Any())
            {
                _pendingReleaseService.AddMany(pendingAddQueue);
            }

            if (budgetEnabled && decisions != null && decisions.Any())
            {
                var details = isDryRun && detailsLines != null ? string.Join("\n", detailsLines) : null;
                _grabBudgetService.RecordBatch(isDryRun ? wouldGrabCount : grabbed.Count, skippedCount, failedCount, GrabBudgetResult.StopReason, runStartedAt, details);
            }

            return new ProcessedDecisions(grabbed, pending, rejected);
        }

        public async Task<ProcessedDecisionResult> ProcessDecision(DownloadDecision decision, int? downloadClientId)
        {
            GrabBudgetResult = GrabBudgetResult.Allow();

            if (decision == null)
            {
                return ProcessedDecisionResult.Skipped;
            }

            if (!IsQualifiedReport(decision))
            {
                return ProcessedDecisionResult.Rejected;
            }

            if (decision.TemporarilyRejected)
            {
                _pendingReleaseService.Add(decision, PendingReleaseReason.Delay);

                return ProcessedDecisionResult.Pending;
            }

            if (IsGrabBudgetEnabled() && _configService.GrabBudgetApplyToInteractive)
            {
                var budget = _grabBudgetService.CheckBudget(0, GetActiveQueueCount());

                if (!budget.Allowed)
                {
                    GrabBudgetResult = budget;
                    _logger.Info("Grab budget exhausted ({0}): {1} Skipping interactive grab.", budget.StopReason, budget.Reason);

                    return ProcessedDecisionResult.Skipped;
                }
            }

            var result = await ProcessDecisionInternal(decision, downloadClientId);

            if (result == ProcessedDecisionResult.Pending)
            {
                _pendingReleaseService.Add(decision, PendingReleaseReason.Delay);
            }
            else if (result == ProcessedDecisionResult.Failed)
            {
                _pendingReleaseService.Add(decision, PendingReleaseReason.DownloadClientUnavailable);
            }
            else if (result == ProcessedDecisionResult.Grabbed && IsGrabBudgetEnabled() && _configService.GrabBudgetApplyToInteractive)
            {
                _grabBudgetService.RecordGrab();
            }

            return result;
        }

        internal List<DownloadDecision> GetQualifiedReports(IEnumerable<DownloadDecision> decisions)
        {
            return decisions.Where(IsQualifiedReport).ToList();
        }

        internal bool IsQualifiedReport(DownloadDecision decision)
        {
            // Process both approved and temporarily rejected
            return (decision.Approved || decision.TemporarilyRejected) && decision.RemoteBook.Books.Any();
        }

        private bool IsGrabBudgetEnabled()
        {
            return _configService != null &&
                   _grabBudgetService != null &&
                   _configService.GrabBudgetEnabled;
        }

        private HashSet<int> GetCooldownIndexerIds()
        {
            if (_indexerStatusService == null)
            {
                return new HashSet<int>();
            }

            try
            {
                // GetBlockedProviders only returns providers that are currently disabled (backed off).
                return (_indexerStatusService.GetBlockedProviders() ?? new List<IndexerStatus>())
                    .Select(p => p.ProviderId)
                    .ToHashSet();
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Unable to check indexer status for grab budget.");
                return new HashSet<int>();
            }
        }

        private int GetActiveQueueCount()
        {
            try
            {
                return _queueService?.GetQueue()?.Count ?? 0;
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Unable to read active queue for grab budget; ignoring queue ceiling.");
                return 0;
            }
        }

        private bool IsBookProcessed(List<DownloadDecision> decisions, DownloadDecision report)
        {
            var bookIds = report.RemoteBook.Books.Select(e => e.Id).ToList();

            return decisions.SelectMany(r => r.RemoteBook.Books)
                            .Select(e => e.Id)
                            .ToList()
                            .Intersect(bookIds)
                            .Any();
        }

        private void PreparePending(List<Tuple<DownloadDecision, PendingReleaseReason>> queue, List<DownloadDecision> grabbed, List<DownloadDecision> pending, DownloadDecision report, PendingReleaseReason reason)
        {
            // If a release was already grabbed with matching books we should store it as a fallback
            // and filter it out the next time it is processed.
            // If a higher quality release failed to add to the download client, but a lower quality release
            // was sent to another client we still list it normally so it apparent that it'll grab next time.
            // Delayed is treated the same, but only the first is listed the subsequent items as stored as Fallback.
            if (IsBookProcessed(grabbed, report) ||
                IsBookProcessed(pending, report))
            {
                reason = PendingReleaseReason.Fallback;
            }

            queue.Add(Tuple.Create(report, reason));
            pending.Add(report);
        }

        private async Task<ProcessedDecisionResult> ProcessDecisionInternal(DownloadDecision decision, int? downloadClientId = null)
        {
            var remoteBook = decision.RemoteBook;

            try
            {
                _logger.Trace("Grabbing from Indexer {0} at priority {1}.", remoteBook.Release.Indexer, remoteBook.Release.IndexerPriority);
                await _downloadService.DownloadReport(remoteBook, downloadClientId);

                return ProcessedDecisionResult.Grabbed;
            }
            catch (MamUnsatisfiedSlotsUnavailableException ex)
            {
                _logger.Debug(ex, "MAM has no safely available unsatisfied-torrent slot; storing release until later. " + remoteBook);

                return ProcessedDecisionResult.Pending;
            }
            catch (ReleaseUnavailableException)
            {
                _logger.Warn("Failed to download release from indexer, no longer available. " + remoteBook);
                return ProcessedDecisionResult.Rejected;
            }
            catch (Exception ex)
            {
                if (ex is DownloadClientUnavailableException || ex is DownloadClientAuthenticationException)
                {
                    _logger.Debug(ex,
                        "Failed to send release to download client, storing until later. " + remoteBook);

                    return ProcessedDecisionResult.Failed;
                }
                else
                {
                    _logger.Warn(ex, "Couldn't add report to download queue. " + remoteBook);
                    return ProcessedDecisionResult.Skipped;
                }
            }
        }
    }
}
