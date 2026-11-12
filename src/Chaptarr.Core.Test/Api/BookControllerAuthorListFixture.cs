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
    public class BookControllerAuthorListFixture
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

        private void AssertBookListUsesDisplayFiles(bool unfiltered)
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
            var controller = new BookController(
                authorService: Proxy<IAuthorService>(new Dictionary<string, Func<MethodInfo, object[], object>>
                {
                    [nameof(IAuthorService.GetAuthor)] = (_, _) => author,
                    [nameof(IAuthorService.GetAllAuthors)] = (_, _) => new List<Author> { author }
                }),
                bookService: Proxy<IBookService>(new Dictionary<string, Func<MethodInfo, object[], object>>
                {
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
                    [nameof(ISeriesBookLinkService.GetLinksByBook)] = (_, _) => new List<SeriesBookLink>()
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
                        Assert.That((List<int>)args[0], Is.EquivalentTo(new[] { 100, 101 }));
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

            var resources = controller.GetBooks(
                authorId: unfiltered ? (int?)null : author.Id,
                bookIds: new List<int>(),
                bookId: null,
                mediaType: "ebook");

            Assert.That(resources.Select(x => x.Id), Is.EquivalentTo(new[] { 100, 101 }));
            Assert.That(displayLoads, Is.EqualTo(unfiltered ? 1 : 0));
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
                Assert.That(books[0].BookFiles.Single(), Is.SameAs(displayFile));
                Assert.That(displayFile.MatchProvenanceLoaded, Is.False);
                Assert.That(displayFile.Edition.Book, Is.Null);
                Assert.That(displayFile.Edition.Title, Is.Null);
                Assert.That(books[1].LazyBookFiles.IsLoaded, Is.True, "No-file book relationship is known-empty");
                Assert.That(books[1].BookFiles, Is.Empty, "No-file book has no display files");
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
