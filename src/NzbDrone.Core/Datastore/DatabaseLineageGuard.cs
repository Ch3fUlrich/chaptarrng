using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using Microsoft.Data.Sqlite;
using Npgsql;

namespace NzbDrone.Core.Datastore
{
    /// <summary>
    /// Legacy filenames are also used by unrelated Readarr/Bookshelf databases.
    /// Reject those before numbered migrations or rebased-schema repairs run.
    /// This check does not convert a legacy database.
    /// </summary>
    public static class DatabaseLineageGuard
    {
        public static void Validate(string connectionString, DatabaseType databaseType)
        {
            using DbConnection connection = OpenReadConnection(connectionString, databaseType);
            if (connection == null)
            {
                return;
            }

            using var command = connection.CreateCommand();
            command.CommandText = databaseType == DatabaseType.SQLite
                ? "PRAGMA table_info(\"Authors\");"
                : "SELECT column_name FROM information_schema.columns WHERE table_schema = current_schema() AND table_name = 'Authors';";

            var columns = new HashSet<string>(StringComparer.Ordinal);
            using (var reader = command.ExecuteReader())
            {
                var ordinal = databaseType == DatabaseType.SQLite ? reader.GetOrdinal("name") : 0;
                while (reader.Read())
                {
                    columns.Add(reader.GetString(ordinal));
                }
            }

            // No Authors table: fresh database. Both roots existed in the earliest
            // Chaptarr baseline, including installations using legacy DB filenames.
            if (columns.Count > 0 &&
                (!columns.Contains("AudiobookRootFolderPath") || !columns.Contains("EbookRootFolderPath")))
            {
                throw new InvalidDataException("This database uses a Readarr/Bookshelf schema, not a Chaptarr schema. " +
                    "Chaptarr migrations have not been run. Keep the original database and use a verified BookshelfNG conversion before switching applications.");
            }
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
