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
            return new NewznabRequestGenerator(new TestCapabilitiesProvider(), NLog.LogManager.GetCurrentClassLogger(), "test")
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
            return new TorznabRequestGenerator(new TestCapabilitiesProvider(), NLog.LogManager.GetCurrentClassLogger(), "test")
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

        // Homelab regression: German kids series are released as '<Series> - Folge N - <Title>', so the
        // '<title> <author>' query finds nothing; the series variants must carry the series and the number.
        [TestCase("27")]
        [TestCase("31")]
        public void should_query_series_and_folge_number_for_german_kids_series(string position)
        {
            var generator = CreateNewznabGenerator();
            var searchCriteria = new BookSearchCriteria
            {
                Author = new Author { Name = "Elfie Donnelly" },
                BookTitle = "Der Zauberspiegel",
                Books = new List<Book>
                {
                    new Book
                    {
                        MediaType = BookMediaType.Audiobook,
                        SeriesName = "Bibi Blocksberg",
                        SeriesPosition = position
                    }
                }
            };

            var chain = generator.GetSearchRequests(searchCriteria);
            var urls = chain.GetTier(chain.Tiers - 1).SelectMany(s => s.Select(r => r.HttpRequest.Url.FullUri)).ToList();

            Assert.That(urls, Has.Some.Contains("t=book").And.Contains("title=Bibi%20Blocksberg%20Der%20Zauberspiegel"));
            Assert.That(urls, Has.Some.Contains("t=book").And.Contains($"title=Bibi%20Blocksberg%20{position}"));
            Assert.That(urls, Has.Some.Contains("t=search").And.Contains($"q=Bibi%20Blocksberg%20{position}"));
        }

        [Test]
        public void should_add_series_search_variants_tier_when_series_and_position_are_present()
        {
            var generator = CreateNewznabGenerator();
            var searchCriteria = new BookSearchCriteria
            {
                Author = new Author { Name = "Mara Lindqvist" },
                BookTitle = "Zauber im Garten",
                Books = new List<Book>
                {
                    new Book
                    {
                        MediaType = BookMediaType.Audiobook,
                        SeriesName = "Hexe Hilda",
                        SeriesPosition = "1"
                    }
                }
            };

            var chain = generator.GetSearchRequests(searchCriteria);

            Assert.That(chain.Tiers, Is.EqualTo(5));

            var lastTierSets = chain.GetTier(4).ToList();
            Assert.That(lastTierSets, Has.Count.EqualTo(4));

            var urls = lastTierSets.Select(s => s.Single().HttpRequest.Url.FullUri).ToList();

            Assert.That(urls[0], Is.EqualTo("https://indexer.example.com/api?t=book&cat=3030&extended=1&apikey=k&offset=0&limit=100&title=Hexe%20Hilda%20Zauber%20im%20Garten"));
            Assert.That(urls[1], Is.EqualTo("https://indexer.example.com/api?t=book&cat=3030&extended=1&apikey=k&offset=0&limit=100&title=Hexe%20Hilda%201"));
            Assert.That(urls[2], Is.EqualTo("https://indexer.example.com/api?t=search&cat=3030&extended=1&apikey=k&offset=0&limit=100&q=Hexe%20Hilda%20Zauber%20im%20Garten"));
            Assert.That(urls[3], Is.EqualTo("https://indexer.example.com/api?t=search&cat=3030&extended=1&apikey=k&offset=0&limit=100&q=Hexe%20Hilda%201"));

            Assert.That(urls[0], Does.Contain("t=book"));
            Assert.That(urls[0], Does.Contain("title=Hexe%20Hilda%20Zauber%20im%20Garten"));

            Assert.That(urls[1], Does.Contain("t=book"));
            Assert.That(urls[1], Does.Contain("title=Hexe%20Hilda%201"));

            Assert.That(urls[2], Does.Contain("t=search"));
            Assert.That(urls[2], Does.Contain("q=Hexe%20Hilda%20Zauber%20im%20Garten"));

            Assert.That(urls[3], Does.Contain("t=search"));
            Assert.That(urls[3], Does.Contain("q=Hexe%20Hilda%201"));
        }

        [Test]
        public void should_not_add_series_tier_for_books_without_series()
        {
            var generator = CreateNewznabGenerator();
            var searchCriteria = new BookSearchCriteria
            {
                Author = new Author { Name = "Mara Lindqvist" },
                BookTitle = "Zauber im Garten",
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
            Assert.That(allUrls[0], Is.EqualTo("https://indexer.example.com/api?t=book&cat=3030&extended=1&apikey=k&offset=0&limit=100&author=Mara%20Lindqvist&title=Zauber%20im%20Garten"));
            Assert.That(allUrls[1], Is.EqualTo("https://indexer.example.com/api?t=book&cat=3030&extended=1&apikey=k&offset=0&limit=100&title=Zauber%20im%20Garten"));
            Assert.That(allUrls[2], Is.EqualTo("https://indexer.example.com/api?t=search&cat=3030&extended=1&apikey=k&offset=0&limit=100&q=Zauber%20im%20Garten+Mara%20Lindqvist"));
            Assert.That(allUrls[3], Is.EqualTo("https://indexer.example.com/api?t=search&cat=3030&extended=1&apikey=k&offset=0&limit=100&q=Mara%20Lindqvist+Zauber%20im%20Garten"));
            Assert.That(allUrls[4], Is.EqualTo("https://indexer.example.com/api?t=search&cat=3030&extended=1&apikey=k&offset=0&limit=100&q=Zauber%20im%20Garten"));

            Assert.That(allUrls.Any(u => u.Contains("Hexe", StringComparison.OrdinalIgnoreCase)), Is.False);
            Assert.That(allUrls.Any(u => u.Contains("Hilda", StringComparison.OrdinalIgnoreCase)), Is.False);
        }

        [Test]
        public void should_yield_only_title_variants_when_series_has_no_position()
        {
            var generator = CreateNewznabGenerator();
            var searchCriteria = new BookSearchCriteria
            {
                Author = new Author { Name = "Mara Lindqvist" },
                BookTitle = "Zauber im Garten",
                Books = new List<Book>
                {
                    new Book
                    {
                        MediaType = BookMediaType.Audiobook,
                        SeriesName = "Hexe Hilda",
                        SeriesPosition = null
                    }
                }
            };

            var chain = generator.GetSearchRequests(searchCriteria);

            Assert.That(chain.Tiers, Is.EqualTo(5));

            var lastTierSets = chain.GetTier(4).ToList();
            Assert.That(lastTierSets, Has.Count.EqualTo(2));

            var urls = lastTierSets.Select(s => s.Single().HttpRequest.Url.FullUri).ToList();

            Assert.That(urls[0], Is.EqualTo("https://indexer.example.com/api?t=book&cat=3030&extended=1&apikey=k&offset=0&limit=100&title=Hexe%20Hilda%20Zauber%20im%20Garten"));
            Assert.That(urls[1], Is.EqualTo("https://indexer.example.com/api?t=search&cat=3030&extended=1&apikey=k&offset=0&limit=100&q=Hexe%20Hilda%20Zauber%20im%20Garten"));

            Assert.That(urls[0], Does.Contain("t=book"));
            Assert.That(urls[0], Does.Contain("title=Hexe%20Hilda%20Zauber%20im%20Garten"));

            Assert.That(urls[1], Does.Contain("t=search"));
            Assert.That(urls[1], Does.Contain("q=Hexe%20Hilda%20Zauber%20im%20Garten"));

            Assert.That(urls.Any(u => u.Contains("&title=Hexe%20Hilda%201")), Is.False);
            Assert.That(urls.Any(u => u.Contains("&q=Hexe%20Hilda%201")), Is.False);
        }

        [Test]
        public void should_not_add_series_title_duplicate_when_title_already_contains_series_name()
        {
            var generator = CreateNewznabGenerator();
            var searchCriteria = new BookSearchCriteria
            {
                Author = new Author { Name = "Mara Lindqvist" },
                BookTitle = "Hexe Hilda - Zauber im Garten",
                Books = new List<Book>
                {
                    new Book
                    {
                        MediaType = BookMediaType.Audiobook,
                        SeriesName = "Hexe Hilda",
                        SeriesPosition = "1"
                    }
                }
            };

            var chain = generator.GetSearchRequests(searchCriteria);

            Assert.That(chain.Tiers, Is.EqualTo(5));

            var lastTierSets = chain.GetTier(4).ToList();
            Assert.That(lastTierSets, Has.Count.EqualTo(2));

            var urls = lastTierSets.Select(s => s.Single().HttpRequest.Url.FullUri).ToList();

            Assert.That(urls[0], Is.EqualTo("https://indexer.example.com/api?t=book&cat=3030&extended=1&apikey=k&offset=0&limit=100&title=Hexe%20Hilda%201"));
            Assert.That(urls[1], Is.EqualTo("https://indexer.example.com/api?t=search&cat=3030&extended=1&apikey=k&offset=0&limit=100&q=Hexe%20Hilda%201"));

            Assert.That(urls[0], Does.Contain("t=book"));
            Assert.That(urls[0], Does.Contain("title=Hexe%20Hilda%201"));

            Assert.That(urls[1], Does.Contain("t=search"));
            Assert.That(urls[1], Does.Contain("q=Hexe%20Hilda%201"));

            Assert.That(urls.Any(u => u.Contains("Zauber", StringComparison.OrdinalIgnoreCase)), Is.False);
            Assert.That(urls.Any(u => u.Contains("Hexe%20Hilda%20Hexe%20Hilda", StringComparison.OrdinalIgnoreCase)), Is.False);
        }

        [Test]
        public void should_add_series_search_variants_for_torznab_generator()
        {
            var generator = CreateTorznabGenerator();
            var searchCriteria = new BookSearchCriteria
            {
                Author = new Author { Name = "Mara Lindqvist" },
                BookTitle = "Zauber im Garten",
                Books = new List<Book>
                {
                    new Book
                    {
                        MediaType = BookMediaType.Audiobook,
                        SeriesName = "Hexe Hilda",
                        SeriesPosition = "1"
                    }
                }
            };

            var chain = generator.GetSearchRequests(searchCriteria);

            Assert.That(chain.Tiers, Is.EqualTo(5));

            var lastTierSets = chain.GetTier(4).ToList();
            Assert.That(lastTierSets, Has.Count.EqualTo(4));

            var urls = lastTierSets.Select(s => s.Single().HttpRequest.Url.FullUri).ToList();

            Assert.That(urls[0], Is.EqualTo("https://indexer.example.com/api?t=book&cat=3030&extended=1&apikey=k&offset=0&limit=100&title=Hexe%20Hilda%20Zauber%20im%20Garten"));
            Assert.That(urls[1], Is.EqualTo("https://indexer.example.com/api?t=book&cat=3030&extended=1&apikey=k&offset=0&limit=100&title=Hexe%20Hilda%201"));
            Assert.That(urls[2], Is.EqualTo("https://indexer.example.com/api?t=search&cat=3030&extended=1&apikey=k&offset=0&limit=100&q=Hexe%20Hilda%20Zauber%20im%20Garten"));
            Assert.That(urls[3], Is.EqualTo("https://indexer.example.com/api?t=search&cat=3030&extended=1&apikey=k&offset=0&limit=100&q=Hexe%20Hilda%201"));

            Assert.That(urls[0], Does.Contain("t=book"));
            Assert.That(urls[0], Does.Contain("title=Hexe%20Hilda%20Zauber%20im%20Garten"));

            Assert.That(urls[1], Does.Contain("t=book"));
            Assert.That(urls[1], Does.Contain("title=Hexe%20Hilda%201"));

            Assert.That(urls[2], Does.Contain("t=search"));
            Assert.That(urls[2], Does.Contain("q=Hexe%20Hilda%20Zauber%20im%20Garten"));

            Assert.That(urls[3], Does.Contain("t=search"));
            Assert.That(urls[3], Does.Contain("q=Hexe%20Hilda%201"));
        }

        [Test]
        public void should_strip_trailing_dot_zero_from_series_position()
        {
            var generator = CreateNewznabGenerator();
            var searchCriteria = new BookSearchCriteria
            {
                Author = new Author { Name = "Mara Lindqvist" },
                BookTitle = "Zauber im Garten",
                Books = new List<Book>
                {
                    new Book
                    {
                        MediaType = BookMediaType.Audiobook,
                        SeriesName = "Hexe Hilda",
                        SeriesPosition = "1.0"
                    }
                }
            };

            var chain = generator.GetSearchRequests(searchCriteria);

            Assert.That(chain.Tiers, Is.EqualTo(5));

            var lastTierSets = chain.GetTier(4).ToList();
            Assert.That(lastTierSets, Has.Count.EqualTo(4));

            var urls = lastTierSets.Select(s => s.Single().HttpRequest.Url.FullUri).ToList();

            Assert.That(urls[1], Is.EqualTo("https://indexer.example.com/api?t=book&cat=3030&extended=1&apikey=k&offset=0&limit=100&title=Hexe%20Hilda%201"));
            Assert.That(urls[3], Is.EqualTo("https://indexer.example.com/api?t=search&cat=3030&extended=1&apikey=k&offset=0&limit=100&q=Hexe%20Hilda%201"));

            Assert.That(urls[1], Does.Contain("title=Hexe%20Hilda%201"));
            Assert.That(urls[3], Does.Contain("q=Hexe%20Hilda%201"));
            Assert.That(urls[1], Does.Contain("%201"));
            Assert.That(urls[3], Does.Contain("%201"));
            Assert.That(urls.Any(u => u.Contains("1.0")), Is.False);
        }

        [Test]
        public void should_skip_position_variant_when_position_is_not_plain_number_or_decimal()
        {
            var generator = CreateNewznabGenerator();
            var searchCriteria = new BookSearchCriteria
            {
                Author = new Author { Name = "Mara Lindqvist" },
                BookTitle = "Zauber im Garten",
                Books = new List<Book>
                {
                    new Book
                    {
                        MediaType = BookMediaType.Audiobook,
                        SeriesName = "Hexe Hilda",
                        SeriesPosition = "Sonderfolge"
                    }
                }
            };

            var chain = generator.GetSearchRequests(searchCriteria);

            Assert.That(chain.Tiers, Is.EqualTo(5));

            var lastTierSets = chain.GetTier(4).ToList();
            Assert.That(lastTierSets, Has.Count.EqualTo(2));

            var urls = lastTierSets.Select(s => s.Single().HttpRequest.Url.FullUri).ToList();

            Assert.That(urls[0], Is.EqualTo("https://indexer.example.com/api?t=book&cat=3030&extended=1&apikey=k&offset=0&limit=100&title=Hexe%20Hilda%20Zauber%20im%20Garten"));
            Assert.That(urls[1], Is.EqualTo("https://indexer.example.com/api?t=search&cat=3030&extended=1&apikey=k&offset=0&limit=100&q=Hexe%20Hilda%20Zauber%20im%20Garten"));

            Assert.That(urls.Any(u => u.Contains("Sonder")), Is.False);
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
                Author = new Author { Name = "Mara Lindqvist" },
                BookTitle = "Zauber im Garten",
                Books = new List<Book>
                {
                    new Book { SeriesName = "Hexe Hilda", SeriesPosition = "1.0" },
                    new Book { SeriesName = "Fuchs Frieda", SeriesPosition = "2" }
                }
            };

            Assert.That(criteria.SeriesName, Is.EqualTo("Hexe Hilda"));
            Assert.That(criteria.SeriesPosition, Is.EqualTo("1.0"));
            Assert.That(criteria.SeriesQuery, Is.EqualTo("Hexe+Hilda"));
            Assert.That(criteria.SeriesTitleQuery, Is.EqualTo("Hexe+Hilda+Zauber+im+Garten"));
            Assert.That(criteria.SeriesPositionQuery, Is.EqualTo("Hexe+Hilda+1"));

            criteria.Books[0].SeriesPosition = "1.5";
            Assert.That(criteria.SeriesPositionQuery, Is.EqualTo("Hexe+Hilda+1.5"));
        }

        [Test]
        public void should_add_decimal_series_position_variant()
        {
            var generator = CreateNewznabGenerator();
            var searchCriteria = new BookSearchCriteria
            {
                Author = new Author { Name = "Mara Lindqvist" },
                BookTitle = "Zauber im Garten",
                Books = new List<Book>
                {
                    new Book
                    {
                        MediaType = BookMediaType.Audiobook,
                        SeriesName = "Hexe Hilda",
                        SeriesPosition = "1.5"
                    }
                }
            };

            var chain = generator.GetSearchRequests(searchCriteria);

            Assert.That(chain.Tiers, Is.EqualTo(5));

            var lastTierSets = chain.GetTier(4).ToList();
            Assert.That(lastTierSets, Has.Count.EqualTo(4));

            var urls = lastTierSets.Select(s => s.Single().HttpRequest.Url.FullUri).ToList();

            Assert.That(urls[1], Does.Contain("Hexe%20Hilda%201.5"));
            Assert.That(urls[3], Does.Contain("Hexe%20Hilda%201.5"));
            Assert.That(searchCriteria.SeriesPositionQuery, Is.EqualTo("Hexe+Hilda+1.5"));
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase(null)]
        public void should_handle_empty_or_whitespace_book_title_with_series(string title)
        {
            var generator = CreateNewznabGenerator();
            var searchCriteria = new BookSearchCriteria
            {
                Author = new Author { Name = "Mara Lindqvist" },
                BookTitle = title,
                Books = new List<Book>
                {
                    new Book
                    {
                        MediaType = BookMediaType.Audiobook,
                        SeriesName = "Hexe Hilda",
                        SeriesPosition = "1"
                    }
                }
            };

            Assert.DoesNotThrow(() =>
            {
                Assert.That(searchCriteria.SeriesTitleQuery, Is.Null);
                Assert.That(searchCriteria.SeriesPositionQuery, Is.EqualTo("Hexe+Hilda+1"));
            });

            var chain = generator.GetSearchRequests(searchCriteria);
            var allUrls = chain.GetAllTiers().Select(s => s.Single().HttpRequest.Url.FullUri).ToList();

            Assert.That(allUrls.Any(u => u.Contains("title=Hexe%20Hilda%201")), Is.True);
            Assert.That(allUrls.Any(u => u.Contains("q=Hexe%20Hilda%201")), Is.True);
            Assert.That(allUrls.Any(u => u.Contains("Zauber")), Is.False);

            var criteriaWithoutPosition = new BookSearchCriteria
            {
                Author = new Author { Name = "Mara Lindqvist" },
                BookTitle = title,
                Books = new List<Book>
                {
                    new Book
                    {
                        MediaType = BookMediaType.Audiobook,
                        SeriesName = "Hexe Hilda",
                        SeriesPosition = null
                    }
                }
            };

            Assert.DoesNotThrow(() =>
            {
                Assert.That(criteriaWithoutPosition.SeriesTitleQuery, Is.Null);
                Assert.That(criteriaWithoutPosition.SeriesPositionQuery, Is.Null);
            });

            var chainWithoutPosition = generator.GetSearchRequests(criteriaWithoutPosition);
            Assert.That(chainWithoutPosition.GetAllTiers().Count(), Is.EqualTo(0));
        }

        [Test]
        public void should_not_add_series_tier_when_books_list_is_null()
        {
            var generator = CreateNewznabGenerator();
            var searchCriteria = new BookSearchCriteria
            {
                Author = new Author { Name = "Mara Lindqvist" },
                BookTitle = "Zauber im Garten",
                Books = null
            };

            var chain = generator.GetSearchRequests(searchCriteria);

            Assert.That(chain.Tiers, Is.EqualTo(4));

            var allSets = chain.GetAllTiers().ToList();
            Assert.That(allSets, Has.Count.EqualTo(5));

            var allUrls = allSets.Select(s => s.Single().HttpRequest.Url.FullUri).ToList();
            Assert.That(allUrls[0], Is.EqualTo("https://indexer.example.com/api?t=book&cat=3030&extended=1&apikey=k&offset=0&limit=100&author=Mara%20Lindqvist&title=Zauber%20im%20Garten"));
            Assert.That(allUrls[1], Is.EqualTo("https://indexer.example.com/api?t=book&cat=3030&extended=1&apikey=k&offset=0&limit=100&title=Zauber%20im%20Garten"));
            Assert.That(allUrls[2], Is.EqualTo("https://indexer.example.com/api?t=search&cat=3030&extended=1&apikey=k&offset=0&limit=100&q=Zauber%20im%20Garten+Mara%20Lindqvist"));
            Assert.That(allUrls[3], Is.EqualTo("https://indexer.example.com/api?t=search&cat=3030&extended=1&apikey=k&offset=0&limit=100&q=Mara%20Lindqvist+Zauber%20im%20Garten"));
            Assert.That(allUrls[4], Is.EqualTo("https://indexer.example.com/api?t=search&cat=3030&extended=1&apikey=k&offset=0&limit=100&q=Zauber%20im%20Garten"));

            Assert.That(searchCriteria.SeriesName, Is.Null);
            Assert.That(searchCriteria.SeriesPosition, Is.Null);
            Assert.That(searchCriteria.SeriesQuery, Is.Null);
            Assert.That(searchCriteria.SeriesTitleQuery, Is.Null);
            Assert.That(searchCriteria.SeriesPositionQuery, Is.Null);
        }

        [Test]
        public void should_emit_series_title_variant_when_parenthetical_series_is_stripped_by_book_query()
        {
            var generator = CreateNewznabGenerator();
            var searchCriteria = new BookSearchCriteria
            {
                Author = new Author { Name = "Mara Lindqvist" },
                BookTitle = "Zauber im Garten (Hexe Hilda)",
                Books = new List<Book>
                {
                    new Book
                    {
                        MediaType = BookMediaType.Audiobook,
                        SeriesName = "Hexe Hilda",
                        SeriesPosition = "1"
                    }
                }
            };

            var chain = generator.GetSearchRequests(searchCriteria);

            Assert.That(chain.Tiers, Is.EqualTo(5));

            var lastTierSets = chain.GetTier(4).ToList();
            Assert.That(lastTierSets, Has.Count.EqualTo(4));

            var urls = lastTierSets.Select(s => s.Single().HttpRequest.Url.FullUri).ToList();

            Assert.That(urls[0], Is.EqualTo("https://indexer.example.com/api?t=book&cat=3030&extended=1&apikey=k&offset=0&limit=100&title=Hexe%20Hilda%20Zauber%20im%20Garten"));
            Assert.That(urls[1], Is.EqualTo("https://indexer.example.com/api?t=book&cat=3030&extended=1&apikey=k&offset=0&limit=100&title=Hexe%20Hilda%201"));
            Assert.That(urls[2], Is.EqualTo("https://indexer.example.com/api?t=search&cat=3030&extended=1&apikey=k&offset=0&limit=100&q=Hexe%20Hilda%20Zauber%20im%20Garten"));
            Assert.That(urls[3], Is.EqualTo("https://indexer.example.com/api?t=search&cat=3030&extended=1&apikey=k&offset=0&limit=100&q=Hexe%20Hilda%201"));

            Assert.That(urls[0], Does.Contain("title=Hexe%20Hilda%20Zauber%20im%20Garten"));
            Assert.That(urls[2], Does.Contain("q=Hexe%20Hilda%20Zauber%20im%20Garten"));
        }
    }
}
