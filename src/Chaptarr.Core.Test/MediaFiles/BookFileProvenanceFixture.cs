using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using Dapper;
using Chaptarr.Api.V1.Books;
using Microsoft.Data.Sqlite;
using Moq;
using NUnit.Framework;
using NLog;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Books;
using NzbDrone.Core.Datastore.Migration.Framework;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Messaging.Events;

namespace Chaptarr.Core.Test.MediaFiles
{
    [TestFixture]
    public class BookFileProvenanceFixture
    {
        private string _path;
        private SqliteConnection _connection;
        private MediaFileRepository _repository;
        private BasicRepository<Edition> _editions;
        private const string DocumentPrefix = "{\"summary\":\"";
        private static readonly string Document = DocumentPrefix + new string('x', 6000) + "\"}";

        [SetUp]
        public void SetUp()
        {
            if (TableMapping.Mapper.TableMap.Count == 0)
            {
                TableMapping.Map();
            }

            _path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"provenance_{Guid.NewGuid():N}.db");
            var connectionString = new SqliteConnectionStringBuilder { DataSource = _path, Pooling = false }.ToString();
            new MigrationController(LogManager.GetLogger(nameof(BookFileProvenanceFixture)), null)
                .Migrate(connectionString, new MigrationContext(MigrationType.Main), DatabaseType.SQLite);
            _connection = new SqliteConnection(connectionString);
            _connection.Open();

            var database = new Database("main", () =>
            {
                var connection = new SqliteConnection(connectionString);
                connection.Open();
                return connection;
            });
            _repository = new MediaFileRepository(new MainDatabase(database), Mock.Of<IEventAggregator>());
            _editions = new BasicRepository<Edition>(new MainDatabase(database), Mock.Of<IEventAggregator>());
        }

        [TearDown]
        public void TearDown()
        {
            _connection?.Dispose();
            if (_path != null && File.Exists(_path))
            {
                File.Delete(_path);
            }
        }

        private void Seed(int count)
        {
            _connection.Execute("INSERT INTO Authors (Id, Name, Path, Monitored, Added, CleanName) VALUES (1, 'Test Author', '/books', 1, '2026-01-01', 'testauthor')");
            _connection.Execute("INSERT INTO Books (Id, AuthorId, Title, CleanTitle, AnyEditionOk, Added) VALUES (1, 1, 'Test Book', 'testbook', 1, '2026-01-01')");
            _connection.Execute("INSERT INTO Editions (Id, BookId, Title, Monitored) VALUES (1, 1, 'Test Edition', 1)");
            using var transaction = _connection.BeginTransaction();
            for (var id = 1; id <= count; id++)
            {
                _connection.Execute("INSERT INTO BookFiles (Id, EditionId, Path, MatchProvenance, Size, Modified, DateAdded, Quality, MediaInfo, CalibreId, Part) VALUES (@id, 1, @path, @document, 123, '2026-01-01', '2026-01-01', '{}', '{}', 0, 1)",
                    new { id, path = $"/books/{id}.m4b", document = Document }, transaction);
            }

            transaction.Commit();
        }

        [Test]
        public void bulk_list_should_not_materialize_large_provenance_documents()
        {
            Seed(20000);
            _repository.GetFilesByBooksForDisplay(new() { 1 }); // Warm Dapper mappings before measuring.
            var before = GC.GetTotalMemory(true);
            var files = _repository.GetFilesByBooksForDisplay(new() { 1 });
            var growth = GC.GetTotalMemory(true) - before;
            TestContext.WriteLine($"Managed retained growth for {files.Count} rows: {growth:N0} bytes");
            Assert.That(files.Count, Is.EqualTo(20000));
            Assert.That(files.All(f => f.MatchProvenance == null), Is.True, "MatchProvenance omitted");
            Assert.That(files.All(f => !f.MatchProvenanceLoaded), Is.True, "MatchProvenance not loaded");
            Assert.That(files.All(f => f.Edition.Id == 1), Is.True, "Edition.Id retained");
            Assert.That(files.All(f => f.Edition.BookId == 1), Is.True, "Edition.BookId retained");
            Assert.That(files.All(f => f.Edition.Title == null), Is.True, "Edition.Title omitted");
            Assert.That(files.All(f => f.Edition.LazyBook?.IsLoaded != true), Is.True, "Edition.Book not loaded (do not invoke lazy getter)");
            Assert.That(files.All(f => ReferenceEquals(f.Edition, files[0].Edition)), Is.True, "Edition key model shared");
            // The summaries alone would occupy about 240 MB as UTF-16 strings;
            // allow 128 MiB for the ordinary rows, lists and joined-query bookkeeping.
            Assert.That(growth, Is.LessThan(128L * 1024 * 1024));
            GC.KeepAlive(files);
        }

