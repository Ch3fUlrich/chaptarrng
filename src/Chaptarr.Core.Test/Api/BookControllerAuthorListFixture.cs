using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Chaptarr.Core.Test;
using Chaptarr.Api.V1.Books;
using Chaptarr.Http.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NLog;
using NLog.Config;
using NLog.Targets;
using NUnit.Framework;
using NzbDrone.Core.AuthorStats;
using NzbDrone.Core.Books;
using NzbDrone.Core.DecisionEngine.Specifications;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Profiles.Metadata;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.RootFolders;
using NzbDrone.Core.Validation;
using NzbDrone.SignalR;

namespace Chaptarr.Core.Test.Api
{
    [TestFixture]
    [NonParallelizable]
    public partial class BookControllerAuthorListFixture
    {
        private class ServiceProxy<T> : DispatchProxy where T : class
        {
            public Dictionary<string, Func<MethodInfo, object[], object>> Handlers { get; set; } = new();

            protected override object Invoke(MethodInfo targetMethod, object[] args)
            {
                if (Handlers.TryGetValue(targetMethod.Name, out var handler))
                {
                    return handler(targetMethod, args);
                }

                throw new NotImplementedException($"Test proxy does not implement {typeof(T).Name}.{targetMethod.Name}");
            }
        }

        private static T Proxy<T>(Dictionary<string, Func<MethodInfo, object[], object>> handlers) where T : class
        {
            var proxy = DispatchProxy.Create<T, ServiceProxy<T>>();
            ((ServiceProxy<T>)(object)proxy).Handlers = handlers;
            return proxy;
        }

        private static T ThrowingProxy<T>() where T : class
        {
            return Proxy<T>(new Dictionary<string, Func<MethodInfo, object[], object>>());
        }

        [Test]
        public void author_book_list_should_not_reapply_metadata_profile_filter_to_local_rows()
        {
            AssertBookListUsesDisplayFiles(unfiltered: false);
        }

        [Test]
        public void unfiltered_book_list_should_hydrate_availability_using_only_display_files()
        {
            AssertBookListUsesDisplayFiles(unfiltered: true);
        }

        [Test]
        public void production_shaped_index_should_keep_mapping_working_set_bounded()
        {
            AssertBookListUsesDisplayFiles(unfiltered: true, large: true);
        }

        [Test]
        public void paged_index_should_match_eager_json_in_the_same_order()
        {
            AssertBookListUsesDisplayFiles(unfiltered: true, paged: true);
        }

        [TestCase(null)]
        [TestCase("links")]
        public void unfiltered_stream_should_match_eager_json_for_multiple_authors(string include)
        {
            AssertBookListUsesDisplayFiles(unfiltered: true, varied: true, include: include);
        }

        [Test]
        public void unfiltered_stream_should_complete_when_book_disappears_after_ids_load()
        {
            WithWarnings(logs =>
            {
                AssertBookListUsesDisplayFiles(unfiltered: true, missingBook: true);
                Assert.That(logs.Logs.Where(log => log.Contains("Skipping book")), Is.EqualTo(new[]
                {
                    "Warn|Skipping book 100: no longer present while loading general list chunk"
                }));
            });
        }

