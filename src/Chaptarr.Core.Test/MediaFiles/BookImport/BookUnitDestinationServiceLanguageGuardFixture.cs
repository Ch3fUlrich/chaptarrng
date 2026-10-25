using System;
using System.Collections.Generic;
using System.Data;
using System.IO.Abstractions;
using System.Linq;
using Microsoft.Data.Sqlite;
using NLog;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.BookImport.Services;

namespace Chaptarr.Core.Test.MediaFiles.BookImport
{
    [TestFixture]
    public class BookUnitDestinationServiceLanguageGuardFixture
    {
        public class NullDiskProvider : System.Reflection.DispatchProxy
        {
            protected override object Invoke(System.Reflection.MethodInfo targetMethod, object[] args)
            {
                return targetMethod.ReturnType.IsValueType && targetMethod.ReturnType != typeof(void)
                    ? Activator.CreateInstance(targetMethod.ReturnType)
                    : null;
            }
        }

        private sealed class StubMainDatabase : IMainDatabase
        {
            public IDbConnection OpenConnection()
            {
                var connection = new SqliteConnection("DataSource=:memory:");
                connection.Open();
                return connection;
            }

            public Version Version => new(0, 0);
            public int Migration => 0;
            public DatabaseType DatabaseType => DatabaseType.SQLite;
            public void Vacuum()
            {
            }
        }

        private sealed class StubMediaFileService : IMediaFileService
        {
            private readonly Dictionary<int, List<BookFile>> _filesByBookId;

            public StubMediaFileService(Dictionary<int, List<BookFile>> filesByBookId)
            {
                _filesByBookId = filesByBookId ?? new Dictionary<int, List<BookFile>>();
            }

            public List<BookFile> GetFilesByBook(int bookId)
            {
                return _filesByBookId.TryGetValue(bookId, out var files) ? files : new List<BookFile>();
            }

            public BookFile Add(BookFile bookFile) => throw new NotImplementedException();
            public void AddMany(List<BookFile> bookFiles) => throw new NotImplementedException();
            public void Update(BookFile bookFile) => throw new NotImplementedException();
            public void Update(List<BookFile> bookFiles) => throw new NotImplementedException();
            public void Delete(BookFile bookFile, DeleteMediaFileReason reason) => throw new NotImplementedException();
            public void DeleteMany(List<BookFile> bookFiles, DeleteMediaFileReason reason) => throw new NotImplementedException();
            public List<IFileInfo> FilterUnchangedFiles(List<IFileInfo> files, FilterFilesType filter) => files;
            public List<BookFile> GetFilesByAuthor(int authorId) => throw new NotImplementedException();
            public List<BookFile> GetFilesByBooks(List<int> bookIds) => throw new NotImplementedException();
            public List<BookFile> GetFilesByEdition(int editionId) => throw new NotImplementedException();
            public List<BookFile> GetUnmappedFiles() => throw new NotImplementedException();
            public BookFile Get(int id) => throw new NotImplementedException();
            public List<BookFile> Get(IEnumerable<int> ids) => throw new NotImplementedException();
            public List<BookFile> GetFilesWithBasePath(string path) => throw new NotImplementedException();
            public List<BookFile> GetFilesWithBasePath(string path, string mediaType) => throw new NotImplementedException();
            public List<BookFile> GetFileWithPath(List<string> path) => throw new NotImplementedException();
            public BookFile GetFileWithPath(string path) => throw new NotImplementedException();
            public void UpdateMediaInfo(List<BookFile> bookFiles) => throw new NotImplementedException();
            public List<BookFile> GetFilesByAuthorAndMediaType(int authorId, string mediaType) => throw new NotImplementedException();
        }

        private sealed class InMemoryBookService : IBookService
        {
            private readonly Dictionary<int, Book> _booksById = new Dictionary<int, Book>();
            private readonly Dictionary<string, int> _idBySlug = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            private int _nextId = 1000;

            public InMemoryBookService(IEnumerable<Book> seed)
            {
                foreach (var b in seed ?? Array.Empty<Book>())
                {
                    if (b == null) continue;
                    _booksById[b.Id] = b;
                    if (!string.IsNullOrWhiteSpace(b.TitleSlug))
                    {
                        _idBySlug[b.TitleSlug] = b.Id;
                    }
                }
            }

            public Book GetBook(int bookId) => _booksById.TryGetValue(bookId, out var b) ? b : null;

