using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NLog;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.Clients;
using NzbDrone.Core.Download.GrabBudget;
using NzbDrone.Core.Download.Pending;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Queue;
using QueueItem = NzbDrone.Core.Queue.Queue;

namespace Chaptarr.Core.Test.Download
{
    [TestFixture]
    public class GrabBudgetFixture
    {
        private static readonly DateTime Now = new DateTime(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);

        [Test]
        public async Task per_run_limit_grabs_exactly_five_audiobooks()
        {
            var repo = InMemoryGrabBudgetLogRepository.Create();
            var config = GrabBudgetConfigProxy.Create(enabled: true, maxPerRun: 5, maxPerDay: 25);
            var subject = BuildSubject(repo, config, Now, out var downloads, out _);

            var decisions = BuildDecisions(50, BookMediaType.Audiobook, Quality.MP3, startId: 1000);

            var result = await subject.ProcessDecisions(decisions);

            Assert.Multiple(() =>
            {
                Assert.That(result.Grabbed, Has.Count.EqualTo(5));
                Assert.That(downloads.Downloaded, Has.Count.EqualTo(5));
                Assert.That(result.Rejected, Is.Empty);
                Assert.That(subject.GrabBudgetResult.StopReason, Is.EqualTo(GrabBudgetStopReason.MaxPerRunReached));
                Assert.That(repo.Store, Has.Count.EqualTo(5));
            });
        }

        [Test]
        public async Task queue_ceiling_reached_grabs_zero_ebooks()
        {
            var repo = InMemoryGrabBudgetLogRepository.Create();
            var config = GrabBudgetConfigProxy.Create(enabled: true, maxPerRun: 50, maxPerDay: 50, maxActiveQueue: 3);
            var subject = BuildSubject(repo, config, Now, out var downloads, out _, activeQueueCount: 3);

            var decisions = BuildDecisions(10, BookMediaType.Ebook, Quality.EPUB, startId: 2000);

            var result = await subject.ProcessDecisions(decisions);

            Assert.Multiple(() =>
            {
                Assert.That(result.Grabbed, Is.Empty);
                Assert.That(downloads.Downloaded, Is.Empty);
                Assert.That(result.Rejected, Is.Empty);
                Assert.That(subject.GrabBudgetResult.StopReason, Is.EqualTo(GrabBudgetStopReason.MaxActiveQueueReached));
            });
        }

        [Test]
        public async Task daily_limit_persisted_across_restart_blocks_mixed_media_grabs()
        {
            var repo = InMemoryGrabBudgetLogRepository.Create();

            foreach (var i in Enumerable.Range(0, 25))
            {
                repo.Store.Add(new GrabBudgetLog { Id = i + 1, GrabbedAt = Now.AddHours(-1) });
            }

            // Simulate a restart: a brand new service instance over the same persisted rows.
            var config = GrabBudgetConfigProxy.Create(enabled: true, maxPerRun: 50, maxPerDay: 25);
            var subject = BuildSubject(repo, config, Now, out var downloads, out _);

            var decisions = BuildDecisions(5, BookMediaType.Audiobook, Quality.MP3, startId: 3000)
                .Concat(BuildDecisions(5, BookMediaType.Ebook, Quality.EPUB, startId: 4000))
                .ToList();

            var result = await subject.ProcessDecisions(decisions);

            Assert.Multiple(() =>
            {
                Assert.That(result.Grabbed, Is.Empty);
                Assert.That(downloads.Downloaded, Is.Empty);
                Assert.That(result.Rejected, Is.Empty);
                Assert.That(subject.GrabBudgetResult.StopReason, Is.EqualTo(GrabBudgetStopReason.MaxPerDayReached));
            });
        }

        [Test]
        public async Task disabled_budget_grabs_all_mixed_media_decisions()
        {
            var repo = InMemoryGrabBudgetLogRepository.Create();
            var config = GrabBudgetConfigProxy.Create(enabled: false);
            var subject = BuildSubject(repo, config, Now, out var downloads, out _);

            var decisions = BuildDecisions(25, BookMediaType.Audiobook, Quality.M4B, startId: 5000)
                .Concat(BuildDecisions(25, BookMediaType.Ebook, Quality.AZW3, startId: 6000))
                .ToList();

            var result = await subject.ProcessDecisions(decisions);

            Assert.Multiple(() =>
            {
                Assert.That(result.Grabbed, Has.Count.EqualTo(50));
                Assert.That(downloads.Downloaded, Has.Count.EqualTo(50));
                Assert.That(subject.GrabBudgetResult.StopReason, Is.EqualTo(GrabBudgetStopReason.None));
                Assert.That(repo.Store, Is.Empty);
            });
        }

