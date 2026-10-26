using System;
using System.IO;
using System.Linq;
using Dapper;
using Microsoft.Data.Sqlite;
using NLog;
using NUnit.Framework;
using NzbDrone.Common.Cache;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Datastore.Migration.Framework;
using NzbDrone.Core.RemotePathMappings;

namespace Chaptarr.Core.Test.Datastore
{
    [TestFixture]
    public class RemotePathMappingUpgradeFixture
    {
        [Test]
        public void should_preserve_host_mappings_after_upgrading_old_fork_budget_versions()
        {
            var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"rpm_upgrade_{Guid.NewGuid():N}.db");
            var connectionString = new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString();
            var logger = LogManager.GetLogger(nameof(RemotePathMappingUpgradeFixture));
            var migrations = new MigrationController(logger, null);

            SqliteConnection Open()
            {
                var connection = new SqliteConnection(connectionString);
                connection.Open();
                return connection;
            }

            try
            {
                migrations.Migrate(connectionString, new MigrationContext(MigrationType.Main, 107), DatabaseType.SQLite);
                using (var connection = Open())
                {
                    // The old fork assigned the budget tables these versions, before NG renumbered them.
                    connection.Execute(@"
                        CREATE TABLE GrabBudgetLog (Id INTEGER PRIMARY KEY AUTOINCREMENT, GrabbedAt TEXT NOT NULL);
                        CREATE INDEX IX_GrabBudgetLog_GrabbedAt ON GrabBudgetLog (GrabbedAt);
                        CREATE TABLE GrabBudgetBatch (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT, StartedAt TEXT NOT NULL,
                            Grabbed INTEGER NOT NULL, Skipped INTEGER NOT NULL, Failed INTEGER NOT NULL,
                            StopReason INTEGER NOT NULL, Details TEXT NULL);
                        CREATE INDEX IX_GrabBudgetBatch_StartedAt ON GrabBudgetBatch (StartedAt);
                        INSERT INTO VersionInfo (Version, AppliedOn, Description)
                            VALUES (108, CURRENT_TIMESTAMP, 'old fork budget log'),
                                   (109, CURRENT_TIMESTAMP, 'old fork budget batch');
                        INSERT INTO RemotePathMappings (Id, DownloadClientId, Host, RemotePath, LocalPath)
                            VALUES (1, 0, 'download.example', '/remote/usenet/completed/', '/downloads/'),
                                   (2, 0, 'download.example', '/remote/usenet/test/', '/test/');
                    ");
                }

                migrations.Migrate(connectionString, new MigrationContext(MigrationType.Main), DatabaseType.SQLite);
                if (!TableMapping.Mapper.TableMap.ContainsKey(typeof(RemotePathMapping)))
                {
                    TableMapping.Map();
                }

                var database = new MainDatabase(new Database("rpm-upgrade", Open));
                var repository = new RemotePathMappingRepository(database, null);
                var service = new RemotePathMappingService(null, repository, null, new CacheManager(), logger);
                Assert.Multiple(() =>
                {
                    Assert.That(repository.All().Count(), Is.EqualTo(2));
                    Assert.That(service.All().Select(m => m.RemotePath), Is.EquivalentTo(new[]
                    {
                        "/remote/usenet/completed/", "/remote/usenet/test/"
                    }));
                });
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
    }
}
