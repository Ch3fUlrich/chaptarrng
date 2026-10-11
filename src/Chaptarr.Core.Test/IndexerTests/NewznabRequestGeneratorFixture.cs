using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Indexers.Newznab;
using NzbDrone.Core.Indexers.Torznab;
using NzbDrone.Core.IndexerSearch.Definitions;

namespace Chaptarr.Core.Test.IndexerTests
{
    [TestFixture]
    public class NewznabRequestGeneratorFixture
    {
        private class TestCapabilitiesProvider : INewznabCapabilitiesProvider
        {
            public NewznabCapabilities Capabilities { get; set; } = new NewznabCapabilities();

            public NewznabCapabilities GetCapabilities(NewznabSettings settings, int? proxyId)
            {
                return Capabilities;
            }
        }

        private NewznabRequestGenerator CreateGenerator(int maxPages = 1)
        {
            return new NewznabRequestGenerator(new TestCapabilitiesProvider())
            {
                Settings = new NewznabSettings
                {
                    BaseUrl = "https://indexer.example.com",
                    ApiKey = "testkey",
                    Categories = new[] { 7020 }
                },
                MaxPages = maxPages,
                PageSize = 100
            };
        }

        [Test]
        public void should_generate_both_variants_for_german_title_ernaehrungskompass()
        {
            var generator = CreateGenerator(maxPages: 1);

            var searchCriteria = new BookSearchCriteria
            {
                Author = new Author { Name = "Bas Kast" },
                BookTitle = "Ernährungskompass"
            };

            var chain = generator.GetSearchRequests(searchCriteria);
            var allUrls = chain.GetAllTiers()
                               .SelectMany(p => p)
                               .Select(r => r.Url.FullUri)
                               .ToList();

            Assert.That(allUrls.Any(u => u.Contains("title=Ernahrungskompass")), Is.True, "Should contain original title variant");
            Assert.That(allUrls.Any(u => u.Contains("title=Ernaehrungskompass")), Is.True, "Should contain transliterated title variant");

            // Tier 0: t=book author+title
            var tier0 = chain.GetTier(0).ToList();
            Assert.That(tier0, Has.Count.EqualTo(2));
            Assert.That(tier0[0].First().Url.FullUri, Does.Contain("author=Bas%20Kast"));
            Assert.That(tier0[0].First().Url.FullUri, Does.Contain("title=Ernahrungskompass"));
            Assert.That(tier0[1].First().Url.FullUri, Does.Contain("author=Bas%20Kast"));
            Assert.That(tier0[1].First().Url.FullUri, Does.Contain("title=Ernaehrungskompass"));

            // Tier 1: t=book title
            var tier1 = chain.GetTier(1).ToList();
            Assert.That(tier1, Has.Count.EqualTo(2));
            Assert.That(tier1[0].First().Url.FullUri, Does.Contain("title=Ernahrungskompass"));
            Assert.That(tier1[1].First().Url.FullUri, Does.Contain("title=Ernaehrungskompass"));

            // Tier 2: q=title+author; q=author+title
            var tier2 = chain.GetTier(2).ToList();
            Assert.That(tier2, Has.Count.EqualTo(4));
            Assert.That(tier2[0].First().Url.FullUri, Does.Contain("q=Ernahrungskompass+Bas%20Kast"));
            Assert.That(tier2[1].First().Url.FullUri, Does.Contain("q=Ernaehrungskompass+Bas%20Kast"));
            Assert.That(tier2[2].First().Url.FullUri, Does.Contain("q=Bas%20Kast+Ernahrungskompass"));
            Assert.That(tier2[3].First().Url.FullUri, Does.Contain("q=Bas%20Kast+Ernaehrungskompass"));

            // Tier 3: q=title
            var tier3 = chain.GetTier(3).ToList();
            Assert.That(tier3, Has.Count.EqualTo(2));
            Assert.That(tier3[0].First().Url.FullUri, Does.Contain("q=Ernahrungskompass"));
            Assert.That(tier3[0].First().Url.FullUri, Does.EndWith("q=Ernahrungskompass"));
            Assert.That(tier3[0].First().Url.FullUri, Does.Not.Contain("Bas%20Kast"));
            Assert.That(tier3[1].First().Url.FullUri, Does.Contain("q=Ernaehrungskompass"));
            Assert.That(tier3[1].First().Url.FullUri, Does.EndWith("q=Ernaehrungskompass"));
            Assert.That(tier3[1].First().Url.FullUri, Does.Not.Contain("Bas%20Kast"));
        }