            public void InsertMany(List<Book> books)
            {
                foreach (var b in books ?? new List<Book>())
                {
                    if (b == null) continue;
                    if (b.Id <= 0) b.Id = _nextId++;
                    _booksById[b.Id] = b;
                    if (!string.IsNullOrWhiteSpace(b.TitleSlug))
                    {
                        _idBySlug[b.TitleSlug] = b.Id;
                    }
                }
            }

            public Book FindBySlug(string titleSlug)
            {
                if (string.IsNullOrWhiteSpace(titleSlug)) return null;
                return _idBySlug.TryGetValue(titleSlug, out var id) ? GetBook(id) : null;
            }

            public Book FindByTitle(int authorId, string title)
            {
                if (string.IsNullOrWhiteSpace(title)) return null;
                return _booksById.Values.FirstOrDefault(b =>
                    b != null &&
                    b.AuthorId == authorId &&
                    string.Equals(b.Title, title, StringComparison.OrdinalIgnoreCase));
            }

            public List<Book> GetBooks(IEnumerable<int> bookIds) => throw new NotImplementedException();
            public List<Book> GetBooksByAuthor(int authorId)
            {
                return _booksById.Values
                    .Where(b => b != null && b.AuthorId == authorId)
                    .ToList();
            }
            public List<Book> GetNextBooksByAuthorId(IEnumerable<int> authorIds) => throw new NotImplementedException();
            public List<Book> GetLastBooksByAuthorId(IEnumerable<int> authorIds) => throw new NotImplementedException();
            public List<Book> GetBooksByAuthorId(int authorId) => throw new NotImplementedException();
            public List<Book> GetBooksForRefresh(int authorId, List<string> foreignIds) => throw new NotImplementedException();
            public List<Book> GetBooksByFileIds(IEnumerable<int> fileIds) => throw new NotImplementedException();
            public Book AddBook(Book newBook, bool doRefresh = true) => throw new NotImplementedException();
            public Book FindByTitleInexact(int authorId, string title) => throw new NotImplementedException();
            public Book FindByGoodreadsId(string goodreadsId) => throw new NotImplementedException();
            public Book FindByProviderId(string provider, string providerId) => throw new NotImplementedException();
            public Book FindByProviderId(string provider, string providerId, BookMediaType mediaType) => throw new NotImplementedException();
            public List<Book> FindAllByProviderId(string provider, string providerId, BookMediaType mediaType) => new List<Book>();
            public Book FindByISBN(string isbn) => throw new NotImplementedException();
            public Book FindByASIN(string asin) => throw new NotImplementedException();
            public List<Book> GetCandidates(int authorId, string title) => throw new NotImplementedException();
            public void DeleteBook(int bookId, bool deleteFiles, bool addImportListExclusion = false, bool applyToBothFormats = false) => throw new NotImplementedException();
            public List<Book> GetAllBooks() => throw new NotImplementedException();
            public Book UpdateBook(Book book) => throw new NotImplementedException();
            public void SetBookMonitored(int bookId, bool monitored) => throw new NotImplementedException();
            public void SetMonitored(IEnumerable<int> ids, bool monitored) => throw new NotImplementedException();
            public void SetMonitoredForMediaType(IEnumerable<int> ids, string mediaType, bool monitored) => throw new NotImplementedException();
            public void UpdateLastSearchTime(List<Book> books) => throw new NotImplementedException();
            public NzbDrone.Core.Datastore.PagingSpec<Book> BooksWithoutFiles(NzbDrone.Core.Datastore.PagingSpec<Book> pagingSpec) => throw new NotImplementedException();
            public List<Book> BooksBetweenDates(DateTime start, DateTime end, bool includeUnmonitored) => throw new NotImplementedException();
            public List<Book> AuthorBooksBetweenDates(Author author, DateTime start, DateTime end, bool includeUnmonitored) => throw new NotImplementedException();
            public void InsertMany(List<Book> books, System.Data.IDbConnection connection, System.Data.IDbTransaction transaction)
            {
                InsertMany(books);
            }
            public void UpdateMany(List<Book> books) => throw new NotImplementedException();
            public void DeleteMany(List<Book> books) => throw new NotImplementedException();
            public void SetAddOptions(IEnumerable<Book> books) => throw new NotImplementedException();
            public List<Book> GetAuthorBooksWithFiles(Author author) => throw new NotImplementedException();
            public List<Book> GetBooksForDisplay(int? authorId = null, string mediaType = null) => throw new NotImplementedException();
            public List<Book> GetBooksByBaseId(string baseBookId) => throw new NotImplementedException();
            public Book AddWantedEdition(int bookId, int editionId, bool asNewVariant = false) => throw new NotImplementedException();
            public bool ShouldSearchForMediaType(Book book, string mediaType) => throw new NotImplementedException();
            public List<Book> GetMonitoredBooksForAuthor(int authorId, string mediaType) => throw new NotImplementedException();
            public BookBucketResource GetBookBuckets(string sortKey, string sortDirection, bool includeUnmonitored = false, string mediaType = null, bool? downloaded = null) => throw new NotImplementedException();
            public PagedBookResource GetBooksPaged(int offset, int pageSize, string sortKey, string sortDirection, bool includeUnmonitored = false, string mediaType = null, bool? downloaded = null) => throw new NotImplementedException();
        }

