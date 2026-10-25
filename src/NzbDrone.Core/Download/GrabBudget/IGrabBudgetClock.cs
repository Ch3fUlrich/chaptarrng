using System;

namespace NzbDrone.Core.Download.GrabBudget
{
    public interface IGrabBudgetClock
    {
        DateTime UtcNow { get; }
    }

    public class GrabBudgetSystemClock : IGrabBudgetClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }
}
