using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.Indexers.Soulseek;
using NzbDrone.Core.Soulseek;

namespace Chaptarr.Core.Test.Soulseek
{
    [TestFixture]
    public class SlskdReleaseBuilderFixture
    {
        private static SlskdSearchContext Dune(BookMediaType? mediaType = BookMediaType.Ebook) =>
            new() { Author = "Frank Herbert", Title = "Dune", MediaType = mediaType };

        private static SlskdSearchContext Jesus(BookMediaType? mediaType = BookMediaType.Audiobook) =>
            new() { Author = "Andreas Eschbach", Title = "Das Jesus Video", MediaType = mediaType };

        [Test]
        public void should_build_one_release_per_ebook_file_and_apply_peer_filters()
        {
            var search = SlskdFixtures.Search("search-ebook.json");

            var releases = SlskdReleaseBuilder.Build(search.Responses, Dune(), new SlskdIndexerSettings());

            // alpha: Dune.epub, Dune.pdf, Dune Messiah.epub. beta (queue 120 > 50), 5 KB file, locked file, mp3 and jpg are dropped.
            Assert.That(releases.Select(r => r.Title), Is.EquivalentTo(new[]
            {
                "Frank Herbert - Dune [epub]",
                "Frank Herbert - Dune [pdf]",
                "Frank Herbert - Dune [epub]"
            }));
            Assert.That(releases.Select(r => r.Guid).Distinct().Count(), Is.EqualTo(3));

            // The sequel is offered but ranked last, and the remote file name is visible.
            Assert.That(releases.Last().Origin, Does.EndWith("Dune Messiah.epub"));
            Assert.That(releases.Take(2).Select(r => r.Origin), Has.All.Matches<string>(o => o.EndsWith("Dune.epub") || o.EndsWith("Dune.pdf")));
            Assert.That(releases.Select(r => r.Container), Is.All.AnyOf("epub", "pdf"));
            Assert.That(releases.Select(r => r.Source), Is.All.EqualTo("Soulseek"));
        }

        [Test]
        public void should_include_slow_peer_when_queue_limit_is_disabled()
        {
            var settings = new SlskdIndexerSettings { MaximumQueueLength = 0 };

            var releases = SlskdReleaseBuilder.Build(SlskdFixtures.Search("search-ebook.json").Responses, Dune(), settings);

            Assert.That(releases.Any(r => r.Container == "mobi" && r.Origin.StartsWith("peer_beta")), Is.True);
        }

        [Test]
        public void should_require_free_slot_and_minimum_speed_when_configured()
        {
            var responses = SlskdFixtures.Search("search-ebook.json").Responses;

            var freeSlot = SlskdReleaseBuilder.Build(responses, Dune(), new SlskdIndexerSettings { MaximumQueueLength = 0, RequireFreeSlot = true });
            var fast = SlskdReleaseBuilder.Build(responses, Dune(), new SlskdIndexerSettings { MaximumQueueLength = 0, MinimumUploadSpeedKBps = 1024 });

            Assert.That(freeSlot.Any(r => r.Origin.StartsWith("peer_beta")), Is.False);
            Assert.That(fast.Any(r => r.Origin.StartsWith("peer_beta")), Is.False);
            Assert.That(fast.Any(r => r.Origin.StartsWith("peer_alpha")), Is.True);
        }

        [Test]
        public void should_reject_wrong_author_when_author_is_required_and_keep_when_not()
        {
            var responses = SlskdFixtures.Search("search-ebook.json").Responses;
            var other = new SlskdSearchContext { Author = "Isaac Asimov", Title = "Dune", MediaType = BookMediaType.Ebook };

            Assert.That(SlskdReleaseBuilder.Build(responses, other, new SlskdIndexerSettings()), Is.Empty);
            Assert.That(SlskdReleaseBuilder.Build(responses, other, new SlskdIndexerSettings { RequireAuthorMatch = false }), Is.Not.Empty);
        }

