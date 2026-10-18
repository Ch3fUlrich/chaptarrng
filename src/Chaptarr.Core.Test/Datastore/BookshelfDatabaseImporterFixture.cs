using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Data.Sqlite;
using NLog;
using NUnit.Framework;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace Chaptarr.Core.Test.Datastore
{
    [TestFixture]
    public class BookshelfDatabaseImporterFixture
    {
        private string _directory;
        private string _sourcePath;
        private string _sourceConnectionString;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), $"bookshelf-importer-{Guid.NewGuid().ToString("N").Substring(0, 8)}");
            Directory.CreateDirectory(_directory);
            _sourcePath = Path.Combine(_directory, "readarr.db");
            _sourceConnectionString = new SqliteConnectionStringBuilder
            {
                DataSource = _sourcePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Pooling = false
            }.ConnectionString;

            CreateSourceDatabase();
        }

        [TearDown]
        public void TearDown()
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
        }

        [Test]
        public void should_convert_a_populated_v158_bookshelf_database_and_preserve_media_relationships()
        {
            new MigrationController(LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null).Migrate(
                _sourceConnectionString,
                new MigrationContext(MigrationType.Main),
                DatabaseType.SQLite);
            var backupPath = Directory.GetFiles(_directory, "readarr.db.bookshelfng-backup-*.db").Single();

            Assert.Multiple(() =>
            {
                Assert.That(File.Exists(backupPath), Is.True);
                Assert.That(DatabaseLineageGuard.Detect(_sourceConnectionString, DatabaseType.SQLite), Is.EqualTo(DatabaseLineage.Chaptarr));
                Assert.That(DatabaseLineageGuard.Detect(ConnectionStringFor(backupPath), DatabaseType.SQLite), Is.EqualTo(DatabaseLineage.BookshelfNG));
                Assert.That(QueryScalar<string>(_sourcePath, "PRAGMA integrity_check;"), Is.EqualTo("ok"));
                Assert.That(QueryRows(_sourcePath, "PRAGMA foreign_key_check;").Length, Is.Zero);
                Assert.That(QueryScalar<string>(backupPath, "SELECT ForeignBookId FROM Books WHERE Id = 30;"), Is.EqualTo("hardcover:book-30"));
            });

            Assert.That(QueryScalar<string>(_sourcePath, "SELECT HardcoverAuthorId FROM Authors WHERE Id = 20;"), Is.EqualTo("hc:author-10"));
            Assert.That(QueryScalar<int>(_sourcePath, "SELECT AudiobookQualityProfileId FROM Authors WHERE Id = 20;"), Is.EqualTo(1));
            Assert.That(QueryScalar<int>(_sourcePath, "SELECT EbookQualityProfileId FROM Authors WHERE Id = 20;"), Is.EqualTo(4));
            Assert.That(QueryScalar<string>(_sourcePath, "SELECT AudiobookPath FROM Authors WHERE Id = 20;"), Is.EqualTo("/library/Fixture Author/Audiobooks"));
            Assert.That(QueryScalar<string>(_sourcePath, "SELECT EbookPath FROM Authors WHERE Id = 20;"), Is.EqualTo("/library/Fixture Author/Ebooks"));
            Assert.That(QueryScalar<int>(_sourcePath, "SELECT AudiobookMetadataProfileId FROM Authors WHERE Id = 20;"), Is.EqualTo(1));
            Assert.That(QueryScalar<int>(_sourcePath, "SELECT EbookMetadataProfileId FROM Authors WHERE Id = 20;"), Is.EqualTo(2));
            Assert.That(QueryScalar<int>(_sourcePath, "SELECT ProfileType FROM QualityProfiles WHERE Id = 1;"), Is.EqualTo(1));
            Assert.That(QueryScalar<int>(_sourcePath, "SELECT ProfileType FROM QualityProfiles WHERE Id = 3;"), Is.EqualTo(2));
            Assert.That(QueryScalar<string>(_sourcePath, "SELECT Name FROM QualityProfiles WHERE Id = 4;"), Is.EqualTo("Ebook override"));
            Assert.That(QueryScalar<int>(_sourcePath, "SELECT Cutoff FROM QualityProfiles WHERE Id = 4;"), Is.EqualTo(2));
            Assert.That(QueryScalar<string>(_sourcePath, "SELECT AudiobookSettings FROM RootFolders WHERE Id = 1;"), Does.Contain("\"QualityProfileId\":1"));
            Assert.That(QueryScalar<string>(_sourcePath, "SELECT EbookSettings FROM RootFolders WHERE Id = 1;"), Does.Contain("\"QualityProfileId\":3"));
            Assert.That(QueryScalar<string>(_sourcePath, "SELECT PreferredEditionTerms FROM MetadataProfiles WHERE Id = 1;"), Is.EqualTo("3|audible"));
            Assert.That(QueryScalar<string>(_sourcePath, "SELECT PreferredSeries FROM MetadataProfiles WHERE Id = 1;"), Is.EqualTo("The Fixture Series"));
            Assert.That(QueryScalar<string>(_sourcePath, "SELECT PreferredEditionTerms FROM MetadataProfiles WHERE Id = 2;"), Is.EqualTo("2|epub"));
            Assert.That(QueryScalar<string>(_sourcePath, "SELECT PreferredSeries FROM MetadataProfiles WHERE Id = 2;"), Is.EqualTo("Fixture Ebooks"));
            Assert.That(QueryScalar<string>(_sourcePath, "SELECT Value FROM Config WHERE Key = 'FixtureSetting';"), Is.EqualTo("preserved"));
            Assert.That(QueryScalar<int>(_sourcePath, "SELECT QualityProfileId FROM ImportLists WHERE Id = 110;"), Is.EqualTo(1));
            Assert.That(QueryScalar<string>(_sourcePath, "SELECT Settings FROM ImportLists WHERE Id = 110;"), Does.Contain("fixture-list-setting"));
            Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM Indexers WHERE Id = 120 AND EnableRss = 1 AND SupportsRss = 0 AND SupportsSearch = 0;"), Is.EqualTo(1));
            Assert.That(QueryScalar<int>(_sourcePath, "SELECT OnBookRetag FROM Notifications WHERE Id = 111;"), Is.EqualTo(1));
            Assert.That(QueryScalar<string>(_sourcePath, "SELECT Password FROM Users WHERE Id = 112;"), Is.EqualTo("legacy-sha256-hash"));
            Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM Users WHERE Id = 112 AND Salt IS NULL AND Iterations IS NULL;"), Is.EqualTo(1));
            Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM Books WHERE AuthorId = 20;"), Is.EqualTo(2));
            Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM Books WHERE Id = 30 AND MediaType = 0 AND AudiobookMonitored = 1 AND ForeignEditionId = 'hc:audio-40';"), Is.EqualTo(1));
            Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM Books WHERE Id = 31 AND MediaType = 1 AND EbookMonitored = 0 AND ForeignEditionId IS NULL;"), Is.EqualTo(1));
            Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM Editions WHERE Id = 40 AND BookId = 30 AND HardcoverEditionId = 'hc:audio-40';"), Is.EqualTo(1));
            Assert.That(QueryScalar<string>(_sourcePath, "SELECT NarratorNames FROM Editions WHERE Id = 40;"), Is.EqualTo("[\"A Narrator\"]"));
            Assert.That(QueryScalar<string>(_sourcePath, "SELECT Images FROM Editions WHERE Id = 40;"), Is.EqualTo("[{\"Url\":\"https://covers.example/audio.jpg\",\"CoverType\":6}]"));
            Assert.That(QueryScalar<string>(_sourcePath, "SELECT Links FROM Editions WHERE Id = 40;"), Is.EqualTo("[{\"Url\":\"https://catalog.example/audio-40\",\"Name\":\"Catalog\"}]"));
            Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM Editions WHERE Id = 41 AND BookId = 31 AND OpenLibraryEditionId = 'ol:OL123M';"), Is.EqualTo(1));
            Assert.That(QueryScalar<string>(_sourcePath, "SELECT Links FROM Editions WHERE Id = 41;"), Is.EqualTo("[]"));
            Assert.That(QueryScalar<string>(_sourcePath, "SELECT MediaType FROM BookFiles WHERE Id = 51;"), Is.EqualTo("ebook"));
            Assert.That(QueryScalar<int>(_sourcePath, "SELECT BookFileId FROM ExtraFiles WHERE Id = 60;"), Is.EqualTo(999));
            Assert.That(QueryScalar<long>(_sourcePath, "SELECT seq FROM sqlite_sequence WHERE name = 'BookFiles';"), Is.GreaterThanOrEqualTo(999));
            Assert.That(QueryScalar<int>(_sourcePath, "SELECT BookId FROM MetadataFiles WHERE Id = 61;"), Is.EqualTo(31));
            Assert.That(QueryScalar<int>(_sourcePath, "SELECT BookId FROM History WHERE Id = 93;"), Is.EqualTo(31));
            Assert.That(QueryScalar<int>(_sourcePath, "SELECT EditionId FROM History WHERE Id = 93;"), Is.EqualTo(41));
            Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM SeriesBookLink WHERE SeriesId = 70 AND BookId IN (30, 31);"), Is.EqualTo(2));
            Assert.That(QueryScalar<string>(_sourcePath, "SELECT AuthorProviderIds FROM Blocklist WHERE Id = 92;"), Is.EqualTo("[\"hc:author-10\"]"));
            Assert.That(QueryScalar<string>(_sourcePath, "SELECT BookProviderIds FROM Blocklist WHERE Id = 92;"), Is.EqualTo("[\"hc:book-30\"]"));
            Assert.That(QueryScalar<int>(_sourcePath, "SELECT BookId FROM DownloadHistory WHERE Id = 91;"), Is.Zero);
            Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM Commands;"), Is.Zero);

            new MigrationController(LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null).Migrate(
                _sourceConnectionString,
                new MigrationContext(MigrationType.Main),
                DatabaseType.SQLite);
            Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM Books;"), Is.EqualTo(2), "a restart must not repeat the conversion");
        }

        [Test]
        public void should_upgrade_an_older_bookshelf_snapshot_and_keep_the_original_schema_for_rollback()
        {
            DowngradeSourceToMigration22();
            var migrationController = new MigrationController(LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null);

            var result = migrationController.MigrateBookshelf(
                _sourceConnectionString,
                new MigrationContext(MigrationType.Main),
                DatabaseType.SQLite,
                previewOnly: false);

            var backupPath = result.BackupPath;
            Assert.Multiple(() =>
            {
                Assert.That(DatabaseLineageGuard.Detect(_sourceConnectionString, DatabaseType.SQLite), Is.EqualTo(DatabaseLineage.Chaptarr));
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM Books WHERE Id IN (30, 31);"), Is.EqualTo(2));
                Assert.That(QueryScalar<int>(backupPath, "SELECT MAX(Version) FROM VersionInfo;"), Is.EqualTo(22));
                Assert.That(QueryRows(backupPath, "SELECT name FROM pragma_table_info('Authors');"), Does.Not.Contain("EbookPath"));
                Assert.That(QueryScalar<string>(_sourcePath, "PRAGMA integrity_check;"), Is.EqualTo("ok"));
                Assert.That(QueryRows(_sourcePath, "PRAGMA foreign_key_check;"), Is.Empty);
            });

            var restored = new BookshelfDatabaseConverter().Restore(
                _sourceConnectionString,
                backupPath,
                migrateBookshelfSource: migrationController.MigrateBookshelfSource);

            Assert.Multiple(() =>
            {
                Assert.That(restored.PreviewOnly, Is.False);
                Assert.That(DatabaseLineageGuard.Detect(_sourceConnectionString, DatabaseType.SQLite), Is.EqualTo(DatabaseLineage.BookshelfNG));
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT MAX(Version) FROM VersionInfo;"), Is.EqualTo(22));
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM Books WHERE Id IN (30, 31);"), Is.EqualTo(1));
                Assert.That(File.Exists(restored.ChaptarrBackupPath), Is.True);
                Assert.That(QueryScalar<string>(_sourcePath, "PRAGMA integrity_check;"), Is.EqualTo("ok"));
            });
        }

        [Test]
        public void should_preview_older_schema_upgrade_without_modifying_the_source_or_leaving_staging_files()
        {
            DowngradeSourceToMigration22();
            var sourceBefore = File.ReadAllBytes(_sourcePath);
            var migrationController = new MigrationController(LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null);

            var result = migrationController.MigrateBookshelf(
                _sourceConnectionString,
                new MigrationContext(MigrationType.Main),
                DatabaseType.SQLite,
                previewOnly: true);

            Assert.Multiple(() =>
            {
                Assert.That(result.PreviewOnly, Is.True);
                Assert.That(result.BackupPath, Is.Null);
                Assert.That(result.ImportReport.Tables.Count, Is.GreaterThan(0));
                Assert.That(File.ReadAllBytes(_sourcePath), Is.EqualTo(sourceBefore));
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT MAX(Version) FROM VersionInfo;"), Is.EqualTo(22));
                Assert.That(Directory.GetFiles(_directory, "*.bookshelf-migrate-*.db"), Is.Empty);
                Assert.That(Directory.GetFiles(_directory, "*.chaptarr-stage-*.db"), Is.Empty);
                Assert.That(Directory.GetFiles(_directory, "*.bookshelf-snapshot-*.db"), Is.Empty);
            });
        }

        [Test]
        public void should_reject_a_bookshelf_schema_newer_than_the_embedded_migration_chain()
        {
            using (var source = new SqliteConnection(_sourceConnectionString))
            {
                source.Open();
                using var command = source.CreateCommand();
                command.CommandText = "UPDATE VersionInfo SET Version = 159 WHERE Version = 158;";
                Assert.That(command.ExecuteNonQuery(), Is.EqualTo(1));
            }

            var before = File.ReadAllBytes(_sourcePath);
            var exception = Assert.Throws<InvalidDataException>(() => new MigrationController(
                LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null).Migrate(
                    _sourceConnectionString,
                    new MigrationContext(MigrationType.Main),
                    DatabaseType.SQLite));

            Assert.Multiple(() =>
            {
                Assert.That(exception.Message, Does.Contain("newer than the bundled migration chain"));
                Assert.That(File.ReadAllBytes(_sourcePath), Is.EqualTo(before));
                Assert.That(Directory.GetFiles(_directory, "*.bookshelfng-backup-*.db"), Is.Empty);
                Assert.That(Directory.GetFiles(_directory, "*.bookshelf-migrate-*.db"), Is.Empty);
            });
        }

        [Test]
        public void should_preview_bookshelf_rollback_without_changing_either_database()
        {
            new MigrationController(LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null).Migrate(
                _sourceConnectionString,
                new MigrationContext(MigrationType.Main),
                DatabaseType.SQLite);
            var bookshelfBackupPath = Directory.GetFiles(_directory, "readarr.db.bookshelfng-backup-*.db").Single();
            var currentBefore = File.ReadAllBytes(_sourcePath);
            var sourceBackupBefore = File.ReadAllBytes(bookshelfBackupPath);

            var result = new BookshelfDatabaseConverter().Restore(_sourceConnectionString, bookshelfBackupPath, previewOnly: true);

            Assert.Multiple(() =>
            {
                Assert.That(result.PreviewOnly, Is.True);
                Assert.That(result.BookshelfBackupPath, Is.EqualTo(bookshelfBackupPath));
                Assert.That(result.ChaptarrBackupPath, Is.Null);
                Assert.That(File.ReadAllBytes(_sourcePath), Is.EqualTo(currentBefore));
                Assert.That(File.ReadAllBytes(bookshelfBackupPath), Is.EqualTo(sourceBackupBefore));
                Assert.That(Directory.GetFiles(_directory, "readarr.db.chaptarrng-backup-*.db"), Is.Empty);
                Assert.That(Directory.GetFiles(_directory, "*.bookshelf-restore-stage-*.db"), Is.Empty);
            });
        }

        [Test]
        public void should_restore_the_source_and_retain_chaptarr_writes_for_rollback()
        {
            new MigrationController(LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null).Migrate(
                _sourceConnectionString,
                new MigrationContext(MigrationType.Main),
                DatabaseType.SQLite);
            var bookshelfBackupPath = Directory.GetFiles(_directory, "readarr.db.bookshelfng-backup-*.db").Single();
            var sourceBackupBefore = File.ReadAllBytes(bookshelfBackupPath);

            using (var current = new SqliteConnection(_sourceConnectionString))
            {
                current.Open();
                using var command = current.CreateCommand();
                command.CommandText = "UPDATE Config SET Value = 'after-migration' WHERE Key = 'FixtureSetting';";
                Assert.That(command.ExecuteNonQuery(), Is.EqualTo(1));
            }

            var result = new BookshelfDatabaseConverter().Restore(_sourceConnectionString, bookshelfBackupPath);

            Assert.Multiple(() =>
            {
                Assert.That(result.PreviewOnly, Is.False);
                Assert.That(File.Exists(result.ChaptarrBackupPath), Is.True);
                Assert.That(DatabaseLineageGuard.Detect(_sourceConnectionString, DatabaseType.SQLite), Is.EqualTo(DatabaseLineage.BookshelfNG));
                Assert.That(DatabaseLineageGuard.Detect(ConnectionStringFor(result.ChaptarrBackupPath), DatabaseType.SQLite), Is.EqualTo(DatabaseLineage.Chaptarr));
                Assert.That(QueryScalar<string>(_sourcePath, "SELECT Value FROM Config WHERE Key = 'FixtureSetting';"), Is.EqualTo("preserved"));
                Assert.That(QueryScalar<string>(result.ChaptarrBackupPath, "SELECT Value FROM Config WHERE Key = 'FixtureSetting';"), Is.EqualTo("after-migration"));
                Assert.That(QueryScalar<string>(_sourcePath, "PRAGMA integrity_check;"), Is.EqualTo("ok"));
                Assert.That(QueryRows(_sourcePath, "PRAGMA foreign_key_check;"), Is.Empty);
                Assert.That(QueryScalar<string>(result.ChaptarrBackupPath, "PRAGMA integrity_check;"), Is.EqualTo("ok"));
                Assert.That(QueryRows(result.ChaptarrBackupPath, "PRAGMA foreign_key_check;"), Is.Empty);
                Assert.That(File.ReadAllBytes(bookshelfBackupPath), Is.EqualTo(sourceBackupBefore));
                Assert.That(Directory.GetFiles(_directory, "*.bookshelf-restore-stage-*.db"), Is.Empty);
            });
        }

        [Test]
        public void should_refuse_a_bookshelf_backup_with_the_wrong_identity_without_changing_the_current_database()
        {
            new MigrationController(LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null).Migrate(
                _sourceConnectionString,
                new MigrationContext(MigrationType.Main),
                DatabaseType.SQLite);
            var bookshelfBackupPath = Directory.GetFiles(_directory, "readarr.db.bookshelfng-backup-*.db").Single();
            var wrongIdentityPath = Path.Combine(_directory, "unrelated.db");
            File.Copy(bookshelfBackupPath, wrongIdentityPath);
            var currentBefore = File.ReadAllBytes(_sourcePath);
            var sourceBackupBefore = File.ReadAllBytes(bookshelfBackupPath);

            var exception = Assert.Throws<InvalidDataException>(() => new BookshelfDatabaseConverter().Restore(
                _sourceConnectionString,
                wrongIdentityPath));

            Assert.Multiple(() =>
            {
                Assert.That(exception.Message, Does.Contain("not a retained BookshelfNG conversion backup"));
                Assert.That(File.ReadAllBytes(_sourcePath), Is.EqualTo(currentBefore));
                Assert.That(File.ReadAllBytes(bookshelfBackupPath), Is.EqualTo(sourceBackupBefore));
                Assert.That(Directory.GetFiles(_directory, "readarr.db.chaptarrng-backup-*.db"), Is.Empty);
            });
        }

        [Test]
        public void should_refuse_an_unsupported_bookshelf_identity_without_changing_the_source()
        {
            using (var source = new SqliteConnection(_sourceConnectionString))
            {
                source.Open();
                using var command = source.CreateCommand();
                command.CommandText = "UPDATE AuthorMetadata SET ForeignAuthorId = 'metadata-api-author:opaque-10' WHERE Id = 10;";
                command.ExecuteNonQuery();
            }

            var before = File.ReadAllBytes(_sourcePath);
            var exception = Assert.Throws<InvalidDataException>(() => new MigrationController(
                LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null).Migrate(
                    _sourceConnectionString,
                    new MigrationContext(MigrationType.Main),
                    DatabaseType.SQLite));

            Assert.That(exception.Message, Does.Contain("unsupported provider"));
            Assert.That(File.ReadAllBytes(_sourcePath), Is.EqualTo(before));
            Assert.That(Directory.GetFiles(_directory, "*.bookshelfng-backup-*.db"), Is.Empty);
        }

        [Test]
        public void should_preserve_one_monitored_edition_for_each_media_format()
        {
            using (var source = new SqliteConnection(_sourceConnectionString))
            {
                source.Open();
                using var command = source.CreateCommand();
                command.CommandText = "UPDATE Editions SET Monitored = 1 WHERE Id = 41;";
                command.ExecuteNonQuery();
            }

            new MigrationController(LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null).Migrate(
                _sourceConnectionString,
                new MigrationContext(MigrationType.Main),
                DatabaseType.SQLite);

            Assert.Multiple(() =>
            {
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM Books WHERE Id = 30 AND MediaType = 0 AND AudiobookMonitored = 1 AND EbookMonitored = 0 AND ForeignEditionId = 'hc:audio-40';"), Is.EqualTo(1));
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM Books WHERE Id = 31 AND MediaType = 1 AND AudiobookMonitored = 0 AND EbookMonitored = 1 AND ForeignEditionId = 'ol:OL123M';"), Is.EqualTo(1));
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM Editions WHERE Id = 40 AND BookId = 30 AND Monitored = 1;"), Is.EqualTo(1));
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM Editions WHERE Id = 41 AND BookId = 31 AND Monitored = 1;"), Is.EqualTo(1));
                Assert.That(QueryScalar<string>(_sourcePath, "PRAGMA integrity_check;"), Is.EqualTo("ok"));
                Assert.That(QueryRows(_sourcePath, "PRAGMA foreign_key_check;"), Is.Empty);
            });
        }

        [Test]
        public void should_refuse_multiple_monitored_editions_of_the_same_media_format()
        {
            using (var source = new SqliteConnection(_sourceConnectionString))
            {
                source.Open();
                using var command = source.CreateCommand();
                command.CommandText = "UPDATE Editions SET IsEbook = 0, Monitored = 1;";
                command.ExecuteNonQuery();
            }

            var before = File.ReadAllBytes(_sourcePath);
            var exception = Assert.Throws<InvalidDataException>(() => new MigrationController(
                LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null).Migrate(
                    _sourceConnectionString,
                    new MigrationContext(MigrationType.Main),
                    DatabaseType.SQLite));

            Assert.That(exception.Message, Does.Contain("more than one monitored audiobook edition"));
            Assert.That(File.ReadAllBytes(_sourcePath), Is.EqualTo(before));
            Assert.That(Directory.GetFiles(_directory, "*.bookshelfng-backup-*.db"), Is.Empty);
        }

        [Test]
        public void should_preserve_a_monitored_mixed_format_book_without_a_selected_edition_as_both_unmonitored()
        {
            using (var source = new SqliteConnection(_sourceConnectionString))
            {
                source.Open();
                using var command = source.CreateCommand();
                command.CommandText = "UPDATE Editions SET Monitored = 0;";
                command.ExecuteNonQuery();
            }

            new MigrationController(LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null).Migrate(
                _sourceConnectionString,
                new MigrationContext(MigrationType.Main),
                DatabaseType.SQLite);

            var backupPath = Directory.GetFiles(_directory, "readarr.db.bookshelfng-backup-*.db").Single();
            var reviewPath = _sourcePath + ".bookshelf-media-review.json";
            var review = File.ReadAllText(reviewPath);
            Assert.Multiple(() =>
            {
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM Books WHERE Id IN (30, 31) AND AudiobookMonitored = 0 AND EbookMonitored = 0;"), Is.EqualTo(2));
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM Editions WHERE BookId IN (30, 31);"), Is.EqualTo(2));
                Assert.That(review, Does.Contain("\"sourceBookId\": 30"));
                Assert.That(review, Does.Contain("\"mediaType\": \"both-unmonitored\""));
                Assert.That(review, Does.Contain("\"monitored\": true"));
                Assert.That(QueryScalar<string>(_sourcePath, "PRAGMA integrity_check;"), Is.EqualTo("ok"));
                Assert.That(QueryRows(_sourcePath, "PRAGMA foreign_key_check;"), Is.Empty);
                if (!OperatingSystem.IsWindows())
                {
                    Assert.That(File.GetUnixFileMode(reviewPath), Is.EqualTo(UnixFileMode.UserRead | UnixFileMode.UserWrite));
                }
                Assert.That(DatabaseLineageGuard.Detect(ConnectionStringFor(backupPath), DatabaseType.SQLite), Is.EqualTo(DatabaseLineage.BookshelfNG));
                Assert.That(QueryScalar<int>(backupPath, "SELECT COUNT(*) FROM Books WHERE Id = 30 AND Monitored = 1;"), Is.EqualTo(1));
            });
        }

        [Test]
        public void should_automatically_preserve_a_book_without_editions_in_both_catalogs_unmonitored()
        {
            InsertBookWithoutEdition(32, monitored: true);
            new MigrationController(LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null).Migrate(
                _sourceConnectionString,
                new MigrationContext(MigrationType.Main),
                DatabaseType.SQLite);

            var backupPath = Directory.GetFiles(_directory, "readarr.db.bookshelfng-backup-*.db").Single();
            var reviewPath = _sourcePath + ".bookshelf-media-review.json";
            var review = File.ReadAllText(reviewPath);
            Assert.Multiple(() =>
            {
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM Books WHERE Id IN (32, 34) AND AudiobookMonitored = 0 AND EbookMonitored = 0;"), Is.EqualTo(2));
                Assert.That(File.Exists(reviewPath), Is.True);
                Assert.That(review, Does.Contain("\"sourceBookId\": 32"));
                Assert.That(review, Does.Contain("\"mediaType\": \"both-unmonitored\""));
                Assert.That(review, Does.Contain("\"monitored\": true"));
                Assert.That(QueryScalar<string>(_sourcePath, "PRAGMA integrity_check;"), Is.EqualTo("ok"));
                Assert.That(QueryRows(_sourcePath, "PRAGMA foreign_key_check;"), Is.Empty);
                Assert.That(DatabaseLineageGuard.Detect(ConnectionStringFor(backupPath), DatabaseType.SQLite), Is.EqualTo(DatabaseLineage.BookshelfNG));
                Assert.That(QueryScalar<int>(backupPath, "SELECT COUNT(*) FROM Books WHERE Id = 32 AND Monitored = 1;"), Is.EqualTo(1));
                Assert.That(QueryScalar<int>(backupPath, "SELECT COUNT(*) FROM Editions WHERE BookId = 32;"), Is.Zero);
            });
        }

        [Test]
        public void should_accept_an_explicit_both_unmonitored_media_choice()
        {
            InsertBookWithoutEdition(32, monitored: true);
            File.WriteAllText(_sourcePath + ".bookshelf-media-map.json", """
                {
                  "version": 1,
                  "books": [
                    { "sourceBookId": 32, "mediaType": "both-unmonitored" }
                  ]
                }
                """);

            new MigrationController(LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null).Migrate(
                _sourceConnectionString,
                new MigrationContext(MigrationType.Main),
                DatabaseType.SQLite);

            Assert.Multiple(() =>
            {
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM Books WHERE Id IN (32, 34) AND AudiobookMonitored = 0 AND EbookMonitored = 0;"), Is.EqualTo(2));
                Assert.That(File.Exists(_sourcePath + ".bookshelf-media-review.json"), Is.False);
                Assert.That(QueryScalar<string>(_sourcePath, "PRAGMA integrity_check;"), Is.EqualTo("ok"));
                Assert.That(QueryRows(_sourcePath, "PRAGMA foreign_key_check;"), Is.Empty);
            });
        }

        [Test]
        public void should_import_a_book_without_editions_using_the_explicit_both_formats_choice()
        {
            InsertBookWithoutEdition(32, monitored: true);
            File.WriteAllText(_sourcePath + ".bookshelf-media-review.json", "{\"version\":1,\"books\":[]}");
            File.WriteAllText(_sourcePath + ".bookshelf-media-map.json", """
                {
                  "version": 1,
                  "books": [
                    { "sourceBookId": 32, "mediaType": "both" }
                  ]
                }
                """);

            new MigrationController(LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null).Migrate(
                _sourceConnectionString,
                new MigrationContext(MigrationType.Main),
                DatabaseType.SQLite);

            Assert.Multiple(() =>
            {
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM Books WHERE Id IN (32, 34);"), Is.EqualTo(2));
                Assert.That(File.Exists(_sourcePath + ".bookshelf-media-review.json"), Is.False);
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM Books WHERE Id = 32 AND MediaType = 0 AND AudiobookMonitored = 1 AND EbookMonitored = 0;"), Is.EqualTo(1));
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM Books WHERE Id = 34 AND MediaType = 1 AND AudiobookMonitored = 0 AND EbookMonitored = 1;"), Is.EqualTo(1));
                Assert.That(QueryScalar<string>(_sourcePath, "PRAGMA integrity_check;"), Is.EqualTo("ok"));
                Assert.That(QueryRows(_sourcePath, "PRAGMA foreign_key_check;"), Is.Empty);
            });
        }

        [Test]
        public void should_apply_an_explicit_monitoring_choice_to_a_mixed_format_book_without_a_selected_edition()
        {
            using (var source = new SqliteConnection(_sourceConnectionString))
            {
                source.Open();
                using var command = source.CreateCommand();
                command.CommandText = "UPDATE Editions SET Monitored = 0;";
                command.ExecuteNonQuery();
            }

            File.WriteAllText(_sourcePath + ".bookshelf-media-map.json", """
                {
                  "version": 1,
                  "books": [
                    { "sourceBookId": 30, "mediaType": "ebook" }
                  ]
                }
                """);

            new MigrationController(LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null).Migrate(
                _sourceConnectionString,
                new MigrationContext(MigrationType.Main),
                DatabaseType.SQLite);

            Assert.Multiple(() =>
            {
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM Books WHERE Id = 30 AND MediaType = 0 AND AudiobookMonitored = 0 AND EbookMonitored = 0;"), Is.EqualTo(1));
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM Books WHERE Id = 31 AND MediaType = 1 AND EbookMonitored = 1 AND ForeignEditionId IS NULL;"), Is.EqualTo(1));
                Assert.That(QueryScalar<string>(_sourcePath, "PRAGMA integrity_check;"), Is.EqualTo("ok"));
                Assert.That(QueryRows(_sourcePath, "PRAGMA foreign_key_check;"), Is.Empty);
            });
        }

        [Test]
        public void should_preserve_unmapped_source_files_without_inventing_media_or_edition_relationships()
        {
            using (var source = new SqliteConnection(_sourceConnectionString))
            {
                source.Open();
                using var command = source.CreateCommand();
                command.CommandText = @"
                    UPDATE BookFiles SET EditionId = 0 WHERE Id = 50;
                    INSERT INTO ExtraFiles (Id, AuthorId, BookId, BookFileId, RelativePath, Extension, Added, LastUpdated)
                    VALUES (62, 20, 30, 50, 'linked-cover.jpg', 'jpg', '2026-01-05', '2026-01-05');
                    INSERT INTO MetadataFiles (Id, AuthorId, Consumer, Type, RelativePath, LastUpdated, BookId, BookFileId, Extension)
                    VALUES (63, 20, 'AudioBookShelf', 0, 'unmapped.opf', '2026-01-05', 30, 50, 'opf');
                    INSERT INTO History (Id, SourceTitle, Date, Quality, Data, EventType, DownloadId, AuthorId, BookId)
                    VALUES (94, 'book.m4b', '2026-01-05', '{}', '{""FileId"":""50"",""ImportedPath"":""/library/Fixture Author/book.m4b""}', 3, 'download-94', 20, 30);";
                command.ExecuteNonQuery();
            }

            new MigrationController(LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null).Migrate(
                _sourceConnectionString,
                new MigrationContext(MigrationType.Main),
                DatabaseType.SQLite);

            Assert.Multiple(() =>
            {
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM BookFiles WHERE Id = 50 AND EditionId = 0 AND MediaType = '' AND UnmappedReason LIKE '%not inferred%';"), Is.EqualTo(1));
                Assert.That(QueryScalar<string>(_sourcePath, "SELECT Path FROM BookFiles WHERE Id = 50;"), Is.EqualTo("/library/Fixture Author/book.m4b"));
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM ExtraFiles WHERE Id = 62 AND BookId = 30 AND BookFileId = 50;"), Is.EqualTo(1));
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM MetadataFiles WHERE Id = 63 AND BookId = 30 AND BookFileId = 50;"), Is.EqualTo(1));
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM History WHERE Id = 94 AND BookId = 30 AND EditionId IS NULL AND Data LIKE '%\"FileId\":\"50\"%';"), Is.EqualTo(1));
                Assert.That(QueryScalar<string>(_sourcePath, "PRAGMA integrity_check;"), Is.EqualTo("ok"));
                Assert.That(QueryRows(_sourcePath, "PRAGMA foreign_key_check;"), Is.Empty);
            });
        }

        [Test]
        public void should_refuse_an_incomplete_media_map_without_changing_the_source()
        {
            InsertBookWithoutEdition(32, monitored: true);
            File.WriteAllText(_sourcePath + ".bookshelf-media-map.json", """
                {
                  "version": 1,
                  "books": []
                }
                """);
            var before = File.ReadAllBytes(_sourcePath);

            var exception = Assert.Throws<InvalidDataException>(() => new MigrationController(
                LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null).Migrate(
                    _sourceConnectionString,
                    new MigrationContext(MigrationType.Main),
                    DatabaseType.SQLite));

            Assert.Multiple(() =>
            {
                Assert.That(exception.Message, Does.Contain("1 BookshelfNG book(s) need an explicit media type"));
                Assert.That(File.ReadAllBytes(_sourcePath), Is.EqualTo(before));
                Assert.That(Directory.GetFiles(_directory, "*.bookshelfng-backup-*.db"), Is.Empty);
            });
        }

        [Test]
        public void should_refuse_a_stale_media_map_when_the_current_source_has_no_ambiguous_books()
        {
            File.WriteAllText(_sourcePath + ".bookshelf-media-map.json", """
                {
                  "version": 1,
                  "books": [
                    { "sourceBookId": 30, "mediaType": "ebook" }
                  ]
                }
                """);
            var before = File.ReadAllBytes(_sourcePath);

            var exception = Assert.Throws<InvalidDataException>(() => new MigrationController(
                LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null).Migrate(
                    _sourceConnectionString,
                    new MigrationContext(MigrationType.Main),
                    DatabaseType.SQLite));
            var review = File.ReadAllText(_sourcePath + ".bookshelf-media-review.json");

            Assert.Multiple(() =>
            {
                Assert.That(exception.Message, Does.Contain("not ambiguous in the current source"));
                Assert.That(File.ReadAllBytes(_sourcePath), Is.EqualTo(before));
                Assert.That(review, Does.Contain("\"books\""));
                Assert.That(review, Does.Not.Contain("\"sourceBookId\": 30"));
                Assert.That(Directory.GetFiles(_directory, "*.bookshelfng-backup-*.db"), Is.Empty);
            });
        }

        [Test]
        public void should_keep_completed_media_choices_in_the_review_when_a_map_is_incomplete()
        {
            InsertBookWithoutEdition(32, monitored: true);
            using (var source = new SqliteConnection(_sourceConnectionString))
            {
                source.Open();
                using var command = source.CreateCommand();
                command.CommandText = "UPDATE Editions SET Monitored = 0;";
                command.ExecuteNonQuery();
            }

            File.WriteAllText(_sourcePath + ".bookshelf-media-map.json", """
                {
                  "version": 1,
                  "books": [
                    { "sourceBookId": 30, "mediaType": "ebook" }
                  ]
                }
                """);
            var before = File.ReadAllBytes(_sourcePath);

            var exception = Assert.Throws<InvalidDataException>(() => new MigrationController(
                LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null).Migrate(
                    _sourceConnectionString,
                    new MigrationContext(MigrationType.Main),
                    DatabaseType.SQLite));
            var review = File.ReadAllText(_sourcePath + ".bookshelf-media-review.json");

            Assert.Multiple(() =>
            {
                Assert.That(exception.Message, Does.Contain("1 BookshelfNG book(s) need an explicit media type"));
                Assert.That(review, Does.Contain("\"sourceBookId\": 30"));
                Assert.That(review, Does.Contain("\"mediaType\": \"ebook\""));
                Assert.That(review, Does.Contain("\"sourceBookId\": 32"));
                Assert.That(File.ReadAllBytes(_sourcePath), Is.EqualTo(before));
                Assert.That(Directory.GetFiles(_directory, "*.bookshelfng-backup-*.db"), Is.Empty);
            });
        }

        [Test]
        public void should_refuse_a_media_map_with_an_invalid_choice_without_changing_the_source()
        {
            InsertBookWithoutEdition(32, monitored: true);
            File.WriteAllText(_sourcePath + ".bookshelf-media-map.json", """
                {
                  "version": 1,
                  "books": [
                    { "sourceBookId": 32, "mediaType": "audio" }
                  ]
                }
                """);
            var before = File.ReadAllBytes(_sourcePath);

            var exception = Assert.Throws<InvalidDataException>(() => new MigrationController(
                LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null).Migrate(
                    _sourceConnectionString,
                    new MigrationContext(MigrationType.Main),
                    DatabaseType.SQLite));

            Assert.Multiple(() =>
            {
                Assert.That(exception.Message, Does.Contain("invalid book ID or mediaType"));
                Assert.That(File.ReadAllBytes(_sourcePath), Is.EqualTo(before));
                Assert.That(Directory.GetFiles(_directory, "*.bookshelfng-backup-*.db"), Is.Empty);
            });
        }

        [Test]
        public void should_refuse_nonempty_unmapped_setting_columns_without_changing_the_source()
        {
            using (var source = new SqliteConnection(_sourceConnectionString))
            {
                source.Open();
                using var command = source.CreateCommand();
                command.CommandText = "ALTER TABLE Config ADD COLUMN UnmappedValue TEXT; UPDATE Config SET UnmappedValue = 'keep-me' WHERE Key = 'FixtureSetting';";
                command.ExecuteNonQuery();
            }

            var before = File.ReadAllBytes(_sourcePath);
            var exception = Assert.Throws<InvalidDataException>(() => new MigrationController(
                LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null).Migrate(
                    _sourceConnectionString,
                    new MigrationContext(MigrationType.Main),
                    DatabaseType.SQLite));

            Assert.That(exception.Message, Does.Contain("Config.UnmappedValue"));
            Assert.That(File.ReadAllBytes(_sourcePath), Is.EqualTo(before));
            Assert.That(Directory.GetFiles(_directory, "*.bookshelfng-backup-*.db"), Is.Empty);
        }

        private void CreateSourceDatabase()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = assembly.GetManifestResourceNames().Single(x => x.EndsWith("BookshelfNG.Schema.158.sql", StringComparison.Ordinal));
            using var schema = assembly.GetManifestResourceStream(resourceName);
            using var reader = new StreamReader(schema);
            using var connection = new SqliteConnection(_sourceConnectionString);
            connection.Open();
            using var create = connection.CreateCommand();
            create.CommandText = reader.ReadToEnd();
            create.ExecuteNonQuery();

            using var data = connection.CreateCommand();
            data.CommandText = @"
                INSERT INTO VersionInfo (Version, AppliedOn, Description)
                WITH RECURSIVE migrations(version) AS
                (
                    SELECT 1 UNION ALL SELECT version + 1 FROM migrations WHERE version < 158
                )
                SELECT version, CURRENT_TIMESTAMP, 'BookshelfNG schema fixture' FROM migrations;

                INSERT INTO QualityProfiles (Id, Name, Cutoff, Items, UpgradeAllowed)
                VALUES (1, 'Audio default', 1, '[]', 1), (2, 'Ebook override', 2, '[]', 0);
                INSERT INTO MetadataProfiles (Id, Name, MinPopularity, SkipMissingDate, SkipMissingIsbn, SkipPartsAndSets, SkipSeriesSecondary, AllowedLanguages, PreferredEditionTerms, PreferredSeries)
                VALUES
                    (1, 'Audio metadata', 0, 0, 0, 0, 0, '[]', '3|audible', 'The Fixture Series'),
                    (2, 'Ebook metadata', 0, 0, 0, 0, 0, '[]', '2|epub', 'Fixture Ebooks');
                INSERT INTO RootFolders (Id, Path, Name, DefaultMetadataProfileId, DefaultQualityProfileId, DefaultMonitorOption, DefaultTags, IsCalibreLibrary, CalibreSettings, DefaultNewItemMonitorOption)
                VALUES (1, '/library', 'Library', 1, 1, 0, '[]', 0, NULL, 2);
                INSERT INTO Config (Id, Key, Value) VALUES (1, 'FixtureSetting', 'preserved');
                INSERT INTO ImportLists (Id, Name, Implementation, Settings, ConfigContract, EnableAutomaticAdd, RootFolderPath, ShouldMonitor, ProfileId, MetadataProfileId, Tags)
                VALUES (110, 'Fixture list', 'FixtureList', '{""Value"":""fixture-list-setting""}', '{}', 1, '/library', 0, 1, 1, '[]');
                INSERT INTO Indexers (Id, Name, Implementation, Settings, ConfigContract, EnableRss, EnableAutomaticSearch, EnableInteractiveSearch, Priority, Tags, DownloadClientId)
                VALUES (120, 'Fixture indexer', 'MyAnonaMouse', '{}', '{}', 1, 1, 1, 25, '[]', 0);
                INSERT INTO Notifications (Id, Name, OnGrab, Settings, Implementation, ConfigContract, OnUpgrade, Tags, OnRename, OnReleaseImport, OnHealthIssue, IncludeHealthWarnings, OnDownloadFailure, OnImportFailure, OnBookRetag, OnAuthorDelete, OnBookDelete, OnBookFileDelete, OnBookFileDeleteForUpgrade, OnApplicationUpdate, OnAuthorAdded)
                VALUES (111, 'Fixture notification', 1, '{}', 'FixtureNotification', '{}', 1, '[]', 1, 1, 1, 0, 1, 1, 1, 0, 0, 0, 0, 1, 0);
                INSERT INTO Users (Id, Identifier, Username, Password)
                VALUES (112, '00000000-0000-0000-0000-000000000020', 'fixture-user', 'legacy-sha256-hash');
                INSERT INTO AuthorMetadata (Id, ForeignAuthorId, TitleSlug, Name, Overview, Status, Images, Aliases, SortName, NameLastFirst, SortNameLastFirst)
                VALUES (10, 'hardcover-author:author-10', 'hardcover-author:author-10', 'Fixture Author', 'Fixture overview', 0, '[]', '[]', 'Fixture Author', 'Author, Fixture', 'Author, Fixture');
                INSERT INTO Authors (Id, CleanName, Path, Monitored, QualityProfileId, Tags, Added, MetadataProfileId, AuthorMetadataId, MonitorNewItems, EbookPath, AudiobookPath, EbookQualityProfileId, AudiobookQualityProfileId, EbookMetadataProfileId, AudiobookMetadataProfileId)
                VALUES (20, 'fixtureauthor', '/library/Fixture Author', 1, 1, '[]', '2026-01-01', 1, 10, 2, '/library/Fixture Author/Ebooks', '/library/Fixture Author/Audiobooks', 2, 1, 2, 1);
                INSERT INTO Books (Id, AuthorMetadataId, ForeignBookId, TitleSlug, Title, Links, Genres, Ratings, CleanTitle, Monitored, AnyEditionOk, Added, RelatedBooks, LastSearchTime)
                VALUES (30, 10, 'hardcover:book-30', 'hardcover:book-30', 'Fixture Book', '[]', '[]', '{}', 'fixture book', 1, 0, '2026-01-02', '[]', NULL);
                INSERT INTO Editions (Id, BookId, ForeignEditionId, Isbn13, Asin, Title, TitleSlug, Images, Links, IsEbook, Monitored, ManualAdd, Narrator)
                VALUES
                    (40, 30, 'hardcover-edition:audio-40', NULL, NULL, 'Fixture Audiobook', 'audio-40', '[{""Url"":""https://covers.example/audio.jpg"",""CoverType"":6}]', '[{""Url"":""https://catalog.example/audio-40"",""Name"":""Catalog""}]', 0, 1, 0, 'A Narrator'),
                    (41, 30, 'openlibrary-edition:OL123M', '9780000000001', NULL, 'Fixture Ebook', 'OL123M', '[]', NULL, 1, 0, 0, NULL);
                INSERT INTO BookFiles (Id, EditionId, CalibreId, Quality, Size, DateAdded, MediaInfo, Modified, Path, Part)
                VALUES
                    (50, 40, 0, '{}', 100, '2026-01-02', '{}', '2026-01-02', '/library/Fixture Author/book.m4b', 0),
                    (51, 41, 0, '{}', 200, '2026-01-03', '{}', '2026-01-03', '/library/Fixture Author/book.epub', 0);
                INSERT INTO ExtraFiles (Id, AuthorId, BookId, BookFileId, RelativePath, Extension, Added, LastUpdated)
                VALUES (60, 20, 30, 999, 'cover.jpg', 'jpg', '2026-01-03', '2026-01-03');
                INSERT INTO MetadataFiles (Id, AuthorId, Consumer, Type, RelativePath, LastUpdated, BookId, BookFileId, Extension)
                VALUES (61, 20, 'AudioBookShelf', 0, 'metadata.opf', '2026-01-03', 30, 51, 'opf');
                INSERT INTO Series (Id, ForeignSeriesId, Title, Numbered, WorkCount, PrimaryWorkCount)
                VALUES (70, 'hardcover-series:series-70', 'Fixture Series', 1, 1, 1);
                INSERT INTO SeriesBookLink (Id, SeriesId, BookId, Position, IsPrimary, SeriesPosition)
                VALUES (80, 70, 30, '1', 1, 1);
                INSERT INTO Blocklist (Id, SourceTitle, Quality, Date, AuthorId, BookIds, IndexerFlags)
                VALUES (92, 'Fixture release', '{}', '2026-01-04', 20, '[30]', 0);
                INSERT INTO DownloadHistory (Id, EventType, AuthorId, DownloadId, SourceTitle, Date, Data)
                VALUES (91, 1, 20, 'download-91', 'Fixture release', '2026-01-04', '{}');
                INSERT INTO History (Id, SourceTitle, Date, Quality, Data, EventType, DownloadId, AuthorId, BookId)
                VALUES (90, 'Fixture release', '2026-01-04', '{}', '{}', 1, 'download-91', 20, 30);
                INSERT INTO History (Id, SourceTitle, Date, Quality, Data, EventType, DownloadId, AuthorId, BookId)
                VALUES (93, 'book.epub', '2026-01-04', '{}', '{""FileId"":""51"",""ImportedPath"":""/library/Fixture Author/book.epub""}', 3, 'download-91', 20, 30);
                INSERT INTO Commands (Id, Name, Body, Priority, Status, QueuedAt, Trigger)
                VALUES (100, 'OldCommand', '{}', 1, 0, '2026-01-04', 0);";
            data.ExecuteNonQuery();
        }

        private void InsertBookWithoutEdition(int id, bool monitored)
        {
            using var source = new SqliteConnection(_sourceConnectionString);
            source.Open();
            using var command = source.CreateCommand();
            command.CommandText = @"
                INSERT INTO Books (Id, AuthorMetadataId, ForeignBookId, TitleSlug, Title, Links, Genres, Ratings, CleanTitle, Monitored, AnyEditionOk, Added, RelatedBooks, LastSearchTime)
                VALUES ($id, 10, $foreignId, $slug, 'Unresolved fixture book', '[]', '[]', '{}', 'unresolved fixture book', $monitored, 0, '2026-01-05', '[]', NULL);";
            command.Parameters.AddWithValue("$id", id);
            command.Parameters.AddWithValue("$foreignId", $"hardcover:book-{id}");
            command.Parameters.AddWithValue("$slug", $"no-edition-{id}");
            command.Parameters.AddWithValue("$monitored", monitored ? 1 : 0);
            command.ExecuteNonQuery();
        }

        private void DowngradeSourceToMigration22()
        {
            using var source = new SqliteConnection(_sourceConnectionString);
            source.Open();
            using var command = source.CreateCommand();
            command.CommandText = @"
                ALTER TABLE Authors DROP COLUMN EbookMetadataProfileId;
                ALTER TABLE Authors DROP COLUMN AudiobookMetadataProfileId;
                ALTER TABLE Authors DROP COLUMN EbookQualityProfileId;
                ALTER TABLE Authors DROP COLUMN AudiobookQualityProfileId;
                ALTER TABLE Authors DROP COLUMN EbookPath;
                ALTER TABLE Authors DROP COLUMN AudiobookPath;
                ALTER TABLE Editions DROP COLUMN Narrator;
                ALTER TABLE MetadataProfiles DROP COLUMN PreferredSeries;
                ALTER TABLE MetadataProfiles DROP COLUMN PreferredEditionTerms;
                ALTER TABLE BookFiles DROP COLUMN LastRemoteSearchTime;
                ALTER TABLE BookFiles DROP COLUMN IndexerFlags;
                ALTER TABLE Blocklist DROP COLUMN IndexerFlags;
                ALTER TABLE Books DROP COLUMN LastSearchTime;
                ALTER TABLE Notifications DROP COLUMN OnAuthorAdded;
                DROP TABLE NotificationStatus;
                ALTER TABLE Commands DROP COLUMN Result;
                ALTER TABLE DownloadClients DROP COLUMN Tags;
                ALTER TABLE DownloadClients DROP COLUMN RemoveCompletedDownloads;
                ALTER TABLE DownloadClients DROP COLUMN RemoveFailedDownloads;
                ALTER TABLE NamingConfig DROP COLUMN ColonReplacementFormat;
                ALTER TABLE Indexers DROP COLUMN DownloadClientId;
                ALTER TABLE Notifications DROP COLUMN OnApplicationUpdate;
                ALTER TABLE DelayProfiles DROP COLUMN BypassIfHighestQuality;
                ALTER TABLE DelayProfiles DROP COLUMN BypassIfAboveCustomFormatScore;
                ALTER TABLE DelayProfiles DROP COLUMN MinimumCustomFormatScore;
                ALTER TABLE BookFiles DROP COLUMN OriginalFilePath;
                ALTER TABLE QualityProfiles DROP COLUMN FormatItems;
                ALTER TABLE QualityProfiles DROP COLUMN MinFormatScore;
                ALTER TABLE QualityProfiles DROP COLUMN CutoffFormatScore;
                ALTER TABLE ReleaseProfiles ADD COLUMN Preferred TEXT;
                ALTER TABLE ReleaseProfiles ADD COLUMN IncludePreferredWhenRenaming INTEGER;
                DROP TABLE CustomFormats;
                ALTER TABLE Indexers DROP COLUMN Tags;
                ALTER TABLE ImportListStatus DROP COLUMN LastInfoSync;
                ALTER TABLE ImportListStatus ADD COLUMN LastSyncListInfo DATETIME;
                DELETE FROM VersionInfo WHERE Version > 22;";
            command.ExecuteNonQuery();
        }

        private static string ConnectionStringFor(string path) => new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ConnectionString;

        private static T QueryScalar<T>(string databasePath, string sql)
        {
            using var connection = new SqliteConnection(ConnectionStringFor(databasePath));
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            return (T)Convert.ChangeType(command.ExecuteScalar(), typeof(T));
        }

        private static object[] QueryRows(string databasePath, string sql)
        {
            using var connection = new SqliteConnection(ConnectionStringFor(databasePath));
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            using var reader = command.ExecuteReader();
            var rows = new System.Collections.Generic.List<object>();
            while (reader.Read()) rows.Add(reader.GetValue(0));
            return rows.ToArray();
        }
    }
}