        private sealed class InMemoryEditionService : IEditionService
        {
            public List<Edition> GetMonitoredEditionsForBookList(BookMediaType? mediaType) => throw new NotImplementedException();
            private readonly Dictionary<int, Edition> _editionsById = new Dictionary<int, Edition>();
            private readonly Dictionary<int, List<Edition>> _editionsByBookId = new Dictionary<int, List<Edition>>();
            private int _nextId = 2000;

            public InMemoryEditionService(IEnumerable<Edition> seed)
            {
                foreach (var e in seed ?? Array.Empty<Edition>())
                {
                    if (e == null) continue;
                    _editionsById[e.Id] = e;
                    if (!_editionsByBookId.TryGetValue(e.BookId, out var list))
                    {
                        list = new List<Edition>();
                        _editionsByBookId[e.BookId] = list;
                    }
                    list.Add(e);
                }
            }

            public Edition GetEdition(int id) => _editionsById.TryGetValue(id, out var e) ? e : null;

            public List<Edition> GetEditionsByBook(int bookId)
            {
                return _editionsByBookId.TryGetValue(bookId, out var list) ? list.ToList() : new List<Edition>();
            }

            public void InsertMany(List<Edition> editions)
            {
                foreach (var e in editions ?? new List<Edition>())
                {
                    if (e == null) continue;
                    if (e.Id <= 0) e.Id = _nextId++;
                    _editionsById[e.Id] = e;
                    if (!_editionsByBookId.TryGetValue(e.BookId, out var list))
                    {
                        list = new List<Edition>();
                        _editionsByBookId[e.BookId] = list;
                    }
                    list.Add(e);
                }
            }

            public List<Edition> GetEditions(IEnumerable<int> ids) => throw new NotImplementedException();
            public Edition GetEditionByForeignEditionId(string foreignEditionId) => throw new NotImplementedException();
            public Edition GetEditionByHardcoverEditionId(string hardcoverEditionId) => throw new NotImplementedException();
            public Edition GetEditionByGoodreadsEditionId(long goodreadsEditionId) => throw new NotImplementedException();
            public Edition GetEditionByGoogleBooksEditionId(string googleBooksEditionId) => throw new NotImplementedException();
            public Edition GetEditionByOpenLibraryEditionId(string openLibraryEditionId) => throw new NotImplementedException();
            public Edition GetEditionByProviderAndId(string providerPrefix, string providerId) => throw new NotImplementedException();
            public System.Collections.Generic.List<Edition> GetEditionsByProviderAndId(string providerPrefix, string providerId) => new System.Collections.Generic.List<Edition>();
            public List<Edition> GetAllMonitoredEditions() => throw new NotImplementedException();
            public void InsertMany(List<Edition> editions, System.Data.IDbConnection connection, System.Data.IDbTransaction transaction)
            {
                InsertMany(editions);
            }
            public void UpdateMany(List<Edition> editions) => throw new NotImplementedException();
            public void DeleteMany(List<Edition> editions) => throw new NotImplementedException();
            public List<Edition> GetEditionsForRefresh(int bookId) => throw new NotImplementedException();
            public List<Edition> GetEditionsByBook(IEnumerable<int> bookIds) => throw new NotImplementedException();
            public List<Edition> GetEditionsByAuthor(int authorId) => throw new NotImplementedException();
            public Edition FindByTitle(int authorId, string title) => throw new NotImplementedException();
            public Edition FindByTitleInexact(int authorId, string title) => throw new NotImplementedException();
            public List<Edition> GetCandidates(int authorId, string title) => throw new NotImplementedException();
            public List<Edition> SetMonitored(Edition edition, bool isManualSelection = false) => throw new NotImplementedException();
        }