        [Test]
        public void should_merge_disc_folders_into_one_german_audiobook_release()
        {
            var releases = SlskdReleaseBuilder.Build(SlskdFixtures.Search("search-audiobook.json").Responses, Jesus(), new SlskdIndexerSettings());

            var german = releases.Single(r => r.Origin.StartsWith("peer_gamma"));
            Assert.That(german.Title, Is.EqualTo("Andreas Eschbach - Das Jesus Video [mp3]"));
            Assert.That(german.Size, Is.EqualTo(8L * 30 * 1024 * 1024));
            Assert.That(german.Languages.Select(l => l.Name), Is.EqualTo(new[] { "German" }));

            Assert.That(SlskdDownloadToken.TryParse(german.DownloadUrl, out var token), Is.True);
            Assert.That(token.Username, Is.EqualTo("peer_gamma"));
            Assert.That(token.Files, Has.Count.EqualTo(8));
            Assert.That(token.Files.Select(f => f.Filename), Has.Some.Contains("\\CD2\\"));
        }

        [Test]
        public void should_prefer_m4b_and_reject_music_and_too_small_folders()
        {
            var releases = SlskdReleaseBuilder.Build(SlskdFixtures.Search("search-audiobook.json").Responses, Jesus(), new SlskdIndexerSettings());

            Assert.That(releases, Has.Count.EqualTo(2));
            var english = releases.Single(r => r.Origin.StartsWith("peer_eps"));
            Assert.That(english.Container, Is.EqualTo("m4b"));
            Assert.That(english.Languages.Select(l => l.Name), Is.EqualTo(new[] { "English" }));
            Assert.That(releases.Any(r => r.Origin.StartsWith("peer_zeta") || r.Origin.StartsWith("peer_eta")), Is.False);
        }

        [Test]
        public void should_filter_by_language_and_unknown_language_switch()
        {
            var responses = SlskdFixtures.Search("search-audiobook.json").Responses;

            var germanOnly = SlskdReleaseBuilder.Build(responses, Jesus(), new SlskdIndexerSettings { Languages = "German" });
            Assert.That(germanOnly.Select(r => r.Origin.Split(' ')[0]), Is.EqualTo(new[] { "peer_gamma" }));

            var noUnknown = SlskdReleaseBuilder.Build(responses, Jesus(), new SlskdIndexerSettings { Languages = "German, English", AllowUnknownLanguage = false, MinimumAudiobookSizeMB = 1, MinimumTitleMatchPercent = 100 });
            Assert.That(noUnknown, Is.Not.Empty);
            Assert.That(noUnknown.All(r => r.Languages.Any()), Is.True);

            // A configured but unknown language name must not silently turn into "accept everything".
            var typo = SlskdReleaseBuilder.Build(responses, Jesus(), new SlskdIndexerSettings { Languages = "Germn", AllowUnknownLanguage = false });
            Assert.That(typo, Is.Empty);
        }

        [Test]
        public void should_keep_best_release_of_each_language_when_capped()
        {
            var releases = SlskdReleaseBuilder.Build(SlskdFixtures.Search("search-audiobook.json").Responses, Jesus(), new SlskdIndexerSettings { MaximumResults = 1 });

            Assert.That(releases.SelectMany(r => r.Languages.Select(l => l.Name)), Is.EquivalentTo(new[] { "German", "English" }));
        }

        [Test]
        public void should_ignore_both_media_types_filters_when_unspecified()
        {
            var releases = SlskdReleaseBuilder.Build(SlskdFixtures.Search("search-ebook.json").Responses, Dune(null), new SlskdIndexerSettings());

            Assert.That(releases, Is.Not.Empty);
        }

        [Test]
        public void should_match_any_alternate_title()
        {
            var responses = SlskdFixtures.Search("search-audiobook.json").Responses;
            var context = new SlskdSearchContext { Author = "Andreas Eschbach", Title = "Totally Different Name", AlternateTitles = new List<string> { "Das Jesus Video" }, MediaType = BookMediaType.Audiobook };

            Assert.That(SlskdReleaseBuilder.Build(responses, context, new SlskdIndexerSettings()), Is.Not.Empty);
            context.AlternateTitles.Clear();
            Assert.That(SlskdReleaseBuilder.Build(responses, context, new SlskdIndexerSettings()), Is.Empty);
        }

