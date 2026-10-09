using System;
using System.Collections.Generic;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Download.GrabBudget
{
    public interface IGrabBudgetLogRepository : IBasicRepository<GrabBudgetLog>
    {
        List<GrabBudgetLog> Since(DateTime cutoff);
        void DeleteBefore(DateTime cutoff);
    }

    public class GrabBudgetLogRepository : BasicRepository<GrabBudgetLog>, IGrabBudgetLogRepository
    {
        public GrabBudgetLogRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public List<GrabBudgetLog> Since(DateTime cutoff)
        {
            return Query(x => x.GrabbedAt >= cutoff);
        }

        public void DeleteBefore(DateTime cutoff)
        {
            Delete(x => x.GrabbedAt < cutoff);
        }
    }
}
