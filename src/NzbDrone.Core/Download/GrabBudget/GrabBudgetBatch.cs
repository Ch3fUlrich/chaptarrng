using System;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Download.GrabBudget
{
    public class GrabBudgetBatch : ModelBase
    {
        public DateTime StartedAt { get; set; }
        public int Grabbed { get; set; }
        public int Skipped { get; set; }
        public int Failed { get; set; }
        public GrabBudgetStopReason StopReason { get; set; }
        public string Details { get; set; }
    }
}
