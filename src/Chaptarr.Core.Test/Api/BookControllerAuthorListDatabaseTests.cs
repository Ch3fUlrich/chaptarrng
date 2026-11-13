using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Chaptarr.Api.V1.Books;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using NLog;
using NUnit.Framework;
using NzbDrone.Common.Cache;
using NzbDrone.Core.AuthorStats;
using NzbDrone.Core.Books;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Validation;

namespace Chaptarr.Core.Test.Api
{
    public partial class BookControllerAuthorListFixture
    {
        [TestCase(false)]
        [TestCase(true)]
        public void sqlite_stream_should_complete_when_book_and_author_are_deleted_after_id_snapshot(bool afterFirstChunk)
        {
            WithGeneralListDatabase(database =>
            {
                var events = ThrowingProxy<IEventAggregator>();
                var books = new BookRepository(database, events);
                var authors = new AuthorRepository(database, events, LogManager.GetCurrentClassLogger());
                var authorService = CreateDatabaseAuthorService(authors);
                var bookService = CreateDatabaseBookService(database, books);
                var controller = CreateDatabaseController(bookService, authorService);

                WithWarnings(logs =>
                {
                    // GetBooks reads the real id snapshot now; entity chunks are still deferred.
                    var sequence = controller.GetBooks(null, new List<int>(), null);

                    void DeleteRows()
                    {
                        books.Delete(257);
                        authors.Delete(43);
                        // Prove the strict APIs still throw for these exact disappearing rows.
                        Assert.Throws<ApplicationException>(() => books.Get(new[] { 257, 259 }));
                        Assert.Throws<ApplicationException>(() => authorService.GetAuthors(new[] { 42, 43 }));
                    }

                    IEnumerable<BookResource> DeleteBetweenReads()
                    {
                        if (!afterFirstChunk)
                        {
                            DeleteRows();
                        }

                        var emitted = 0;
                        foreach (var resource in sequence)
                        {
                            yield return resource;
                            if (afterFirstChunk && ++emitted == 256)
                            {
                                // JSON has already emitted the first chunk, but the second is unread.
                                DeleteRows();
                            }
                        }
                    }

                    // Serialize the sequence itself, not an eagerly materialized list. Parsing the
                    // result proves the array closed normally after both concurrent deletions.
                    var json = NzbDrone.Common.Serializer.STJson.ToJson(DeleteBetweenReads());
                    using var document = JsonDocument.Parse(json);
                    Assert.That(document.RootElement.EnumerateArray().Select(row => row.GetProperty("id").GetInt32()),
                        Is.EqualTo(Enumerable.Range(1, 260).Except(new[] { 257, 258 })));
                    Assert.That(logs.Logs.Where(log => log.Contains("Skipping book")), Is.EqualTo(new[]
                    {
                        "Warn|Skipping book 257: no longer present while loading general list chunk",
                        "Warn|Skipping book 258: author 43 no longer present while loading general list chunk"
                    }));
                });
            });
        }

        [TestCase(null)]
        [TestCase("author,overview,links")]
        public void sqlite_unchanged_stream_should_match_eager_json_across_chunks(string include)
        {
            WithGeneralListDatabase(database =>
            {
                var events = ThrowingProxy<IEventAggregator>();
                var books = new BookRepository(database, events);
                var authors = CreateDatabaseAuthorService(new AuthorRepository(database, events, LogManager.GetCurrentClassLogger()));
                var bookService = CreateDatabaseBookService(database, books);
                var controller = CreateDatabaseController(bookService, authors);

                var eagerBooks = bookService.GetBooksForDisplay();
                var authorsById = authors.GetAllAuthors(bypassCache: true).ToDictionary(author => author.Id);
                foreach (var book in eagerBooks)
                {
                    book.Author = authorsById[book.AuthorId];
                }

                var map = typeof(BookControllerWithSignalR).GetMethod("MapToResource",
                    BindingFlags.Instance | BindingFlags.NonPublic, null,
                    new[] { typeof(List<Book>), typeof(bool), typeof(IReadOnlyDictionary<int, BookStatistics>), typeof(bool), typeof(bool) }, null);
                var expectedJson = NzbDrone.Common.Serializer.STJson.ToJson(map.Invoke(controller,
                    new object[] { eagerBooks, include != null, new Dictionary<int, BookStatistics>(), include != null, include != null }));

                var actualJson = NzbDrone.Common.Serializer.STJson.ToJson(controller.GetBooks(null, new List<int>(), null, include: include));
                Assert.That(actualJson, Is.EqualTo(expectedJson));
                using var document = JsonDocument.Parse(actualJson);
                Assert.That(document.RootElement.GetArrayLength(), Is.EqualTo(260));
            });
        }