        [Test]
        public void should_generate_both_variants_for_der_ernaehrungskompass()
        {
            var generator = CreateGenerator(maxPages: 1);

            var searchCriteria = new BookSearchCriteria
            {
                Author = new Author { Name = "Bas Kast" },
                BookTitle = "Der Ernährungskompass"
            };

            var chain = generator.GetSearchRequests(searchCriteria);
            var allUrls = chain.GetAllTiers()
                               .SelectMany(p => p)
                               .Select(r => r.Url.FullUri)
                               .ToList();

            Assert.That(allUrls.Any(u => u.Contains("Ernahrungskompass")), Is.True);
            Assert.That(allUrls.Any(u => u.Contains("Ernaehrungskompass")), Is.True);
            Assert.That(allUrls.Any(u => u.Contains("title=Der%20Ernahrungskompass")), Is.True);
            Assert.That(allUrls.Any(u => u.Contains("title=Der%20Ernaehrungskompass")), Is.True);
        }

        [Test]
        public void ascii_title_should_yield_exact_same_request_count_and_urls_as_before()
        {
            var generator = CreateGenerator(maxPages: 2);

            var searchCriteria = new BookSearchCriteria
            {
                Author = new Author { Name = "Frank Herbert" },
                BookTitle = "Dune"
            };

            var chain = generator.GetSearchRequests(searchCriteria);

            // 4 tiers: t=book author+title (1), t=book title (1), q=title+author & q=author+title (2), q=title (1)
            // Total request sets = 1 + 1 + 2 + 1 = 5
            Assert.That(chain.Tiers, Is.EqualTo(4));
            Assert.That(chain.GetTier(0).Count(), Is.EqualTo(1));
            Assert.That(chain.GetTier(1).Count(), Is.EqualTo(1));
            Assert.That(chain.GetTier(2).Count(), Is.EqualTo(2));
            Assert.That(chain.GetTier(3).Count(), Is.EqualTo(1));

            var allPageableRequests = chain.GetAllTiers().ToList();
            Assert.That(allPageableRequests, Has.Count.EqualTo(5));

            var allRequests = allPageableRequests.SelectMany(p => p).ToList();
            Assert.That(allRequests, Has.Count.EqualTo(5 * generator.MaxPages));

            var allUrls = allRequests.Select(r => r.Url.FullUri).ToList();
            Assert.That(allUrls.Any(u => u.Contains("author=Frank%20Herbert&title=Dune")), Is.True);
            Assert.That(allUrls.Any(u => u.Contains("title=Dune")), Is.True);
            Assert.That(allUrls.Any(u => u.Contains("q=Dune+Frank%20Herbert")), Is.True);
            Assert.That(allUrls.Any(u => u.Contains("q=Frank%20Herbert+Dune")), Is.True);
            Assert.That(allUrls.Any(u => u.Contains("q=Dune")), Is.True);
        }

        [Test]
        public void author_with_umlauts_and_ascii_title_should_only_duplicate_author_bearing_tiers()
        {
            var generator = CreateGenerator(maxPages: 1);

            var searchCriteria = new BookSearchCriteria
            {
                Author = new Author { Name = "Bäst" },
                BookTitle = "Dune"
            };

            var chain = generator.GetSearchRequests(searchCriteria);

            // Tier 0: t=book author+title (author-bearing -> 2 sets)
            var tier0 = chain.GetTier(0).ToList();
            Assert.That(tier0, Has.Count.EqualTo(2));
            Assert.That(tier0[0].First().Url.FullUri, Does.Contain("author=Bast"));
            Assert.That(tier0[0].First().Url.FullUri, Does.Contain("title=Dune"));
            Assert.That(tier0[1].First().Url.FullUri, Does.Contain("author=Baest"));
            Assert.That(tier0[1].First().Url.FullUri, Does.Contain("title=Dune"));

            // Tier 1: t=book title (title-only -> exactly 1 set)
            var tier1 = chain.GetTier(1).ToList();
            Assert.That(tier1, Has.Count.EqualTo(1));
            Assert.That(tier1[0].First().Url.FullUri, Does.Contain("title=Dune"));
            Assert.That(tier1[0].First().Url.FullUri, Does.Not.Contain("Bast"));
            Assert.That(tier1[0].First().Url.FullUri, Does.Not.Contain("Baest"));

            // Tier 2: q=title+author & q=author+title (author-bearing -> 2 sets each, total 4)
            var tier2 = chain.GetTier(2).ToList();
            Assert.That(tier2, Has.Count.EqualTo(4));
            Assert.That(tier2[0].First().Url.FullUri, Does.Contain("q=Dune+Bast"));
            Assert.That(tier2[1].First().Url.FullUri, Does.Contain("q=Dune+Baest"));
            Assert.That(tier2[2].First().Url.FullUri, Does.Contain("q=Bast+Dune"));
            Assert.That(tier2[3].First().Url.FullUri, Does.Contain("q=Baest+Dune"));

            // Tier 3: q=title (title-only -> exactly 1 set)
            var tier3 = chain.GetTier(3).ToList();
            Assert.That(tier3, Has.Count.EqualTo(1));
            Assert.That(tier3[0].First().Url.FullUri, Does.Contain("q=Dune"));
            Assert.That(tier3[0].First().Url.FullUri, Does.EndWith("q=Dune"));
            Assert.That(tier3[0].First().Url.FullUri, Does.Not.Contain("Bast"));
            Assert.That(tier3[0].First().Url.FullUri, Does.Not.Contain("Baest"));
        }

