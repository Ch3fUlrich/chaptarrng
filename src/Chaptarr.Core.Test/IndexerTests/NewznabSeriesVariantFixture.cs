using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.Indexers.Newznab;
using NzbDrone.Core.Indexers.Torznab;
using NzbDrone.Core.IndexerSearch.Definitions;

namespace Chaptarr.Core.Test.IndexerTests
{
    [TestFixture]
    public class NewznabSeriesVariantFixture
    {
        private class TestCapabilitiesProvider : INewznabCapabilitiesProvider
        {
            public NewznabCapabilities GetCapabilities(NewznabSettings settings, int? proxyId)
            {
                return new NewznabCapabilities();
            }
        }

        private NewznabRequestGenerator CreateNewznabGenerator()
        {
            return new NewznabRequestGenerator(new TestCapabilitiesProvider())
            {
                Settings = new NewznabSettings
                {
                    BaseUrl = "https://indexer.example.com",
                    ApiKey = "k",
                    Categories = new[] { 3030 }
                },
                MaxPages = 1,
                PageSize = 100
            };
        }

        private TorznabRequestGenerator CreateTorznabGenerator()
        {
            return new TorznabRequestGenerator(new TestCapabilitiesProvider())
            {
                Settings = new NewznabSettings
                {
                    BaseUrl = "https://indexer.example.com",
                    ApiKey = "k",
                    Categories = new[] { 3030 }
                },
                MaxPages = 1,
                PageSize = 100
            };
        }

        [Test]
        public void should_add_series_search_variants_tier_when_series_and_position_are_present()
        {
            var generator = CreateNewznabGenerator();
            var searchCriteria = new BookSearchCriteria
            {
                Author = new Author { Name = "Elfie Donnelly" },
                BookTitle = "Hexen gibt es doch",
                Books = new List<Book>
                {
                    new Book
                    {
                        MediaType = BookMediaType.Audiobook,
                        SeriesName = "Bibi Blocksberg",
                        SeriesPosition = "1"
                    }
                }
            };

            var chain = generator.GetSearchRequests(searchCriteria);

            Assert.That(chain.Tiers, Is.EqualTo(5));

            var lastTierSets = chain.GetTier(4).ToList();
            Assert.That(lastTierSets, Has.Count.EqualTo(4));

            var urls = lastTierSets.Select(s => s.Single().HttpRequest.Url.FullUri).ToList();

            Assert.That(urls[0], Is.EqualTo("https://indexer.example.com/api?t=book&cat=3030&extended=1&apikey=k&offset=0&limit=100&title=Bibi%20Blocksberg%20Hexen%20gibt%20es%20doch"));
            Assert.That(urls[1], Is.EqualTo("https://indexer.example.com/api?t=book&cat=3030&extended=1&apikey=k&offset=0&limit=100&title=Bibi%20Blocksberg%201"));
            Assert.That(urls[2], Is.EqualTo("https://indexer.example.com/api?t=search&cat=3030&extended=1&apikey=k&offset=0&limit=100&q=Bibi%20Blocksberg%20Hexen%20gibt%20es%20doch"));
            Assert.That(urls[3], Is.EqualTo("https://indexer.example.com/api?t=search&cat=3030&extended=1&apikey=k&offset=0&limit=100&q=Bibi%20Blocksberg%201"));

            Assert.That(urls[0], Does.Contain("t=book"));
            Assert.That(urls[0], Does.Contain("title=Bibi%20Blocksberg%20Hexen%20gibt%20es%20doch"));

            Assert.That(urls[1], Does.Contain("t=book"));
            Assert.That(urls[1], Does.Contain("title=Bibi%20Blocksberg%201"));

            Assert.That(urls[2], Does.Contain("t=search"));
            Assert.That(urls[2], Does.Contain("q=Bibi%20Blocksberg%20Hexen%20gibt%20es%20doch"));

            Assert.That(urls[3], Does.Contain("t=search"));
            Assert.That(urls[3], Does.Contain("q=Bibi%20Blocksberg%201"));
        }