        private static Book CreateRow(int id, string titleSlug, string languageCode, string languageName)
        {
            return new Book
            {
                Id = id,
                AuthorId = 55,
                Title = "Test Book",
                TitleSlug = titleSlug,
                BaseBookId = "hc:4242",
                HardcoverBookId = "hc:4242",
                LanguageCode = languageCode,
                LanguageName = languageName,
                MediaType = BookMediaType.Audiobook,
                AddOptions = new AddBookOptions { AddType = BookAddType.Manual }
            };
        }

        private static Edition CreateEdition(int id, Book book, string language)
        {
            return new Edition
            {
                Id = id,
                BookId = book.Id,
                Title = book.Title,
                TitleSlug = "test-book-us",
                ForeignEditionId = "gr:ed:test-book-us",
                Language = language
            };
        }

        [Test]
        public void BookUnitDestinationServiceLanguageGuard_NoCrossLanguageAdoption()
        {
            var logger = LogManager.GetCurrentClassLogger();

            var canonicalBook = CreateRow(2100, "test-book", "eng", "English");
            var germanRow = CreateRow(2101, "test-book_wanted_audiobook_deu_77", "deu", "German");

            var canonicalEdition = CreateEdition(6000, canonicalBook, "eng");
            var germanEdition = CreateEdition(6001, germanRow, "deu");

            var canonicalFile = new BookFile
            {
                EditionId = canonicalEdition.Id,
                Path = "/audiobooks/Author/Test Book/Test Book.m4b"
            };

            var mediaFileService = new StubMediaFileService(new Dictionary<int, List<BookFile>>
            {
                [canonicalBook.Id] = new List<BookFile> { canonicalFile }
            });

            var bookService = new InMemoryBookService(new[] { canonicalBook, germanRow });
            var editionService = new InMemoryEditionService(new[] { canonicalEdition, germanEdition });

            var unitDestination = new BookUnitDestinationService(
                mediaFileService: mediaFileService,
                bookService: bookService,
                editionService: editionService,
                mainDatabase: new StubMainDatabase(),
                diskProvider: System.Reflection.DispatchProxy.Create<NzbDrone.Common.Disk.IDiskProvider, NullDiskProvider>(),
                logger: logger);

            var unitKey = unitDestination.BuildRootUnitKeyWithExtension(
                "/audiobooks/Author/Test Book German/Test Book.m4b",
                "Test Book German",
                canonicalBook.MediaType);

            var destination = unitDestination.ResolveDestinationForUnit(canonicalBook, canonicalEdition, unitKey);

            Assert.That(destination.Item1, Is.Not.EqualTo(germanRow.Id), "an English import unit must not adopt the German variant row");
            Assert.That(string.IsNullOrWhiteSpace(germanRow.UnitKeyHash), Is.True, "the German variant row must not be stamped with the unit hash");
        }

        [Test]
        public void BookUnitDestinationServiceLanguageGuard_SameLanguageEmptyRowStillAdopted()
        {
            var logger = LogManager.GetCurrentClassLogger();

            var canonicalBook = CreateRow(2100, "test-book", "eng", "English");
            var englishVariantRow = CreateRow(2102, "test-book_wanted_audiobook_eng_77", "eng", "English");

            var canonicalEdition = CreateEdition(6000, canonicalBook, "eng");
            var englishVariantEdition = CreateEdition(6002, englishVariantRow, "eng");

            var canonicalFile = new BookFile
            {
                EditionId = canonicalEdition.Id,
                Path = "/audiobooks/Author/Test Book/Test Book.m4b"
            };

            var mediaFileService = new StubMediaFileService(new Dictionary<int, List<BookFile>>
            {
                [canonicalBook.Id] = new List<BookFile> { canonicalFile }
            });

            var bookService = new InMemoryBookService(new[] { canonicalBook, englishVariantRow });
            var editionService = new InMemoryEditionService(new[] { canonicalEdition, englishVariantEdition });

            var unitDestination = new BookUnitDestinationService(
                mediaFileService: mediaFileService,
                bookService: bookService,
                editionService: editionService,
                mainDatabase: new StubMainDatabase(),
                diskProvider: System.Reflection.DispatchProxy.Create<NzbDrone.Common.Disk.IDiskProvider, NullDiskProvider>(),
                logger: logger);

            var unitKey = unitDestination.BuildRootUnitKeyWithExtension(
                "/audiobooks/Author/Test Book English/Test Book.m4b",
                "Test Book English",
                canonicalBook.MediaType);

            var destination = unitDestination.ResolveDestinationForUnit(canonicalBook, canonicalEdition, unitKey);

            Assert.That(destination.Item1, Is.EqualTo(englishVariantRow.Id), "a same-language empty variant row must still be adoptable");
        }
    }
}
