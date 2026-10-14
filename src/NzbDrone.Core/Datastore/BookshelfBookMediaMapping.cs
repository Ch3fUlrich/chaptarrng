using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;

namespace NzbDrone.Core.Datastore
{
    public enum BookshelfMediaChoice
    {
        Audiobook,
        Ebook,
        Both
    }

    /// <summary>
    /// Resolves BookshelfNG works whose source rows do not identify a media type.
    /// These choices cannot be inferred safely from a missing edition relationship.
    /// </summary>
    public static class BookshelfBookMediaMapping
    {
        private const int SupportedMapVersion = 1;

        public static IReadOnlyDictionary<int, BookshelfMediaChoice> Load(
            SqliteConnection source,
            string sourceDatabasePath)
        {
            var required = FindRequiredMappings(source);
            var reviewPath = Path.GetFullPath(sourceDatabasePath) + ".bookshelf-media-review.json";
            if (required.Count == 0)
            {
                RemoveStaleReview(reviewPath);
                return new Dictionary<int, BookshelfMediaChoice>();
            }

            var mapPath = Path.GetFullPath(sourceDatabasePath) + ".bookshelf-media-map.json";
            if (!File.Exists(mapPath))
            {
                WriteReview(reviewPath, required, ReadReviewChoices(reviewPath));
                throw MissingMappings(required.Count, mapPath, reviewPath);
            }

            ResolutionDocument document;
            try
            {
                document = JsonConvert.DeserializeObject<ResolutionDocument>(File.ReadAllText(mapPath));
            }
            catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
            {
                WriteReview(reviewPath, required, ReadReviewChoices(reviewPath));
                throw new InvalidDataException(
                    $"BookshelfNG media map '{mapPath}' could not be read as JSON. The source database was not changed.",
                    exception);
            }

            if (document == null || document.Version != SupportedMapVersion || document.Books == null)
            {
                WriteReview(reviewPath, required, ReadReviewChoices(reviewPath));
                throw new InvalidDataException(
                    $"BookshelfNG media map '{mapPath}' must contain version {SupportedMapVersion} and a books array. The source database was not changed.");
            }

            var choices = new Dictionary<int, BookshelfMediaChoice>();
            foreach (var entry in document.Books)
            {
                if (entry == null || entry.SourceBookId <= 0 || !TryParseChoice(entry.MediaType, out var choice))
                {
                    WriteReview(reviewPath, required, choices);
                    throw new InvalidDataException(
                        $"BookshelfNG media map '{mapPath}' contains an invalid book ID or mediaType. Use audiobook, ebook, or both. The source database was not changed.");
                }

                if (!choices.TryAdd(entry.SourceBookId, choice))
                {
                    WriteReview(reviewPath, required, choices);
                    throw new InvalidDataException(
                        $"BookshelfNG media map '{mapPath}' contains duplicate source book ID {entry.SourceBookId}. The source database was not changed.");
                }
            }

            var requiredIds = required.Select(x => x.SourceBookId).ToHashSet();
            var unexpectedIds = choices.Keys.Except(requiredIds).OrderBy(x => x).ToArray();
            if (unexpectedIds.Length > 0)
            {
                WriteReview(reviewPath, required, choices);
                throw new InvalidDataException(
                    $"BookshelfNG media map '{mapPath}' contains IDs that are not ambiguous in the current source ({string.Join(", ", unexpectedIds)}). The source database was not changed.");
            }

            var missingCount = required.Count(x => !choices.ContainsKey(x.SourceBookId));
            if (missingCount > 0)
            {
                WriteReview(reviewPath, required, choices);
                throw MissingMappings(missingCount, mapPath, reviewPath);
            }

            RemoveStaleReview(reviewPath);
            return choices;
        }

        private static void RemoveStaleReview(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // A stale review file is harmless once every current source choice is resolved.
            }
        }

        private static List<ReviewBook> FindRequiredMappings(SqliteConnection source)
        {
            using var command = source.CreateCommand();
            command.CommandText = @"
                SELECT b.Id,
                       COALESCE(b.Title, ''),
                       COALESCE(am.Name, ''),
                       b.Monitored,
                       COUNT(e.Id),
                       COUNT(DISTINCT CASE WHEN e.Id IS NOT NULL THEN CASE WHEN COALESCE(e.IsEbook, 0) = 1 THEN 1 ELSE 0 END END),
                       SUM(CASE WHEN e.Monitored = 1 THEN 1 ELSE 0 END)
                FROM Books b
                LEFT JOIN AuthorMetadata am ON am.Id = b.AuthorMetadataId
                LEFT JOIN Editions e ON e.BookId = b.Id
                GROUP BY b.Id
                ORDER BY b.Id;";

            var result = new List<ReviewBook>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var sourceBookId = reader.GetInt32(0);
                var title = reader.GetString(1);
                var author = reader.GetString(2);
                var monitored = Convert.ToInt32(reader.GetValue(3), CultureInfo.InvariantCulture) != 0;
                var editionCount = Convert.ToInt32(reader.GetValue(4), CultureInfo.InvariantCulture);
                var mediaTypeCount = Convert.ToInt32(reader.GetValue(5), CultureInfo.InvariantCulture);
                var monitoredEditionCount = Convert.ToInt32(reader.GetValue(6) is DBNull ? 0 : reader.GetValue(6), CultureInfo.InvariantCulture);

                if (editionCount == 0)
                {
                    result.Add(new ReviewBook
                    {
                        SourceBookId = sourceBookId,
                        Title = title,
                        Author = author,
                        Monitored = monitored,
                        Reason = "No source edition row identifies this work as an audiobook or ebook."
                    });
                }
                else if (monitored && mediaTypeCount > 1 && monitoredEditionCount == 0)
                {
                    result.Add(new ReviewBook
                    {
                        SourceBookId = sourceBookId,
                        Title = title,
                        Author = author,
                        Monitored = true,
                        Reason = "The work has audiobook and ebook editions, but no edition is selected for monitoring."
                    });
                }
            }