        [Test]
        public async Task interactive_grab_exempt_by_default_when_daily_limit_reached()
        {
            var repo = InMemoryGrabBudgetLogRepository.Create();

            foreach (var i in Enumerable.Range(0, 25))
            {
                repo.Store.Add(new GrabBudgetLog { Id = i + 1, GrabbedAt = Now.AddHours(-2) });
            }

            var config = GrabBudgetConfigProxy.Create(enabled: true, maxPerRun: 5, maxPerDay: 25, applyToInteractive: false);
            var subject = BuildSubject(repo, config, Now, out var downloads, out _);

            var decision = BuildDecisions(1, BookMediaType.Audiobook, Quality.MP3, startId: 7000).Single();

            var result = await subject.ProcessDecision(decision, null);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(ProcessedDecisionResult.Grabbed));
                Assert.That(downloads.Downloaded, Has.Count.EqualTo(1));
                Assert.That(repo.Store, Has.Count.EqualTo(25));
            });
        }

        [Test]
        public async Task interactive_grab_blocked_when_applied_and_daily_limit_reached()
        {
            var repo = InMemoryGrabBudgetLogRepository.Create();

            foreach (var i in Enumerable.Range(0, 25))
            {
                repo.Store.Add(new GrabBudgetLog { Id = i + 1, GrabbedAt = Now.AddHours(-2) });
            }

            var config = GrabBudgetConfigProxy.Create(enabled: true, maxPerRun: 5, maxPerDay: 25, applyToInteractive: true);
            var subject = BuildSubject(repo, config, Now, out var downloads, out _);

            var decision = BuildDecisions(1, BookMediaType.Ebook, Quality.EPUB, startId: 8000).Single();

            var result = await subject.ProcessDecision(decision, null);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(ProcessedDecisionResult.Skipped));
                Assert.That(downloads.Downloaded, Is.Empty);
                Assert.That(subject.GrabBudgetResult.StopReason, Is.EqualTo(GrabBudgetStopReason.MaxPerDayReached));
            });
        }

        [Test]
        public void rolling_window_ignores_rows_older_than_24h()
        {
            var repo = InMemoryGrabBudgetLogRepository.Create();

            foreach (var i in Enumerable.Range(0, 25))
            {
                repo.Store.Add(new GrabBudgetLog { Id = i + 1, GrabbedAt = Now.AddHours(-25) });
            }

            var config = GrabBudgetConfigProxy.Create(enabled: true, maxPerRun: 50, maxPerDay: 25);
            var service = new GrabBudgetService((IGrabBudgetLogRepository)(object)repo, config, new FixedGrabBudgetClock(Now), LogManager.GetLogger("GrabBudgetFixture"));

            Assert.Multiple(() =>
            {
                Assert.That(service.GrabsInLastDay(Now), Is.EqualTo(0));
                Assert.That(service.CheckBudget(0, 0, Now).Allowed, Is.True);
            });
        }

        [Test]
        public void record_grab_prunes_rows_older_than_24h()
        {
            var repo = InMemoryGrabBudgetLogRepository.Create();
            repo.Store.Add(new GrabBudgetLog { Id = 1, GrabbedAt = Now.AddHours(-30) });

            var config = GrabBudgetConfigProxy.Create(enabled: true);
            var service = new GrabBudgetService((IGrabBudgetLogRepository)(object)repo, config, new FixedGrabBudgetClock(Now), LogManager.GetLogger("GrabBudgetFixture"));

            service.RecordGrab();

            Assert.Multiple(() =>
            {
                Assert.That(repo.Store, Has.Count.EqualTo(1));
                Assert.That(repo.Store.Single().GrabbedAt, Is.EqualTo(Now));
            });
        }

        [Test]
        public async Task interactive_grab_exempt_by_default_does_not_add_repo_rows()
        {
            var repo = InMemoryGrabBudgetLogRepository.Create();
            var config = GrabBudgetConfigProxy.Create(enabled: true, maxPerRun: 5, maxPerDay: 25, applyToInteractive: false);
            var subject = BuildSubject(repo, config, Now, out var downloads, out _);

            var decision = BuildDecisions(1, BookMediaType.Audiobook, Quality.MP3, startId: 7100).Single();

            var result = await subject.ProcessDecision(decision, null);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(ProcessedDecisionResult.Grabbed));
                Assert.That(downloads.Downloaded, Has.Count.EqualTo(1));
                Assert.That(repo.Store, Is.Empty);
            });
        }

        [Test]
        public async Task mixed_audiobook_and_ebook_with_limit_five_grabs_exactly_five()
        {
            var repo = InMemoryGrabBudgetLogRepository.Create();
            var config = GrabBudgetConfigProxy.Create(enabled: true, maxPerRun: 5, maxPerDay: 25);
            var subject = BuildSubject(repo, config, Now, out var downloads, out _);

            var decisions = BuildDecisions(25, BookMediaType.Audiobook, Quality.MP3, startId: 10000)
                .Concat(BuildDecisions(25, BookMediaType.Ebook, Quality.EPUB, startId: 11000))
                .ToList();

            var result = await subject.ProcessDecisions(decisions);

            Assert.Multiple(() =>
            {
                Assert.That(result.Grabbed, Has.Count.EqualTo(5));
                Assert.That(downloads.Downloaded, Has.Count.EqualTo(5));
                Assert.That(subject.GrabBudgetResult.StopReason, Is.EqualTo(GrabBudgetStopReason.MaxPerRunReached));
                Assert.That(repo.Store, Has.Count.EqualTo(5));
            });
        }

        [Test]
        public async Task ceiling_reached_mid_run_pauses_grabs()
        {
            var repo = InMemoryGrabBudgetLogRepository.Create();
            var config = GrabBudgetConfigProxy.Create(enabled: true, maxPerRun: 50, maxPerDay: 50, maxActiveQueue: 4);
            var subject = BuildSubject(repo, config, Now, out var downloads, out _, activeQueueCount: 2);

            var decisions = BuildDecisions(5, BookMediaType.Audiobook, Quality.MP3, startId: 12000)
                .Concat(BuildDecisions(5, BookMediaType.Ebook, Quality.EPUB, startId: 13000))
                .ToList();

            var result = await subject.ProcessDecisions(decisions);

            Assert.Multiple(() =>
            {
                Assert.That(result.Grabbed, Has.Count.EqualTo(2));
                Assert.That(downloads.Downloaded, Has.Count.EqualTo(2));
                Assert.That(subject.GrabBudgetResult.StopReason, Is.EqualTo(GrabBudgetStopReason.MaxActiveQueueReached));
                Assert.That(repo.Store, Has.Count.EqualTo(2));
            });
        }

        [Test]
        public async Task restart_persistence_distributes_daily_limit_across_instances()
        {
            var repo = InMemoryGrabBudgetLogRepository.Create();
            var config = GrabBudgetConfigProxy.Create(enabled: true, maxPerRun: 50, maxPerDay: 5);

            var subject1 = BuildSubject(repo, config, Now, out var downloads1, out _);
            var decisions1 = BuildDecisions(3, BookMediaType.Audiobook, Quality.MP3, startId: 14000);
            var result1 = await subject1.ProcessDecisions(decisions1);

            Assert.Multiple(() =>
            {
                Assert.That(result1.Grabbed, Has.Count.EqualTo(3));
                Assert.That(downloads1.Downloaded, Has.Count.EqualTo(3));
                Assert.That(repo.Store, Has.Count.EqualTo(3));
            });

            var subject2 = BuildSubject(repo, config, Now.AddMinutes(5), out var downloads2, out _);
            var decisions2 = BuildDecisions(5, BookMediaType.Ebook, Quality.EPUB, startId: 15000);
            var result2 = await subject2.ProcessDecisions(decisions2);

            Assert.Multiple(() =>
            {
                Assert.That(result2.Grabbed, Has.Count.EqualTo(2));
                Assert.That(downloads2.Downloaded, Has.Count.EqualTo(2));
                Assert.That(subject2.GrabBudgetResult.StopReason, Is.EqualTo(GrabBudgetStopReason.MaxPerDayReached));
                Assert.That(repo.Store, Has.Count.EqualTo(5));
            });
        }

        [Test]
        public async Task stale_grab_budget_result_reset_at_entry_of_process_decisions_and_decision()
        {
            var repo = InMemoryGrabBudgetLogRepository.Create();
            var config = GrabBudgetConfigProxy.Create(enabled: true, maxPerRun: 1, maxPerDay: 25);
            var subject = BuildSubject(repo, config, Now, out _, out _);

            var decisions1 = BuildDecisions(2, BookMediaType.Audiobook, Quality.MP3, startId: 16000);
            await subject.ProcessDecisions(decisions1);
            Assert.That(subject.GrabBudgetResult.StopReason, Is.EqualTo(GrabBudgetStopReason.MaxPerRunReached));

            await subject.ProcessDecisions(new List<DownloadDecision>());
            Assert.That(subject.GrabBudgetResult.StopReason, Is.EqualTo(GrabBudgetStopReason.None));
            Assert.That(subject.GrabBudgetResult.Allowed, Is.True);

            await subject.ProcessDecisions(decisions1);
            Assert.That(subject.GrabBudgetResult.StopReason, Is.EqualTo(GrabBudgetStopReason.MaxPerRunReached));

            var interactiveDecision = BuildDecisions(1, BookMediaType.Ebook, Quality.EPUB, startId: 17000).Single();
            await subject.ProcessDecision(interactiveDecision, null);
            Assert.That(subject.GrabBudgetResult.StopReason, Is.EqualTo(GrabBudgetStopReason.None));
            Assert.That(subject.GrabBudgetResult.Allowed, Is.True);
        }

        [Test]
        public async Task consecutive_failure_stops_run_after_threshold()
        {
            var repo = InMemoryGrabBudgetLogRepository.Create();
            var batchRepo = InMemoryGrabBudgetBatchRepository.Create();
            var config = GrabBudgetConfigProxy.Create(enabled: true, maxPerRun: 50, maxPerDay: 50, maxConsecutiveFailures: 3);
            var failingDownloads = new FailingDownloadService(failCount: 5);
            var subject = BuildSubject(repo, config, Now, out _, out _, downloadService: failingDownloads, batchRepo: batchRepo);

            var decisions = BuildDecisions(2, BookMediaType.Audiobook, Quality.MP3, startId: 18000, protocol: DownloadProtocol.Unknown)
                .Concat(BuildDecisions(3, BookMediaType.Ebook, Quality.EPUB, startId: 19000, protocol: DownloadProtocol.Unknown))
                .ToList();

            var result = await subject.ProcessDecisions(decisions);

            Assert.Multiple(() =>
            {
                Assert.That(result.Grabbed, Is.Empty);
                Assert.That(subject.GrabBudgetResult.StopReason, Is.EqualTo(GrabBudgetStopReason.ConsecutiveFailures));
                Assert.That(batchRepo.Store, Has.Count.EqualTo(1));
                Assert.That(batchRepo.Store[0].StopReason, Is.EqualTo(GrabBudgetStopReason.ConsecutiveFailures));
                Assert.That(batchRepo.Store[0].Failed, Is.EqualTo(3));
                Assert.That(batchRepo.Store[0].Skipped, Is.EqualTo(2));
                Assert.That(batchRepo.Store[0].Grabbed, Is.EqualTo(0));
            });
        }

        [Test]
        public async Task consecutive_failure_counter_reset_by_success()
        {
            var repo = InMemoryGrabBudgetLogRepository.Create();
            var batchRepo = InMemoryGrabBudgetBatchRepository.Create();
            var config = GrabBudgetConfigProxy.Create(enabled: true, maxPerRun: 50, maxPerDay: 50, maxConsecutiveFailures: 3);
            var seqDownloads = new SequenceDownloadService(true, true, false, true, true);
            var subject = BuildSubject(repo, config, Now, out _, out _, downloadService: seqDownloads, batchRepo: batchRepo);

            var decisions = BuildDecisions(2, BookMediaType.Audiobook, Quality.MP3, startId: 20000, protocol: DownloadProtocol.Unknown)
                .Concat(BuildDecisions(3, BookMediaType.Ebook, Quality.EPUB, startId: 21000, protocol: DownloadProtocol.Unknown))
                .ToList();

            var result = await subject.ProcessDecisions(decisions);

            Assert.Multiple(() =>
            {
                Assert.That(result.Grabbed, Has.Count.EqualTo(1));
                Assert.That(subject.GrabBudgetResult.StopReason, Is.EqualTo(GrabBudgetStopReason.None));
                Assert.That(batchRepo.Store, Has.Count.EqualTo(1));
                Assert.That(batchRepo.Store[0].StopReason, Is.EqualTo(GrabBudgetStopReason.None));
                Assert.That(batchRepo.Store[0].Grabbed, Is.EqualTo(1));
                Assert.That(batchRepo.Store[0].Failed, Is.EqualTo(4));
            });
        }

        [Test]
        public async Task indexer_cooldown_skips_blocked_indexer_and_records_stop_reason()
        {
            var repo = InMemoryGrabBudgetLogRepository.Create();
            var batchRepo = InMemoryGrabBudgetBatchRepository.Create();
            var config = GrabBudgetConfigProxy.Create(enabled: true, maxPerRun: 50, maxPerDay: 50);
            var fakeIndexerService = FakeIndexerStatusService.Create(5);
            var subject = BuildSubject(
                repo,
                config,
                Now,
                out var downloads,
                out _,
                batchRepo: batchRepo,
                indexerStatusService: (IIndexerStatusService)(object)fakeIndexerService);

            var decisionBlocked = BuildDecision(
                new Book { Id = 22000, Title = "Audiobook Blocked", MediaType = BookMediaType.Audiobook },
                "Release Blocked",
                Quality.MP3,
                DownloadProtocol.Torrent,
                indexerId: 5);

            var decisionAllowed = BuildDecision(
                new Book { Id = 22001, Title = "Ebook Allowed", MediaType = BookMediaType.Ebook },
                "Release Allowed",
                Quality.EPUB,
                DownloadProtocol.Torrent,
                indexerId: 2);

            var decisions = new List<DownloadDecision> { decisionBlocked, decisionAllowed };

            var result = await subject.ProcessDecisions(decisions);

            Assert.Multiple(() =>
            {
                Assert.That(result.Grabbed, Has.Count.EqualTo(1));
                Assert.That(downloads.Downloaded, Has.Count.EqualTo(1));
                Assert.That(downloads.Downloaded[0].Release.IndexerId, Is.EqualTo(2));
                Assert.That(subject.GrabBudgetResult.StopReason, Is.EqualTo(GrabBudgetStopReason.IndexerCooldown));
                Assert.That(batchRepo.Store, Has.Count.EqualTo(1));
                Assert.That(batchRepo.Store[0].StopReason, Is.EqualTo(GrabBudgetStopReason.IndexerCooldown));
                Assert.That(batchRepo.Store[0].Grabbed, Is.EqualTo(1));
                Assert.That(batchRepo.Store[0].Skipped, Is.EqualTo(1));
                Assert.That(batchRepo.Store[0].Failed, Is.EqualTo(0));
            });
        }

        [Test]
        public async Task indexer_cooldown_and_consecutive_failures_ignored_when_budget_disabled()
        {
            var repo = InMemoryGrabBudgetLogRepository.Create();
            var batchRepo = InMemoryGrabBudgetBatchRepository.Create();
            var config = GrabBudgetConfigProxy.Create(enabled: false);
            var fakeIndexerService = FakeIndexerStatusService.Create(5);
            var subject = BuildSubject(
                repo,
                config,
                Now,
                out var downloads,
                out _,
                batchRepo: batchRepo,
                indexerStatusService: (IIndexerStatusService)(object)fakeIndexerService);

            var decisions = new List<DownloadDecision>
            {
                BuildDecision(new Book { Id = 23000, Title = "Audiobook 1", MediaType = BookMediaType.Audiobook }, "Release 1", Quality.MP3, DownloadProtocol.Torrent, indexerId: 5),
                BuildDecision(new Book { Id = 23001, Title = "Ebook 2", MediaType = BookMediaType.Ebook }, "Release 2", Quality.EPUB, DownloadProtocol.Torrent, indexerId: 5)
            };

            var result = await subject.ProcessDecisions(decisions);

            Assert.Multiple(() =>
            {
                Assert.That(result.Grabbed, Has.Count.EqualTo(2));
                Assert.That(downloads.Downloaded, Has.Count.EqualTo(2));
                Assert.That(subject.GrabBudgetResult.StopReason, Is.EqualTo(GrabBudgetStopReason.None));
                Assert.That(batchRepo.Store, Is.Empty);
            });
        }

        [Test]
        public async Task batch_row_recorded_with_stop_reason_and_counts()
        {
            var repo = InMemoryGrabBudgetLogRepository.Create();
            var batchRepo = InMemoryGrabBudgetBatchRepository.Create();
            var config = GrabBudgetConfigProxy.Create(enabled: true, maxPerRun: 3, maxPerDay: 50);
            var subject = BuildSubject(repo, config, Now, out var downloads, out _, batchRepo: batchRepo);

            var decisions = BuildDecisions(3, BookMediaType.Audiobook, Quality.MP3, startId: 24000)
                .Concat(BuildDecisions(2, BookMediaType.Ebook, Quality.EPUB, startId: 25000))
                .ToList();

            var result = await subject.ProcessDecisions(decisions);

            Assert.Multiple(() =>
            {
                Assert.That(result.Grabbed, Has.Count.EqualTo(3));
                Assert.That(downloads.Downloaded, Has.Count.EqualTo(3));
                Assert.That(subject.GrabBudgetResult.StopReason, Is.EqualTo(GrabBudgetStopReason.MaxPerRunReached));
                Assert.That(batchRepo.Store, Has.Count.EqualTo(1));
                Assert.That(batchRepo.Store[0].Grabbed, Is.EqualTo(3));
                Assert.That(batchRepo.Store[0].Skipped, Is.EqualTo(2));
                Assert.That(batchRepo.Store[0].Failed, Is.EqualTo(0));
                Assert.That(batchRepo.Store[0].StopReason, Is.EqualTo(GrabBudgetStopReason.MaxPerRunReached));
                Assert.That(batchRepo.Store[0].StartedAt, Is.EqualTo(DateTime.UtcNow).Within(TimeSpan.FromMinutes(1)));
            });
        }

        [Test]
        public void record_batch_prunes_rows_older_than_thirty_days()
        {
            var repo = InMemoryGrabBudgetLogRepository.Create();
            var batchRepo = InMemoryGrabBudgetBatchRepository.Create();
            batchRepo.Store.Add(new GrabBudgetBatch { Id = 1, StartedAt = Now.AddDays(-35) });

            var config = GrabBudgetConfigProxy.Create(enabled: true);
            var service = new GrabBudgetService(
                (IGrabBudgetLogRepository)(object)repo,
                config,
                new FixedGrabBudgetClock(Now),
                LogManager.GetLogger("GrabBudgetFixture"),
                (IGrabBudgetBatchRepository)(object)batchRepo);

            service.RecordBatch(1, 0, 0, GrabBudgetStopReason.None);

            Assert.Multiple(() =>
            {
                Assert.That(batchRepo.Store, Has.Count.EqualTo(1));
                Assert.That(batchRepo.Store[0].StartedAt, Is.EqualTo(Now));
            });
        }

        [Test]
        public async Task dry_run_with_50_eligible_limit_5_downloads_zero_and_records_batch_with_details()
        {
            var repo = InMemoryGrabBudgetLogRepository.Create();
            var batchRepo = InMemoryGrabBudgetBatchRepository.Create();
            var config = GrabBudgetConfigProxy.Create(enabled: true, maxPerRun: 5, maxPerDay: 25, dryRun: true);
            var subject = BuildSubject(repo, config, Now, out var downloads, out _, batchRepo: batchRepo);

            var decisions = BuildDecisions(50, BookMediaType.Audiobook, Quality.MP3, startId: 1000);

            var result = await subject.ProcessDecisions(decisions);

            Assert.Multiple(() =>
            {
                Assert.That(result.Grabbed, Is.Empty);
                Assert.That(downloads.Downloaded, Is.Empty);
                Assert.That(repo.Store, Is.Empty);
                Assert.That(batchRepo.Store, Has.Count.EqualTo(1));

                var batch = batchRepo.Store[0];
                Assert.That(batch.Grabbed, Is.EqualTo(5));
                Assert.That(batch.StopReason, Is.EqualTo(GrabBudgetStopReason.MaxPerRunReached));
                Assert.That(batch.Details, Is.Not.Null);

                var lines = batch.Details.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                var wouldGrabLines = lines.Where(l => l.StartsWith("[would grab]")).ToList();
                Assert.That(wouldGrabLines, Has.Count.EqualTo(5));
            });
        }

        [Test]
        public async Task dry_run_off_records_batch_with_null_details()
        {
            var repo = InMemoryGrabBudgetLogRepository.Create();
            var batchRepo = InMemoryGrabBudgetBatchRepository.Create();
            var config = GrabBudgetConfigProxy.Create(enabled: true, maxPerRun: 5, maxPerDay: 25, dryRun: false);
            var subject = BuildSubject(repo, config, Now, out var downloads, out _, batchRepo: batchRepo);

            var decisions = BuildDecisions(10, BookMediaType.Audiobook, Quality.MP3, startId: 1000);

            var result = await subject.ProcessDecisions(decisions);

            Assert.Multiple(() =>
            {
                Assert.That(result.Grabbed, Has.Count.EqualTo(5));
                Assert.That(downloads.Downloaded, Has.Count.EqualTo(5));
                Assert.That(repo.Store, Has.Count.EqualTo(5));
                Assert.That(batchRepo.Store, Has.Count.EqualTo(1));
                Assert.That(batchRepo.Store[0].Details, Is.Null);
            });
        }

        [Test]
        public void batch_repository_latest_returns_newest_first()
        {
            var batchRepo = InMemoryGrabBudgetBatchRepository.Create();
            var repoInterface = (IGrabBudgetBatchRepository)(object)batchRepo;
            repoInterface.Insert(new GrabBudgetBatch { StartedAt = Now.AddMinutes(-10), Grabbed = 1 });
            repoInterface.Insert(new GrabBudgetBatch { StartedAt = Now.AddMinutes(-5), Grabbed = 2 });
            repoInterface.Insert(new GrabBudgetBatch { StartedAt = Now, Grabbed = 3 });

            var latest = repoInterface.Latest(2);

            Assert.Multiple(() =>
            {
                Assert.That(latest, Has.Count.EqualTo(2));
                Assert.That(latest[0].Id, Is.EqualTo(3));
                Assert.That(latest[1].Id, Is.EqualTo(2));
            });
        }

        private static ProcessDownloadDecisions BuildSubject(
            InMemoryGrabBudgetLogRepository repo,
            IConfigService config,
            DateTime now,
            out RecordingDownloadService downloads,
            out RecordingQueueService queue,
            int activeQueueCount = 0,
            IDownloadService downloadService = null,
            InMemoryGrabBudgetBatchRepository batchRepo = null,
            IIndexerStatusService indexerStatusService = null)
        {
            downloads = downloadService as RecordingDownloadService ?? new RecordingDownloadService();
            var effectiveDownloads = downloadService ?? downloads;
            queue = new RecordingQueueService(activeQueueCount);
            var budget = new GrabBudgetService(
                (IGrabBudgetLogRepository)(object)repo,
                config,
                new FixedGrabBudgetClock(now),
                LogManager.GetLogger("GrabBudgetFixture"),
                batchRepo != null ? (IGrabBudgetBatchRepository)(object)batchRepo : null);

            return new ProcessDownloadDecisions(
                effectiveDownloads,
                new IdentityPrioritizer(),
                new NoopPendingReleaseService(),
                LogManager.GetLogger("GrabBudgetFixture"),
                config,
                budget,
                queue,
                indexerStatusService);
        }

        private static List<DownloadDecision> BuildDecisions(
            int count,
            BookMediaType mediaType,
            Quality quality,
            int startId,
            DownloadProtocol protocol = DownloadProtocol.Torrent,
            int indexerId = 1)
        {
            return Enumerable.Range(0, count)
                .Select(i => BuildDecision(
                    new Book { Id = startId + i, Title = $"Book {startId + i}", MediaType = mediaType },
                    $"Release {startId + i}",
                    quality,
                    protocol,
                    indexerId))
                .ToList();
        }

        private static DownloadDecision BuildDecision(
            Book book,
            string title,
            Quality quality,
            DownloadProtocol protocol = DownloadProtocol.Torrent,
            int indexerId = 1)
        {
            var author = new Author { Id = 38, Name = "Joe Abercrombie" };
            return new DownloadDecision(new RemoteBook
            {
                Author = author,
                Books = new List<Book> { book },
                Release = new ReleaseInfo
                {
                    Title = title,
                    DownloadProtocol = protocol,
                    IndexerId = indexerId,
                    PublishDate = Now
                },
                ParsedBookInfo = new ParsedBookInfo
                {
                    Quality = new QualityModel(quality)
                }
            });
        }

        private sealed class RecordingDownloadService : IDownloadService
        {
            public List<RemoteBook> Downloaded { get; } = new();

            public Task DownloadReport(RemoteBook remoteBook, int? downloadClientId)
            {
                Downloaded.Add(remoteBook);
                return Task.CompletedTask;
            }
        }

        private sealed class FailingDownloadService : IDownloadService
        {
            private readonly int _failCount;
            private int _attempts;

            public FailingDownloadService(int failCount = int.MaxValue)
            {
                _failCount = failCount;
            }

            public Task DownloadReport(RemoteBook remoteBook, int? downloadClientId)
            {
                _attempts++;
                if (_attempts <= _failCount)
                {
                    throw new DownloadClientUnavailableException("Client is unavailable");
                }

                return Task.CompletedTask;
            }
        }

        private sealed class SequenceDownloadService : IDownloadService
        {
            private readonly bool[] _failSequence;
            private int _callIndex;

            public SequenceDownloadService(params bool[] failSequence)
            {
                _failSequence = failSequence;
            }

            public Task DownloadReport(RemoteBook remoteBook, int? downloadClientId)
            {
                var shouldFail = _callIndex < _failSequence.Length && _failSequence[_callIndex];
                _callIndex++;

                if (shouldFail)
                {
                    throw new DownloadClientUnavailableException("Client is unavailable");
                }

                return Task.CompletedTask;
            }
        }

        private sealed class RecordingQueueService : IQueueService
        {
            private readonly List<QueueItem> _queue;

            public RecordingQueueService(int count)
            {
                _queue = Enumerable.Range(0, count).Select(_ => new QueueItem()).ToList();
            }

            public List<QueueItem> GetQueue() => _queue.ToList();
            public QueueItem Find(int id) => _queue.FirstOrDefault(q => q.Id == id);
            public void Remove(int id) => _queue.RemoveAll(q => q.Id == id);
        }

        private sealed class IdentityPrioritizer : IPrioritizeDownloadDecision
        {
            public List<DownloadDecision> PrioritizeDecisions(List<DownloadDecision> decisions)
            {
                return decisions;
            }
        }

        private sealed class NoopPendingReleaseService : IPendingReleaseService
        {
            public void Add(DownloadDecision decision, PendingReleaseReason reason) { }
            public void AddMany(List<Tuple<DownloadDecision, PendingReleaseReason>> decisions) { }
            public List<ReleaseInfo> GetPending() => new();
            public List<RemoteBook> GetPendingRemoteBooks(int authorId) => new();
            public List<QueueItem> GetPendingQueue() => new();
            public QueueItem FindPendingQueueItem(int queueId) => null;
            public void RemovePendingQueueItems(int queueId) { }
            public RemoteBook OldestPendingRelease(int authorId, int[] bookIds) => null;
        }

        private sealed class FixedGrabBudgetClock : IGrabBudgetClock
        {
            public FixedGrabBudgetClock(DateTime utcNow)
            {
                UtcNow = utcNow;
            }

            public DateTime UtcNow { get; set; }
        }

        public class GrabBudgetConfigProxy : DispatchProxy
        {
            public bool GrabBudgetEnabled { get; set; }
            public int GrabBudgetMaxPerRun { get; set; } = 5;
            public int GrabBudgetMaxPerDay { get; set; } = 25;
            public int GrabBudgetMaxActiveQueue { get; set; }
            public bool GrabBudgetApplyToInteractive { get; set; }
            public int GrabBudgetMaxConsecutiveFailures { get; set; } = 3;
            public bool GrabBudgetDryRun { get; set; }

            protected override object Invoke(MethodInfo targetMethod, object[] args)
            {
                switch (targetMethod?.Name)
                {
                    case "get_GrabBudgetEnabled": return GrabBudgetEnabled;
                    case "set_GrabBudgetEnabled": GrabBudgetEnabled = (bool)args[0]; return null;
                    case "get_GrabBudgetMaxPerRun": return GrabBudgetMaxPerRun;
                    case "set_GrabBudgetMaxPerRun": GrabBudgetMaxPerRun = (int)args[0]; return null;
                    case "get_GrabBudgetMaxPerDay": return GrabBudgetMaxPerDay;
                    case "set_GrabBudgetMaxPerDay": GrabBudgetMaxPerDay = (int)args[0]; return null;
                    case "get_GrabBudgetMaxActiveQueue": return GrabBudgetMaxActiveQueue;
                    case "set_GrabBudgetMaxActiveQueue": GrabBudgetMaxActiveQueue = (int)args[0]; return null;
                    case "get_GrabBudgetApplyToInteractive": return GrabBudgetApplyToInteractive;
                    case "set_GrabBudgetApplyToInteractive": GrabBudgetApplyToInteractive = (bool)args[0]; return null;
                    case "get_GrabBudgetMaxConsecutiveFailures": return GrabBudgetMaxConsecutiveFailures;
                    case "set_GrabBudgetMaxConsecutiveFailures": GrabBudgetMaxConsecutiveFailures = (int)args[0]; return null;
                    case "get_GrabBudgetDryRun": return GrabBudgetDryRun;
                    case "set_GrabBudgetDryRun": GrabBudgetDryRun = (bool)args[0]; return null;
                    default: throw new NotImplementedException($"Test proxy does not implement {targetMethod?.Name}");
                }
            }

            public static IConfigService Create(
                bool enabled = false,
                int maxPerRun = 5,
                int maxPerDay = 25,
                int maxActiveQueue = 0,
                bool applyToInteractive = false,
                int maxConsecutiveFailures = 3,
                bool dryRun = false)
            {
                var service = DispatchProxy.Create<IConfigService, GrabBudgetConfigProxy>();
                var proxy = (GrabBudgetConfigProxy)service;
                proxy.GrabBudgetEnabled = enabled;
                proxy.GrabBudgetMaxPerRun = maxPerRun;
                proxy.GrabBudgetMaxPerDay = maxPerDay;
                proxy.GrabBudgetMaxActiveQueue = maxActiveQueue;
                proxy.GrabBudgetApplyToInteractive = applyToInteractive;
                proxy.GrabBudgetMaxConsecutiveFailures = maxConsecutiveFailures;
                proxy.GrabBudgetDryRun = dryRun;
                return service;
            }
        }

        public class InMemoryGrabBudgetLogRepository : DispatchProxy
        {
            public List<GrabBudgetLog> Store { get; } = new();
            private int _nextId;

            protected override object Invoke(MethodInfo targetMethod, object[] args)
            {
                switch (targetMethod?.Name)
                {
                    case "Insert":
                        var model = (GrabBudgetLog)args[0];
                        model.Id = ++_nextId;
                        Store.Add(model);
                        return model;
                    case "Since":
                        var cutoff = (DateTime)args[0];
                        return Store.Where(r => r.GrabbedAt >= cutoff).ToList();
                    case "DeleteBefore":
                        var before = (DateTime)args[0];
                        Store.RemoveAll(r => r.GrabbedAt < before);
                        return null;
                    case "All":
                        return Store.ToList();
                    default: throw new NotImplementedException($"Test repository does not implement {targetMethod?.Name}");
                }
            }

            public static InMemoryGrabBudgetLogRepository Create()
            {
                // Returns the proxy as its concrete stub type so tests can inspect Store.
                return (InMemoryGrabBudgetLogRepository)DispatchProxy.Create<IGrabBudgetLogRepository, InMemoryGrabBudgetLogRepository>();
            }
        }

        public class InMemoryGrabBudgetBatchRepository : DispatchProxy
        {
            public List<GrabBudgetBatch> Store { get; } = new();
            private int _nextId;

            protected override object Invoke(MethodInfo targetMethod, object[] args)
            {
                switch (targetMethod?.Name)
                {
                    case "Insert":
                        var model = (GrabBudgetBatch)args[0];
                        model.Id = ++_nextId;
                        Store.Add(model);
                        return model;
                    case "DeleteBefore":
                        var before = (DateTime)args[0];
                        Store.RemoveAll(r => r.StartedAt < before);
                        return null;
                    case "Latest":
                        var count = (int)args[0];
                        return Store.OrderByDescending(b => b.StartedAt).ThenByDescending(b => b.Id).Take(count).ToList();
                    case "All":
                        return Store.ToList();
                    default: throw new NotImplementedException($"Test repository does not implement {targetMethod?.Name}");
                }
            }

            public List<GrabBudgetBatch> Latest(int count)
            {
                return Store.OrderByDescending(b => b.StartedAt).ThenByDescending(b => b.Id).Take(count).ToList();
            }

            public static InMemoryGrabBudgetBatchRepository Create()
            {
                return (InMemoryGrabBudgetBatchRepository)DispatchProxy.Create<IGrabBudgetBatchRepository, InMemoryGrabBudgetBatchRepository>();
            }
        }

        public class FakeIndexerStatusService : DispatchProxy
        {
            public List<IndexerStatus> BlockedIndexers { get; } = new();

            protected override object Invoke(MethodInfo targetMethod, object[] args)
            {
                switch (targetMethod?.Name)
                {
                    case "GetBlockedProviders":
                        return BlockedIndexers.ToList();
                    default: throw new NotImplementedException($"Test indexer status service does not implement {targetMethod?.Name}");
                }
            }

            public static FakeIndexerStatusService Create(params int[] blockedIndexerIds)
            {
                var service = (FakeIndexerStatusService)DispatchProxy.Create<IIndexerStatusService, FakeIndexerStatusService>();
                foreach (var id in blockedIndexerIds)
                {
                    service.BlockedIndexers.Add(new IndexerStatus
                    {
                        ProviderId = id,
                        DisabledTill = DateTime.UtcNow.AddHours(1)
                    });
                }
                return service;
            }
        }
    }
}