        [Test]
        public void production_shaped_display_should_share_keys_and_omit_all_documents()
        {
            Seed(20000);
            var chapters = "[" + string.Join(",", Enumerable.Range(1, 400)
                .Select(i => "{\"title\":\"" + new string('c', 150) + "\",\"start\":0}")) + "]";
            var filler = new string('o', 4000);
            using (var transaction = _connection.BeginTransaction())
            {
                for (var id = 2; id <= 100; id++)
                {
                    _connection.Execute("INSERT INTO Editions (Id, BookId, Title, Monitored) VALUES (@id, 1, 'Part edition', 1)", new { id }, transaction);
                }

                _connection.Execute("UPDATE Editions SET Chapters = @chapters, Overview = @filler, Images = @images, Links = @links",
                    new { chapters, filler, images = "[{\"url\":\"" + filler + "\"}]", links = "[{\"url\":\"" + filler + "\"}]" }, transaction);
                _connection.Execute("UPDATE Books SET Overview = @filler", new { filler }, transaction);
                _connection.Execute("UPDATE BookFiles SET EditionId = ((Id - 1) / 200) + 1, AllTags = @tags",
                    new { tags = "{\"comment\":[\"" + new string('t', 1000) + "\"]}" }, transaction);
                transaction.Commit();
            }

            _repository.GetFilesByBooksForDisplay(new() { 1 });
            var before = GC.GetTotalMemory(true);
            var files = _repository.GetFilesByBooksForDisplay(new() { 1 });
            var growth = GC.GetTotalMemory(true) - before;
            TestContext.WriteLine($"Production-shaped managed retained growth: {growth:N0} bytes");
            Assert.That(files.Count, Is.EqualTo(20000));
            Assert.That(files.All(f => f.Id > 0), Is.True, "Id retained");
            Assert.That(files.All(f => f.EditionId > 0), Is.True, "EditionId retained");
            Assert.That(files.All(f => f.Edition.Id == f.EditionId), Is.True, "Edition key matches file");
            Assert.That(files.All(f => f.Edition.BookId == 1), Is.True, "Edition.BookId retained");
            Assert.That(files.All(f => f.Path.EndsWith(".m4b")), Is.True, "Path retained");
            Assert.That(files.All(f => f.Size == 123), Is.True, "Size retained");
            Assert.That(files.All(f => f.MatchProvenance == null), Is.True, "MatchProvenance omitted");
            Assert.That(files.All(f => !f.MatchProvenanceLoaded), Is.True, "MatchProvenance not loaded");
            Assert.That(files.All(f => f.AllTags == null), Is.True, "AllTags omitted");
            Assert.That(files.All(f => f.MediaInfo == null), Is.True, "MediaInfo omitted");
            Assert.That(files.All(f => f.Quality == null), Is.True, "Quality omitted");
            Assert.That(files.All(f => !f.Edition.Chapters.Any()), Is.True, "Edition.Chapters empty default");
            Assert.That(files.All(f => !f.Edition.Images.Any()), Is.True, "Edition.Images empty default");
            Assert.That(files.All(f => !f.Edition.Links.Any()), Is.True, "Edition.Links empty default");
            Assert.That(files.All(f => f.Edition.Overview == string.Empty), Is.True, "Edition.Overview constructor default, not stored document");
            Assert.That(files.All(f => f.Edition.Title == null), Is.True, "Edition.Title omitted");
            Assert.That(files.All(f => f.Edition.LazyBook?.IsLoaded != true), Is.True, "Edition.Book not loaded (do not invoke lazy getter)");
            Assert.That(files.GroupBy(f => f.EditionId).All(g => g.All(f => ReferenceEquals(f.Edition, g.First().Edition))), Is.True, "Edition key instances shared by reference");
            Assert.That(files.Select(f => f.Edition).Distinct().Count(), Is.EqualTo(100));
            // Chapters alone: 20,000 * 400 * 150 * 2 UTF-16 bytes = 2.4 GB,
            // excluding object overhead and other edition documents. 128 MiB allows
            // ordinary file objects but cannot retain a full edition per file row.
            Assert.That(growth, Is.LessThan(128L * 1024 * 1024));
            GC.KeepAlive(files);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void display_updates_should_preserve_every_omitted_column(bool batch)
        {
            Seed(1);
            _connection.Execute("UPDATE BookFiles SET AllTags = '{\"tag\":[\"keep\"]}', Narrator = 'keep', DurationSeconds = 123");
            var before = _connection.QuerySingle("SELECT * FROM BookFiles");
            var file = _repository.GetFilesByBooksForDisplay(new() { 1 }).Single();
            file.AllTags = null;
            file.Narrator = null;
            file.MediaType = null;
            file.Path = "/books/new.m4b";
            if (batch)
            {
                _repository.UpdateMany(new[] { file });
                Assert.Throws<InvalidOperationException>(() => _repository.SetFields(new[] { file }, f => f.AllTags, f => f.Narrator, f => f.MediaType));
            }
            else
            {
                _repository.Update(file);
                Assert.Throws<InvalidOperationException>(() => _repository.SetFields(file, f => f.AllTags, f => f.Narrator, f => f.MediaType));
            }

            var after = (IDictionary<string, object>)_connection.QuerySingle("SELECT * FROM BookFiles");
            Assert.That(after["Path"], Is.EqualTo(file.Path));
            foreach (var column in (IDictionary<string, object>)before)
            {
                if (column.Key != "Path")
                {
                    Assert.That(after[column.Key], Is.EqualTo(column.Value), column.Key);
                }
            }
        }

        [Test]
        public void display_columns_should_round_trip_stored_values()
        {
            Seed(1);
            _connection.Execute("UPDATE BookFiles SET Id = 27, Path = '/books/measured.mp3', Size = 987654321, DurationSeconds = 3723, MediaType = 'ebook'");
            var file = _repository.GetFilesByBooksForDisplay(new() { 1 }).Single();
            Assert.Multiple(() =>
            {
                Assert.That(file.Id, Is.EqualTo(27));
                Assert.That(file.EditionId, Is.EqualTo(1));
                Assert.That(file.Path, Is.EqualTo("/books/measured.mp3"));
                Assert.That(file.Size, Is.EqualTo(987654321));
                Assert.That(file.DurationSeconds, Is.EqualTo(3723));
                Assert.That(file.Edition.Id, Is.EqualTo(1));
                Assert.That(file.Edition.BookId, Is.EqualTo(1));
                Assert.That(file.MediaType, Is.EqualTo("audiobook"), "Omitted column retains constructor default, not stored value");
            });
        }

        [TestCase("insert")]
        [TestCase("update")]
        [TestCase("updateMany")]
        [TestCase("upsert")]
        [TestCase("delete")]
        [TestCase("deleteMany")]
        [TestCase("setFields")]
        [TestCase("setFieldsMany")]
        public void display_edition_writes_should_throw_and_preserve_stored_row(string operation)
        {
            Seed(1);
            var before = (IDictionary<string, object>)_connection.QuerySingle("SELECT * FROM Editions");
            var edition = _repository.GetFilesByBooksForDisplay(new() { 1 }).Single().Edition;
            var exception = Assert.Throws<InvalidOperationException>(() =>
            {
                switch (operation)
                {
                    case "insert": edition.Id = 0; _editions.Insert(edition); break;
                    case "update": _editions.Update(edition); break;
                    case "updateMany": _editions.UpdateMany(new[] { edition }); break;
                    case "upsert": _editions.Upsert(edition); break;
                    case "delete": _editions.Delete(edition); break;
                    case "deleteMany": _editions.DeleteMany(new List<Edition> { edition }); break;
                    case "setFields": _editions.SetFields(edition, e => e.Title); break;
                    case "setFieldsMany": _editions.SetFields(new[] { edition }, e => e.Title); break;
                }
            });
            Assert.That(exception.Message, Does.Contain("Edition display projection"));
            var after = (IDictionary<string, object>)_connection.QuerySingle("SELECT * FROM Editions");
            foreach (var column in before)
            {
                Assert.That(after[column.Key], Is.EqualTo(column.Value), column.Key);
            }
        }

        [TestCase("audiobook", "epub", true, false)]
        [TestCase("ebook", "m4b", false, true)]
        public void repository_display_files_should_map_real_availability(string mediaType, string extension, bool canRead, bool canListen)
        {
            Seed(1);
            _connection.Execute("UPDATE BookFiles SET MediaType = @mediaType, Path = @path",
                new { mediaType, path = "/books/book." + extension });
            var files = _repository.GetFilesByBooksForDisplay(new() { 1 });
            Assert.That(files.Single().MediaType, Is.EqualTo("audiobook"), "Display row retains the omitted column's constructor default");
            var filesByBookId = files.GroupBy(f => f.Edition.BookId).ToDictionary(g => g.Key, g => g.ToList());
            var book = new Book
            {
                Id = 1,
                MediaType = canRead ? BookMediaType.Ebook : BookMediaType.Audiobook,
                Editions = new List<Edition> { new Edition { Id = 1, BookId = 1, Monitored = true } },
                BookFiles = filesByBookId[1]
            };
            var resource = book.ToResource(BookResourceMappingOptions.Lean());
            Assert.That(resource.Availability, Is.Not.Null);
            Assert.That(resource.Availability.CanRead, Is.EqualTo(canRead));
            Assert.That(resource.Availability.CanListen, Is.EqualTo(canListen));
            Assert.That(resource.Availability.FileFormats, Is.EqualTo(new[] { extension }));
        }

        [Test]
        public void single_file_should_load_provenance()
        {
            Seed(1);
            var file = _repository.Get(1);
            Assert.That(file.MatchProvenanceLoaded, Is.True);
            Assert.That(file.MatchProvenance.Summary, Is.EqualTo(new string('x', 6000)));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void updating_partial_row_should_preserve_stored_provenance(bool updateMany)
        {
            Seed(1);
            var file = _repository.GetFilesByBooksForDisplay(new() { 1 }).Single();
            Assert.That(file.MatchProvenanceLoaded, Is.False);
            file.Path = "/books/renamed.m4b";
            if (updateMany)
            {
                _repository.UpdateMany(new[] { file });
            }
            else
            {
                _repository.Update(file);
            }

            Assert.That(_connection.QuerySingle<string>("SELECT \"MatchProvenance\" FROM \"BookFiles\""), Is.EqualTo(Document));
            Assert.That(_repository.Get(1).Path, Is.EqualTo(file.Path));
        }

        [Test]
        public void explicitly_assigned_provenance_should_be_saved_on_partial_row()
        {
            Seed(1);
            var file = _repository.GetFilesByBooksForDisplay(new() { 1 }).Single();
            file.MatchProvenance = new MatchProvenance { Summary = "replacement" };
            _repository.Update(file);
            Assert.That(_repository.Get(1).MatchProvenance.Summary, Is.EqualTo("replacement"));
        }

        [Test]
        public void explicitly_cleared_provenance_should_be_saved()
        {
            Seed(1);
            var file = _repository.Get(1);
            file.ClearMatchProvenance();
            _repository.Update(file);
            Assert.That(_repository.Get(1).MatchProvenance, Is.Null);
        }

        [Test]
        public void default_queries_should_return_provenance_and_joined_values()
        {
            Seed(1);
            var files = _repository.GetFilesByBook(1)
                .Concat(_repository.Get(new[] { 1 }))
                .Concat(_repository.GetFileWithPath(new List<string> { "/books/1.m4b" }))
                .Concat(_repository.All())
                .Concat(_repository.GetFilesByBooks(new() { 1 }))
                .Append(_repository.Find(1))
                .Append(_repository.GetFileWithPath("/books/1.m4b"));
            foreach (var file in files)
            {
                Assert.That(file.MatchProvenance.Summary, Is.EqualTo(new string('x', 6000)));
                Assert.That(file.Edition.Title, Is.EqualTo("Test Edition"));
            }

            Assert.That(_repository.GetFilesByBook(1).Single().Author.Name, Is.EqualTo("Test Author"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void null_assignment_on_display_row_should_preserve_provenance(bool batch)
        {
            Seed(1);
            var file = _repository.GetFilesByBooksForDisplay(new() { 1 }).Single();
            file.MatchProvenance = null;
            Assert.That(file.MatchProvenanceLoaded, Is.False);
            if (batch)
            {
                _repository.UpdateMany(new[] { file });
                Assert.Throws<InvalidOperationException>(() => _repository.SetFields(new[] { file }, f => f.MatchProvenance));
            }
            else
            {
                _repository.Update(file);
                Assert.Throws<InvalidOperationException>(() => _repository.SetFields(file, f => f.MatchProvenance));
            }

            Assert.That(_connection.QuerySingle<string>("SELECT MatchProvenance FROM BookFiles"), Is.EqualTo(Document));
        }
    }
}