        [Test]
        public void should_generate_transliterated_variant_with_both_author_and_title_transliterated()
        {
            var generator = CreateGenerator(maxPages: 1);

            var searchCriteria = new BookSearchCriteria
            {
                Author = new Author { Name = "Bäst" },
                BookTitle = "Ernährungskompass"
            };

            var chain = generator.GetSearchRequests(searchCriteria);

            // Tier 2: q=title+author tier contains URL with q=Ernaehrungskompass+Baest
            var tier2 = chain.GetTier(2).ToList();
            Assert.That(tier2, Has.Count.EqualTo(4));

            var tier2Urls = tier2.SelectMany(p => p).Select(r => r.Url.FullUri).ToList();
            Assert.That(tier2Urls.Any(u => u.Contains("q=Ernaehrungskompass+Baest")), Is.True);
            Assert.That(tier2Urls.Any(u => u.Contains("q=Ernahrungskompass+Bast")), Is.True);
            Assert.That(tier2Urls.Any(u => u.Contains("q=Baest+Ernaehrungskompass")), Is.True);
            Assert.That(tier2Urls.Any(u => u.Contains("q=Bast+Ernahrungskompass")), Is.True);
        }

        [Test]
        public void author_search_criteria_should_generate_extra_variant_when_author_has_umlauts()
        {
            var generator = CreateGenerator(maxPages: 1);

            var searchCriteria = new AuthorSearchCriteria
            {
                Author = new Author { Name = "Bäst" }
            };

            var chain = generator.GetSearchRequests(searchCriteria);

            Assert.That(chain.Tiers, Is.EqualTo(2));

            var tier0 = chain.GetTier(0).ToList();
            Assert.That(tier0, Has.Count.EqualTo(2));
            Assert.That(tier0[0].First().Url.FullUri, Does.Contain("author=Bast"));
            Assert.That(tier0[1].First().Url.FullUri, Does.Contain("author=Baest"));

            var tier1 = chain.GetTier(1).ToList();
            Assert.That(tier1, Has.Count.EqualTo(2));
            Assert.That(tier1[0].First().Url.FullUri, Does.Contain("q=Bast"));
            Assert.That(tier1[1].First().Url.FullUri, Does.Contain("q=Baest"));
        }

        [Test]
        public void author_search_criteria_should_not_generate_extra_variant_when_author_is_ascii()
        {
            var generator = CreateGenerator(maxPages: 1);

            var searchCriteria = new AuthorSearchCriteria
            {
                Author = new Author { Name = "Bas Kast" }
            };

            var chain = generator.GetSearchRequests(searchCriteria);

            Assert.That(chain.Tiers, Is.EqualTo(2));

            var tier0 = chain.GetTier(0).ToList();
            Assert.That(tier0, Has.Count.EqualTo(1));
            Assert.That(tier0[0].First().Url.FullUri, Does.Contain("author=Bas%20Kast"));

            var tier1 = chain.GetTier(1).ToList();
            Assert.That(tier1, Has.Count.EqualTo(1));
            Assert.That(tier1[0].First().Url.FullUri, Does.Contain("q=Bas%20Kast"));
        }

        [Test]
        public void torznab_request_generator_should_also_support_umlaut_transliteration()
        {
            var generator = new TorznabRequestGenerator(new TestCapabilitiesProvider())
            {
                Settings = new NewznabSettings
                {
                    BaseUrl = "https://torznab.example.com",
                    ApiKey = "testkey",
                    Categories = new[] { 7020 }
                },
                MaxPages = 1,
                PageSize = 100
            };

            var searchCriteria = new BookSearchCriteria
            {
                Author = new Author { Name = "Bas Kast" },
                BookTitle = "Ernährungskompass"
            };

            var chain = generator.GetSearchRequests(searchCriteria);
            var allUrls = chain.GetAllTiers()
                               .SelectMany(p => p)
                               .Select(r => r.Url.FullUri)
                               .ToList();

            Assert.That(allUrls.Any(u => u.Contains("title=Ernahrungskompass")), Is.True);
            Assert.That(allUrls.Any(u => u.Contains("title=Ernaehrungskompass")), Is.True);
        }
    }
}
