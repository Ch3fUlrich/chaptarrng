using System;
using System.IO;
using Microsoft.Data.Sqlite;
using NLog;
using NUnit.Framework;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace Chaptarr.Core.Test.Datastore
{
    [TestFixture]
    public class DatabaseLineageGuardFixture
    {
        private string _path;
        private string ConnectionString => new SqliteConnectionStringBuilder { DataSource = _path, Pooling = false }.ToString();

        [SetUp]
        public void SetUp() => _path = Path.Combine(Path.GetTempPath(), $"lineage-{Guid.NewGuid():N}.db");

        [TearDown]
        public void TearDown() => File.Delete(_path);

        [Test]
        public void should_not_create_a_database_during_preflight()
        {
            DatabaseLineageGuard.Validate(ConnectionString, DatabaseType.SQLite);
            Assert.That(File.Exists(_path), Is.False);
        }

        [Test]
        public void should_accept_an_empty_database()
        {
            Create("CREATE TABLE VersionInfo (Version INTEGER);");
            Assert.DoesNotThrow(() => DatabaseLineageGuard.Validate(ConnectionString, DatabaseType.SQLite));
        }

        [TestCase("Path TEXT")]
        [TestCase("AudiobookRootFolderPath TEXT")]
        [TestCase("EbookRootFolderPath TEXT")]
        public void should_reject_foreign_or_partial_schema_without_changing_bytes(string columns)
        {
            Create($"CREATE TABLE Authors (Id INTEGER PRIMARY KEY, {columns}); CREATE TABLE VersionInfo (Version INTEGER); INSERT INTO VersionInfo VALUES (46);");
            var before = File.ReadAllBytes(_path);
            Assert.Throws<InvalidDataException>(() => DatabaseLineageGuard.Validate(ConnectionString, DatabaseType.SQLite));
            Assert.That(File.ReadAllBytes(_path), Is.EqualTo(before));
        }

        [Test]
        public void should_reject_bookshelf_schema_before_numbered_migrations_without_changing_bytes()
        {
            Create(@"
                CREATE TABLE AuthorMetadata (Id INTEGER PRIMARY KEY, ForeignAuthorId TEXT);
                CREATE TABLE Authors (Id INTEGER PRIMARY KEY, CleanName TEXT, Path TEXT, EbookPath TEXT, AudiobookPath TEXT, AuthorMetadataId INTEGER);
                CREATE TABLE Books (Id INTEGER PRIMARY KEY, AuthorMetadataId INTEGER, ForeignBookId TEXT, Title TEXT);
                CREATE TABLE VersionInfo (Version INTEGER, AppliedOn TEXT, Description TEXT);
                INSERT INTO VersionInfo VALUES (46, '2026-10-13T00:00:00', 'BookshelfNG source schema');");

            var before = File.ReadAllBytes(_path);
            var migrationController = new MigrationController(LogManager.GetLogger(nameof(DatabaseLineageGuardFixture)), null);

            Assert.Throws<InvalidDataException>(() => migrationController.Migrate(
                ConnectionString,
                new MigrationContext(MigrationType.Main),
                DatabaseType.SQLite));

            Assert.That(File.ReadAllBytes(_path), Is.EqualTo(before));
        }

        [Test]
        public void should_accept_old_chaptarr_schema_without_new_monitoring_columns()
        {
            Create("CREATE TABLE Authors (Id INTEGER PRIMARY KEY, AudiobookRootFolderPath TEXT, EbookRootFolderPath TEXT);");
            Assert.DoesNotThrow(() => DatabaseLineageGuard.Validate(ConnectionString, DatabaseType.SQLite));
        }

        [Test]
        public void should_reject_corrupt_database_without_changing_bytes()
        {
            File.WriteAllText(_path, "not a database");
            Assert.Throws<SqliteException>(() => DatabaseLineageGuard.Validate(ConnectionString, DatabaseType.SQLite));
            Assert.That(File.ReadAllText(_path), Is.EqualTo("not a database"));
        }

        private void Create(string sql)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }
    }
}