        [Test]
        public void should_not_add_series_tier_for_books_without_series()
        {
            var generator = CreateNewznabGenerator();
            var searchCriteria = new BookSearchCriteria
            {
                Author = new Author { Name = "Elfie Donnelly" },
                BookTitle = "Hexen gibt es doch",
                Books = new List<Book>
                {
                    new Book
                    {
                        MediaType = BookMediaType.Audiobook,
                        SeriesName = null,
                        SeriesPosition = null
                    }
                }
            };

            var chain = generator.GetSearchRequests(searchCriteria);

            Assert.That(chain.Tiers, Is.EqualTo(4));

            var allSets = chain.GetAllTiers().ToList();
            Assert.That(allSets, Has.Count.EqualTo(5));

            var allUrls = allSets.Select(s => s.Single().HttpRequest.Url.FullUri).ToList();
            Assert.That(allUrls[0], Is.EqualTo("https://indexer.example.com/api?t=book&cat=3030&extended=1&apikey=k&offset=0&limit=100&author=Elfie%20Donnelly&title=Hexen%20gibt%20es%20doch"));
            Assert.That(allUrls[1], Is.EqualTo("https://indexer.example.com/api?t=book&cat=3030&extended=1&apikey=k&offset=0&limit=100&title=Hexen%20gibt%20es%20doch"));
            Assert.That(allUrls[2], Is.EqualTo("https://indexer.example.com/api?t=search&cat=3030&extended=1&apikey=k&offset=0&limit=100&q=Hexen%20gibt%20es%20doch+Elfie%20Donnelly"));
            Assert.That(allUrls[3], Is.EqualTo("https://indexer.example.com/api?t=search&cat=3030&extended=1&apikey=k&offset=0&limit=100&q=Elfie%20Donnelly+Hexen%20gibt%20es%20doch"));
            Assert.That(allUrls[4], Is.EqualTo("https://indexer.example.com/api?t=search&cat=3030&extended=1&apikey=k&offset=0&limit=100&q=Hexen%20gibt%20es%20doch"));

            Assert.That(allUrls.Any(u => u.Contains("Bibi", StringComparison.OrdinalIgnoreCase)), Is.False);
            Assert.That(allUrls.Any(u => u.Contains("Blocksberg", StringComparison.OrdinalIgnoreCase)), Is.False);
        }

        [Test]
        public void should_yield_only_title_variants_when_series_has_no_position()
        {
            var generator = CreateNewznabGenerator();
            var searchCriteria = new BookSearchCriteria
            {
                Author = new Author { Name = "Elfie Donnelly" },
                BookTitle = "Hexen gibt es doch",
                Books = new List<Book>
                {
                    new Book
                    {
                        MediaType = BookMediaType.Audiobook,
                        SeriesName = "Bibi Blocksberg",
                        SeriesPosition = null
                    }
                }
            };

            var chain = generator.GetSearchRequests(searchCriteria);

            Assert.That(chain.Tiers, Is.EqualTo(5));

            var lastTierSets = chain.GetTier(4).ToList();
            Assert.That(lastTierSets, Has.Count.EqualTo(2));

            var urls = lastTierSets.Select(s => s.Single().HttpRequest.Url.FullUri).ToList();

            Assert.That(urls[0], Is.EqualTo("https://indexer.example.com/api?t=book&cat=3030&extended=1&apikey=k&offset=0&limit=100&title=Bibi%20Blocksberg%20Hexen%20gibt%20es%20doch"));
            Assert.That(urls[1], Is.EqualTo("https://indexer.example.com/api?t=search&cat=3030&extended=1&apikey=k&offset=0&limit=100&q=Bibi%20Blocksberg%20Hexen%20gibt%20es%20doch"));

            Assert.That(urls[0], Does.Contain("t=book"));
            Assert.That(urls[0], Does.Contain("title=Bibi%20Blocksberg%20Hexen%20gibt%20es%20doch"));

            Assert.That(urls[1], Does.Contain("t=search"));
            Assert.That(urls[1], Does.Contain("q=Bibi%20Blocksberg%20Hexen%20gibt%20es%20doch"));

            Assert.That(urls.Any(u => u.Contains("&title=Bibi%20Blocksberg%201")), Is.False);
            Assert.That(urls.Any(u => u.Contains("&q=Bibi%20Blocksberg%201")), Is.False);
        }

