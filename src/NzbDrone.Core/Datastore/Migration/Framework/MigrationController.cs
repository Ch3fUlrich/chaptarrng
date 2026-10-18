using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using FluentMigrator.Runner;
using FluentMigrator.Runner.Generators;
using FluentMigrator.Runner.Initialization;
using FluentMigrator.Runner.Processors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Data.Sqlite;
using NLog;
using NLog.Extensions.Logging;

namespace NzbDrone.Core.Datastore.Migration.Framework
{
    public interface IMigrationController
    {
        void Migrate(string connectionString, MigrationContext migrationContext, DatabaseType databaseType);
        void MigrateBookshelfSource(string connectionString);
        BookshelfDatabaseConversionResult MigrateBookshelf(
            string connectionString,
            MigrationContext migrationContext,
            DatabaseType databaseType,
            bool previewOnly);
    }

    public class MigrationController : IMigrationController
    {
        private readonly Logger _logger;
        private readonly ILoggerProvider _migrationLoggerProvider;

        public MigrationController(Logger logger,
                                   ILoggerProvider migrationLoggerProvider)
        {
            _logger = logger;
            _migrationLoggerProvider = migrationLoggerProvider;
        }

        public void Migrate(string connectionString, MigrationContext migrationContext, DatabaseType databaseType)
        {
            if (migrationContext.MigrationType == MigrationType.Main &&
                databaseType == DatabaseType.SQLite &&
                DatabaseLineageGuard.Detect(connectionString, databaseType) == DatabaseLineage.BookshelfNG)
            {
                if (migrationContext.DesiredVersion.HasValue)
                {
                    throw new InvalidDataException("A BookshelfNG database can only be converted during a full Chaptarr startup migration. The source database was not changed.");
                }

                var result = MigrateBookshelf(connectionString, migrationContext, databaseType, previewOnly: false);
                _logger.Info("BookshelfNG database conversion completed. Original database backup retained at {0}", result.BackupPath);
                var sourceDatabasePath = Path.GetFullPath(new SqliteConnectionStringBuilder(connectionString).DataSource);
                var mediaReviewPath = sourceDatabasePath + ".bookshelf-media-review.json";
                if (File.Exists(mediaReviewPath))
                {
                    _logger.Warn("BookshelfNG books were kept in both catalogs without monitoring where their media type was unclear. Review the migration choices at {0}", mediaReviewPath);
                }

                return;
            }

            MigrateNative(connectionString, migrationContext, databaseType);
        }

        public BookshelfDatabaseConversionResult MigrateBookshelf(
            string connectionString,
            MigrationContext migrationContext,
            DatabaseType databaseType,
            bool previewOnly)
        {
            if (databaseType != DatabaseType.SQLite ||
                DatabaseLineageGuard.Detect(connectionString, databaseType) != DatabaseLineage.BookshelfNG)
            {
                throw new InvalidDataException("BookshelfNG migration requires a supported BookshelfNG SQLite database. No database was changed.");
            }

            if (migrationContext?.DesiredVersion.HasValue == true)
            {
                throw new InvalidDataException("BookshelfNG conversion requires a full Chaptarr database migration. No database was changed.");
            }

            var sourceDatabasePath = Path.GetFullPath(new SqliteConnectionStringBuilder(connectionString).DataSource);
            return new BookshelfDatabaseConverter().Convert(
                connectionString,
                targetConnectionString => MigrateNative(targetConnectionString, migrationContext ?? new MigrationContext(MigrationType.Main), DatabaseType.SQLite),
                (source, target, transaction) => BookshelfDatabaseImporter.Import(source, target, transaction, sourceDatabasePath),
                previewOnly,
                MigrateBookshelfSource);
        }

