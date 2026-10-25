using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Download.GrabBudget
{
    public interface IGrabBudgetBatchRepository : IBasicRepository<GrabBudgetBatch>
    {
        void DeleteBefore(DateTime cutoff);
        List<GrabBudgetBatch> Latest(int count);
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

        public List<GrabBudgetBatch> Latest(int count)
        {
            return All().OrderByDescending(x => x.StartedAt).ThenByDescending(x => x.Id).Take(count).ToList();
        }
    }
}