        [Test]
        public void sqlite_general_list_chunk_should_skip_missing_rows_and_preserve_snapshot_order()
        {
            WithGeneralListDatabase(database =>
            {
                var books = new BookRepository(database, ThrowingProxy<IEventAggregator>());
                var service = CreateDatabaseBookService(database, books);
                books.Delete(257);

                WithWarnings(logs =>
                {
                    var chunk = service.GetGeneralListChunk(new List<int> { 260, 257, 1 });
                    Assert.That(chunk.Select(book => book.Id), Is.EqualTo(new[] { 260, 1 }));
                    Assert.That(chunk.All(book => book.Editions.Count == 1 && book.Editions.Single().BookFiles == null), Is.True);
                    Assert.That(logs.Logs.Single(), Does.Contain("Warn|Skipping book 257:"));
                });
            });
        }

        [Test]
        public void sqlite_existing_authors_should_tolerate_missing_duplicate_and_empty_ids_without_changing_get_authors()
        {
            WithGeneralListDatabase(database =>
            {
                var repository = new AuthorRepository(database, ThrowingProxy<IEventAggregator>(), LogManager.GetCurrentClassLogger());
                var service = CreateDatabaseAuthorService(repository);
                repository.Delete(43);

                Assert.That(service.GetExistingAuthors(new[] { 43, 42, 42 }).Select(author => author.Id), Is.EqualTo(new[] { 42 }));
                Assert.That(service.GetExistingAuthors(new[] { 43 }), Is.Empty);
                Assert.That(service.GetExistingAuthors(Array.Empty<int>()), Is.Empty);
                Assert.That(service.GetExistingAuthors(null), Is.Empty);
                Assert.Throws<ApplicationException>(() => service.GetAuthors(new[] { 43, 42 }));
                Assert.That(service.GetAuthors(new[] { 42 }).Single().Id, Is.EqualTo(42));
            });
        }

        private static AuthorService CreateDatabaseAuthorService(IAuthorRepository repository)
        {
            return new AuthorService(repository, eventAggregator: null, authorPathBuilder: null, rootFolderService: null,
                commandQueueManager: null, cacheManager: new CacheManager(), bookRepository: null,
                mediaFileService: null, logger: LogManager.GetCurrentClassLogger());
        }

        private static BookService CreateDatabaseBookService(IMainDatabase database, IBookRepository repository)
        {
            var events = ThrowingProxy<IEventAggregator>();
            var editions = new EditionService(new EditionRepository(database, events), events, LogManager.GetCurrentClassLogger());
            return new BookService(repository, editions, eventAggregator: null, authorService: null, mediaFileService: null,
                rootFolderService: null, seriesBookLinkRepository: null, multiCopySeriesService: null,
                logger: LogManager.GetCurrentClassLogger());
        }

