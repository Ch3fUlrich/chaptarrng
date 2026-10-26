using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dapper;
using DryIoc;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using NLog;
using NUnit.Framework;
using NzbDrone.Common.Composition.Extensions;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Common.Options;
using NzbDrone.Core.Books.Services;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Datastore.Extensions;
using NzbDrone.Core.Datastore.Migration.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.Http;
using NzbDrone.Core.Organizer;
using NzbDrone.Core.RemotePathMappings;
using NzbDrone.Core.RootFolders;
using NzbDrone.Host;

namespace Chaptarr.Core.Test.Composition
{
    // Regression guard for the production DI scan: the container the host builds from
    // Bootstrap.ASSEMBLIES + AutoAddServices must resolve service interfaces to their real
    // implementations. A nested stub (NoopRemotePathMappingService) used to be picked up by
    // the assembly scan and shadow RemotePathMappingService, so the published binary served
    // empty remote path mappings while download clients and root folders kept working.
    [TestFixture]
    public class ContainerServiceRegistrationFixture
    {
        private IContainer _container;
        private string _dbPath;
        private string _localDir;
        private string _connectionString;

        [SetUp]
        public void Setup()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"container_rpm_{Guid.NewGuid():N}.db");
            _connectionString = new SqliteConnectionStringBuilder { DataSource = _dbPath, Pooling = false }.ToString();

            _localDir = Path.Combine(Path.GetTempPath(), $"container_rpm_local_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_localDir);

            var logger = LogManager.GetLogger(nameof(ContainerServiceRegistrationFixture));
            var migrations = new MigrationController(logger, null);
            migrations.Migrate(_connectionString, new MigrationContext(MigrationType.Main), DatabaseType.SQLite);

            if (!TableMapping.Mapper.TableMap.ContainsKey(typeof(RemotePathMapping)))
            {
                TableMapping.Map();
            }

            _container = BuildHostLikeContainer();
        }

        [TearDown]
        public void TearDown()
        {
            _container?.Dispose();

            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }

            if (Directory.Exists(_localDir))
            {
                Directory.Delete(_localDir, true);
            }
        }

        // Mirrors Bootstrap.CreateConsoleHostBuilder's container setup: same rules, same
        // assembly scan (Bootstrap.ASSEMBLIES), same registration chain. Only the IDbFactory
        // binding differs so the database is a temp sqlite file instead of the app-data one.
        private IContainer BuildHostLikeContainer()
        {
            var container = new Container(rules => rules.WithNzbDroneRules());

            container.AutoAddServices(Bootstrap.ASSEMBLIES);
            container.RegisterInstance<IDbFactory>(new TestDbFactory(_connectionString), ifAlreadyRegistered: IfAlreadyRegistered.Replace);
            container.RegisterInstance<IServiceProvider>(new ResolverServiceProvider(container));

            // What Bootstrap's ConfigureServices(config => ...) provides through the MS DI
            // bridge in the host: the typed options bound from the configuration sections.
            container.RegisterInstance<IOptions<PostgresOptions>>(Options.Create(new PostgresOptions()));
            container.RegisterInstance<IOptions<AppOptions>>(Options.Create(new AppOptions()));
            container.RegisterInstance<IOptions<AuthOptions>>(Options.Create(new AuthOptions()));
            container.RegisterInstance<IOptions<ServerOptions>>(Options.Create(new ServerOptions()));
            container.RegisterInstance<IOptions<LogOptions>>(Options.Create(new LogOptions()));
            container.RegisterInstance<IOptions<UpdateOptions>>(Options.Create(new UpdateOptions()));

            container.RegisterHttpClients()
                .AddIndexerProxyProvider()
                .AddNzbDroneLogger()
                .AddDatabase()
                .AddFuzzyMatchingServices()
                .AddImportServices()
                .AddNamingPatternServices()
                .AddStartupContext(new StartupContext());

            return container;
        }

        private static List<Type> ImplTypes<T>(IContainer container)
        {
            return container.ResolveMany<T>().Select(s => s.GetType()).ToList();
        }

        [Test]
        public void should_resolve_the_real_remote_path_mapping_service()
        {
            var service = _container.Resolve<IRemotePathMappingService>();

            Assert.That(service, Is.TypeOf<RemotePathMappingService>(),
                $"DI resolved IRemotePathMappingService to {service.GetType().FullName}");
        }

        [Test]
        public void should_resolve_the_working_endpoints_services_the_same_way()
        {
            // These back the download client (GET /api/v1/downloadclient) and root folder
            // (GET /api/v1/rootfolder) endpoints, which kept working when the mappings
            // endpoint did not.
            Assert.That(_container.Resolve<IDownloadClientFactory>(), Is.TypeOf<DownloadClientFactory>());
            Assert.That(_container.Resolve<IRootFolderService>(), Is.TypeOf<RootFolderService>());
        }

        [Test]
        public void should_register_exactly_one_implementation_per_service_interface()
        {
            Assert.That(ImplTypes<IRemotePathMappingService>(_container),
                Is.EqualTo(new[] { typeof(RemotePathMappingService) }),
                "IRemotePathMappingService must only be implemented by RemotePathMappingService in the container");

            Assert.That(ImplTypes<IDownloadClientFactory>(_container),
                Is.EqualTo(new[] { typeof(DownloadClientFactory) }),
                "IDownloadClientFactory must only be implemented by DownloadClientFactory in the container");

            Assert.That(ImplTypes<IRootFolderService>(_container),
                Is.EqualTo(new[] { typeof(RootFolderService) }),
                "IRootFolderService must only be implemented by RootFolderService in the container");
        }

        [Test]
        public void resolved_remote_path_mapping_service_should_read_and_write_the_database()
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                connection.Execute(
                    @"INSERT INTO RemotePathMappings (Id, DownloadClientId, Host, RemotePath, LocalPath)
                      VALUES (1, 0, 'download.example', '/remote/completed/', '/downloads/')");
            }

            var service = _container.Resolve<IRemotePathMappingService>();

            Assert.That(service.All().Select(m => m.RemotePath), Is.EqualTo(new[] { "/remote/completed/" }),
                "All() must return rows present in the database");
            Assert.That(service.Get(1), Is.Not.Null, "Get(1) must return the inserted row");

            service.Add(new RemotePathMapping
            {
                DownloadClientId = 0,
                Host = "download.example",
                RemotePath = "/remote/queued/",
                LocalPath = _localDir
            });

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                Assert.That(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM RemotePathMappings"), Is.EqualTo(2),
                    "Add() must persist the mapping");
            }
        }

        private sealed class TestDbFactory : IDbFactory
        {
            private readonly string _connectionString;

            public TestDbFactory(string connectionString)
            {
                _connectionString = connectionString;
            }

            // Log/Cache databases are never resolved by these tests; AddDatabase() still binds
            // IMainDatabase exactly like the host does.
            public IDatabase Create(MigrationType migrationType = MigrationType.Main)
            {
                return CreateMain();
            }

            public IDatabase Create(MigrationContext migrationContext)
            {
                return CreateMain();
            }

            private IDatabase CreateMain()
            {
                return new Database("container-rpm", () => new SqliteConnection(_connectionString));
            }
        }

        private sealed class ResolverServiceProvider : IServiceProvider
        {
            private readonly IResolver _resolver;

            public ResolverServiceProvider(IResolver resolver)
            {
                _resolver = resolver;
            }

            public object GetService(Type serviceType)
            {
                return _resolver.Resolve(serviceType, ifUnresolved: IfUnresolved.ReturnDefault);
            }
        }
    }
}