        [Test]
        public void should_not_add_series_title_duplicate_when_title_already_contains_series_name()
        {
            var generator = CreateNewznabGenerator();
            var searchCriteria = new BookSearchCriteria
            {
                Author = new Author { Name = "Elfie Donnelly" },
                BookTitle = "Bibi Blocksberg - Hexen gibt es doch",
                Books = new List<Book>
                {
                    new Book
                    {
                        MediaType = BookMediaType.Audiobook,
                        SeriesName = "Bibi Blocksberg",
                        SeriesPosition = "1"
                    }
                }
            };

            var chain = generator.GetSearchRequests(searchCriteria);

            Assert.That(chain.Tiers, Is.EqualTo(5));

            var lastTierSets = chain.GetTier(4).ToList();
            Assert.That(lastTierSets, Has.Count.EqualTo(2));

            var urls = lastTierSets.Select(s => s.Single().HttpRequest.Url.FullUri).ToList();

            Assert.That(urls[0], Is.EqualTo("https://indexer.example.com/api?t=book&cat=3030&extended=1&apikey=k&offset=0&limit=100&title=Bibi%20Blocksberg%201"));
            Assert.That(urls[1], Is.EqualTo("https://indexer.example.com/api?t=search&cat=3030&extended=1&apikey=k&offset=0&limit=100&q=Bibi%20Blocksberg%201"));

            Assert.That(urls[0], Does.Contain("t=book"));
            Assert.That(urls[0], Does.Contain("title=Bibi%20Blocksberg%201"));

            Assert.That(urls[1], Does.Contain("t=search"));
            Assert.That(urls[1], Does.Contain("q=Bibi%20Blocksberg%201"));

            Assert.That(urls.Any(u => u.Contains("Hexen", StringComparison.OrdinalIgnoreCase)), Is.False);
            Assert.That(urls.Any(u => u.Contains("Bibi%20Blocksberg%20Bibi%20Blocksberg", StringComparison.OrdinalIgnoreCase)), Is.False);
        }

        [Test]
        public void should_add_series_search_variants_for_torznab_generator()
        {
            var generator = CreateTorznabGenerator();
            var searchCriteria = new BookSearchCriteria
            {
                Author = new Author { Name = "Elfie Donnelly" },
                BookTitle = "Hexen gibt es doch",
                Books = new List<Book>
                {
                    new Book
                    {
                        MediaType = BookMediaType.Audiobook,
                        SeriesName = "Bibi Blocksberg",
                        SeriesPosition = "1"
                    }
                }
            };

            var chain = generator.GetSearchRequests(searchCriteria);

            Assert.That(chain.Tiers, Is.EqualTo(5));

            var lastTierSets = chain.GetTier(4).ToList();
            Assert.That(lastTierSets, Has.Count.EqualTo(4));

            var urls = lastTierSets.Select(s => s.Single().HttpRequest.Url.FullUri).ToList();

            Assert.That(urls[0], Is.EqualTo("https://indexer.example.com/api?t=book&cat=3030&extended=1&apikey=k&offset=0&limit=100&title=Bibi%20Blocksberg%20Hexen%20gibt%20es%20doch"));
            Assert.That(urls[1], Is.EqualTo("https://indexer.example.com/api?t=book&cat=3030&extended=1&apikey=k&offset=0&limit=100&title=Bibi%20Blocksberg%201"));
            Assert.That(urls[2], Is.EqualTo("https://indexer.example.com/api?t=search&cat=3030&extended=1&apikey=k&offset=0&limit=100&q=Bibi%20Blocksberg%20Hexen%20gibt%20es%20doch"));
            Assert.That(urls[3], Is.EqualTo("https://indexer.example.com/api?t=search&cat=3030&extended=1&apikey=k&offset=0&limit=100&q=Bibi%20Blocksberg%201"));

            Assert.That(urls[0], Does.Contain("t=book"));
            Assert.That(urls[0], Does.Contain("title=Bibi%20Blocksberg%20Hexen%20gibt%20es%20doch"));

            Assert.That(urls[1], Does.Contain("t=book"));
            Assert.That(urls[1], Does.Contain("title=Bibi%20Blocksberg%201"));

            Assert.That(urls[2], Does.Contain("t=search"));
            Assert.That(urls[2], Does.Contain("q=Bibi%20Blocksberg%20Hexen%20gibt%20es%20doch"));

            Assert.That(urls[3], Does.Contain("t=search"));
            Assert.That(urls[3], Does.Contain("q=Bibi%20Blocksberg%201"));
        }