            return result;
        }

        private static InvalidDataException MissingMappings(int count, string mapPath, string reviewPath)
        {
            return new InvalidDataException(
                $"{count} BookshelfNG book(s) need an explicit media type before conversion. Review '{reviewPath}', " +
                $"fill each mediaType with audiobook, ebook, or both, and save it as '{mapPath}'. The source database was not changed.");
        }

        private static bool TryParseChoice(string value, out BookshelfMediaChoice choice)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case "audiobook":
                    choice = BookshelfMediaChoice.Audiobook;
                    return true;
                case "ebook":
                    choice = BookshelfMediaChoice.Ebook;
                    return true;
                case "both":
                    choice = BookshelfMediaChoice.Both;
                    return true;
                default:
                    choice = default;
                    return false;
            }
        }

        private static string FormatChoice(BookshelfMediaChoice choice)
        {
            return choice switch
            {
                BookshelfMediaChoice.Audiobook => "audiobook",
                BookshelfMediaChoice.Ebook => "ebook",
                BookshelfMediaChoice.Both => "both",
                _ => throw new ArgumentOutOfRangeException(nameof(choice), choice, null)
            };
        }

        private static Dictionary<int, BookshelfMediaChoice> ReadReviewChoices(string path)
        {
            var result = new Dictionary<int, BookshelfMediaChoice>();
            if (!File.Exists(path))
            {
                return result;
            }

            try
            {
                var document = JsonConvert.DeserializeObject<ResolutionDocument>(File.ReadAllText(path));
                if (document?.Books == null)
                {
                    return result;
                }

                foreach (var entry in document.Books)
                {
                    if (entry != null && entry.SourceBookId > 0 &&
                        TryParseChoice(entry.MediaType, out var choice) &&
                        !result.ContainsKey(entry.SourceBookId))
                    {
                        result.Add(entry.SourceBookId, choice);
                    }
                }
            }
            catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
            {
                // Replace an unreadable review with a fresh one without losing any source data.
            }

            return result;
        }

        private static void WriteReview(
            string path,
            IReadOnlyCollection<ReviewBook> books,
            IReadOnlyDictionary<int, BookshelfMediaChoice> preservedChoices = null)
        {
            var document = new ReviewDocument
            {
                Version = SupportedMapVersion,
                Instructions = "Set mediaType to audiobook, ebook, or both for every entry, then save as the matching .bookshelf-media-map.json file.",
                Books = books.Select(book => new ResolutionEntry
                {
                    SourceBookId = book.SourceBookId,
                    Title = book.Title,
                    Author = book.Author,
                    Monitored = book.Monitored,
                    Reason = book.Reason,
                    MediaType = preservedChoices != null && preservedChoices.TryGetValue(book.SourceBookId, out var choice)
                        ? FormatChoice(choice)
                        : string.Empty
                }).ToList()
            };

            var directory = Path.GetDirectoryName(path);
            Directory.CreateDirectory(directory);
            var temporaryPath = path + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(temporaryPath, JsonConvert.SerializeObject(document, Formatting.Indented));
                if (!OperatingSystem.IsWindows())
                {
                    File.SetUnixFileMode(temporaryPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                }

                File.Move(temporaryPath, path, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        private class ResolutionDocument
        {
            [JsonProperty("version")]
            public int Version { get; set; }

            [JsonProperty("books")]
            public List<ResolutionEntry> Books { get; set; }

            [JsonProperty("instructions")]
            public string Instructions { get; set; }
        }

        private sealed class ReviewDocument : ResolutionDocument
        {
        }

        private sealed class ResolutionEntry
        {
            [JsonProperty("sourceBookId")]
            public int SourceBookId { get; set; }

            [JsonProperty("mediaType")]
            public string MediaType { get; set; }

            [JsonProperty("title")]
            public string Title { get; set; }

            [JsonProperty("author")]
            public string Author { get; set; }

            [JsonProperty("monitored")]
            public bool Monitored { get; set; }

            [JsonProperty("reason")]
            public string Reason { get; set; }
        }

        private sealed class ReviewBook
        {
            public int SourceBookId { get; set; }
            public string Title { get; set; }
            public string Author { get; set; }
            public bool Monitored { get; set; }
            public string Reason { get; set; }
        }
    }
}
