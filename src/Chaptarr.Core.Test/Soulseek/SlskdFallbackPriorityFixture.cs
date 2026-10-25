using System.Collections.Generic;
using System.Linq;
using Chaptarr.Core.Test;
using NLog;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Indexers.Soulseek;
using NzbDrone.Core.Soulseek;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles.Delay;
using NzbDrone.Core.Qualities;

namespace Chaptarr.Core.Test.Soulseek
{
    // Proves the documented fallback behaviour: with Usenet preferred (or a lower indexer priority for
    // slskd), Soulseek releases only win when nothing else is acceptable. See docs/slskd.md.
    [TestFixture]
    public class SlskdFallbackPriorityFixture
    {
        private sealed class FakeDelayProfiles : IDelayProfileService
        {
            public DelayProfile Profile { get; set; }

            public DelayProfile BestForTags(HashSet<int> tagIds) => Profile;
            public DelayProfile Add(DelayProfile profile) => profile;
            public DelayProfile Update(DelayProfile profile) => profile;
            public void Delete(int id) { }
            public List<DelayProfile> All() => new() { Profile };
            public DelayProfile Get(int id) => Profile;
            public List<DelayProfile> AllForTag(int tagId) => new() { Profile };
            public List<DelayProfile> AllForTags(HashSet<int> tagIds) => new() { Profile };
            public List<DelayProfile> Reorder(int id, int? afterId) => new() { Profile };
        }

        private static DownloadDecision Decision(DownloadProtocol protocol, int priority, long size)
        {
            var author = new Author { Name = "Frank Herbert" };
            return new DownloadDecision(new RemoteBook
            {
                Author = author,
                Books = new List<Book> { new() { Author = author, Title = "Dune", MediaType = BookMediaType.Ebook } },
                Release = new ReleaseInfo { Title = $"Frank Herbert - Dune {protocol}", Size = size, DownloadProtocol = protocol, IndexerPriority = priority },
                ParsedBookInfo = new ParsedBookInfo { Quality = new QualityModel { Quality = Quality.EPUB } }
            });
        }

        private static DownloadDecisionComparer Comparer(DownloadProtocol preferred)
        {
            var profiles = new FakeDelayProfiles { Profile = new DelayProfile { PreferredProtocol = preferred } };
            return new DownloadDecisionComparer(ConfigServiceTestProxy.Create(), profiles, null, LogManager.CreateNullLogger());
        }

        [Test]
        public void usenet_wins_over_a_better_soulseek_release_when_usenet_is_preferred()
        {
            var usenet = Decision(DownloadProtocol.Usenet, 25, 1_000);
            var soulseek = Decision(DownloadProtocol.Soulseek, 25, 5_000_000);

            var ordered = new[] { soulseek, usenet }.OrderByDescending(d => d, Comparer(DownloadProtocol.Usenet)).ToList();

            Assert.That(ordered.First(), Is.SameAs(usenet));
        }

        [Test]
        public void protocol_preference_outranks_indexer_priority()
        {
            // Usenet preferred, slskd given the "best" indexer priority: Usenet must still win.
            var usenet = Decision(DownloadProtocol.Usenet, 50, 1_000);
            var soulseek = Decision(DownloadProtocol.Soulseek, 1, 5_000_000);

            var ordered = new[] { soulseek, usenet }.OrderByDescending(d => d, Comparer(DownloadProtocol.Usenet)).ToList();

            Assert.That(ordered.First(), Is.SameAs(usenet));
        }

        [Test]
        public void indexer_priority_decides_when_the_preferred_protocol_is_not_a_candidate()
        {
            var usenet = Decision(DownloadProtocol.Usenet, 25, 1_000);
            var soulseek = Decision(DownloadProtocol.Soulseek, 50, 5_000_000);
            var preferTorrent = Comparer(DownloadProtocol.Torrent);

            Assert.That(new[] { soulseek, usenet }.OrderByDescending(d => d, preferTorrent).First(), Is.SameAs(usenet));

            // Flip the priorities: now the slskd release wins, so priority (not a hidden protocol rule) decides.
            var usenetLow = Decision(DownloadProtocol.Usenet, 50, 1_000);
            var soulseekHigh = Decision(DownloadProtocol.Soulseek, 1, 5_000_000);
            Assert.That(new[] { usenetLow, soulseekHigh }.OrderByDescending(d => d, preferTorrent).First(), Is.SameAs(soulseekHigh));
        }

        [Test]
        public void soulseek_releases_are_stamped_with_the_search_time_so_the_delay_gate_has_an_age()
        {
            var responses = SlskdFixtures.Search("search-ebook.json").Responses;
            var releases = SlskdReleaseBuilder.Build(responses, new SlskdSearchContext { Author = "Frank Herbert", Title = "Dune", MediaType = BookMediaType.Ebook }, new SlskdIndexerSettings());

            Assert.That(releases, Is.Not.Empty);
            Assert.That(releases.Select(r => r.AgeMinutes), Has.All.LessThan(1));
        }

        [TestCase(0, 0, 0)]
        [TestCase(60, 0, 60)]
        [TestCase(0, 120, 120)]
        [TestCase(60, 120, 120)]
        public void soulseek_waits_at_least_as_long_as_usenet_and_torrent(int usenetDelay, int torrentDelay, int expected)
        {
            var profile = new DelayProfile { UsenetDelay = usenetDelay, TorrentDelay = torrentDelay };

            Assert.That(profile.GetProtocolDelay(DownloadProtocol.Soulseek), Is.EqualTo(expected));
            Assert.That(profile.GetProtocolDelay(DownloadProtocol.Direct), Is.EqualTo(0));
        }
    }
}
