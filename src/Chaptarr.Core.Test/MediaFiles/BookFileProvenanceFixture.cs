using System;
using System.IO;
using System.Linq;
using Dapper;
using Microsoft.Data.Sqlite;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Datastore;
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
            _connection = new SqliteConnection(connectionString);
            _connection.Open();

            // This checkout has no DbTest base. Use the same real SQLite/Database wrapper
            // as the other MediaFileRepository fixtures, with all mapped columns present.
            foreach (var type in new[] { typeof(BookFile), typeof(NzbDrone.Core.Books.Edition), typeof(NzbDrone.Core.Books.Book), typeof(NzbDrone.Core.Books.Author) })
            {
                var excluded = TableMapping.Mapper.ExcludeProperties(type).Select(p => p.Name).ToHashSet();
                var columns = type.GetProperties().Where(p => p.IsMappableProperty() && !excluded.Contains(p.Name))
                    .Select(p => p.Name == "Id" ? "\"Id\" INTEGER PRIMARY KEY" : $"\"{p.Name}\" {(p.PropertyType == typeof(string) ? "TEXT" : "BLOB")}");
                _connection.Execute($"CREATE TABLE \"{TableMapping.Mapper.TableNameMapping(type)}\" ({string.Join(", ", columns)})");
            }

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
            using var transaction = _connection.BeginTransaction();
            for (var id = 1; id <= count; id++)
            {
                _connection.Execute("INSERT INTO \"BookFiles\" (\"Id\", \"EditionId\", \"Path\", \"MatchProvenance\") VALUES (@id, 0, @path, @document)",
                    new { id, path = $"/books/{id}.m4b", document = Document }, transaction);
            }

            transaction.Commit();
        }

        [Test]
        public void bulk_list_should_not_materialize_large_provenance_documents()
        {
            Seed(20000);
            _repository.All().Take(1).ToList(); // Warm Dapper mappings before measuring.
            var before = GC.GetTotalMemory(true);
            var files = _repository.All().ToList();
            var growth = GC.GetTotalMemory(true) - before;
            TestContext.WriteLine($"Managed retained growth for {files.Count} rows: {growth:N0} bytes");
            Assert.That(files.Count, Is.EqualTo(20000));
            Assert.That(files.All(f => f.MatchProvenance == null && !f.MatchProvenanceLoaded), Is.True);
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
            var file = _repository.All().Single();
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
            var file = _repository.All().Single();
            file.MatchProvenance = new MatchProvenance { Summary = "replacement" };
            _repository.Update(file);
            Assert.That(_repository.Get(1).MatchProvenance.Summary, Is.EqualTo("replacement"));
        }

        [Test]
        public void explicitly_cleared_provenance_should_be_saved()
        {
            Seed(1);
            var file = _repository.Get(1);
            file.MatchProvenance = null;
            _repository.Update(file);
            Assert.That(_repository.Get(1).MatchProvenance, Is.Null);
        }
    }
}