        [Test]
        public void should_not_offer_folders_the_token_cannot_carry()
        {
            var files = Enumerable.Range(1, SlskdDownloadToken.MaxFiles + 1)
                .Select(i => new SlskdFile { Filename = $"@@s\\Eschbach\\Das Jesus Video\\{i:000}.mp3", Size = 1024 * 1024 })
                .ToList();
            var responses = new List<SlskdSearchResponse> { new() { Username = "peer_big", HasFreeUploadSlot = true, Files = files } };

            Assert.That(SlskdReleaseBuilder.Build(responses, Jesus(), new SlskdIndexerSettings { MinimumAudiobookSizeMB = 1 }), Is.Empty);
        }

        [Test]
        public void should_roundtrip_download_token_and_reject_tampering()
        {
            var token = new SlskdDownloadToken
            {
                Username = "peer",
                Directory = "@@share\\Books\\Author",
                Files = { new SlskdTokenFile { Filename = "@@share\\Books\\Author\\a.epub", Size = 10 } }
            };

            var url = SlskdDownloadToken.ToUrl(token);

            Assert.That(url, Does.StartWith("slskd://soulseek/"));
            Assert.That(SlskdDownloadToken.TryParse(url, out var parsed), Is.True);
            Assert.That(parsed.Files.Single().Filename, Is.EqualTo("@@share\\Books\\Author\\a.epub"));
            Assert.That(SlskdDownloadToken.TryParse(url + "!!", out _), Is.False);
            Assert.That(SlskdDownloadToken.TryParse("https://example.invalid/x", out _), Is.False);

            token.Files[0].Filename = "@@share\\Books\\Author\\..\\..\\x.epub";
            Assert.That(SlskdDownloadToken.TryParse(SlskdDownloadToken.ToUrl(token), out _), Is.False);
        }

        [TestCase("@@s\\a\\.. \\x.epub")]
        [TestCase("@@s/a/../x.epub")]
        [TestCase("C:\\Windows\\x.epub")]
        [TestCase("@@s\\a\\b\u0001.epub")]
        public void should_reject_unsafe_remote_paths(string filename)
        {
            Assert.That(SlskdDownloadToken.HasTraversal(filename), Is.True);
        }

        [TestCase("@@s\\Dr. No.\\x.epub")]
        [TestCase("@@s\\...And Justice\\x.epub")]
        [TestCase("@@s/a b/c.epub")]
        public void should_accept_ordinary_remote_paths(string filename)
        {
            Assert.That(SlskdDownloadToken.HasTraversal(filename), Is.False);
        }

        [Test]
        public void should_reject_crafted_tokens_without_throwing()
        {
            string Encode(string json) => "slskd://soulseek/" + System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

            Assert.That(SlskdDownloadToken.TryParse(Encode("{\"U\":\"p\",\"D\":\"@@s\",\"S\":\"\\\\\",\"F\":[[\"a.epub\",99999999999999999999999]]}"), out _), Is.False);
            Assert.That(SlskdDownloadToken.TryParse(Encode("{\"U\":\"p\",\"D\":\"@@s\",\"S\":\"|\",\"F\":[[\"a.epub\",5]]}"), out _), Is.False);
            Assert.That(SlskdDownloadToken.TryParse(Encode("{\"U\":\"p\",\"D\":\"@@s\",\"S\":\"\\\\\",\"F\":[[1,5]]}"), out _), Is.False);
            Assert.That(SlskdDownloadToken.TryParse(Encode("{\"U\":\"p\",\"D\":\"@@s\",\"S\":\"\\\\\",\"F\":[[\"a.epub\",5]]}"), out var ok), Is.True);
            Assert.That(ok.Files.Single().Size, Is.EqualTo(5));
        }
    }
}