        public void MigrateBookshelfSource(string connectionString)
        {
            if (DatabaseLineageGuard.Detect(connectionString, DatabaseType.SQLite) != DatabaseLineage.BookshelfNG)
            {
                throw new InvalidDataException("The database is not a supported BookshelfNG SQLite source. No database was changed.");
            }

            using (var source = new SqliteConnection(connectionString))
            {
                source.Open();
                BookshelfDatabaseImporter.ValidateSourceBackup(source);
            }

            var serviceProvider = new ServiceCollection()
                .AddLogging(b => b.AddNLog())
                .AddFluentMigratorCore()
                .Configure<RunnerOptions>(cfg => cfg.IncludeUntaggedMaintenances = true)
                .ConfigureRunner(
                    builder => builder
                    .AddNzbDroneSQLite()
                    .WithGlobalConnectionString(connectionString)
                    .ScanIn(Assembly.GetExecutingAssembly()).For.All())
                .Configure<TypeFilterOptions>(opt => opt.Namespace = "NzbDrone.Core.Datastore.BookshelfMigration")
                .Configure<ProcessorOptions>(opt =>
                {
                    opt.PreviewOnly = false;
                    opt.Timeout = TimeSpan.FromMinutes(5);
                })
                .Configure<SelectingProcessorAccessorOptions>(cfg => cfg.ProcessorId = "SQLite")
                .Configure<SelectingGeneratorAccessorOptions>(cfg => cfg.GeneratorId = "SQLite")
                .BuildServiceProvider();

            using (var scope = serviceProvider.CreateScope())
            {
                var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
                MigrationContext.Current = new MigrationContext(MigrationType.Main);
                try
                {
                    runner.MigrateUp();
                }
                catch (Exception exception)
                {
                    throw new InvalidDataException(
                        "BookshelfNG source migrations failed on the disposable snapshot. The original database was not changed.",
                        exception);
                }
                finally
                {
                    MigrationContext.Current = null;
                }
            }
        }

        private void MigrateNative(string connectionString, MigrationContext migrationContext, DatabaseType databaseType)
        {
            if (migrationContext.MigrationType == MigrationType.Main)
            {
                DatabaseLineageGuard.Validate(connectionString, databaseType);
            }

            var sw = Stopwatch.StartNew();

            _logger.Info("*** Migrating {0} database ***", databaseType);

            ServiceProvider serviceProvider;

            // FluentMigrator processor/generator ids are case-sensitive and use "SQLite"/"PostgreSQL"
            // (older versions used "sqlite"/"postgres").
            var db = databaseType == DatabaseType.SQLite ? "SQLite" : "PostgreSQL";

            serviceProvider = new ServiceCollection()
                .AddLogging(b => b.AddNLog())
                .AddFluentMigratorCore()
                .Configure<RunnerOptions>(cfg => cfg.IncludeUntaggedMaintenances = true)
                .ConfigureRunner(
                    builder => builder
                    .AddPostgres()
                    .AddSQLite() // Use SQLite runner (Microsoft.Data.Sqlite)
                    .WithGlobalConnectionString(connectionString)
                    .ScanIn(Assembly.GetExecutingAssembly()).For.All())
                .Configure<TypeFilterOptions>(opt => opt.Namespace = "NzbDrone.Core.Datastore.Migration")
                .Configure<ProcessorOptions>(opt =>
                {
                    opt.PreviewOnly = false;
                    opt.Timeout = TimeSpan.FromMinutes(5);
                })
                .Configure<SelectingProcessorAccessorOptions>(cfg =>
                {
                    cfg.ProcessorId = db;
                })
                .Configure<SelectingGeneratorAccessorOptions>(cfg =>
                {
                    cfg.GeneratorId = db;
                })
                .BuildServiceProvider();

            using (var scope = serviceProvider.CreateScope())
            {
                var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();

                MigrationContext.Current = migrationContext;

                if (migrationContext.DesiredVersion.HasValue)
                {
                    runner.MigrateUp(migrationContext.DesiredVersion.Value);
                }
                else
                {
                    runner.MigrateUp();
                }

                MigrationContext.Current = null;
            }

            sw.Stop();

            _logger.Debug("Took: {0}", sw.Elapsed);

            // Optional: validate baseline schema checksum on fresh installs
            try
            {
                if (databaseType == DatabaseType.SQLite && !string.IsNullOrWhiteSpace(BaselineSchema.ExpectedSqliteSha256))
                {
                    var actual = SchemaChecksum.ComputeSqliteHash(connectionString);
                    if (!string.Equals(actual, BaselineSchema.ExpectedSqliteSha256, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new ApplicationException($"Schema checksum mismatch. Expected {BaselineSchema.ExpectedSqliteSha256}, got {actual}");
                    }
                    _logger.Info("Baseline schema checksum OK: {0}", actual);
                }
                else if (databaseType == DatabaseType.PostgreSQL && !string.IsNullOrWhiteSpace(BaselineSchema.ExpectedPostgresSha256))
                {
                    var actual = SchemaChecksum.ComputePostgresHash(connectionString);
                    if (!string.Equals(actual, BaselineSchema.ExpectedPostgresSha256, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new ApplicationException($"Postgres schema checksum mismatch. Expected {BaselineSchema.ExpectedPostgresSha256}, got {actual}");
                    }
                    _logger.Info("Baseline Postgres schema checksum OK: {0}", actual);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Schema checksum validation failed");
                throw;
            }
        }
    }
}