        [Test]
        public void unfiltered_stream_should_complete_when_author_disappears_at_chunk_load()
        {
            WithWarnings(logs =>
            {
                AssertBookListUsesDisplayFiles(unfiltered: true, missingAuthor: true);
                Assert.That(logs.Logs.Where(log => log.Contains("Skipping book")), Is.EqualTo(new[]
                {
                    "Warn|Skipping book 100: author 42 no longer present while loading general list chunk"
                }));
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void streamed_facade_list_should_log_one_identity_warning_across_chunks(bool filtered)
        {
            WithWarnings(logs =>
            {
                AssertBookListUsesDisplayFiles(unfiltered: true, multipleChunks: true, facade: true, filtered: filtered);
                var identityWarnings = logs.Logs.Where(log => log.Contains("[ReadarrFacade]")).ToList();
                Assert.That(identityWarnings, Has.Count.EqualTo(1));
                Assert.That(identityWarnings.Single(), Does.Contain("Warn|[ReadarrFacade] Emitted 257 book resource(s) without hc identity from book response"));
            });
        }

        private static void WithWarnings(Action<MemoryTarget> action)
        {
            var originalConfiguration = LogManager.Configuration;
            try
            {
                var logs = new MemoryTarget("book-list-warnings") { Layout = "${level}|${message}" };
                var configuration = new LoggingConfiguration();
                configuration.AddRule(LogLevel.Warn, LogLevel.Fatal, logs);
                LogManager.Configuration = configuration;
                LogManager.ReconfigExistingLoggers();
                action(logs);
            }
            finally
            {
                LogManager.Configuration = originalConfiguration;
                LogManager.ReconfigExistingLoggers();
            }
        }

        private void AssertBookListUsesDisplayFiles(bool unfiltered, bool large = false, bool paged = false,
            bool varied = false, string include = null, bool missingBook = false, bool missingAuthor = false,
            bool multipleChunks = false, bool facade = false, bool filtered = false)
        {
            var author = new Author
            {
                Id = 42,
                Name = "Author Name",
                SortNameLastFirst = "Name, Author",
                EbookMetadataProfileId = 7
            };

            var books = new List<Book>
            {
                new Book
                {
                    Id = 100,
                    AuthorId = author.Id,
                    Title = "Visible Book One",
                    TitleSlug = "visible-book-one",
                    MediaType = BookMediaType.Ebook,
                    EbookMonitored = true,
                    AnyEditionOk = true,
                    Ratings = new Ratings()
                },
                new Book
                {
                    Id = 101,
                    AuthorId = author.Id,
                    Title = "Visible Book Two",
                    TitleSlug = "visible-book-two",
                    MediaType = BookMediaType.Ebook,
                    EbookMonitored = false,
                    AnyEditionOk = true,
                    Ratings = new Ratings()
                }
            };

            var displayLoads = 0;
            var authors = new List<Author> { author };
            if (missingAuthor)
            {
                authors.Add(new Author { Id = 43, Name = "Surviving Author" });
                books[1].AuthorId = 43;
            }

            if (varied)
            {
                authors.Add(new Author { Id = 43, Name = "Second Author" });
                authors.Add(new Author { Id = 44, Name = "Third Author" });
                books[0].Editions = new List<Edition>();
                books[0].HardcoverBookId = "100";
                books[1].Editions = new List<Edition>();
                books[1].AuthorId = 43;
                books.Add(new Book
                {
                    Id = 102, AuthorId = 44, Title = "Third Book", MediaType = BookMediaType.Ebook,
                    Ratings = new Ratings(), Editions = new List<Edition>
                    {
                        new Edition { Id = 502, BookId = 102, Monitored = true }
                    }
                });
                books.Add(new Book { Id = 103, AuthorId = 999, Title = "Orphan", Ratings = new Ratings() });
            }

            var liveBooks = new List<WeakReference>();
            var entityLoads = 0;
            if (multipleChunks)
            {
                books.AddRange(Enumerable.Range(102, 255).Select(id => new Book
                {
                    Id = id, AuthorId = author.Id, Title = "Generated book " + id,
                    MediaType = BookMediaType.Ebook, Ratings = new Ratings()
                }));
            }

            if (large)
            {
                books.Clear();
                // Only ids are generated below; no eager production-sized entity set.
            }
            var displayFile = new BookFile
            {
                Id = 900,
                EditionId = 500,
                Edition = new Edition { Id = 500, BookId = 100 },
                Path = "/books/book.epub",
                MediaType = "ebook",
                DurationSeconds = 3723,
                Size = 987654321
            };
            if (!unfiltered)
            {
                // Mirror BookService.GetBooksForDisplay's author-scoped attachment:
                // resource metadata editions are distinct from the file's key-only join.
                books[0].BookFiles = new List<BookFile> { displayFile };
                books[0].Editions = new List<Edition>
                {
                    new Edition
                    {
                        Id = 500, BookId = 100, Monitored = true, Language = "en",
                        BookFiles = new List<BookFile> { displayFile }
                    }
                };
            }

            var expectedBookIds = books
                .Where(b => authors.Any(a => a.Id == b.AuthorId) &&
                    (!missingBook || b.Id != 100) && (!missingAuthor || b.AuthorId != author.Id))
                .Select(b => b.Id).ToList();
            var controller = new BookController(
                authorService: Proxy<IAuthorService>(new Dictionary<string, Func<MethodInfo, object[], object>>
                {
                    [nameof(IAuthorService.GetAuthor)] = (_, _) => author,
                    [nameof(IAuthorService.GetExistingAuthors)] = (_, args) =>
                    {
                        var service = CreateDatabaseAuthorService(
                            Proxy<IAuthorRepository>(new Dictionary<string, Func<MethodInfo, object[], object>>
                            {
                                [nameof(IAuthorRepository.FindExisting)] = (_, repositoryArgs) => authors
                                    .Where(a => ((IEnumerable<int>)repositoryArgs[0]).Contains(a.Id)).ToList()
                            }));
                        return service.GetExistingAuthors((IEnumerable<int>)args[0]);
                    },
                    [nameof(IAuthorService.GetAllAuthors)] = (_, _) => authors
                }),
                bookService: Proxy<IBookService>(new Dictionary<string, Func<MethodInfo, object[], object>>
                {
                    [nameof(IBookService.GetGeneralListIds)] = (_, _) => large
                        ? Enumerable.Range(100, 50000).ToList() : books.Select(b => b.Id).ToList(),
                    [nameof(IBookService.GetGeneralListChunk)] = (_, args) =>
                    {
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        GC.Collect();
                        Assert.That(liveBooks.Count(w => w.IsAlive), Is.Zero, "Previous entity chunk must be released before loading another");
                        liveBooks.Clear();
                        var ids = (List<int>)args[0];
                        Assert.That(ids.Count, Is.LessThanOrEqualTo(256));
                        entityLoads++;
                        if (missingAuthor)
                        {
                            authors.Remove(author);
                        }

                        var chunk = large ? ids.Select(id => new Book
                        {
                            Id = id, AuthorId = author.Id, Title = "Generated book " + id,
                            MediaType = BookMediaType.Ebook, Ratings = new Ratings(),
                            Editions = new List<Edition> { new Edition { Id = id, BookId = id, Monitored = true } }
                        }).ToList() : ids.Select(id => books.Single(b => b.Id == id)).ToList();
                        if (large)
                        {
                            liveBooks.AddRange(chunk.Select(b => new WeakReference(b)));
                        }

                        if (missingBook)
                        {
                            // Exercise the real service after the id snapshot, with a partial repository result.
                            var service = new BookService(
                                Proxy<IBookRepository>(new Dictionary<string, Func<MethodInfo, object[], object>>
                                {
                                    [nameof(IBookRepository.FindExisting)] = (_, _) => chunk.Where(b => b.Id != 100).ToList()
                                }),
                                Proxy<IEditionService>(new Dictionary<string, Func<MethodInfo, object[], object>>
                                {
                                    [nameof(IEditionService.GetMonitoredEditionsForBookIds)] = (_, _) => new List<Edition>()
                                }),
                                eventAggregator: null, authorService: null, mediaFileService: null,
                                rootFolderService: null, seriesBookLinkRepository: null,
                                multiCopySeriesService: null, logger: LogManager.GetCurrentClassLogger());
                            return service.GetGeneralListChunk(ids);
                        }

                        return chunk;
                    },
                    [nameof(IBookService.GetBooksForDisplay)] = (_, args) =>
                    {
                        Assert.That(args[0], Is.EqualTo(unfiltered ? (int?)null : author.Id));
                        Assert.That(args[1], Is.EqualTo("ebook"));
                        return books;
                    }
                }),
                addBookService: null,
                editionService: Proxy<IEditionService>(new Dictionary<string, Func<MethodInfo, object[], object>>
                {
                    [nameof(IEditionService.GetEditionsByAuthor)] = (_, _) => new List<Edition>()
                }),
                editionSelector: null,
                seriesBookLinkService: Proxy<ISeriesBookLinkService>(new Dictionary<string, Func<MethodInfo, object[], object>>
                {
                    [nameof(ISeriesBookLinkService.GetLinksByBook)] = (_, _) => varied
                        ? new List<SeriesBookLink>
                        {
                            new SeriesBookLink
                            {
                                BookId = 102, SeriesId = 700, Position = "1",
                                Series = new Series { Id = 700, Title = "Fixture Series" }
                            }
                        }
                        : new List<SeriesBookLink>()
                }),
                authorStatisticsService: Proxy<IAuthorStatisticsService>(new Dictionary<string, Func<MethodInfo, object[], object>>
                {
                    [nameof(IAuthorStatisticsService.AuthorStatistics)] = (_, _) => new List<AuthorStatistics>()
                }),
                mediaFileService: Proxy<IMediaFileService>(new Dictionary<string, Func<MethodInfo, object[], object>>
                {
                    // Every other call (including full loads and writes) fails through the proxy.
                    [nameof(IMediaFileService.GetFilesByBooksForDisplay)] = (_, args) =>
                    {
                        if (!large)
                        {
                            Assert.That((List<int>)args[0], Is.EqualTo(expectedBookIds
                                .Skip(displayLoads * 256).Take(256).ToList()));
                        }
                        displayLoads++;
                        return new List<BookFile> { displayFile };
                    }
                }),
                coverMapper: Proxy<IMapCoversToLocal>(new Dictionary<string, Func<MethodInfo, object[], object>>
                {
                    [nameof(IMapCoversToLocal.ConvertToLocalUrls)] = (_, _) => null
                }),
                mediaCoverProxy: null,
                upgradableSpecification: ThrowingProxy<IUpgradableSpecification>(),
                signalRBroadcaster: null,
                commandQueueManager: ThrowingProxy<IManageCommandQueue>(),
                eventAggregator: ThrowingProxy<IEventAggregator>(),
                metadataProfileService: ThrowingProxy<IMetadataProfileService>(),
                qualityProfileService: ThrowingProxy<IQualityProfileService>(),
                rootFolderService: ThrowingProxy<IRootFolderService>(),
                qualityProfileExistsValidator: new QualityProfileExistsValidator(new TestQualityProfileService()),
                metadataProfileExistsValidator: new MetadataProfileExistsValidator(new TestMetadataProfileService()),
                logger: LogManager.GetCurrentClassLogger());

            var before = GC.GetTotalMemory(true);
            controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
            if (facade)
            {
                controller.HttpContext.Items[ReadarrFacadeContext.ItemKey] = new ReadarrFacadeContext("hc", "ebook", "/ebook");
            }

            string expectedJson = null;
            if (unfiltered && !large && !missingBook && !missingAuthor)
            {
                // Reproduce the former eager general-list mapping on the same fixture.
                foreach (var book in books)
                {
                    // Set the relationship directly: Author's setter also changes AuthorId,
                    // including resetting the orphan's foreign key when no author exists.
                    book.LazyAuthor = authors.SingleOrDefault(a => a.Id == book.AuthorId);
                    book.BookFiles = book.Id == 100 ? new List<BookFile> { displayFile } : new List<BookFile>();
                }

                var map = typeof(BookControllerWithSignalR).GetMethod("MapToResource",
                    BindingFlags.Instance | BindingFlags.NonPublic, null,
                    new[] { typeof(List<Book>), typeof(bool), typeof(IReadOnlyDictionary<int, BookStatistics>), typeof(bool), typeof(bool), typeof(bool) }, null);
                expectedJson = NzbDrone.Common.Serializer.STJson.ToJson(map.Invoke(controller,
                    new object[] { books.Where(b => b.Author != null).ToList(), false, new Dictionary<int, BookStatistics>(), false, include == "links", false }));
                // Do not let eager hydration mask a missing streamed relationship load.
                foreach (var book in books)
                {
                    // Clear only the relationship, retaining the stored foreign key for chunk loading.
                    book.LazyAuthor = null;
                    book.SeriesLinks = new List<SeriesBookLink>();
                    book.BookFiles = new List<BookFile>();
                }
            }

            var sequence = controller.GetBooks(
                authorId: unfiltered ? (int?)null : author.Id,
                bookIds: new List<int>(),
                bookId: null,
                include: include,
                mediaType: unfiltered && !paged && !filtered ? null : "ebook",
                offset: paged ? 0 : (int?)null,
                limit: paged ? 2 : (int?)null);

            if (large)
            {
                var count = 0;
                var peak = before;
                foreach (var resource in sequence)
                {
                    count++;
                    if (count % 256 == 1)
                    {
                        peak = Math.Max(peak, GC.GetTotalMemory(true));
                    }
                }

                TestContext.WriteLine($"50000-book mapping managed growth: {peak - before:N0} bytes");
                // Old eager mapping retains 50,000 resources plus nested lists/availability.
                // Even 512 bytes/resource = 25.6 MB; a 16 MiB bound excludes that path.
                Assert.That(peak - before, Is.LessThan(16L * 1024 * 1024));
                Assert.That(count, Is.EqualTo(50000));
                Assert.That(entityLoads, Is.EqualTo(196));
                return;
            }

            var resources = sequence.ToList();
            if (expectedJson != null)
            {
                Assert.That(resources, Is.Not.Empty);
                Assert.That(NzbDrone.Common.Serializer.STJson.ToJson(resources), Is.EqualTo(expectedJson));
            }

            Assert.That(resources.Select(x => x.Id), Is.EqualTo(expectedBookIds));
            Assert.That(displayLoads, Is.EqualTo(unfiltered ? (books.Count + 255) / 256 : 0));
            if (missingBook || missingAuthor)
            {
                Assert.That(NzbDrone.Common.Serializer.STJson.ToJson(resources), Does.EndWith("]"));
                return;
            }
            if (!unfiltered)
            {
                var resource = resources.Single(x => x.Id == 100);
                Assert.Multiple(() =>
                {
                    Assert.That(resource.Duration, Is.EqualTo(TimeSpan.FromSeconds(3723)));
                    Assert.That(resource.DurationMinutes, Is.EqualTo(3723 / 60.0));
                    Assert.That(resource.HasFiles, Is.True);
                    Assert.That(resource.Availability, Is.Not.Null);
                    Assert.That(resource.Availability.CanRead, Is.True);
                    Assert.That(resource.Availability.CanListen, Is.False);
                    Assert.That(resource.Availability.FileFormats, Is.EqualTo(new[] { "epub" }));
                    Assert.That(resource.Availability.Languages, Is.EqualTo(new[] { "en" }));
                    Assert.That(resource.BookFiles, Is.Null, "List does not map individual file resources");
                });
            }
            if (unfiltered)
            {
                Assert.That(displayFile.MatchProvenanceLoaded, Is.False);
                Assert.That(displayFile.Edition.Book, Is.Null);
                Assert.That(displayFile.Edition.Title, Is.Null);
                Assert.That(resources.Single(x => x.Id == 100).Availability, Is.Not.Null, "File-backed availability populated");
                Assert.That(resources.Single(x => x.Id == 100).Availability.CanRead, Is.True, "EPUB can be read");
                Assert.That(resources.Single(x => x.Id == 101).Availability, Is.Not.Null, "No-file availability populated");
                Assert.That(resources.Single(x => x.Id == 101).Availability.CanRead, Is.False, "No-file book cannot be read");
                Assert.That(resources.All(x => x.BookFiles == null), Is.True);
            }
        }

        [Test]
        public void facade_put_should_promote_only_requested_media_side_and_return_monitored_book()
        {
            var author = new Author
            {
                Id = 42,
                Name = "Susanna Clarke",
                AudiobookMonitored = false,
                EbookMonitored = false,
                SyncMonitoredAcrossFormats = true,
                AudiobookRootFolderPath = "/audiobooks",
                EbookRootFolderPath = "/ebooks"
            };
            var book = new Book
            {
                Id = 100,
                AuthorId = author.Id,
                Author = author,
                Title = "Piranesi",
                TitleSlug = "piranesi",
                CleanTitle = "piranesi",
                MediaType = BookMediaType.Audiobook,
                AudiobookMonitored = false,
                EbookMonitored = false,
                AnyEditionOk = true,
                Ratings = new Ratings(),
                Editions = new List<Edition>
                {
                    new Edition
                    {
                        Id = 501,
                        BookId = 100,
                        Title = "Piranesi",
                        ForeignEditionId = "hc:edition:501-audiobook",
                        Monitored = true
                    }
                }
            };

            var authorUpdateCount = 0;
            var scopedBookUpdateCount = 0;
            var genericBookUpdateCount = 0;
            var bookService = Proxy<IBookService>(new Dictionary<string, Func<MethodInfo, object[], object>>
            {
                [nameof(IBookService.GetBook)] = (_, _) => book,
                [nameof(IBookService.SetMonitoredForMediaType)] = (_, args) =>
                {
                    Assert.That((IEnumerable<int>)args[0], Is.EquivalentTo(new[] { book.Id }));
                    Assert.That(args[1], Is.EqualTo("audiobook"));
                    Assert.That(args[2], Is.True);
                    book.SetMonitoredForMediaType("audiobook", true);
                    scopedBookUpdateCount++;
                    return null;
                },
                [nameof(IBookService.UpdateBook)] = (_, args) =>
                {
                    genericBookUpdateCount++;
                    return args[0];
                }
            });
            var controller = new BookController(
                authorService: Proxy<IAuthorService>(new Dictionary<string, Func<MethodInfo, object[], object>>
                {
                    [nameof(IAuthorService.EnsureMediaTypeMonitoring)] = (_, args) =>
                    {
                        Assert.That(args[0], Is.EqualTo(author.Id));
                        Assert.That(args[1], Is.EqualTo("audiobook"));
                        author.AudiobookMonitored = true;
                        author.Monitored = true;
                        authorUpdateCount++;
                        return null;
                    }
                }),
                bookService: bookService,
                addBookService: null,
                editionService: Proxy<IEditionService>(new Dictionary<string, Func<MethodInfo, object[], object>>
                {
                    [nameof(IEditionService.UpdateMany)] = (_, _) => null
                }),
                editionSelector: Proxy<IEditionSelector>(new Dictionary<string, Func<MethodInfo, object[], object>>
                {
                    [nameof(IEditionSelector.EnsureSingleMonitoredEdition)] = (_, _) => false
                }),
                seriesBookLinkService: ThrowingProxy<ISeriesBookLinkService>(),
                authorStatisticsService: Proxy<IAuthorStatisticsService>(new Dictionary<string, Func<MethodInfo, object[], object>>
                {
                    [nameof(IAuthorStatisticsService.AuthorStatistics)] = (method, _) =>
                        method.ReturnType == typeof(AuthorStatistics)
                            ? new AuthorStatistics()
                            : new List<AuthorStatistics>()
                }),
                mediaFileService: null,
                coverMapper: Proxy<IMapCoversToLocal>(new Dictionary<string, Func<MethodInfo, object[], object>>
                {
                    [nameof(IMapCoversToLocal.ConvertToLocalUrls)] = (_, _) => null
                }),
                mediaCoverProxy: null,
                upgradableSpecification: ThrowingProxy<IUpgradableSpecification>(),
                signalRBroadcaster: Proxy<IBroadcastSignalRMessage>(new Dictionary<string, Func<MethodInfo, object[], object>>
                {
                    ["get_IsConnected"] = (_, _) => false
                }),
                commandQueueManager: ThrowingProxy<IManageCommandQueue>(),
                eventAggregator: ThrowingProxy<IEventAggregator>(),
                metadataProfileService: ThrowingProxy<IMetadataProfileService>(),
                qualityProfileService: ThrowingProxy<IQualityProfileService>(),
                rootFolderService: ThrowingProxy<IRootFolderService>(),
                qualityProfileExistsValidator: new QualityProfileExistsValidator(new TestQualityProfileService()),
                metadataProfileExistsValidator: new MetadataProfileExistsValidator(new TestMetadataProfileService()),
                logger: LogManager.GetCurrentClassLogger());
            var httpContext = new DefaultHttpContext();
            httpContext.Items[ReadarrFacadeContext.ItemKey] =
                new ReadarrFacadeContext("hc", "audiobook", "/audiobook");
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

            var result = controller.UpdateBookByBody(new BookResource
            {
                Id = book.Id,
                Monitored = true,
                AnyEditionOk = true,
                TitleSlug = book.TitleSlug,
                Editions = new List<EditionResource>
                {
                    new EditionResource
                    {
                        Id = 501,
                        BookId = book.Id,
                        Title = "Piranesi",
                        ForeignEditionId = "501",
                        Monitored = true
                    }
                }
            });

            var accepted = result.Result as AcceptedAtActionResult;
            var resource = accepted?.Value as BookResource;
            Assert.Multiple(() =>
            {
                Assert.That(authorUpdateCount, Is.EqualTo(1));
                Assert.That(scopedBookUpdateCount, Is.EqualTo(1));
                Assert.That(genericBookUpdateCount, Is.Zero);
                Assert.That(author.AudiobookMonitored, Is.True);
                Assert.That(author.EbookMonitored, Is.False);
                Assert.That(author.SyncMonitoredAcrossFormats, Is.True);
                Assert.That(book.AudiobookMonitored, Is.True);
                Assert.That(book.EbookMonitored, Is.False);
                Assert.That(resource?.Monitored, Is.True);
            });
        }
    }
}
