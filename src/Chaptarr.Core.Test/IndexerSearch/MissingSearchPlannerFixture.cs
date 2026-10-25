using System;
using System.Linq;
using NUnit.Framework;
using NzbDrone.Core.IndexerSearch;

namespace Chaptarr.Core.Test.IndexerSearch
{
    [TestFixture]
    public class MissingSearchPlannerFixture
    {
        private static readonly DateTime Now = new DateTime(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc);

        private static MissingSearchPlanOptions Options(int maxRows = 0, int cooldownHours = 24, int maxAttempts = 5) => new()
        {
            MaxRows = maxRows,
            Cooldown = TimeSpan.FromHours(cooldownHours),
            MaxAttempts = maxAttempts
        };

        private static MissingSearchCandidate C(int book, int author, double? searchedHoursAgo = null, int attempts = 0, double? attemptHoursAgo = null) => new()
        {
            BookId = book,
            AuthorId = author,
            LastSearchTime = searchedHoursAgo.HasValue ? Now.AddHours(-searchedHoursAgo.Value) : null,
            Attempts = attempts,
            LastAttemptAt = attemptHoursAgo.HasValue ? Now.AddHours(-attemptHoursAgo.Value) : null
        };

        [Test]
        public void never_searched_rows_are_planned_oldest_first_within_an_author()
        {
            var plan = MissingSearchPlanner.Plan(new[] { C(1, 10, 30), C(2, 10), C(3, 10, 100) }, Now, Options());

            Assert.That(plan.Select(c => c.BookId), Is.EqualTo(new[] { 2, 3, 1 }));
        }

        [Test]
        public void rows_inside_the_cooldown_are_skipped()
        {
            var plan = MissingSearchPlanner.Plan(new[] { C(1, 10, 2), C(2, 10, 25), C(3, 10, 100, attempts: 1, attemptHoursAgo: 3) }, Now, Options());

            Assert.That(plan.Select(c => c.BookId), Is.EqualTo(new[] { 2 }));
        }

        [Test]
        public void authors_are_served_round_robin_up_to_max_rows()
        {
            var plan = MissingSearchPlanner.Plan(
                new[] { C(1, 10), C(2, 10), C(3, 10), C(4, 20), C(5, 30) }, Now, Options(maxRows: 4));

            Assert.That(plan.Select(c => c.AuthorId), Is.EqualTo(new[] { 10, 20, 30, 10 }));
        }

        [Test]
        public void sibling_variant_rows_of_one_work_are_all_planned()
        {
            // mv1: one Book row per language x medium, all under the same author.
            var plan = MissingSearchPlanner.Plan(new[] { C(1, 10), C(2, 10), C(3, 10) }, Now, Options());

            Assert.That(plan.Count, Is.EqualTo(3));
        }

        [Test]
        public void retry_capped_rows_are_skipped_until_the_reset_window_passes()
        {
            var capped = C(1, 10, 100, attempts: 5, attemptHoursAgo: 48);
            var resetDue = C(2, 10, 1000, attempts: 5, attemptHoursAgo: 31 * 24);
            var below = C(3, 10, 100, attempts: 4, attemptHoursAgo: 48);

            var plan = MissingSearchPlanner.Plan(new[] { capped, resetDue, below }, Now, Options());

            Assert.That(plan.Select(c => c.BookId), Is.EquivalentTo(new[] { 2, 3 }));
        }

        [Test]
        public void a_zero_cap_means_no_retry_limit()
        {
            var plan = MissingSearchPlanner.Plan(new[] { C(1, 10, 100, attempts: 99, attemptHoursAgo: 48) }, Now, Options(maxAttempts: 0));

            Assert.That(plan.Count, Is.EqualTo(1));
        }
    }
}
