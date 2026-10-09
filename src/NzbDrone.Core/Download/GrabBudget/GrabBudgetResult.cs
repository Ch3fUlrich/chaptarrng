namespace NzbDrone.Core.Download.GrabBudget
{
    public enum GrabBudgetStopReason
    {
        None,
        MaxPerRunReached,
        MaxPerDayReached,
        MaxActiveQueueReached
    }

    public class GrabBudgetResult
    {
        public bool Allowed { get; set; }
        public GrabBudgetStopReason StopReason { get; set; }
        public string Reason { get; set; }

        public static GrabBudgetResult Allow()
        {
            return new GrabBudgetResult
            {
                Allowed = true,
                StopReason = GrabBudgetStopReason.None,
                Reason = null
            };
        }

        public static GrabBudgetResult Block(GrabBudgetStopReason stopReason, string reason)
        {
            return new GrabBudgetResult
            {
                Allowed = false,
                StopReason = stopReason,
                Reason = reason
            };
        }
    }
}
