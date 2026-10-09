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
using NzbDrone.Core.Download.GrabBudget;
using NzbDrone.Core.Download.Pending;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Queue;

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
            var service = new GrabBudgetService(repo, config, new FixedGrabBudgetClock(Now), LogManager.GetLogger("GrabBudgetFixture"));

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
            var service = new GrabBudgetService(repo, config, new FixedGrabBudgetClock(Now), LogManager.GetLogger("GrabBudgetFixture"));

            service.RecordGrab();

            Assert.Multiple(() =>
            {
                Assert.That(repo.Store, Has.Count.EqualTo(1));
                Assert.That(repo.Store.Single().GrabbedAt, Is.EqualTo(Now));
            });
        }

        private static ProcessDownloadDecisions BuildSubject(
            IGrabBudgetLogRepository repo,
            IConfigService config,
            DateTime now,
            out RecordingDownloadService downloads,
            out RecordingQueueService queue,
            int activeQueueCount = 0)
        {
            downloads = new RecordingDownloadService();
            queue = new RecordingQueueService(activeQueueCount);
            var budget = new GrabBudgetService(repo, config, new FixedGrabBudgetClock(now), LogManager.GetLogger("GrabBudgetFixture"));

            return new ProcessDownloadDecisions(
                downloads,
                new IdentityPrioritizer(),
                new NoopPendingReleaseService(),
                LogManager.GetLogger("GrabBudgetFixture"),
                config,
                budget,
                queue);
        }

        private static List<DownloadDecision> BuildDecisions(int count, BookMediaType mediaType, Quality quality, int startId)
        {
            return Enumerable.Range(0, count)
                .Select(i => BuildDecision(new Book { Id = startId + i, Title = $"Book {startId + i}", MediaType = mediaType }, $"Release {startId + i}", quality))
                .ToList();
        }

        private static DownloadDecision BuildDecision(Book book, string title, Quality quality)
        {
            var author = new Author { Id = 38, Name = "Joe Abercrombie" };
            return new DownloadDecision(new RemoteBook
            {
                Author = author,
                Books = new List<Book> { book },
                Release = new ReleaseInfo
                {
                    Title = title,
                    DownloadProtocol = DownloadProtocol.Torrent,
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

        private sealed class RecordingQueueService : IQueueService
        {
            private readonly List<Queue> _queue;

            public RecordingQueueService(int count)
            {
                _queue = Enumerable.Range(0, count).Select(_ => new Queue()).ToList();
            }

            public List<Queue> GetQueue() => _queue.ToList();
            public Queue Find(int id) => _queue.FirstOrDefault(q => q.Id == id);
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
            public List<Queue> GetPendingQueue() => new();
            public Queue FindPendingQueueItem(int queueId) => null;
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
                    default: throw new NotImplementedException($"Test proxy does not implement {targetMethod?.Name}");
                }
            }

            public static IConfigService Create(bool enabled = false, int maxPerRun = 5, int maxPerDay = 25, int maxActiveQueue = 0, bool applyToInteractive = false)
            {
                var service = DispatchProxy.Create<IConfigService, GrabBudgetConfigProxy>();
                var proxy = (GrabBudgetConfigProxy)service;
                proxy.GrabBudgetEnabled = enabled;
                proxy.GrabBudgetMaxPerRun = maxPerRun;
                proxy.GrabBudgetMaxPerDay = maxPerDay;
                proxy.GrabBudgetMaxActiveQueue = maxActiveQueue;
                proxy.GrabBudgetApplyToInteractive = applyToInteractive;
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
    }
}
