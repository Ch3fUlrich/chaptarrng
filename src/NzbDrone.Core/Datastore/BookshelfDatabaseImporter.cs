using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NzbDrone.Core.Datastore
{
    /// <summary>
    /// Imports the latest BookshelfNG SQLite schema into a freshly migrated Chaptarr database.
    /// Unknown schema versions, tables, providers, and ambiguous relationships fail closed.
    /// </summary>
    public static class BookshelfDatabaseImporter
    {
        private const int SupportedSourceMigration = 158;

        private static readonly HashSet<string> SupportedSourceTables = new(StringComparer.Ordinal)
        {
            "AuthorMetadata", "Authors", "Blocklist", "BookFiles", "Books", "Commands", "Config",
            "CustomFilters", "CustomFormats", "DelayProfiles", "DownloadClientStatus", "DownloadClients",
            "DownloadHistory", "Editions", "ExtraFiles", "History", "ImportListExclusions",
            "ImportListStatus", "ImportLists", "IndexerStatus", "Indexers", "Metadata", "MetadataFiles",
            "MetadataProfiles", "NamingConfig", "NotificationStatus", "Notifications", "PendingReleases",
            "QualityDefinitions", "QualityProfiles", "ReleaseProfiles", "RemotePathMappings", "RootFolders",
            "ScheduledTasks", "Series", "SeriesBookLink", "Tags", "Users", "VersionInfo"
        };

        private static readonly string[] TargetTablesToClear =
        {
            "ProviderAliasIndex", "AuthorSeries", "SeriesBookLink", "EditionNarratorLink", "BookNarratorLink",
            "ExtraFiles", "OtherFiles", "MetadataFiles", "BookFiles", "Editions", "Books", "Authors", "Series",
            "Narrators", "NarratorMetadata", "ImportListExclusions", "ImportListStatus", "ImportLists", "Indexers",
            "IndexerStatus", "Notifications", "NotificationStatus", "DownloadClients", "DownloadClientStatus", "Metadata",
            "MetadataProfiles", "NamingConfig", "QualityProfiles", "QualityDefinitions", "CustomFormats", "CustomFilters",
            "DelayProfiles", "ReleaseProfiles", "RemotePathMappings", "RootFolders", "Tags", "Users", "History",
            "DownloadHistory", "Blocklist", "PendingReleases", "ScheduledTasks", "Commands", "Config"
        };

        public static BookshelfDatabaseImportReport Import(
            SqliteConnection source,
            SqliteConnection target,
            SqliteTransaction transaction,
            string sourceDatabasePath)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));

            ValidateSourceSchema(source);
            var mediaChoices = BookshelfBookMediaMapping.Load(source, sourceDatabasePath);
            DeferForeignKeys(target, transaction);
            ClearTarget(target, transaction);

            var report = new BookshelfDatabaseImportReport();
            var qualityProfiles = ImportQualityProfiles(source, target, transaction, report);
            var metadataProfiles = ImportMetadataProfiles(source, target, transaction, report);
            var rootFolders = ImportRootFolders(source, target, transaction, qualityProfiles, metadataProfiles, report);
            var authors = ImportAuthors(source, target, transaction, qualityProfiles, metadataProfiles, rootFolders, report);
            var bookMediaTypes = ReadBookMediaTypes(source);
            var books = ImportBooks(source, target, transaction, authors, bookMediaTypes, mediaChoices, report);
            var editions = ImportEditions(source, target, transaction, books, report);
            var files = ImportBookFiles(source, target, transaction, editions, report);

            ImportExtraFiles(source, target, transaction, books, files, report);
            ImportMetadataFiles(source, target, transaction, books, files, report);
            ReserveOrphanBookFileIds(source, target, transaction);
            var series = ImportSeries(source, target, transaction, report);
            ImportSeriesLinks(source, target, transaction, books, series, report);
            RebuildAuthorSeries(target, transaction);
            ImportBlocklist(source, target, transaction, authors, books, report);
            ImportDownloadHistory(source, target, transaction, report);

            CopyTable(source, target, transaction, report, "Config");
            CopyTable(source, target, transaction, report, "CustomFilters");
            CopyTable(source, target, transaction, report, "CustomFormats");
            CopyTable(source, target, transaction, report, "DelayProfiles");
            CopyTable(source, target, transaction, report, "DownloadClients");
            ImportHistory(source, target, transaction, authors, books, files, report);
            CopyTable(source, target, transaction, report, "ImportListExclusions");
            ImportLists(source, target, transaction, qualityProfiles, metadataProfiles, report);
            CopyTable(source, target, transaction, report, "Indexers", row =>
            {
                SetIfPresent(row, "ConfigContract", "");
                // These are implementation capabilities, not user preferences. The runtime
                // provider factory recalculates them from the installed Chaptarr provider.
                SetIfPresent(row, "SupportsRss", false);
                SetIfPresent(row, "SupportsSearch", false);
            });
            CopyTable(source, target, transaction, report, "Metadata");
            CopyTable(source, target, transaction, report, "QualityDefinitions");
            CopyTable(source, target, transaction, report, "ReleaseProfiles");
            CopyTable(source, target, transaction, report, "RemotePathMappings");
            CopyTable(source, target, transaction, report, "Tags");
            CopyTable(source, target, transaction, report, "Users");
            ImportNotifications(source, target, transaction, report);
            ImportNamingConfig(source, target, transaction, report);

            ResetTable(source, report, "Commands", "Command queue and execution state is transient and will be recreated by Chaptarr.");
            ResetTable(source, report, "DownloadClientStatus", "Client health backoff is transient and will be probed again.");
            ResetTable(source, report, "ImportListStatus", "Import-list health backoff is transient and will be probed again.");
            ResetTable(source, report, "IndexerStatus", "Indexer health and RSS cursors are rebuilt by Chaptarr.");
            ResetTable(source, report, "NotificationStatus", "Notification health backoff is transient and will be probed again.");
            ResetTable(source, report, "PendingReleases", "Pending search results are transient and are not portable across implementations.");
            ResetTable(source, report, "ScheduledTasks", "Task schedule state is recreated by Chaptarr.");
            ResetTable(source, report, "VersionInfo", "Chaptarr migration history was created before importing source rows.");

            RebuildProviderAliases(target, transaction, authors, books, editions, series);
            return report;
        }

        internal static void ValidateSourceSchema(SqliteConnection source)
        {
            var tables = GetTables(source);
            var missing = SupportedSourceTables.Except(tables, StringComparer.Ordinal).OrderBy(x => x).ToArray();
            var unknown = tables.Except(SupportedSourceTables, StringComparer.Ordinal).OrderBy(x => x).ToArray();
            if (missing.Length > 0 || unknown.Length > 0)
            {
                throw new InvalidDataException(
                    "BookshelfNG database table set is not supported (missing: " + string.Join(", ", missing) +
                    "; unknown: " + string.Join(", ", unknown) + "). The source database was not changed.");
            }

            using var command = source.CreateCommand();
            command.CommandText = "SELECT COALESCE(MAX(Version), 0) FROM VersionInfo;";
            var version = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            if (version != SupportedSourceMigration)
            {
                throw new InvalidDataException(
                    $"BookshelfNG schema version {version} is not supported; this converter supports version {SupportedSourceMigration}. Update BookshelfNG first. The source database was not changed.");
            }
        }

        internal static int ValidateSourceBackup(SqliteConnection source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var tables = GetTables(source);
            var unknown = tables.Except(SupportedSourceTables, StringComparer.Ordinal).OrderBy(x => x).ToArray();
            if (unknown.Length > 0 || !tables.Contains("VersionInfo"))
            {
                throw new InvalidDataException(
                    "BookshelfNG backup table set cannot be upgraded by the bundled migration chain (unknown: " +
                    string.Join(", ", unknown) + "). The source database was not changed.");
            }

            using var command = source.CreateCommand();
            command.CommandText = "SELECT COALESCE(MAX(Version), 0) FROM VersionInfo;";
            var version = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            if (version < 0 || version > SupportedSourceMigration)
            {
                throw new InvalidDataException(
                    $"BookshelfNG schema version {version} is newer than the bundled migration chain (latest {SupportedSourceMigration}). The source database was not changed.");
            }

            return version;
        }

        private static void DeferForeignKeys(SqliteConnection target, SqliteTransaction transaction)
        {
            using var command = target.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "PRAGMA defer_foreign_keys = ON;";
            command.ExecuteNonQuery();
        }

        private static void ClearTarget(SqliteConnection target, SqliteTransaction transaction)
        {
            foreach (var table in TargetTablesToClear)
            {
                if (!TableExists(target, table))
                {
                    continue;
                }

                using var command = target.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = $"DELETE FROM {Quote(table)};";
                command.ExecuteNonQuery();
            }
        }

        private static Dictionary<(int SourceId, int MediaType), int> ImportQualityProfiles(
            SqliteConnection source,
            SqliteConnection target,
            SqliteTransaction transaction,
            BookshelfDatabaseImportReport report)
        {
            var sourceCount = CountRows(source, "QualityProfiles");
            var targetColumns = GetColumns(target, "QualityProfiles");
            var map = new Dictionary<(int SourceId, int MediaType), int>();
            var nextId = Convert.ToInt32(Scalar(source, "SELECT COALESCE(MAX(Id), 0) + 1 FROM QualityProfiles;"), CultureInfo.InvariantCulture);

            foreach (var row in ReadRows(source, "QualityProfiles"))
            {
                var sourceId = GetInt(row, "Id");
                var audiobookId = sourceId;
                var ebookId = nextId++;

                foreach (var (mediaType, targetId) in new[] { (1, audiobookId), (2, ebookId) })
                {
                    var values = Intersect(row, targetColumns);
                    values["Id"] = targetId;
                    values["ProfileType"] = mediaType;
                    InsertRow(target, transaction, "QualityProfiles", targetColumns, values);
                }

                map[(sourceId, 0)] = audiobookId;
                map[(sourceId, 1)] = ebookId;
            }

            report.RecordTable("QualityProfiles", sourceCount, 0, sourceCount, 0, 0,
                "each generic BookshelfNG profile is cloned as Chaptarr audiobook and ebook profiles");
            return map;
        }

        private static Dictionary<int, int> ImportMetadataProfiles(
            SqliteConnection source,
            SqliteConnection target,
            SqliteTransaction transaction,
            BookshelfDatabaseImportReport report)
        {
            var sourceCount = CountRows(source, "MetadataProfiles");
            var targetColumns = GetColumns(target, "MetadataProfiles");
            var map = new Dictionary<int, int>();

            foreach (var row in ReadRows(source, "MetadataProfiles"))
            {
                var values = Intersect(row, targetColumns);
                values["ProfileType"] = 0;
                SetIfPresent(values, "Ignored", "[]");
                SetIfPresent(values, "IgnoredGenres", "[]");
                InsertRow(target, transaction, "MetadataProfiles", targetColumns, values);
                map[GetInt(row, "Id")] = GetInt(row, "Id");
            }

            report.RecordTable("MetadataProfiles", sourceCount, 0, sourceCount, 0, 0,
                "BookshelfNG general metadata profiles remain general Chaptarr profiles");
            return map;
        }

        private static List<RootFolderMap> ImportRootFolders(
            SqliteConnection source,
            SqliteConnection target,
            SqliteTransaction transaction,
            IReadOnlyDictionary<(int SourceId, int MediaType), int> qualityProfiles,
            IReadOnlyDictionary<int, int> metadataProfiles,
            BookshelfDatabaseImportReport report)
        {
            var sourceCount = CountRows(source, "RootFolders");
            var targetColumns = GetColumns(target, "RootFolders");
            var result = new List<RootFolderMap>();

            foreach (var row in ReadRows(source, "RootFolders"))
            {
                var values = Intersect(row, targetColumns);
                var qualityId = GetNullableInt(row, "DefaultQualityProfileId");
                var metadataId = GetNullableInt(row, "DefaultMetadataProfileId");
                var monitorExisting = GetNullableInt(row, "DefaultMonitorOption");
                var monitorNew = GetNullableInt(row, "DefaultNewItemMonitorOption");
                var tags = GetString(row, "DefaultTags");

                values["FolderType"] = 0;
                values["AudiobookSettings"] = BuildRootSettings(qualityId, metadataId, monitorExisting, monitorNew, tags, 0, qualityProfiles, metadataProfiles);
                values["EbookSettings"] = BuildRootSettings(qualityId, metadataId, monitorExisting, monitorNew, tags, 1, qualityProfiles, metadataProfiles);
                SetIfPresent(values, "PlaceEbooksWithAudiobooks", false);
                SetIfPresent(values, "DefaultSyncMonitoredAcrossFormats", true);

                InsertRow(target, transaction, "RootFolders", targetColumns, values);
                result.Add(new RootFolderMap(GetString(row, "Path")));
            }

            report.RecordTable("RootFolders", sourceCount, 0, sourceCount, 0, 0,
                "root paths and Calibre settings are preserved; legacy defaults are moved into per-media Chaptarr settings");
            return result;
        }

        private static string BuildRootSettings(
            int? sourceQualityId,
            int? sourceMetadataId,
            int? sourceMonitorExisting,
            int? sourceMonitorNew,
            string tags,
            int mediaType,
            IReadOnlyDictionary<(int SourceId, int MediaType), int> qualityProfiles,
            IReadOnlyDictionary<int, int> metadataProfiles)
        {
            var settings = new JObject();
            if (sourceQualityId.GetValueOrDefault() > 0)
            {
                settings["QualityProfileId"] = MapQualityProfile(qualityProfiles, sourceQualityId.Value, mediaType, "root-folder quality");
            }

            if (sourceMetadataId.GetValueOrDefault() > 0)
            {
                settings["MetadataProfileId"] = MapMetadataProfile(metadataProfiles, sourceMetadataId.Value, "root-folder metadata");
            }

            if (sourceMonitorExisting.HasValue)
            {
                settings["MonitorExistingMode"] = RemapMonitorType(sourceMonitorExisting.Value);
            }

            if (sourceMonitorNew.HasValue)
            {
                settings["MonitorNewItems"] = sourceMonitorNew.Value;
            }

            if (!string.IsNullOrWhiteSpace(tags))
            {
                settings["Tags"] = JToken.Parse(tags);
            }

            return settings.HasValues ? settings.ToString(Formatting.None) : null;
        }

        private static Dictionary<int, AuthorMap> ImportAuthors(
            SqliteConnection source,
            SqliteConnection target,
            SqliteTransaction transaction,
            IReadOnlyDictionary<(int SourceId, int MediaType), int> qualityProfiles,
            IReadOnlyDictionary<int, int> metadataProfiles,
            IReadOnlyList<RootFolderMap> rootFolders,
            BookshelfDatabaseImportReport report)
        {
            var metadataRows = ReadRows(source, "AuthorMetadata").ToDictionary(x => GetInt(x, "Id"));
            var referencedMetadataIds = new HashSet<int>();
            var targetColumns = GetColumns(target, "Authors");
            var authors = new Dictionary<int, AuthorMap>();
            var metadataToAuthor = new Dictionary<int, int>();
            var sourceCount = CountRows(source, "Authors");

            foreach (var row in ReadRows(source, "Authors"))
            {
                var metadataId = GetInt(row, "AuthorMetadataId");
                if (!metadataRows.TryGetValue(metadataId, out var metadata))
                {
                    throw new InvalidDataException($"BookshelfNG author {GetInt(row, "Id")} references missing author metadata {metadataId}.");
                }

                if (!metadataToAuthor.TryAdd(metadataId, GetInt(row, "Id")))
                {
                    throw new InvalidDataException($"BookshelfNG author metadata {metadataId} is shared by multiple managed authors; Chaptarr cannot preserve that relationship unambiguously.");
                }

                referencedMetadataIds.Add(metadataId);
                var sourceId = GetInt(row, "Id");
                var canonicalId = NormalizeSourceId(GetString(metadata, "ForeignAuthorId"), "author");
                var values = Intersect(row, targetColumns);
                AddMetadataColumns(values, metadata);
                values["Id"] = sourceId;
                values["Name"] = GetString(metadata, "Name") ?? GetString(row, "CleanName") ?? "Unknown author";
                values["SortName"] = GetString(metadata, "SortName") ?? GetString(metadata, "Name") ?? "Unknown author";
                values["CleanName"] = GetString(row, "CleanName") ?? GetString(metadata, "Name") ?? "Unknown author";
                values["Path"] = GetString(row, "Path") ?? throw new InvalidDataException($"BookshelfNG author {sourceId} has no managed path.");
                values["Added"] = GetValue(row, "Added") ?? DateTime.UnixEpoch;
                values["Monitored"] = GetValue(row, "Monitored") ?? false;
                values["AudiobookMonitored"] = GetValue(row, "Monitored") ?? false;
                values["EbookMonitored"] = GetValue(row, "Monitored") ?? false;
                values["MonitorNewItems"] = GetValue(row, "MonitorNewItems") ?? 0;
                values["AudiobookMonitorNewItems"] = GetValue(row, "MonitorNewItems") ?? 0;
                values["EbookMonitorNewItems"] = GetValue(row, "MonitorNewItems") ?? 0;
                SetIfPresent(values, "AudiobookTags", GetValue(row, "Tags") ?? "[]");
                SetIfPresent(values, "EbookTags", GetValue(row, "Tags") ?? "[]");

                var qualityId = GetNullableInt(row, "QualityProfileId");
                var audioQualityId = GetNullableInt(row, "AudiobookQualityProfileId") ?? qualityId;
                var ebookQualityId = GetNullableInt(row, "EbookQualityProfileId") ?? qualityId;
                values["QualityProfileId"] = audioQualityId.HasValue ? MapQualityProfile(qualityProfiles, audioQualityId.Value, 0, "author quality") : 1;
                values["AudiobookQualityProfileId"] = audioQualityId.HasValue ? MapQualityProfile(qualityProfiles, audioQualityId.Value, 0, "author audiobook quality") : null;
                values["EbookQualityProfileId"] = ebookQualityId.HasValue ? MapQualityProfile(qualityProfiles, ebookQualityId.Value, 1, "author ebook quality") : null;

                var metadataIdLegacy = GetNullableInt(row, "MetadataProfileId");
                var audioMetadataId = GetNullableInt(row, "AudiobookMetadataProfileId") ?? metadataIdLegacy;
                var ebookMetadataId = GetNullableInt(row, "EbookMetadataProfileId") ?? metadataIdLegacy;
                values["MetadataProfileId"] = metadataIdLegacy.HasValue ? MapMetadataProfile(metadataProfiles, metadataIdLegacy.Value, "author metadata") : null;
                values["AudiobookMetadataProfileId"] = audioMetadataId.HasValue ? MapMetadataProfile(metadataProfiles, audioMetadataId.Value, "author audiobook metadata") : null;
                values["EbookMetadataProfileId"] = ebookMetadataId.HasValue ? MapMetadataProfile(metadataProfiles, ebookMetadataId.Value, "author ebook metadata") : null;

                var audioPath = GetString(row, "AudiobookPath");
                var ebookPath = GetString(row, "EbookPath");
                values["AudiobookPath"] = audioPath;
                values["EbookPath"] = ebookPath;
                values["AudiobookRootFolderPath"] = FindRootFolder(audioPath ?? GetString(row, "Path"), rootFolders);
                values["EbookRootFolderPath"] = FindRootFolder(ebookPath ?? GetString(row, "Path"), rootFolders);
                SetProviderColumns(values, "Author", canonicalId);
                SetIfPresent(values, "LastUpdated", GetValue(row, "LastInfoSync"));
                SetIfPresent(values, "ProviderUrls", "{}");

                InsertRow(target, transaction, "Authors", targetColumns, values);
                var providerIds = canonicalId == null ? new List<string>() : new List<string> { canonicalId };
                authors[sourceId] = new AuthorMap(sourceId, metadataId, providerIds);
            }

            var metadataCount = CountRows(source, "AuthorMetadata");
            var metadataTransformed = referencedMetadataIds.Count;
            report.RecordTable("Authors", sourceCount, 0, sourceCount, 0, 0,
                "managed author rows are merged with their AuthorMetadata row while preserving Authors.Id");
            report.RecordTable("AuthorMetadata", metadataCount, 0, metadataTransformed, 0, metadataCount - metadataTransformed,
                "managed metadata is merged into Authors; unreferenced metadata-only cache rows are deliberately reset");
            return authors;
        }

        private static void AddMetadataColumns(IDictionary<string, object> values, IReadOnlyDictionary<string, object> metadata)
        {
            foreach (var field in new[]
            {
                "TitleSlug", "Name", "SortName", "NameLastFirst", "SortNameLastFirst", "Aliases", "Overview", "Disambiguation",
                "Gender", "Hometown", "Born", "Died", "Status", "Images", "Links", "Genres", "Ratings"
            })
            {
                if (metadata.TryGetValue(field, out var value)) values[field] = value;
            }

            SetIfPresent(values, "Aliases", "[]");
            SetIfPresent(values, "Images", "[]");
            SetIfPresent(values, "Links", "[]");
            SetIfPresent(values, "Genres", "[]");
        }

        private static Dictionary<int, BookMediaState> ReadBookMediaTypes(SqliteConnection source)
        {
            var result = new Dictionary<int, BookMediaState>();
            foreach (var row in ReadRows(source, "Editions"))
            {
                var bookId = GetInt(row, "BookId");
                var isEbook = GetNullableBool(row, "IsEbook").GetValueOrDefault();
                var mediaType = isEbook ? 1 : 0;
                if (!result.TryGetValue(bookId, out var state))
                {
                    state = new BookMediaState();
                    result[bookId] = state;
                }

                state.MediaTypes.Add(mediaType);
                if (GetBoolean(row, "Monitored"))
                {
                    if (!state.MonitoredEditionIdsByMediaType.TryAdd(
                            mediaType,
                            NormalizeSourceId(GetString(row, "ForeignEditionId"), "edition")))
                    {
                        var format = mediaType == 1 ? "ebook" : "audiobook";
                        throw new InvalidDataException($"BookshelfNG book {bookId} has more than one monitored {format} edition; conversion stopped before promotion.");
                    }
                }
            }

            return result;
        }

        private static Dictionary<int, BookMap> ImportBooks(
            SqliteConnection source,
            SqliteConnection target,
            SqliteTransaction transaction,
            IReadOnlyDictionary<int, AuthorMap> authors,
            IReadOnlyDictionary<int, BookMediaState> bookMediaTypes,
            IReadOnlyDictionary<int, BookshelfMediaChoice> mediaChoices,
            BookshelfDatabaseImportReport report)
        {
            var sourceCount = CountRows(source, "Books");
            var targetColumns = GetColumns(target, "Books");
            var result = new Dictionary<int, BookMap>();
            var nextId = Convert.ToInt32(Scalar(source, "SELECT COALESCE(MAX(Id), 0) + 1 FROM Books;"), CultureInfo.InvariantCulture);

            foreach (var row in ReadRows(source, "Books"))
            {
                var sourceId = GetInt(row, "Id");
                var metadataId = GetInt(row, "AuthorMetadataId");
                var author = authors.Values.SingleOrDefault(x => x.MetadataId == metadataId);
                if (author == null)
                {
                    throw new InvalidDataException($"BookshelfNG book {sourceId} references author metadata {metadataId} with no unambiguous managed author.");
                }

                bookMediaTypes.TryGetValue(sourceId, out var mediaState);
                mediaState ??= new BookMediaState();
                var hasAudio = mediaState.MediaTypes.Contains(0);
                var hasEbook = mediaState.MediaTypes.Contains(1);
                if (!hasAudio && !hasEbook)
                {
                    if (!mediaChoices.TryGetValue(sourceId, out var noEditionChoice))
                    {
                        throw new InvalidDataException($"BookshelfNG book {sourceId} has no edition and no explicit media mapping. The source database was not changed.");
                    }

                    hasAudio = noEditionChoice is BookshelfMediaChoice.Audiobook or BookshelfMediaChoice.Both or BookshelfMediaChoice.BothUnmonitored;
                    hasEbook = noEditionChoice is BookshelfMediaChoice.Ebook or BookshelfMediaChoice.Both or BookshelfMediaChoice.BothUnmonitored;
                }

                var sourceMonitored = GetBoolean(row, "Monitored");
                var monitoredMediaTypes = new HashSet<int>();
                if (mediaState.MonitoredEditionIdsByMediaType.Count > 0)
                {
                    monitoredMediaTypes.UnionWith(mediaState.MonitoredEditionIdsByMediaType.Keys);
                }
                else if (sourceMonitored)
                {
                    if (mediaState.MediaTypes.Count == 0)
                    {
                        if (!mediaChoices.TryGetValue(sourceId, out var noEditionChoice))
                        {
                            throw new InvalidDataException($"BookshelfNG book {sourceId} has no edition and no explicit media mapping. The source database was not changed.");
                        }

                        if (noEditionChoice is BookshelfMediaChoice.Audiobook or BookshelfMediaChoice.Both)
                        {
                            monitoredMediaTypes.Add(0);
                        }

                        if (noEditionChoice is BookshelfMediaChoice.Ebook or BookshelfMediaChoice.Both)
                        {
                            monitoredMediaTypes.Add(1);
                        }
                    }
                    else if (mediaState.MediaTypes.Count == 1)
                    {
                        monitoredMediaTypes.Add(mediaState.MediaTypes.Single());
                    }
                    else if (mediaState.MediaTypes.Count > 1)
                    {
                        if (!mediaChoices.TryGetValue(sourceId, out var mixedFormatChoice))
                        {
                            throw new InvalidDataException($"BookshelfNG book {sourceId} is monitored but has audiobook and ebook editions without a selected edition. The source database was not changed.");
                        }

                        if (mixedFormatChoice is BookshelfMediaChoice.Audiobook or BookshelfMediaChoice.Both)
                        {
                            monitoredMediaTypes.Add(0);
                        }

                        if (mixedFormatChoice is BookshelfMediaChoice.Ebook or BookshelfMediaChoice.Both)
                        {
                            monitoredMediaTypes.Add(1);
                        }
                    }
                }

                var mapping = new Dictionary<int, int>();
                if (hasAudio) mapping[0] = sourceId;
                if (hasEbook) mapping[1] = hasAudio ? nextId++ : sourceId;

                var foreignBookId = GetString(row, "ForeignBookId");
                var canonicalBookId = NormalizeSourceId(foreignBookId, "book");
                foreach (var mediaType in mapping.Keys.OrderBy(x => x))
                {
                    var targetId = mapping[mediaType];
                    var values = Intersect(row, targetColumns);
                    values["Id"] = targetId;
                    values["AuthorId"] = author.Id;
                    values["Title"] = GetString(row, "Title") ?? "Unknown title";
                    values["CleanTitle"] = GetString(row, "CleanTitle") ?? GetString(row, "Title") ?? "Unknown title";
                    values["Added"] = GetValue(row, "Added") ?? DateTime.UnixEpoch;
                    values["MediaType"] = mediaType;
                    var mediaMonitored = sourceMonitored && monitoredMediaTypes.Contains(mediaType);
                    values["Monitored"] = mediaMonitored;
                    values["AudiobookMonitored"] = mediaType == 0 && mediaMonitored;
                    values["EbookMonitored"] = mediaType == 1 && mediaMonitored;
                    values["ForeignEditionId"] = mediaState.MonitoredEditionIdsByMediaType.TryGetValue(mediaType, out var monitoredEditionId)
                        ? monitoredEditionId
                        : null;
                    values["BaseBookId"] = canonicalBookId;
                    values["AnyEditionOk"] = GetBoolean(row, "AnyEditionOk");
                    SetProviderColumns(values, "Book", canonicalBookId);
                    SetIfPresent(values, "Images", "[]");
                    SetIfPresent(values, "ProviderUrls", "{}");
                    InsertRow(target, transaction, "Books", targetColumns, values);
                }

                result[sourceId] = new BookMap(sourceId, mapping, canonicalBookId, author.Id);
            }

            report.RecordTable("Books", sourceCount, 0, sourceCount, 0, 0,
                $"BookshelfNG books are split by the source editions' media types and linked to the preserved managed author ID; {mediaChoices.Count(x => x.Value == BookshelfMediaChoice.BothUnmonitored)} unresolved books are kept in both catalogs without monitoring until reviewed");
            return result;
        }

        private static Dictionary<int, EditionMap> ImportEditions(
            SqliteConnection source,
            SqliteConnection target,
            SqliteTransaction transaction,
            IReadOnlyDictionary<int, BookMap> books,
            BookshelfDatabaseImportReport report)
        {
            var sourceCount = CountRows(source, "Editions");
            var targetColumns = GetColumns(target, "Editions");
            var result = new Dictionary<int, EditionMap>();

            foreach (var row in ReadRows(source, "Editions"))
            {
                var sourceBookId = GetInt(row, "BookId");
                if (!books.TryGetValue(sourceBookId, out var book))
                {
                    throw new InvalidDataException($"BookshelfNG edition {GetInt(row, "Id")} references missing book {sourceBookId}.");
                }

                var ebook = GetNullableBool(row, "IsEbook").GetValueOrDefault();
                var mediaType = ebook ? 1 : 0;
                if (!book.TargetIds.TryGetValue(mediaType, out var targetBookId))
                {
                    throw new InvalidDataException($"BookshelfNG edition {GetInt(row, "Id")} has a media type with no target book row.");
                }

                var foreignEditionId = NormalizeSourceId(GetString(row, "ForeignEditionId"), "edition");
                var values = Intersect(row, targetColumns);
                values["BookId"] = targetBookId;
                values["IsEbook"] = ebook;
                values["MediaType"] = mediaType;
                values["ForeignEditionId"] = foreignEditionId;
                SetProviderColumns(values, "Edition", foreignEditionId);
                SetIfPresent(values, "Images", PreserveJsonArrayOrEmpty(GetValue(row, "Images")));
                SetIfPresent(values, "Links", PreserveJsonArrayOrEmpty(GetValue(row, "Links")));
                var narrator = GetString(row, "Narrator")?.Trim();
                SetIfPresent(values, "NarratorNames", JsonConvert.SerializeObject(
                    string.IsNullOrWhiteSpace(narrator) ? Array.Empty<string>() : new[] { narrator }));
                SetIfPresent(values, "ProviderUrls", "{}");
                InsertRow(target, transaction, "Editions", targetColumns, values);
                result[GetInt(row, "Id")] = new EditionMap(GetInt(row, "Id"), sourceBookId, mediaType, targetBookId, foreignEditionId);
            }

            report.RecordTable("Editions", sourceCount, 0, sourceCount, 0, 0,
                "edition rows and IDs are preserved and attached to the corresponding audiobook or ebook book row");
            return result;
        }

        private static Dictionary<int, BookFileMap> ImportBookFiles(
            SqliteConnection source,
            SqliteConnection target,
            SqliteTransaction transaction,
            IReadOnlyDictionary<int, EditionMap> editions,
            BookshelfDatabaseImportReport report)
        {
            var sourceCount = CountRows(source, "BookFiles");
            var targetColumns = GetColumns(target, "BookFiles");
            var result = new Dictionary<int, BookFileMap>();

            foreach (var row in ReadRows(source, "BookFiles"))
            {
                var sourceEditionId = GetInt(row, "EditionId");
                if (sourceEditionId == 0)
                {
                    var orphanModified = GetValue(row, "Modified") ?? GetValue(row, "DateAdded") ?? DateTime.UnixEpoch;
                    var orphanValues = Intersect(row, targetColumns);
                    orphanValues["EditionId"] = 0;
                    orphanValues["Modified"] = orphanModified;
                    orphanValues["DateAdded"] = GetValue(row, "DateAdded") ?? orphanModified;
                    orphanValues["MediaInfo"] = GetValue(row, "MediaInfo") ?? "{}";
                    SetIfPresent(orphanValues, "MediaType", string.Empty);
                    SetIfPresent(orphanValues, "UnmappedReason", "Source file had no linked edition; its media type and book relationship were not inferred.");
                    SetIfPresent(orphanValues, "Part", GetValue(row, "Part") ?? 0);
                    SetIfPresent(orphanValues, "IndexerFlags", GetValue(row, "IndexerFlags") ?? 0);
                    InsertRow(target, transaction, "BookFiles", targetColumns, orphanValues);
                    result[GetInt(row, "Id")] = new BookFileMap(
                        GetInt(row, "Id"),
                        0,
                        -1,
                        0,
                        0,
                        GetString(row, "Path"));
                    continue;
                }

                if (!editions.TryGetValue(sourceEditionId, out var edition))
                {
                    throw new InvalidDataException($"BookshelfNG book file {GetInt(row, "Id")} references missing edition {sourceEditionId}.");
                }

                var modified = GetValue(row, "Modified") ?? GetValue(row, "DateAdded") ?? DateTime.UnixEpoch;
                var dateAdded = GetValue(row, "DateAdded") ?? modified;
                var values = Intersect(row, targetColumns);
                values["EditionId"] = sourceEditionId;
                values["Modified"] = modified;
                values["DateAdded"] = dateAdded;
                values["MediaInfo"] = GetValue(row, "MediaInfo") ?? "{}";
                values["MediaType"] = edition.MediaType == 1 ? "ebook" : "audiobook";
                SetIfPresent(values, "Part", GetValue(row, "Part") ?? 0);
                SetIfPresent(values, "IndexerFlags", GetValue(row, "IndexerFlags") ?? 0);
                InsertRow(target, transaction, "BookFiles", targetColumns, values);
                result[GetInt(row, "Id")] = new BookFileMap(
                    GetInt(row, "Id"),
                    edition.SourceBookId,
                    edition.MediaType,
                    edition.TargetBookId,
                    sourceEditionId,
                    GetString(row, "Path"));
            }

            report.RecordTable("BookFiles", sourceCount, 0, sourceCount, 0, 0,
                "book-file IDs and paths are preserved; linked files retain their edition-derived media type, and EditionId=0 files remain unmapped with no inferred media type");
            return result;
        }

        private static void ImportExtraFiles(
            SqliteConnection source,
            SqliteConnection target,
            SqliteTransaction transaction,
            IReadOnlyDictionary<int, BookMap> books,
            IReadOnlyDictionary<int, BookFileMap> files,
            BookshelfDatabaseImportReport report)
        {
            var sourceCount = CountRows(source, "ExtraFiles");
            var targetColumns = GetColumns(target, "ExtraFiles");
            foreach (var row in ReadRows(source, "ExtraFiles"))
            {
                var sourceBookId = GetInt(row, "BookId");
                var sourceFileId = GetInt(row, "BookFileId");
                if (!books.TryGetValue(sourceBookId, out var book))
                {
                    throw new InvalidDataException($"BookshelfNG extra file {GetInt(row, "Id")} references missing book {sourceBookId}.");
                }

                var targetBookId = book.TargetIds.Values.OrderBy(x => x).First();
                if (files.TryGetValue(sourceFileId, out var file))
                {
                    if (file.SourceBookId != 0 && file.SourceBookId != sourceBookId)
                    {
                        throw new InvalidDataException($"BookshelfNG extra file {GetInt(row, "Id")} points to book file {sourceFileId} from a different book; conversion stopped to preserve the relationship.");
                    }

                    if (file.TargetBookId > 0)
                    {
                        targetBookId = file.TargetBookId;
                    }
                }

                var values = Intersect(row, targetColumns);
                values["BookId"] = targetBookId;
                // Keep an orphaned source BookFileId verbatim. Chaptarr must not attach it to an unrelated file.
                values["BookFileId"] = sourceFileId;
                InsertRow(target, transaction, "ExtraFiles", targetColumns, values);
            }

            report.RecordTable("ExtraFiles", sourceCount, 0, sourceCount, 0, 0,
                "extra-file IDs and file references are retained; orphaned BookFileId values remain orphaned and are reserved against future ID reuse");
        }

        private static void ImportMetadataFiles(
            SqliteConnection source,
            SqliteConnection target,
            SqliteTransaction transaction,
            IReadOnlyDictionary<int, BookMap> books,
            IReadOnlyDictionary<int, BookFileMap> files,
            BookshelfDatabaseImportReport report)
        {
            var sourceCount = CountRows(source, "MetadataFiles");
            var targetColumns = GetColumns(target, "MetadataFiles");
            foreach (var row in ReadRows(source, "MetadataFiles"))
            {
                var values = Intersect(row, targetColumns);
                var sourceBookId = GetNullableInt(row, "BookId");
                var sourceFileId = GetNullableInt(row, "BookFileId");
                if (sourceFileId.HasValue && files.TryGetValue(sourceFileId.Value, out var file))
                {
                    if (sourceBookId.HasValue && file.SourceBookId != 0 && sourceBookId.Value != file.SourceBookId)
                    {
                        throw new InvalidDataException($"BookshelfNG metadata file {GetInt(row, "Id")} has mismatched book and book-file references.");
                    }

                    if (file.SourceBookId == 0 && sourceBookId.HasValue)
                    {
                        if (!books.TryGetValue(sourceBookId.Value, out var linkedBook))
                        {
                            throw new InvalidDataException($"BookshelfNG metadata file {GetInt(row, "Id")} references missing book {sourceBookId.Value}.");
                        }

                        values["BookId"] = linkedBook.TargetIds.Values.OrderBy(x => x).First();
                    }
                    else if (file.TargetBookId > 0)
                    {
                        values["BookId"] = file.TargetBookId;
                    }

                    values["BookFileId"] = sourceFileId.Value;
                }
                else if (sourceBookId.HasValue)
                {
                    if (!books.TryGetValue(sourceBookId.Value, out var book))
                    {
                        throw new InvalidDataException($"BookshelfNG metadata file {GetInt(row, "Id")} references missing book {sourceBookId.Value}.");
                    }

                    values["BookId"] = book.TargetIds.Values.OrderBy(x => x).First();
                    // Keep a missing source file ID verbatim for audit and later recovery.
                    values["BookFileId"] = sourceFileId;
                }

                InsertRow(target, transaction, "MetadataFiles", targetColumns, values);
            }

            report.RecordTable("MetadataFiles", sourceCount, 0, sourceCount, 0, 0,
                "sidecar records are retained and book/file references are remapped without inventing relationships");
        }

        private static void ReserveOrphanBookFileIds(SqliteConnection source, SqliteConnection target, SqliteTransaction transaction)
        {
            var maxOrphanValue = Scalar(source, @"
                SELECT MAX(BookFileId)
                  FROM (
                        SELECT ExtraFiles.BookFileId
                          FROM ExtraFiles
                         WHERE NOT EXISTS (SELECT 1 FROM BookFiles WHERE BookFiles.Id = ExtraFiles.BookFileId)
                        UNION ALL
                        SELECT MetadataFiles.BookFileId
                          FROM MetadataFiles
                         WHERE MetadataFiles.BookFileId IS NOT NULL
                           AND NOT EXISTS (SELECT 1 FROM BookFiles WHERE BookFiles.Id = MetadataFiles.BookFileId)
                  );");
            if (maxOrphanValue == null || maxOrphanValue == DBNull.Value) return;
            var maxOrphanId = Convert.ToInt64(maxOrphanValue, CultureInfo.InvariantCulture);

            if (maxOrphanId == 0) return;
            if (maxOrphanId >= int.MaxValue)
            {
                throw new InvalidDataException("BookshelfNG contains an orphaned book-file ID at the SQLite integer limit; conversion stopped before promotion to prevent future ID collisions.");
            }

            using var update = target.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = "UPDATE sqlite_sequence SET seq = MAX(seq, @sequence) WHERE name = 'BookFiles';";
            update.Parameters.AddWithValue("@sequence", maxOrphanId);
            if (update.ExecuteNonQuery() == 0)
            {
                using var insert = target.CreateCommand();
                insert.Transaction = transaction;
                insert.CommandText = "INSERT INTO sqlite_sequence (name, seq) VALUES ('BookFiles', @sequence);";
                insert.Parameters.AddWithValue("@sequence", maxOrphanId);
                insert.ExecuteNonQuery();
            }
        }

        private static Dictionary<int, SeriesMap> ImportSeries(
            SqliteConnection source,
            SqliteConnection target,
            SqliteTransaction transaction,
            BookshelfDatabaseImportReport report)
        {
            var sourceCount = CountRows(source, "Series");
            var targetColumns = GetColumns(target, "Series");
            var result = new Dictionary<int, SeriesMap>();

            foreach (var row in ReadRows(source, "Series"))
            {
                var canonicalId = NormalizeSourceId(GetString(row, "ForeignSeriesId"), "series");
                var values = Intersect(row, targetColumns);
                values["Title"] = GetString(row, "Title") ?? "Unknown series";
                values["BaseSeriesId"] = canonicalId;
                SetProviderColumns(values, "Series", canonicalId);
                SetIfPresent(values, "Links", "{}");
                SetIfPresent(values, "ProviderUrls", "{}");
                InsertRow(target, transaction, "Series", targetColumns, values);
                result[GetInt(row, "Id")] = new SeriesMap(GetInt(row, "Id"), canonicalId);
            }

            report.RecordTable("Series", sourceCount, 0, sourceCount, 0, 0,
                "series IDs and descriptions are preserved; provider identity is routed into the matching Chaptarr provider field");
            return result;
        }

        private static void ImportSeriesLinks(
            SqliteConnection source,
            SqliteConnection target,
            SqliteTransaction transaction,
            IReadOnlyDictionary<int, BookMap> books,
            IReadOnlyDictionary<int, SeriesMap> series,
            BookshelfDatabaseImportReport report)
        {
            var sourceCount = CountRows(source, "SeriesBookLink");
            var targetColumns = GetColumns(target, "SeriesBookLink");
            var nextId = Convert.ToInt32(Scalar(source, "SELECT COALESCE(MAX(Id), 0) + 1 FROM SeriesBookLink;"), CultureInfo.InvariantCulture);

            foreach (var row in ReadRows(source, "SeriesBookLink"))
            {
                var sourceBookId = GetInt(row, "BookId");
                var sourceSeriesId = GetInt(row, "SeriesId");
                if (!books.TryGetValue(sourceBookId, out var book) || !series.ContainsKey(sourceSeriesId))
                {
                    throw new InvalidDataException($"BookshelfNG series link {GetInt(row, "Id")} references a missing book or series.");
                }

                var targets = book.TargetIds.OrderBy(x => x.Key).ToArray();
                for (var i = 0; i < targets.Length; i++)
                {
                    var values = Intersect(row, targetColumns);
                    values["Id"] = i == 0 ? GetInt(row, "Id") : nextId++;
                    values["BookId"] = targets[i].Value;
                    values["SeriesId"] = sourceSeriesId;
                    if (!values.ContainsKey("SeriesPosition"))
                    {
                        values["SeriesPosition"] = ParsePosition(GetString(row, "Position"));
                    }

                    InsertRow(target, transaction, "SeriesBookLink", targetColumns, values);
                }
            }

            report.RecordTable("SeriesBookLink", sourceCount, 0, sourceCount, 0, 0,
                "links are rebuilt for each corresponding audiobook and ebook row");
        }

        private static void RebuildAuthorSeries(SqliteConnection target, SqliteTransaction transaction)
        {
            if (!TableExists(target, "AuthorSeries")) return;
            using var command = target.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = @"
                INSERT INTO AuthorSeries (SeriesId, AuthorId)
                SELECT DISTINCT links.SeriesId, books.AuthorId
                  FROM SeriesBookLink links
                  JOIN Books books ON books.Id = links.BookId;";
            command.ExecuteNonQuery();
        }

        private static void ImportBlocklist(
            SqliteConnection source,
            SqliteConnection target,
            SqliteTransaction transaction,
            IReadOnlyDictionary<int, AuthorMap> authors,
            IReadOnlyDictionary<int, BookMap> books,
            BookshelfDatabaseImportReport report)
        {
            var sourceCount = CountRows(source, "Blocklist");
            var targetColumns = GetColumns(target, "Blocklist");
            foreach (var row in ReadRows(source, "Blocklist"))
            {
                var sourceAuthorId = GetInt(row, "AuthorId");
                if (!authors.TryGetValue(sourceAuthorId, out var author))
                {
                    throw new InvalidDataException($"BookshelfNG blocklist row {GetInt(row, "Id")} references missing author {sourceAuthorId}.");
                }

                var authorIds = author.ProviderIds;
                if (authorIds.Count == 0)
                {
                    throw new InvalidDataException($"BookshelfNG blocklist row {GetInt(row, "Id")} cannot be mapped because its author has no supported provider ID.");
                }

                var bookIds = ParseIntegerArray(GetString(row, "BookIds"));
                var bookProviderIds = new List<string>();
                foreach (var sourceBookId in bookIds)
                {
                    if (!books.TryGetValue(sourceBookId, out var book) || string.IsNullOrWhiteSpace(book.ProviderId))
                    {
                        throw new InvalidDataException($"BookshelfNG blocklist row {GetInt(row, "Id")} cannot map book {sourceBookId} to a supported provider ID.");
                    }

                    bookProviderIds.Add(book.ProviderId);
                }

                var values = Intersect(row, targetColumns);
                values["AuthorProviderIds"] = JsonConvert.SerializeObject(authorIds.Distinct(StringComparer.Ordinal).ToArray());
                values["BookProviderIds"] = JsonConvert.SerializeObject(bookProviderIds.Distinct(StringComparer.Ordinal).ToArray());
                SetIfPresent(values, "IndexerFlags", GetValue(row, "IndexerFlags") ?? 0);
                InsertRow(target, transaction, "Blocklist", targetColumns, values);
            }

            report.RecordTable("Blocklist", sourceCount, 0, sourceCount, 0, 0,
                "numeric author/book links are converted to the provider-qualified IDs Chaptarr blocklists use");
        }

        private static void ImportDownloadHistory(
            SqliteConnection source,
            SqliteConnection target,
            SqliteTransaction transaction,
            BookshelfDatabaseImportReport report)
        {
            var sourceCount = CountRows(source, "DownloadHistory");
            var targetColumns = GetColumns(target, "DownloadHistory");
            foreach (var row in ReadRows(source, "DownloadHistory"))
            {
                var values = Intersect(row, targetColumns);
                values["BookId"] = 0; // BookshelfNG does not store a book ID on these events.
                SetIfPresent(values, "Data", GetValue(row, "Data"));
                InsertRow(target, transaction, "DownloadHistory", targetColumns, values);
            }

            report.RecordTable("DownloadHistory", sourceCount, 0, sourceCount, 0, 0,
                "download events are preserved; source events without a book use the target's non-null BookId=0 sentinel");
        }

        private static void ImportHistory(
            SqliteConnection source,
            SqliteConnection target,
            SqliteTransaction transaction,
            IReadOnlyDictionary<int, AuthorMap> authors,
            IReadOnlyDictionary<int, BookMap> books,
            IReadOnlyDictionary<int, BookFileMap> files,
            BookshelfDatabaseImportReport report)
        {
            var sourceCount = CountRows(source, "History");
            var targetColumns = GetColumns(target, "History");
            foreach (var row in ReadRows(source, "History"))
            {
                var sourceAuthorId = GetNullableInt(row, "AuthorId");
                if (sourceAuthorId.HasValue && !authors.ContainsKey(sourceAuthorId.Value))
                {
                    throw new InvalidDataException($"BookshelfNG history row {GetInt(row, "Id")} references missing author {sourceAuthorId.Value}.");
                }

                var sourceBookId = GetNullableInt(row, "BookId");
                var targetBookId = sourceBookId.GetValueOrDefault();
                if (sourceBookId.GetValueOrDefault() != 0)
                {
                    if (!books.TryGetValue(sourceBookId.Value, out var book))
                    {
                        throw new InvalidDataException($"BookshelfNG history row {GetInt(row, "Id")} references missing book {sourceBookId.Value}.");
                    }

                    // Bookshelf history is book-scoped, so retain its source ID as the
                    // default target identity. When the event names a specific file,
                    // follow that file to the correct media-specific target book.
                    targetBookId = book.TargetIds.TryGetValue(0, out var audiobookId)
                        ? audiobookId
                        : book.TargetIds.Values.OrderBy(x => x).First();

                    var file = FindHistoryFile(row, files);
                    if (file != null)
                    {
                        if (file.SourceBookId != 0 && file.SourceBookId != sourceBookId.Value)
                        {
                            throw new InvalidDataException($"BookshelfNG history row {GetInt(row, "Id")} identifies a file from another book.");
                        }

                        if (file.TargetBookId > 0)
                        {
                            targetBookId = file.TargetBookId;
                        }
                    }
                }

                var values = Intersect(row, targetColumns);
                values["BookId"] = targetBookId;
                var historyFile = FindHistoryFile(row, files);
                SetIfPresent(values, "EditionId", historyFile?.TargetEditionId > 0 ? historyFile.TargetEditionId : null);
                InsertRow(target, transaction, "History", targetColumns, values);
            }

            report.RecordTable("History", sourceCount, 0, sourceCount, 0, 0,
                "history rows and IDs are retained; file-specific events follow their source file to the correct media row and edition");
        }

        private static BookFileMap FindHistoryFile(
            IReadOnlyDictionary<string, object> row,
            IReadOnlyDictionary<int, BookFileMap> files)
        {
            var dataText = GetString(row, "Data");
            JObject data = null;
            if (!string.IsNullOrWhiteSpace(dataText))
            {
                try
                {
                    data = JObject.Parse(dataText);
                }
                catch (JsonException)
                {
                    // Preserve an opaque legacy payload; fall back to the book identity.
                }
            }

            var fileIdText = data?.GetValue("FileId", StringComparison.OrdinalIgnoreCase)?.Value<string>();
            if (int.TryParse(fileIdText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var fileId) &&
                files.TryGetValue(fileId, out var fileById))
            {
                return fileById;
            }

            var sourceTitle = GetString(row, "SourceTitle");
            var candidatePaths = new[]
            {
                sourceTitle,
                data?.GetValue("Path", StringComparison.OrdinalIgnoreCase)?.Value<string>(),
                data?.GetValue("ImportedPath", StringComparison.OrdinalIgnoreCase)?.Value<string>(),
                data?.GetValue("SourcePath", StringComparison.OrdinalIgnoreCase)?.Value<string>()
            }.Where(x => !string.IsNullOrWhiteSpace(x)).Select(NormalizePath).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var matches = files.Values.Where(x => candidatePaths.Contains(NormalizePath(x.Path))).ToArray();
            if (matches.Length > 1)
            {
                throw new InvalidDataException($"BookshelfNG history row {GetInt(row, "Id")} matches multiple source files.");
            }

            return matches.SingleOrDefault();
        }

        private static void ImportLists(
            SqliteConnection source,
            SqliteConnection target,
            SqliteTransaction transaction,
            IReadOnlyDictionary<(int SourceId, int MediaType), int> qualityProfiles,
            IReadOnlyDictionary<int, int> metadataProfiles,
            BookshelfDatabaseImportReport report)
        {
            var sourceCount = CountRows(source, "ImportLists");
            var targetColumns = GetColumns(target, "ImportLists");
            foreach (var row in ReadRows(source, "ImportLists"))
            {
                var values = Intersect(row, targetColumns);
                var sourceProfileId = GetNullableInt(row, "ProfileId");
                var sourceMetadataProfileId = GetNullableInt(row, "MetadataProfileId");
                values["QualityProfileId"] = sourceProfileId.HasValue
                    ? MapQualityProfile(qualityProfiles, sourceProfileId.Value, 0, "import-list quality")
                    : 1;
                values["MetadataProfileId"] = sourceMetadataProfileId.HasValue
                    ? MapMetadataProfile(metadataProfiles, sourceMetadataProfileId.Value, "import-list metadata")
                    : 1;
                values["ShouldMonitor"] = RemapMonitorType(GetInt(row, "ShouldMonitor"));
                SetIfPresent(values, "EnableAutomaticAdd", GetBoolean(row, "EnableAutomaticAdd"));
                SetIfPresent(values, "ConfigContract", GetString(row, "ConfigContract") ?? "");
                SetIfPresent(values, "MonitorNewItems", GetValue(row, "MonitorNewItems") ?? 0);
                InsertRow(target, transaction, "ImportLists", targetColumns, values);
            }

            report.RecordTable("ImportLists", sourceCount, 0, sourceCount, 0, 0,
                "list settings, credentials, monitoring, profile IDs, and root paths are retained with the target quality-profile mapping");
        }

        private static void ImportNamingConfig(
            SqliteConnection source,
            SqliteConnection target,
            SqliteTransaction transaction,
            BookshelfDatabaseImportReport report)
        {
            var sourceCount = CountRows(source, "NamingConfig");
            var targetColumns = GetColumns(target, "NamingConfig");
            foreach (var row in ReadRows(source, "NamingConfig"))
            {
                var values = Intersect(row, targetColumns);
                SetIfPresent(values, "MultiEpisodeStyle", 0);
                SetIfPresent(values, "AuthorFolderFormat", GetString(row, "AuthorFolderFormat") ?? "{Author Name}");
                SetIfPresent(values, "StandardBookFormat", GetString(row, "StandardBookFormat") ?? "{Book Title}");
                InsertRow(target, transaction, "NamingConfig", targetColumns, values);
            }

            report.RecordTable("NamingConfig", sourceCount, 0, sourceCount, 0, 0,
                "filename and folder patterns are preserved; target-only naming settings use Chaptarr defaults");
        }

        private static void ImportNotifications(
            SqliteConnection source,
            SqliteConnection target,
            SqliteTransaction transaction,
            BookshelfDatabaseImportReport report)
        {
            var sourceCount = CountRows(source, "Notifications");
            var targetColumns = GetColumns(target, "Notifications");
            foreach (var row in ReadRows(source, "Notifications"))
            {
                var values = Intersect(row, targetColumns);
                if (!row.ContainsKey("OnBookRetag"))
                {
                    SetIfPresent(values, "OnBookRetag", GetValue(row, "OnTrackRetag") ?? false);
                }
                SetIfPresent(values, "OnBookAdded", false);
                SetIfPresent(values, "OnHealthRestored", false);
                InsertRow(target, transaction, "Notifications", targetColumns, values);
            }

            report.RecordTable("Notifications", sourceCount, 0, sourceCount, 0, 0,
                "notification settings and triggers are preserved; the legacy track-retag trigger maps to book retag");
        }

        private static void CopyTable(
            SqliteConnection source,
            SqliteConnection target,
            SqliteTransaction transaction,
            BookshelfDatabaseImportReport report,
            string table,
            Action<Dictionary<string, object>> transform = null)
        {
            var sourceCount = CountRows(source, table);
            var targetColumns = GetColumns(target, table);
            foreach (var row in ReadRows(source, table))
            {
                var unmapped = row.Where(x => x.Value != null && !targetColumns.ContainsKey(x.Key))
                    .Select(x => x.Key)
                    .OrderBy(x => x, StringComparer.Ordinal)
                    .ToArray();
                if (unmapped.Length > 0)
                {
                    throw new InvalidDataException(
                        $"BookshelfNG {table} row {GetInt(row, "Id")} has non-empty fields with no Chaptarr mapping: " +
                        string.Join(", ", unmapped.Select(x => table + "." + x)) + ". Conversion stopped before promotion.");
                }

                var values = Intersect(row, targetColumns);
                transform?.Invoke(values);
                InsertRow(target, transaction, table, targetColumns, values);
            }

            report.RecordTable(table, sourceCount, 0, sourceCount, 0, 0,
                $"source {table} rows are mapped by their compatible fields into Chaptarr {table}");
        }

        private static void ResetTable(SqliteConnection source, BookshelfDatabaseImportReport report, string table, string reason)
        {
            var sourceCount = CountRows(source, table);
            report.RecordTable(table, sourceCount, 0, 0, 0, sourceCount, reason);
        }

        private static void RebuildProviderAliases(
            SqliteConnection target,
            SqliteTransaction transaction,
            IReadOnlyDictionary<int, AuthorMap> authors,
            IReadOnlyDictionary<int, BookMap> books,
            IReadOnlyDictionary<int, EditionMap> editions,
            IReadOnlyDictionary<int, SeriesMap> series)
        {
            foreach (var author in authors.Values)
            {
                foreach (var id in author.ProviderIds) InsertAlias(target, transaction, "Author", author.Id, "author", id);
            }

            foreach (var book in books.Values)
            {
                if (book.ProviderId != null)
                {
                    foreach (var id in book.TargetIds.Values)
                    {
                        InsertAlias(target, transaction, "Book", id, "work", book.ProviderId);
                    }
                }
            }

            foreach (var edition in editions.Values)
            {
                if (edition.ProviderId != null) InsertAlias(target, transaction, "Edition", edition.Id, "edition", edition.ProviderId);
            }

            foreach (var item in series.Values)
            {
                if (item.ProviderId != null) InsertAlias(target, transaction, "Series", item.Id, "series", item.ProviderId);
            }
        }

        private static void InsertAlias(SqliteConnection target, SqliteTransaction transaction, string entityType, int entityId, string scope, string canonicalId)
        {
            var separator = canonicalId.IndexOf(':');
            if (separator <= 0 || separator == canonicalId.Length - 1) return;

            using var command = target.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = @"
                INSERT OR IGNORE INTO ProviderAliasIndex
                    (EntityType, EntityId, Scope, Provider, NormalizedProviderId, CreatedAt, UpdatedAt)
                VALUES (@entityType, @entityId, @scope, @provider, @providerId, @createdAt, @createdAt);";
            command.Parameters.AddWithValue("@entityType", entityType);
            command.Parameters.AddWithValue("@entityId", entityId);
            command.Parameters.AddWithValue("@scope", scope);
            command.Parameters.AddWithValue("@provider", canonicalId.Substring(0, separator));
            command.Parameters.AddWithValue("@providerId", canonicalId.Substring(separator + 1));
            command.Parameters.AddWithValue("@createdAt", DateTime.UtcNow);
            command.ExecuteNonQuery();
        }

        private static int MapQualityProfile(
            IReadOnlyDictionary<(int SourceId, int MediaType), int> profiles,
            int sourceId,
            int mediaType,
            string context)
        {
            if (profiles.TryGetValue((sourceId, mediaType), out var targetId)) return targetId;
            throw new InvalidDataException($"BookshelfNG {context} profile {sourceId} has no matching Chaptarr profile.");
        }

        private static int MapMetadataProfile(IReadOnlyDictionary<int, int> profiles, int sourceId, string context)
        {
            if (profiles.TryGetValue(sourceId, out var targetId)) return targetId;
            throw new InvalidDataException($"BookshelfNG {context} profile {sourceId} has no matching Chaptarr profile.");
        }

        private static string NormalizeSourceId(string value, string entityType)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;

            var normalized = value.Trim();
            var separator = normalized.IndexOf(':');
            if (separator < 0)
            {
                if (normalized.All(char.IsDigit)) return "gr:" + normalized;
                throw new InvalidDataException($"BookshelfNG {entityType} provider ID '{normalized}' has no recognized provider prefix.");
            }

            var prefix = normalized.Substring(0, separator).Trim().ToLowerInvariant();
            var id = normalized.Substring(separator + 1).Trim();
            if (id.Length == 0 || id.Contains(':'))
            {
                throw new InvalidDataException($"BookshelfNG {entityType} provider ID '{normalized}' is malformed.");
            }

            prefix = prefix switch
            {
                "hardcover" or "hardcover-author" or "hardcover-book" or "hardcover-edition" or "hardcover-series" => "hc",
                "goodreads" or "goodreads-author" or "goodreads-book" or "goodreads-work" or "goodreads-edition" or "goodreads-series" => "gr",
                "openlibrary" or "openlibrary-author" or "openlibrary-work" or "openlibrary-edition" or "openlibrary-series" => "ol",
                "googlebooks" or "googlebooks-author" or "googlebooks-book" or "googlebooks-edition" => "gb",
                "audnexus" or "audnexus-author" or "amazon" or "amazon-author" or "amazon-book" or "amazon-edition" or "amazon-series" => "az",
                "ranobedb" or "ranobedb-author" or "ranobedb-book" => "rn",
                "ranobedb-release" => "rnr",
                _ => prefix
            };

            if (prefix is not ("hc" or "gr" or "ol" or "gb" or "az" or "rn" or "rnr"))
            {
                throw new InvalidDataException($"BookshelfNG {entityType} provider ID '{normalized}' uses unsupported provider '{prefix}'. Conversion stopped with the source database intact.");
            }

            if (prefix == "az") id = id.ToUpperInvariant();
            return prefix + ":" + id;
        }

        private static void SetProviderColumns(IDictionary<string, object> values, string entityType, string canonicalId)
        {
            if (string.IsNullOrWhiteSpace(canonicalId)) return;
            var separator = canonicalId.IndexOf(':');
            if (separator <= 0) return;
            var prefix = canonicalId.Substring(0, separator);
            var id = canonicalId.Substring(separator + 1);

            switch (entityType)
            {
                case "Author":
                    SetIfPresent(values, prefix switch
                    {
                        "gr" => "GoodreadsAuthorId",
                        "hc" => "HardcoverAuthorId",
                        "ol" => "OpenLibraryAuthorId",
                        "gb" => "GoogleBooksAuthorId",
                        "az" => "AudnexusAuthorId",
                        "rn" => "RanobeDbAuthorId",
                        _ => null
                    }, canonicalId);
                    break;
                case "Book":
                    SetIfPresent(values, prefix switch
                    {
                        "gr" => "GoodreadsWorkId",
                        "hc" => "HardcoverBookId",
                        "ol" => "OpenLibraryWorkId",
                        "gb" => "GoogleBooksId",
                        "az" => "ASIN",
                        "rn" => "RanobeDbBookId",
                        _ => null
                    }, canonicalId);
                    break;
                case "Edition":
                    switch (prefix)
                    {
                        case "gr":
                            if (long.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var goodreadsId))
                            {
                                SetIfPresent(values, "GoodreadsEditionId", goodreadsId);
                            }
                            break;
                        case "hc": SetIfPresent(values, "HardcoverEditionId", canonicalId); break;
                        case "ol": SetIfPresent(values, "OpenLibraryEditionId", canonicalId); break;
                        case "gb": SetIfPresent(values, "GoogleBooksEditionId", canonicalId); break;
                        case "az":
                            SetIfPresent(values, "Asin", id);
                            SetIfPresent(values, "AudibleASIN", canonicalId);
                            break;
                    }
                    break;
                case "Series":
                    SetIfPresent(values, prefix switch
                    {
                        "gr" => "GoodreadsSeriesId",
                        "hc" => "HardcoverSeriesId",
                        "ol" => "OpenLibrarySeriesId",
                        "az" => "AmazonSeriesAsin",
                        _ => null
                    }, canonicalId);
                    break;
            }
        }

        private static string FindRootFolder(string mediaPath, IReadOnlyList<RootFolderMap> rootFolders)
        {
            if (string.IsNullOrWhiteSpace(mediaPath)) return null;
            var normalizedMediaPath = NormalizePath(mediaPath);
            var matches = rootFolders
                .Where(x => IsPathWithin(normalizedMediaPath, NormalizePath(x.Path)))
                .OrderByDescending(x => NormalizePath(x.Path).Length)
                .ToArray();
            return matches.FirstOrDefault()?.Path;
        }

        private static bool IsPathWithin(string path, string root)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(root)) return false;
            return string.Equals(path, root, StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith(root.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizePath(string path)
        {
            return (path ?? string.Empty).Trim().Replace('\\', '/').TrimEnd('/');
        }

        private static int RemapMonitorType(int sourceValue)
        {
            // Chaptarr inserted SpecificBook before Unknown; BookshelfNG's value 7 meant Unknown.
            return sourceValue >= 7 ? sourceValue + 1 : sourceValue;
        }

        private static int ParsePosition(string value)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var position) ? position : 0;
        }

        private static List<int> ParseIntegerArray(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return new List<int>();
            try
            {
                return JArray.Parse(value).Select(x => x.Value<int>()).ToList();
            }
            catch (Exception exception) when (exception is JsonException or FormatException or InvalidCastException)
            {
                throw new InvalidDataException("A BookshelfNG blocklist contains an unreadable book ID list.", exception);
            }
        }

        private static HashSet<string> GetTables(SqliteConnection connection)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%';";
            using var reader = command.ExecuteReader();
            while (reader.Read()) result.Add(reader.GetString(0));
            return result;
        }

        private static bool TableExists(SqliteConnection connection, string table)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = @name LIMIT 1;";
            command.Parameters.AddWithValue("@name", table);
            return command.ExecuteScalar() != null;
        }

        private static Dictionary<string, ColumnInfo> GetColumns(SqliteConnection connection, string table)
        {
            var result = new Dictionary<string, ColumnInfo>(StringComparer.Ordinal);
            using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info({Quote(table)});";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var name = reader.GetString(reader.GetOrdinal("name"));
                var notNull = reader.GetInt32(reader.GetOrdinal("notnull")) != 0;
                var defaultValueOrdinal = reader.GetOrdinal("dflt_value");
                var defaultValue = reader.IsDBNull(defaultValueOrdinal) ? null : reader.GetValue(defaultValueOrdinal)?.ToString();
                var primaryKey = reader.GetInt32(reader.GetOrdinal("pk")) != 0;
                result[name] = new ColumnInfo(notNull, defaultValue, primaryKey);
            }

            if (result.Count == 0) throw new InvalidDataException($"Target table '{table}' does not exist in the staged Chaptarr schema.");
            return result;
        }

        private static long CountRows(SqliteConnection connection, string table)
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM {Quote(table)};";
            return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
        }

        private static object Scalar(SqliteConnection connection, string sql)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            return command.ExecuteScalar();
        }

        private static IEnumerable<Dictionary<string, object>> ReadRows(SqliteConnection connection, string table)
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT * FROM {Quote(table)} ORDER BY Id;";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var row = new Dictionary<string, object>(StringComparer.Ordinal);
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }

                yield return row;
            }
        }

        private static Dictionary<string, object> Intersect(IReadOnlyDictionary<string, object> source, IReadOnlyDictionary<string, ColumnInfo> target)
        {
            return source.Where(x => target.ContainsKey(x.Key)).ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        }

        private static void InsertRow(
            SqliteConnection target,
            SqliteTransaction transaction,
            string table,
            IReadOnlyDictionary<string, ColumnInfo> targetColumns,
            IReadOnlyDictionary<string, object> values)
        {
            foreach (var targetColumn in targetColumns)
            {
                if (!targetColumn.Value.NotNull || targetColumn.Value.HasDefault || targetColumn.Value.PrimaryKey) continue;
                if (!values.TryGetValue(targetColumn.Key, out var value) || value == null)
                {
                    throw new InvalidDataException($"BookshelfNG conversion did not provide required Chaptarr column '{table}.{targetColumn.Key}'.");
                }
            }

            var insertValues = values.Where(x => targetColumns.ContainsKey(x.Key) && !(targetColumns[x.Key].PrimaryKey && x.Value == null)).ToArray();
            using var command = target.CreateCommand();
            command.Transaction = transaction;
            if (insertValues.Length == 0)
            {
                command.CommandText = $"INSERT INTO {Quote(table)} DEFAULT VALUES;";
            }
            else
            {
                var names = insertValues.Select(x => Quote(x.Key)).ToArray();
                var parameters = insertValues.Select((_, index) => "@p" + index.ToString(CultureInfo.InvariantCulture)).ToArray();
                command.CommandText = $"INSERT INTO {Quote(table)} ({string.Join(", ", names)}) VALUES ({string.Join(", ", parameters)});";
                for (var i = 0; i < insertValues.Length; i++)
                {
                    command.Parameters.AddWithValue(parameters[i], insertValues[i].Value ?? DBNull.Value);
                }
            }

            try
            {
                command.ExecuteNonQuery();
            }
            catch (SqliteException exception)
            {
                throw new InvalidDataException($"BookshelfNG row could not be represented in Chaptarr table '{table}'; conversion was stopped before promotion.", exception);
            }
        }

        private static void SetIfPresent(IDictionary<string, object> values, string column, object value)
        {
            if (!string.IsNullOrWhiteSpace(column)) values[column] = value;
        }

        private static string PreserveJsonArrayOrEmpty(object value)
        {
            var json = value?.ToString()?.Trim();
            if (string.IsNullOrEmpty(json) || json.Equals("null", StringComparison.OrdinalIgnoreCase) || json == "{}") return "[]";
            return json;
        }

        private static object GetValue(IReadOnlyDictionary<string, object> row, string name)
        {
            return row.TryGetValue(name, out var value) ? value : null;
        }

        private static string GetString(IReadOnlyDictionary<string, object> row, string name)
        {
            return GetValue(row, name)?.ToString();
        }

        private static int GetInt(IReadOnlyDictionary<string, object> row, string name)
        {
            var value = GetValue(row, name);
            if (value == null) throw new InvalidDataException($"BookshelfNG row is missing required value '{name}'.");
            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        private static int? GetNullableInt(IReadOnlyDictionary<string, object> row, string name)
        {
            var value = GetValue(row, name);
            return value == null ? null : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        private static bool GetBoolean(IReadOnlyDictionary<string, object> row, string name)
        {
            var value = GetValue(row, name);
            return value != null && Convert.ToBoolean(value, CultureInfo.InvariantCulture);
        }

        private static bool? GetNullableBool(IReadOnlyDictionary<string, object> row, string name)
        {
            var value = GetValue(row, name);
            return value == null ? null : Convert.ToBoolean(value, CultureInfo.InvariantCulture);
        }

        private static string Quote(string identifier)
        {
            return "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        }

        private sealed record ColumnInfo(bool NotNull, string DefaultValue, bool PrimaryKey)
        {
            public bool HasDefault => DefaultValue != null;
        }

        private sealed record RootFolderMap(string Path);

        private sealed record AuthorMap(int Id, int MetadataId, List<string> ProviderIds);

        private sealed record BookMap(int SourceId, Dictionary<int, int> TargetIds, string ProviderId, int AuthorId);

        private sealed class BookMediaState
        {
            public HashSet<int> MediaTypes { get; } = new();
            public Dictionary<int, string> MonitoredEditionIdsByMediaType { get; } = new();
        }

        private sealed record EditionMap(int Id, int SourceBookId, int MediaType, int TargetBookId, string ProviderId);

        private sealed record BookFileMap(int Id, int SourceBookId, int MediaType, int TargetBookId, int TargetEditionId, string Path);

        private sealed record SeriesMap(int Id, string ProviderId);
    }
}
