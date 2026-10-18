using System;
using System.IO;
using System.Linq;
using NLog;
using NLog.Extensions.Logging;
using NzbDrone.Common;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Processes;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Datastore.Migration.Framework;
using NzbDrone.Host.AccessControl;
using IServiceProvider = NzbDrone.Common.IServiceProvider;

namespace NzbDrone.Host
{
    public interface IUtilityModeRouter
    {
        void Route(ApplicationModes applicationModes);
    }

    public class UtilityModeRouter : IUtilityModeRouter
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConsoleService _consoleService;
        private readonly IProcessProvider _processProvider;
        private readonly IRemoteAccessAdapter _remoteAccessAdapter;
        private readonly IAppFolderFactory _appFolderFactory;
        private readonly IConnectionStringFactory _connectionStringFactory;
        private readonly IStartupContext _startupContext;
        private readonly Logger _logger;

        public UtilityModeRouter(IServiceProvider serviceProvider,
                      IConsoleService consoleService,
                      IProcessProvider processProvider,
                      IRemoteAccessAdapter remoteAccessAdapter,
                      IAppFolderFactory appFolderFactory,
                      IConnectionStringFactory connectionStringFactory,
                      IStartupContext startupContext,
                      Logger logger)
        {
            _serviceProvider = serviceProvider;
            _consoleService = consoleService;
            _processProvider = processProvider;
            _remoteAccessAdapter = remoteAccessAdapter;
            _appFolderFactory = appFolderFactory;
            _connectionStringFactory = connectionStringFactory;
            _startupContext = startupContext;
            _logger = logger;
        }

        public void Route(ApplicationModes applicationModes)
        {
            _logger.Info("Application mode: {0}", applicationModes);

            switch (applicationModes)
            {
                case ApplicationModes.InstallService:
                    {
                        _logger.Debug("Install Service selected");
                        if (_serviceProvider.ServiceExist(ServiceProvider.SERVICE_NAME))
                        {
                            _consoleService.PrintServiceAlreadyExist();
                        }
                        else
                        {
                            _remoteAccessAdapter.MakeAccessible(true);
                            _serviceProvider.Install(ServiceProvider.SERVICE_NAME);
                            _serviceProvider.SetPermissions(ServiceProvider.SERVICE_NAME);

                            // Start the service and exit.
                            // Ensures that there isn't an instance of Chaptarr already running that the service account cannot stop.
                            _processProvider.SpawnNewProcess("sc.exe", $"start {ServiceProvider.SERVICE_NAME}", null, true);
                        }

                        break;
                    }

                case ApplicationModes.UninstallService:
                    {
                        _logger.Debug("Uninstall Service selected");
                        if (!_serviceProvider.ServiceExist(ServiceProvider.SERVICE_NAME))
                        {
                            _consoleService.PrintServiceDoesNotExist();
                        }
                        else
                        {
                            _serviceProvider.Uninstall(ServiceProvider.SERVICE_NAME);
                        }

                        break;
                    }

                case ApplicationModes.RegisterUrl:
                    {
                        _logger.Debug("Regiser URL selected");
                        _remoteAccessAdapter.MakeAccessible(false);
                        _appFolderFactory.SetPermissions();

                        break;
                    }

                case ApplicationModes.MigrateBookshelf:
                    {
                        _appFolderFactory.Register();
                        var database = _connectionStringFactory.MainDbConnection;
                        if (database.DatabaseType != DatabaseType.SQLite ||
                            DatabaseLineageGuard.Detect(database.ConnectionString, database.DatabaseType) != DatabaseLineage.BookshelfNG)
                        {
                            throw new InvalidDataException("No supported BookshelfNG SQLite database was found in this data directory. No database was changed.");
                        }

                        var previewOnly = _startupContext.PreviewBookshelfMigration;
                        if (!previewOnly && !_startupContext.ConfirmBookshelfServiceStopped)
                        {
                            throw new InvalidOperationException(
                                "Applying a BookshelfNG migration requires -confirm-bookshelf-service-stopped. Stop every process using this data directory first.");
                        }

                        var migrationController = new MigrationController(_logger, new NLogLoggerProvider());
                        var result = migrationController.MigrateBookshelf(
                            database.ConnectionString,
                            new MigrationContext(MigrationType.Main),
                            database.DatabaseType,
                            previewOnly);

                        Console.WriteLine(previewOnly
                            ? "BookshelfNG migration preview passed. The source database was not changed."
                            : $"BookshelfNG migration completed. Backup retained at: {result.BackupPath}");
                        Console.WriteLine("Source rows by table: source | preserved | transformed | rebuilt | reset");
                        foreach (var table in result.ImportReport.Tables.OrderBy(item => item.Key, StringComparer.Ordinal))
                        {
                            var row = table.Value;
                            Console.WriteLine($"{table.Key}: {row.SourceRows} | {row.PreservedRows} | {row.TransformedRows} | {row.RebuiltRows} | {row.ResetRows} — {row.TargetMapping}");
                        }

                        break;
                    }

                case ApplicationModes.RestoreBookshelf:
                    {
                        _appFolderFactory.Register();
                        var database = _connectionStringFactory.MainDbConnection;
                        if (database.DatabaseType != DatabaseType.SQLite ||
                            DatabaseLineageGuard.Detect(database.ConnectionString, database.DatabaseType) != DatabaseLineage.Chaptarr)
                        {
                            throw new InvalidDataException("BookshelfNG rollback requires a valid ChaptarrNG SQLite database in this data directory.");
                        }

                        if (!_startupContext.Args.TryGetValue("bookshelf-backup", out var backupPath) ||
                            string.IsNullOrWhiteSpace(backupPath))
                        {
                            throw new InvalidOperationException("BookshelfNG rollback requires -bookshelf-backup=/path/to/readarr.db.bookshelfng-backup-....db.");
                        }

                        var previewOnly = _startupContext.PreviewBookshelfRestore;
                        if (!previewOnly && !_startupContext.ConfirmBookshelfServiceStopped)
                        {
                            throw new InvalidOperationException(
                                "Restoring a BookshelfNG database requires -confirm-bookshelf-service-stopped. Stop every process using this data directory first.");
                        }

                        var migrationController = new MigrationController(_logger, new NLogLoggerProvider());
                        var result = new BookshelfDatabaseConverter().Restore(
                            database.ConnectionString,
                            backupPath,
                            previewOnly,
                            migrationController.MigrateBookshelfSource);
                        Console.WriteLine(previewOnly
                            ? $"BookshelfNG rollback preview passed. The current ChaptarrNG database and source backup were not changed: {result.BookshelfBackupPath}"
                            : $"BookshelfNG database restored from: {result.BookshelfBackupPath}");
                        if (!previewOnly)
                        {
                            Console.WriteLine($"The previous ChaptarrNG database, including post-migration writes, is retained at: {result.ChaptarrBackupPath}");
                            Console.WriteLine("Start only BookshelfNG against this restored database. Do not start ChaptarrNG until you intentionally convert again.");
                        }

                        break;
                    }

                default:
                    {
                        _consoleService.PrintHelp();
                        break;
                    }
            }
        }
    }
}
