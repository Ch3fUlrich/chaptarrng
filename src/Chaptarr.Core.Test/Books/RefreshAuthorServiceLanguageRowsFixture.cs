using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using NLog;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.MediaFiles;

namespace Chaptarr.Core.Test.Books
{
    [TestFixture]
    public class RefreshAuthorServiceLanguageRowsFixture
    {
        private sealed class StubBookService : IBookService
        {
            public List<(int bookId, bool deleteFiles, bool addImportListExclusion, bool applyToBothFormats)> DeleteBookCalls { get; } = new();

            public void DeleteBook(int bookId, bool deleteFiles, bool addImportListExclusion = false, bool applyToBothFormats = false)
            {
                DeleteBookCalls.Add((bookId, deleteFiles, addImportListExclusion, applyToBothFormats));
            }

            public Book GetBook(int bookId) => throw new NotImplementedException();
            public List<Book> GetBooks(IEnumerable<int> bookIds) => throw new NotImplementedException();
            public List<Book> GetBooksByAuthor(int authorId) => throw new NotImplementedException();
            public List<Book> GetNextBooksByAuthorId(IEnumerable<int> authorIds) => throw new NotImplementedException();
            public List<Book> GetLastBooksByAuthorId(IEnumerable<int> authorIds) => throw new NotImplementedException();
            public List<Book> GetBooksByAuthorId(int authorId) => throw new NotImplementedException();
            public List<Book> GetBooksForRefresh(int authorId, List<string> foreignIds) => throw new NotImplementedException();
            public List<Book> GetBooksByFileIds(IEnumerable<int> fileIds) => throw new NotImplementedException();
            public Book AddBook(Book newBook, bool doRefresh = true) => throw new NotImplementedException();
            public Book FindBySlug(string titleSlug) => throw new NotImplementedException();
            public Book FindByTitle(int authorId, string title) => throw new NotImplementedException();
            public Book FindByTitleInexact(int authorId, string title) => throw new NotImplementedException();
            public Book FindByGoodreadsId(string goodreadsId) => throw new NotImplementedException();
            public Book FindByProviderId(string provider, string providerId) => throw new NotImplementedException();
            public Book FindByProviderId(string provider, string providerId, BookMediaType mediaType) => throw new NotImplementedException();
            public List<Book> FindAllByProviderId(string provider, string providerId, BookMediaType mediaType) => new List<Book>();
            public Book FindByISBN(string isbn) => throw new NotImplementedException();
            public Book FindByASIN(string asin) => throw new NotImplementedException();
            public List<Book> GetCandidates(int authorId, string title) => throw new NotImplementedException();
            public List<Book> GetAllBooks() => throw new NotImplementedException();
            public Book UpdateBook(Book book) => throw new NotImplementedException();
            public void SetBookMonitored(int bookId, bool monitored) => throw new NotImplementedException();
            public void SetMonitored(IEnumerable<int> ids, bool monitored) => throw new NotImplementedException();
            public void SetMonitoredForMediaType(IEnumerable<int> ids, string mediaType, bool monitored) => throw new NotImplementedException();
            public void UpdateLastSearchTime(List<Book> books) => throw new NotImplementedException();
            public PagingSpec<Book> BooksWithoutFiles(PagingSpec<Book> pagingSpec) => throw new NotImplementedException();
            public List<Book> BooksBetweenDates(DateTime start, DateTime end, bool includeUnmonitored) => throw new NotImplementedException();
            public List<Book> AuthorBooksBetweenDates(Author author, DateTime start, DateTime end, bool includeUnmonitored) => throw new NotImplementedException();
            public void InsertMany(List<Book> books) => throw new NotImplementedException();
            public void InsertMany(List<Book> books, System.Data.IDbConnection connection, System.Data.IDbTransaction transaction) => throw new NotImplementedException();
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

        private sealed class StubMediaFileService : IMediaFileService
        {
            private readonly Dictionary<int, List<BookFile>> _filesByBookId = new();

