using System;
using NLog;
using NzbDrone.Core.Configuration;

namespace NzbDrone.Core.Download.GrabBudget
{
    public interface IGrabBudgetService
    {
        int GrabsInLastDay(DateTime utcNow);
        void RecordGrab(DateTime? grabbedAt = null);
        void Prune(DateTime? utcNow = null);
        GrabBudgetResult CheckBudget(int grabbedThisRun, int activeQueueCount, DateTime? utcNow = null);
    }

    public class GrabBudgetService : IGrabBudgetService
    {
        public static readonly TimeSpan RollingWindow = TimeSpan.FromHours(24);

        private readonly IGrabBudgetLogRepository _repository;
        private readonly IConfigService _configService;
        private readonly IGrabBudgetClock _clock;
        private readonly Logger _logger;

        public GrabBudgetService(IGrabBudgetLogRepository repository,
                                 IConfigService configService,
                                 IGrabBudgetClock clock,
                                 Logger logger)
        {
            _repository = repository;
            _configService = configService;
            _clock = clock;
            _logger = logger;
        }

        public int GrabsInLastDay(DateTime utcNow)
        {
            return _repository.Since(utcNow - RollingWindow).Count;
        }

        public void RecordGrab(DateTime? grabbedAt = null)
        {
            var at = grabbedAt ?? _clock.UtcNow;

            _repository.Insert(new GrabBudgetLog { GrabbedAt = at });
            Prune(at);
        }

        public void Prune(DateTime? utcNow = null)
        {
            var now = utcNow ?? _clock.UtcNow;

            _repository.DeleteBefore(now - RollingWindow);
        }

        public GrabBudgetResult CheckBudget(int grabbedThisRun, int activeQueueCount, DateTime? utcNow = null)
        {
            if (!_configService.GrabBudgetEnabled)
            {
                return GrabBudgetResult.Allow();
            }

            var now = utcNow ?? _clock.UtcNow;
            var maxPerRun = _configService.GrabBudgetMaxPerRun;
            var maxPerDay = _configService.GrabBudgetMaxPerDay;
            var maxActiveQueue = _configService.GrabBudgetMaxActiveQueue;

            if (maxPerRun > 0 && grabbedThisRun >= maxPerRun)
            {
                return GrabBudgetResult.Block(
                    GrabBudgetStopReason.MaxPerRunReached,
                    $"Grab budget per-run limit reached ({grabbedThisRun}/{maxPerRun}).");
            }

            if (maxPerDay > 0 && GrabsInLastDay(now) >= maxPerDay)
            {
                return GrabBudgetResult.Block(
                    GrabBudgetStopReason.MaxPerDayReached,
                    $"Grab budget daily limit reached ({maxPerDay} grabs in the last 24 hours).");
            }

            if (maxActiveQueue > 0 && activeQueueCount >= maxActiveQueue)
            {
                return GrabBudgetResult.Block(
                    GrabBudgetStopReason.MaxActiveQueueReached,
                    $"Grab budget queue ceiling reached ({activeQueueCount}/{maxActiveQueue} active).");
            }

            return GrabBudgetResult.Allow();
        }
    }
}