        private static BookController CreateDatabaseController(IBookService books, IAuthorService authors)
        {
            var controller = new BookController(
                authorService: authors, bookService: books, addBookService: null, editionService: null, editionSelector: null,
                seriesBookLinkService: Proxy<ISeriesBookLinkService>(new Dictionary<string, Func<MethodInfo, object[], object>>
                {
                    [nameof(ISeriesBookLinkService.GetLinksByBook)] = (_, _) => new List<SeriesBookLink>()
                }),
                authorStatisticsService: Proxy<IAuthorStatisticsService>(new Dictionary<string, Func<MethodInfo, object[], object>>
                {
                    [nameof(IAuthorStatisticsService.AuthorStatistics)] = (_, _) => new List<AuthorStatistics>()
                }),
                mediaFileService: Proxy<IMediaFileService>(new Dictionary<string, Func<MethodInfo, object[], object>>
                {
                    [nameof(IMediaFileService.GetFilesByBooksForDisplay)] = (_, _) => new List<BookFile>()
                }),
                coverMapper: Proxy<IMapCoversToLocal>(new Dictionary<string, Func<MethodInfo, object[], object>>
                {
                    [nameof(IMapCoversToLocal.ConvertToLocalUrls)] = (_, _) => null
                }),
                mediaCoverProxy: null, upgradableSpecification: null, signalRBroadcaster: null, commandQueueManager: null,
                eventAggregator: null, metadataProfileService: null, qualityProfileService: null, rootFolderService: null,
                qualityProfileExistsValidator: new QualityProfileExistsValidator(new TestQualityProfileService()),
                metadataProfileExistsValidator: new MetadataProfileExistsValidator(new TestMetadataProfileService()),
                logger: LogManager.GetCurrentClassLogger());
            controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
            return controller;
        }

        private static void WithGeneralListDatabase(Action<MainDatabase> action)
        {
            if (TableMapping.Mapper.TableMap.Count == 0)
            {
                TableMapping.Map();
            }

            // Follow the direct SQLite repository fixtures; the keeper holds this isolated
            // in-memory database alive while real repositories open their own connections.
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = "book_list_stream_" + Guid.NewGuid().ToString("N"),
                Mode = SqliteOpenMode.Memory,
                Cache = SqliteCacheMode.Shared,
                Pooling = false
            }.ToString();
            using var keeper = new SqliteConnection(connectionString);
            keeper.Open();
            CreateGeneralListSchema<Author>(keeper);
            CreateGeneralListSchema<Book>(keeper);
            CreateGeneralListSchema<Edition>(keeper);
            keeper.Execute("INSERT INTO \"Authors\" (\"Id\", \"Name\") VALUES (42, 'Surviving Author'), (43, 'Deleted Author')");
            keeper.Execute("INSERT INTO \"Books\" (\"Id\", \"AuthorId\", \"Title\", \"TitleSlug\", \"MediaType\", \"EbookMonitored\") VALUES (@Id, @AuthorId, @Title, @TitleSlug, 1, 1)",
                Enumerable.Range(1, 260).Select(id => new
                {
                    Id = id, AuthorId = id == 258 ? 43 : 42, Title = "Book " + id, TitleSlug = "book-" + id
                }));
            keeper.Execute("INSERT INTO \"Editions\" (\"Id\", \"BookId\", \"Title\", \"Monitored\", \"Language\") VALUES (@Id, @Id, @Title, 1, 'en')",
                Enumerable.Range(1, 260).Select(id => new { Id = id, Title = "Edition " + id }));

            var database = new Database("main", () =>
            {
                var connection = new SqliteConnection(connectionString);
                connection.Open();
                return connection;
            });
            action(new MainDatabase(database));
        }

        private static void CreateGeneralListSchema<T>(SqliteConnection connection)
            where T : ModelBase
        {
            var excluded = TableMapping.Mapper.ExcludeProperties(typeof(T))
                .Select(property => property.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var columns = typeof(T).GetProperties()
                .Where(property => property.Name == nameof(ModelBase.Id) || (property.IsMappableProperty() && !excluded.Contains(property.Name)))
                .GroupBy(property => property.Name, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .Select(property =>
                {
                    if (property.Name == nameof(ModelBase.Id))
                    {
                        return "\"Id\" INTEGER PRIMARY KEY";
                    }

                    var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                    var columnType = type.IsEnum || type == typeof(int) || type == typeof(long) || type == typeof(bool) ? "INTEGER" : "TEXT";
                    return $"\"{property.Name}\" {columnType} NULL";
                });
            connection.Execute($"CREATE TABLE \"{TableMapping.Mapper.TableNameMapping(typeof(T))}\" ({string.Join(", ", columns)})");
        }
    }
}