        [Test]
        public void should_strip_trailing_dot_zero_from_series_position()
        {
            var generator = CreateNewznabGenerator();
            var searchCriteria = new BookSearchCriteria
            {
                Author = new Author { Name = "Elfie Donnelly" },
                BookTitle = "Hexen gibt es doch",
                Books = new List<Book>
                {
                    new Book
                    {
                        MediaType = BookMediaType.Audiobook,
                        SeriesName = "Bibi Blocksberg",
                        SeriesPosition = "1.0"
                    }
                }
            };

            var chain = generator.GetSearchRequests(searchCriteria);

            Assert.That(chain.Tiers, Is.EqualTo(5));

            var lastTierSets = chain.GetTier(4).ToList();
            Assert.That(lastTierSets, Has.Count.EqualTo(4));

            var urls = lastTierSets.Select(s => s.Single().HttpRequest.Url.FullUri).ToList();

            Assert.That(urls[1], Is.EqualTo("https://indexer.example.com/api?t=book&cat=3030&extended=1&apikey=k&offset=0&limit=100&title=Bibi%20Blocksberg%201"));
            Assert.That(urls[3], Is.EqualTo("https://indexer.example.com/api?t=search&cat=3030&extended=1&apikey=k&offset=0&limit=100&q=Bibi%20Blocksberg%201"));

            Assert.That(urls[1], Does.Contain("title=Bibi%20Blocksberg%201"));
            Assert.That(urls[3], Does.Contain("q=Bibi%20Blocksberg%201"));
            Assert.That(urls.Any(u => u.Contains("1.0")), Is.False);
        }

        [Test]
        public void should_skip_position_variant_when_position_is_not_plain_number_or_decimal()
        {
            var generator = CreateNewznabGenerator();
            var searchCriteria = new BookSearchCriteria
            {
                Author = new Author { Name = "Elfie Donnelly" },
                BookTitle = "Hexen gibt es doch",
                Books = new List<Book>
                {
                    new Book
                    {
                        MediaType = BookMediaType.Audiobook,
                        SeriesName = "Bibi Blocksberg",
                        SeriesPosition = "Special Episode"
                    }
                }
            };

            var chain = generator.GetSearchRequests(searchCriteria);

            Assert.That(chain.Tiers, Is.EqualTo(5));

            var lastTierSets = chain.GetTier(4).ToList();
            Assert.That(lastTierSets, Has.Count.EqualTo(2));

            var urls = lastTierSets.Select(s => s.Single().HttpRequest.Url.FullUri).ToList();

            Assert.That(urls[0], Is.EqualTo("https://indexer.example.com/api?t=book&cat=3030&extended=1&apikey=k&offset=0&limit=100&title=Bibi%20Blocksberg%20Hexen%20gibt%20es%20doch"));
            Assert.That(urls[1], Is.EqualTo("https://indexer.example.com/api?t=search&cat=3030&extended=1&apikey=k&offset=0&limit=100&q=Bibi%20Blocksberg%20Hexen%20gibt%20es%20doch"));

            Assert.That(urls.Any(u => u.Contains("Special")), Is.False);
        }

        [TestCase("1", "1")]
        [TestCase("1.0", "1")]
        [TestCase("1.5", "1.5")]
        [TestCase("10.0", "10")]
        [TestCase("Book 1", null)]
        [TestCase("1-2", null)]
        [TestCase("special", null)]
        [TestCase("", null)]
        [TestCase(null, null)]
        public void should_normalize_series_position_correctly(string rawPosition, string expectedNormalized)
        {
            Assert.That(BookSearchCriteria.NormalizeSeriesPosition(rawPosition), Is.EqualTo(expectedNormalized));
        }

        [Test]
        public void should_populate_series_properties_from_first_book()
        {
            var criteria = new BookSearchCriteria
            {
                Author = new Author { Name = "Elfie Donnelly" },
                BookTitle = "Hexen gibt es doch",
                Books = new List<Book>
                {
                    new Book { SeriesName = "Bibi Blocksberg", SeriesPosition = "1.0" },
                    new Book { SeriesName = "Other Series", SeriesPosition = "2" }
                }
            };

            Assert.That(criteria.SeriesName, Is.EqualTo("Bibi Blocksberg"));
            Assert.That(criteria.SeriesPosition, Is.EqualTo("1.0"));
            Assert.That(criteria.SeriesQuery, Is.EqualTo("Bibi+Blocksberg"));
            Assert.That(criteria.SeriesTitleQuery, Is.EqualTo("Bibi+Blocksberg+Hexen+gibt+es+doch"));
            Assert.That(criteria.SeriesPositionQuery, Is.EqualTo("Bibi+Blocksberg+1"));
        }
    }
}
