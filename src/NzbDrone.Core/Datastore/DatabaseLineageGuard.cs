using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using Microsoft.Data.Sqlite;
using Npgsql;

namespace NzbDrone.Core.Datastore
{
    public enum DatabaseLineage
    {
        Empty,
        Chaptarr,
        BookshelfNG,
        Unsupported
    }

    /// <summary>
    /// Legacy filenames are also used by unrelated Readarr/Bookshelf databases.
    /// Reject those before numbered migrations or rebased-schema repairs run.
    /// This check does not convert a legacy database.
    /// </summary>
    public static class DatabaseLineageGuard
    {
        public static void Validate(string connectionString, DatabaseType databaseType)
        {
            var lineage = Detect(connectionString, databaseType);
            if (lineage is DatabaseLineage.Empty or DatabaseLineage.Chaptarr)
            {
                if (databaseType == DatabaseType.SQLite)
                {
                    ValidateNoLegacyDatabaseBesideNative(connectionString);
                }

                return;
            }

            if (lineage == DatabaseLineage.BookshelfNG)
            {
                throw new InvalidDataException("This database uses a BookshelfNG schema, not a Chaptarr schema. " +
                    "Chaptarr migrations have not been run. Keep the original database and use a verified BookshelfNG conversion before switching applications.");
            }

            throw new InvalidDataException("This database does not use a supported Chaptarr schema. Chaptarr migrations have not been run.");
        }

