using System;
using System.IO;
using System.Linq;
using Dapper;
using Microsoft.Data.Sqlite;
using Moq;
using NUnit.Framework;
using NLog;
using NzbDrone.Core.Datastore;
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
            Assert.That(files.All(f => f.MatchProvenance == null && !f.MatchProvenanceLoaded), Is.True);
            Assert.That(files.All(f => f.Edition.Id == 1 && f.Edition.Title == "Test Edition"
                && f.Edition.Book.Id == 1 && f.Edition.Book.Title == "Test Book"), Is.True);
            // The summaries alone would occupy about 240 MB as UTF-16 strings;
            // allow 64 MiB for the ordinary rows, lists and joined-query bookkeeping.
            Assert.That(growth, Is.LessThan(64L * 1024 * 1024));
            GC.KeepAlive(files);
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
                .Concat(_repository.GetFileWithPath(new() { "/books/1.m4b" }))
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
                _repository.SetFields(new[] { file }, f => f.MatchProvenance);
            }
            else
            {
                _repository.Update(file);
                _repository.SetFields(file, f => f.MatchProvenance);
            }

            Assert.That(_connection.QuerySingle<string>("SELECT MatchProvenance FROM BookFiles"), Is.EqualTo(Document));
        }
    }
}
