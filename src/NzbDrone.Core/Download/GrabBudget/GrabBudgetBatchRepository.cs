using System;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Download.GrabBudget
{
    public interface IGrabBudgetBatchRepository : IBasicRepository<GrabBudgetBatch>
    {
        void DeleteBefore(DateTime cutoff);
    }

    public class GrabBudgetBatchRepository : BasicRepository<GrabBudgetBatch>, IGrabBudgetBatchRepository
    {
        public GrabBudgetBatchRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public void DeleteBefore(DateTime cutoff)
        {
            Delete(x => x.StartedAt < cutoff);
        }
    }
}
