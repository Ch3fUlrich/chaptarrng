using System;
using System.Collections.Generic;
using System.Linq;

namespace NzbDrone.Core.IndexerSearch
{
    public class MissingSearchCandidate
    {
        public int BookId { get; set; }
        public int AuthorId { get; set; }
        public DateTime? LastSearchTime { get; set; }
        public int Attempts { get; set; }
        public DateTime? LastAttemptAt { get; set; }
    }

    public class MissingSearchPlanOptions
    {
        public int MaxRows { get; set; }
        public TimeSpan Cooldown { get; set; }
        public int MaxAttempts { get; set; }
        public TimeSpan RetryCapReset { get; set; } = TimeSpan.FromDays(30);
    }

    public static class MissingSearchPlanner
    {
        // True when a row has used up its retries and the reset window has not yet passed.
        public static bool IsRetryCapped(MissingSearchCandidate c, DateTime now, MissingSearchPlanOptions o)
        {
            return o.MaxAttempts > 0 &&
                   c.Attempts >= o.MaxAttempts &&
                   c.LastAttemptAt.HasValue &&
                   now - c.LastAttemptAt.Value < o.RetryCapReset;
        }

        // Cool-down uses the newer of the book's own search time (any search, manual too) and our last attempt.
        public static DateTime? EffectiveLastSearch(MissingSearchCandidate c)
        {
            if (c.LastSearchTime.HasValue && c.LastAttemptAt.HasValue)
            {
                return c.LastSearchTime > c.LastAttemptAt ? c.LastSearchTime : c.LastAttemptAt;
            }

            return c.LastSearchTime ?? c.LastAttemptAt;
        }

        // Eligible rows, oldest-searched first within each author, then round-robin across authors
        // (authors ordered by their oldest eligible row) up to MaxRows.
        public static List<MissingSearchCandidate> Plan(IEnumerable<MissingSearchCandidate> candidates, DateTime now, MissingSearchPlanOptions options)
        {
            var eligible = candidates
                .Where(c => !IsRetryCapped(c, now, options))
                .Where(c => options.Cooldown <= TimeSpan.Zero ||
                            EffectiveLastSearch(c) is not { } last ||
                            now - last >= options.Cooldown)
                .ToList();

            var queues = eligible
                .GroupBy(c => c.AuthorId)
                .Select(g => new Queue<MissingSearchCandidate>(g
                    .OrderBy(c => EffectiveLastSearch(c) ?? DateTime.MinValue)
                    .ThenBy(c => c.BookId)))
                .OrderBy(q => EffectiveLastSearch(q.Peek()) ?? DateTime.MinValue)
                .ThenBy(q => q.Peek().AuthorId)
                .ToList();

            var limit = options.MaxRows > 0 ? options.MaxRows : int.MaxValue;
            var plan = new List<MissingSearchCandidate>();

            while (plan.Count < limit && queues.Any(q => q.Count > 0))
            {
                foreach (var queue in queues)
                {
                    if (queue.Count > 0 && plan.Count < limit)
                    {
                        plan.Add(queue.Dequeue());
                    }
                }
            }

            return plan;
        }
    }
}