            public void SetFilesByBook(int bookId, params BookFile[] files)
            {
                _filesByBookId[bookId] = files?.ToList() ?? new List<BookFile>();
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

        private sealed class StubRefreshBookService : IRefreshBookService
        {
            public List<Book> RefreshCallsBooks { get; } = new();

            public bool RefreshBookInfo(Book book, List<Book> remoteBooks, Author remoteData, bool forceUpdateFileTags)
            {
                RefreshCallsBooks.Add(book);
                return false;
            }

            public bool RefreshBookInfo(List<Book> books, List<Book> remoteBooks, Author remoteData, bool forceBookRefresh, bool forceUpdateFileTags, DateTime? lastUpdate)
            {
                RefreshCallsBooks.AddRange(books);
                return false;
            }
        }

        private sealed class TestableRefreshAuthorService : RefreshAuthorService
        {
            public TestableRefreshAuthorService(IBookService bookService, IMediaFileService mediaFileService, IRefreshBookService refreshBookService, Logger logger)
                : base(authorInfo: null,
                    authorService: null,
                    bookService: bookService,
                    editionService: null,
                    metadataProfileService: null,
                    refreshBookService: refreshBookService,
                    refreshSeriesService: null,
                    eventAggregator: null,
                    commandQueueManager: null,
                    mediaFileService: mediaFileService,
                    historyService: null,
                    rootFolderService: null,
                    checkIfAuthorShouldBeRefreshed: null,
                    monitorNewBookService: null,
                    configService: null,
                    importListExclusionService: null,
                    syncMetadataService: null,
                    syncQueueService: null,
                    rootFolderSettingsResolver: null,
                    logger: logger)
            {
            }

            public Tuple<Book, List<Book>> MatchExisting(List<Book> existingChildren, Book remote)
            {
                return GetMatchingExistingChildren(existingChildren, remote);
            }

            public bool RefreshBooks(RefreshEntityServiceBase<Author, Book>.SortedChildren localChildren, List<Book> remoteChildren)
            {
                return RefreshChildren(localChildren, remoteChildren, remoteData: null, forceChildRefresh: false, forceUpdateFileTags: false, lastUpdate: null);
            }
        }

        private static Book CreateLanguageRow(int id, string titleSlug, string languageCode, string languageName)
        {
            return new Book
            {
                Id = id,
                AuthorId = 77,
                Title = "Test Book",
                TitleSlug = titleSlug,
                LanguageCode = languageCode,
                LanguageName = languageName,
                BaseBookId = "hc:4242",
                HardcoverBookId = "hc:4242",
                MediaType = BookMediaType.Audiobook,
                AddOptions = new AddBookOptions { AddType = BookAddType.Manual }
            };
        }

        private static Book CreateRemote(string languageCode, string languageName)
        {
            return new Book
            {
                LanguageCode = languageCode,
                LanguageName = languageName,
                BaseBookId = "hc:4242",
                HardcoverBookId = "hc:4242",
                MediaType = BookMediaType.Audiobook
            };
        }

        [Test]
        public void RefreshAuthorServiceLanguageRows_KeepsNLanguageRows()
        {
            var bookService = new StubBookService();
            var mediaFileService = new StubMediaFileService();
            var refreshBookService = new StubRefreshBookService();
            var service = new TestableRefreshAuthorService(bookService, mediaFileService, refreshBookService, LogManager.GetCurrentClassLogger());

            var rowEn = CreateLanguageRow(1, "test-book", "eng", "English");
            var rowDe = CreateLanguageRow(2, "test-book_wanted_audiobook_deu_77", "deu", "German");

            var remote = CreateRemote("eng", "English");

            var match = service.MatchExisting(new List<Book> { rowEn, rowDe }, remote);

            var mergeIds = match.Item2.Select(b => b.Id).ToList();
            Assert.That(mergeIds, Does.Not.Contain(rowEn.Id), "the English language row must survive the refresh");
            Assert.That(mergeIds, Does.Not.Contain(rowDe.Id), "the German language row must survive the refresh");
            Assert.That(match.Item1 == null ? -1 : match.Item1.Id, Is.Not.EqualTo(rowDe.Id), "the German wanted variant must not absorb the English remote copy");
        }

        [Test]
        public void RefreshAuthorServiceLanguageRows_ExistingChildIsFiledRow()
        {
            var bookService = new StubBookService();
            var mediaFileService = new StubMediaFileService();
            var refreshBookService = new StubRefreshBookService();
            var service = new TestableRefreshAuthorService(bookService, mediaFileService, refreshBookService, LogManager.GetCurrentClassLogger());

            var rowEn = CreateLanguageRow(1, "test-book", "eng", "English");
            var rowDe = CreateLanguageRow(2, "test-book_wanted_audiobook_deu_77", "deu", "German");

            mediaFileService.SetFilesByBook(rowDe.Id, new BookFile { Id = 11 });

            var remote = CreateRemote("deu", "German");

            var match = service.MatchExisting(new List<Book> { rowEn, rowDe }, remote);

            Assert.That(match.Item1.Id, Is.EqualTo(rowDe.Id), "the filed German row is the refresh target");
            Assert.That(match.Item2.Select(b => b.Id).ToList(), Does.Not.Contain(rowEn.Id), "the fileless English row must not be merged away");
        }
    }
}
