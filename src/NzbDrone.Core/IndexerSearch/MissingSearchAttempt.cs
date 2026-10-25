using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.IndexerSearch
{
    // One row per wanted book row (a language x medium variant is its own Book row) that the
    // scheduled missing search has tried. Powers the per-row retry cap.
    public class MissingSearchAttempt : ModelBase
    {
        public int BookId { get; set; }
        public int Attempts { get; set; }
        public DateTime LastAttemptAt { get; set; }
    }

    public interface IMissingSearchAttemptRepository : IBasicRepository<MissingSearchAttempt>
    {
        List<MissingSearchAttempt> ForBooks(IEnumerable<int> bookIds);
        void RecordAttempt(int bookId, DateTime at, bool restart);
        void DeleteExcept(ICollection<int> bookIdsToKeep);
    }

    public class MissingSearchAttemptRepository : BasicRepository<MissingSearchAttempt>, IMissingSearchAttemptRepository
    {
        public MissingSearchAttemptRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public List<MissingSearchAttempt> ForBooks(IEnumerable<int> bookIds)
        {
            var ids = bookIds.ToHashSet();
            return All().Where(a => ids.Contains(a.BookId)).ToList();
        }

        // restart = the retry cap had been reached and its reset window passed: begin a new count.
        public void RecordAttempt(int bookId, DateTime at, bool restart)
        {
            var existing = Query(a => a.BookId == bookId).FirstOrDefault();

            if (existing == null)
            {
                Insert(new MissingSearchAttempt { BookId = bookId, Attempts = 1, LastAttemptAt = at });
                return;
            }

            existing.Attempts = restart ? 1 : existing.Attempts + 1;
            existing.LastAttemptAt = at;
            Update(existing);
        }

        // Books that are no longer missing (imported, unmonitored, deleted) start from zero next time.
        public void DeleteExcept(ICollection<int> bookIdsToKeep)
        {
            var stale = All().Where(a => !bookIdsToKeep.Contains(a.BookId)).Select(a => a.Id).ToList();
            if (stale.Count > 0)
            {
                DeleteMany(stale);
            }
        }
    }
}