        private static void ValidateNoLegacyDatabaseBesideNative(string connectionString)
        {
            var nativeBuilder = new SqliteConnectionStringBuilder(connectionString);
            if (nativeBuilder.Mode == SqliteOpenMode.Memory ||
                string.IsNullOrWhiteSpace(nativeBuilder.DataSource) ||
                string.Equals(nativeBuilder.DataSource, ":memory:", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var nativePath = Path.GetFullPath(nativeBuilder.DataSource);
            if (!string.Equals(Path.GetFileName(nativePath), "chaptarr.db", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var directory = Path.GetDirectoryName(nativePath);
            foreach (var legacyName in new[] { "readarr.db", "audioarr.db" })
            {
                var legacyPath = Path.Combine(directory, legacyName);
                if (!File.Exists(legacyPath) || new FileInfo(legacyPath).Length == 0)
                {
                    continue;
                }

                DatabaseLineage legacyLineage;
                try
                {
                    var legacyConnectionString = new SqliteConnectionStringBuilder
                    {
                        DataSource = legacyPath,
                        Mode = SqliteOpenMode.ReadOnly,
                        Pooling = false
                    }.ConnectionString;
                    legacyLineage = Detect(legacyConnectionString, DatabaseType.SQLite);
                }
                catch (Exception exception) when (exception is SqliteException or IOException or InvalidOperationException)
                {
                    throw new InvalidDataException(
                        $"A Chaptarr database and an unreadable legacy database ({legacyName}) both exist in this app-data directory. " +
                        "Startup was stopped so neither file is silently ignored. Keep both files unchanged and resolve the legacy database before restarting.",
                        exception);
                }

                if (legacyLineage != DatabaseLineage.Empty)
                {
                    throw new InvalidDataException(
                        $"A Chaptarr database and a legacy database ({legacyName}) both exist in this app-data directory. " +
                        "Startup was stopped so the legacy database is not silently ignored. Keep both files unchanged and complete a verified conversion before restarting.");
                }
            }
        }

        public static DatabaseLineage Detect(string connectionString, DatabaseType databaseType)
        {
            using DbConnection connection = OpenReadConnection(connectionString, databaseType);
            if (connection == null)
            {
                return DatabaseLineage.Empty;
            }

            var tables = GetTables(connection, databaseType);
            if (tables.Count == 0)
            {
                return DatabaseLineage.Empty;
            }

            if (!tables.Contains("Authors"))
            {
                return DatabaseLineage.Unsupported;
            }

            var authorColumns = GetColumns(connection, databaseType, "Authors");
            if (authorColumns.Contains("AudiobookRootFolderPath") && authorColumns.Contains("EbookRootFolderPath"))
            {
                return DatabaseLineage.Chaptarr;
            }

            if (tables.Contains("AuthorMetadata") && tables.Contains("Books"))
            {
                var bookColumns = GetColumns(connection, databaseType, "Books");
                if (authorColumns.Contains("AuthorMetadataId") &&
                    authorColumns.Contains("EbookPath") &&
                    authorColumns.Contains("AudiobookPath") &&
                    bookColumns.Contains("ForeignBookId"))
                {
                    return DatabaseLineage.BookshelfNG;
                }
            }

            return DatabaseLineage.Unsupported;
        }

        private static HashSet<string> GetTables(DbConnection connection, DatabaseType databaseType)
        {
            using var command = connection.CreateCommand();
            command.CommandText = databaseType == DatabaseType.SQLite
                ? "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%';"
                : "SELECT table_name FROM information_schema.tables WHERE table_schema = current_schema() AND table_type = 'BASE TABLE';";

            var tables = new HashSet<string>(StringComparer.Ordinal);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                tables.Add(reader.GetString(0));
            }

            return tables;
        }

        private static HashSet<string> GetColumns(DbConnection connection, DatabaseType databaseType, string tableName)
        {
            using var command = connection.CreateCommand();
            command.CommandText = databaseType == DatabaseType.SQLite
                ? $"PRAGMA table_info(\"{tableName}\");"
                : "SELECT column_name FROM information_schema.columns WHERE table_schema = current_schema() AND table_name = @tableName;";

            if (databaseType == DatabaseType.PostgreSQL)
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = "tableName";
                parameter.Value = tableName;
                command.Parameters.Add(parameter);
            }

            var columns = new HashSet<string>(StringComparer.Ordinal);
            using (var reader = command.ExecuteReader())
            {
                var ordinal = databaseType == DatabaseType.SQLite ? reader.GetOrdinal("name") : 0;
                while (reader.Read())
                {
                    columns.Add(reader.GetString(ordinal));
                }
            }

            return columns;
        }

        private static DbConnection OpenReadConnection(string connectionString, DatabaseType databaseType)
        {
            if (databaseType == DatabaseType.SQLite)
            {
                var builder = new SqliteConnectionStringBuilder(connectionString);
                if (builder.Mode == SqliteOpenMode.Memory || builder.DataSource == ":memory:")
                {
                    return null;
                }

                if (!File.Exists(builder.DataSource))
                {
                    return null;
                }

                builder.Mode = SqliteOpenMode.ReadOnly;
                builder.Pooling = false;
                var connection = new SqliteConnection(builder.ToString());
                try
                {
                    connection.Open();
                    return connection;
                }
                catch (SqliteException exception) when (exception.SqliteErrorCode == 14)
                {
                    connection.Dispose();

                    // A standalone WAL database can lack its -wal/-shm sidecars after the
                    // last writer closes. Some SQLite builds cannot open that file read-only
                    // even when the directory is writable. Retry without Create semantics,
                    // then make the metadata probe query-only before issuing any query.
                    var queryOnlyBuilder = new SqliteConnectionStringBuilder(builder.ConnectionString)
                    {
                        Mode = SqliteOpenMode.ReadWrite,
                        Pooling = false
                    };
                    var queryOnlyConnection = new SqliteConnection(queryOnlyBuilder.ToString());
                    try
                    {
                        queryOnlyConnection.Open();
                        using var queryOnly = queryOnlyConnection.CreateCommand();
                        queryOnly.CommandText = "PRAGMA query_only=ON;";
                        queryOnly.ExecuteNonQuery();
                        return queryOnlyConnection;
                    }
                    catch
                    {
                        queryOnlyConnection.Dispose();
                        throw;
                    }
                }
                catch
                {
                    connection.Dispose();
                    throw;
                }
            }

            var postgres = new NpgsqlConnection(connectionString);
            try
            {
                postgres.Open();
                return postgres;
            }
            catch
            {
                postgres.Dispose();
                throw;
            }
        }

    }
}
