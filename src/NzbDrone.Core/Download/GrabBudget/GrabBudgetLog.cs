using System;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Download.GrabBudget
{
    public class GrabBudgetLog : ModelBase
    {
        public DateTime GrabbedAt { get; set; }
    }
}
