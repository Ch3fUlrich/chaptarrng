using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NzbDrone.Core.Books;
using NzbDrone.Core.Indexers.Soulseek;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Soulseek
{
    public class SlskdSearchContext
    {
        public string Author { get; set; }
        public string Title { get; set; }

        // Further names the book is searched/known under (search title override, main title); a path matching
        // any of them counts. Title is always considered.
        public List<string> AlternateTitles { get; set; } = new();

        public IEnumerable<string> AllTitles => new[] { Title }.Concat(AlternateTitles ?? new List<string>()).Where(t => !string.IsNullOrWhiteSpace(t)).Distinct();

        // null = both media types acceptable
        public BookMediaType? MediaType { get; set; }
    }

    // Pure mapping of slskd search responses to releases (no I/O), see docs/slskd.md.
    public static class SlskdReleaseBuilder
    {
        public static readonly string[] EbookExtensions = { "epub", "azw3", "mobi", "pdf" };
        public static readonly string[] AudioExtensions = { "mp3", "m4b", "m4a", "flac", "ogg", "opus", "aac" };

        private const long MinimumEbookBytes = 20 * 1024;
        private const int AlbumGuardFileCount = 8;
        private const long AlbumGuardAverageBytes = 2500 * 1024;
        private static readonly Regex DiscFolderRegex = new(@"^(cd|disc|disk|teil|part)\s*\d+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private sealed class Candidate
        {
            public ReleaseInfo Release { get; set; }
            public double Score { get; set; }
            public Language Language { get; set; }
        }

        public static IList<ReleaseInfo> Build(IEnumerable<SlskdSearchResponse> responses, SlskdSearchContext context, SlskdIndexerSettings settings)
        {
            var candidates = new List<Candidate>();
            var allowedLanguages = ResolveLanguages(settings);
            var restrictLanguages = settings.LanguageNames().Count > 0;

            foreach (var response in responses ?? Enumerable.Empty<SlskdSearchResponse>())
            {
                if (string.IsNullOrWhiteSpace(response?.Username) || response.Files == null || response.Files.Count == 0)
                {
                    continue;
                }

                if (settings.RequireFreeSlot && !response.HasFreeUploadSlot)
                {
                    continue;
                }

                if (settings.MaximumQueueLength > 0 && response.QueueLength > settings.MaximumQueueLength)
                {
                    continue;
                }

                if (settings.MinimumUploadSpeedKBps > 0 && response.UploadSpeed < settings.MinimumUploadSpeedKBps * 1024L)
                {
                    continue;
                }

                var files = response.Files
                    .Where(file => file != null && !file.IsLocked && file.Size > 0 &&
                                   !string.IsNullOrWhiteSpace(file.Filename) && !SlskdDownloadToken.HasTraversal(file.Filename))
                    .Select(file => new RemoteFile(file))
                    .ToList();

                if (context.MediaType != BookMediaType.Audiobook)
                {
                    foreach (var file in files.Where(f => EbookExtensions.Contains(f.Extension) && f.Size >= MinimumEbookBytes))
                    {
                        AddCandidate(candidates, BuildEbook(response, file, context, settings), allowedLanguages, restrictLanguages, settings);
                    }
                }

                if (context.MediaType != BookMediaType.Ebook)
                {
                    foreach (var group in GroupAudioFolders(files.Where(f => AudioExtensions.Contains(f.Extension))))
                    {
                        AddCandidate(candidates, BuildAudiobook(response, group.Key, group.ToList(), context, settings), allowedLanguages, restrictLanguages, settings);
                    }
                }
            }

            return Cap(candidates, settings.MaximumResults);
        }

        private static Candidate BuildEbook(SlskdSearchResponse response, RemoteFile file, SlskdSearchContext context, SlskdIndexerSettings settings)
        {
            var ratio = BestRatio(context, file.Filename);
            if (!PassesMatch(ratio, context, file.Filename, settings))
            {
                return null;
            }

            var stem = file.Name.Contains('.') ? file.Name.Substring(0, file.Name.LastIndexOf('.')) : file.Name;
            var score = (ratio * 100) + (file.Extension == "epub" ? 5 : 0) - ExtraPenalty(context, stem);
            return Create(response, file.Directory, file.Separator, new List<RemoteFile> { file }, file.Extension, context, ratio, score, file.Filename);
        }

        private static Candidate BuildAudiobook(SlskdSearchResponse response, string directory, List<RemoteFile> files, SlskdSearchContext context, SlskdIndexerSettings settings)
        {
            var total = files.Sum(f => f.Size);
            if (total < settings.MinimumAudiobookSizeMB * 1024L * 1024L || files.Count > SlskdDownloadToken.MaxFiles)
            {
                return null;
            }

            var ratio = BestRatio(context, directory + " " + string.Join(" ", files.Select(f => f.Name)));
            var hasAuthor = SlskdTextMatcher.PathContainsAuthor(context.Author, directory);

            // Album guard: many small tracks are music, not an audiobook, unless the author is named.
            if (files.Count >= AlbumGuardFileCount && total / files.Count < AlbumGuardAverageBytes && !hasAuthor)
            {
                return null;
            }

            if (!PassesMatch(ratio, context, directory, settings))
            {
                return null;
            }

            var container = files.GroupBy(f => f.Extension).OrderByDescending(g => g.Count()).First().Key;
            if (files.Any(f => f.Extension == "m4b"))
            {
                container = "m4b";
            }

            var score = (ratio * 100) + (container == "m4b" ? 10 : 0) - ExtraPenalty(context, LastSegment(directory, files[0].Separator));
            return Create(response, directory, files[0].Separator, files, container, context, ratio, score, directory);
        }

        private static double BestRatio(SlskdSearchContext context, string path)
        {
            return context.AllTitles.Select(title => SlskdTextMatcher.TitleMatchRatio(title, path)).DefaultIfEmpty(0).Max();
        }

        // Sequels and companions ("Dune Messiah" for "Dune") carry extra significant words: rank them below the
        // exact match instead of treating them as equal (they stay visible, the decision engine decides).
        private static double ExtraPenalty(SlskdSearchContext context, string name)
        {
            return 30 * Math.Min(SlskdTextMatcher.ExtraTokenCount(context.AllTitles, context.Author, name), 3);
        }

        private static bool PassesMatch(double ratio, SlskdSearchContext context, string path, SlskdIndexerSettings settings)
        {
            if (ratio < settings.MinimumTitleMatchPercent / 100.0)
            {
                return false;
            }

            return !settings.RequireAuthorMatch || SlskdTextMatcher.PathContainsAuthor(context.Author, path);
        }

        private static Candidate Create(SlskdSearchResponse response, string directory, char separator, List<RemoteFile> files, string container, SlskdSearchContext context, double ratio, double score, string languagePath)
        {
            var token = new SlskdDownloadToken
            {
                Username = response.Username,
                Directory = directory,
                Separator = separator,
                Files = files.OrderBy(f => f.Filename, StringComparer.Ordinal).Select(f => new SlskdTokenFile { Filename = f.Filename, Size = f.Size }).ToList()
            };

            var key = response.Username + "|" + string.Join("|", token.Files.Select(f => f.Filename + ":" + f.Size));
            var title = string.IsNullOrWhiteSpace(context.Author) ? $"{context.Title} [{container}]" : $"{context.Author} - {context.Title} [{container}]";
            var language = SlskdTextMatcher.DetectLanguage(languagePath);
            var kbps = response.UploadSpeed / 1024;

            score += response.HasFreeUploadSlot ? 20 : 0;
            score -= Math.Min(response.QueueLength, 50) / 5.0;
            score += Math.Min(kbps / 100.0, 5);

            var release = new ReleaseInfo
            {
                Guid = "Soulseek-" + key.SHA256Hash().Substring(0, 24),
                Title = title,
                Author = context.Author,
                Book = context.Title,
                Size = files.Sum(f => f.Size),
                PublishDate = DateTime.UtcNow,
                DownloadUrl = SlskdDownloadToken.ToUrl(token),
                Container = container,
                Source = "Soulseek",
                Origin = $"{response.Username} (free slot: {(response.HasFreeUploadSlot ? "yes" : "no")}, queue: {response.QueueLength}, {kbps} KB/s) | {Shorten(LastSegment(languagePath, separator))}",
                Languages = language == null ? new List<Language>() : new List<Language> { language }
            };

            return new Candidate { Release = release, Score = score, Language = language };
        }

        private static void AddCandidate(List<Candidate> candidates, Candidate candidate, HashSet<Language> allowed, bool restrictLanguages, SlskdIndexerSettings settings)
        {
            if (candidate == null)
            {
                return;
            }

            if (candidate.Language == null ? !settings.AllowUnknownLanguage : restrictLanguages && !allowed.Contains(candidate.Language))
            {
                return;
            }

            if (candidates.All(existing => existing.Release.Guid != candidate.Release.Guid))
            {
                candidates.Add(candidate);
            }
        }

        private static IList<ReleaseInfo> Cap(List<Candidate> candidates, int maximum)
        {
            var ordered = candidates.OrderByDescending(c => c.Score).ThenBy(c => c.Release.Guid, StringComparer.Ordinal).ToList();
            var chosen = ordered.Take(Math.Max(1, maximum)).ToList();

            // Best release of every detected language survives the cap.
            foreach (var best in ordered.Where(c => c.Language != null).GroupBy(c => c.Language.Id).Select(g => g.First()))
            {
                if (!chosen.Contains(best))
                {
                    chosen.Add(best);
                }
            }

            return chosen.OrderByDescending(c => c.Score).Select(c => c.Release).ToList();
        }

        private static HashSet<Language> ResolveLanguages(SlskdIndexerSettings settings)
        {
            var result = new HashSet<Language>();
            foreach (var name in settings.LanguageNames())
            {
                var language = Language.All.FirstOrDefault(l => l != null && string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase));
                if (language != null)
                {
                    result.Add(language);
                }
            }

            return result;
        }

        // Disc/part subfolders are merged into their parent folder.
        private static IEnumerable<IGrouping<string, RemoteFile>> GroupAudioFolders(IEnumerable<RemoteFile> files)
        {
            return files.GroupBy(file =>
            {
                var directory = file.Directory;
                var last = LastSegment(directory, file.Separator);
                return DiscFolderRegex.IsMatch(last) && directory.Length > last.Length + 1
                    ? directory.Substring(0, directory.Length - last.Length - 1)
                    : directory;
            });
        }

        private static string Shorten(string text) => text.Length <= 120 ? text : text.Substring(0, 120);

        private static string LastSegment(string path, char separator)
        {
            var index = path.LastIndexOf(separator);
            return index < 0 ? path : path.Substring(index + 1);
        }

        private sealed class RemoteFile
        {
            public RemoteFile(SlskdFile file)
            {
                Filename = file.Filename;
                Size = file.Size;
                Separator = file.Filename.LastIndexOf('\\') >= file.Filename.LastIndexOf('/') && file.Filename.Contains('\\') ? '\\' : '/';
                var index = file.Filename.LastIndexOf(Separator);
                Directory = index < 0 ? string.Empty : file.Filename.Substring(0, index);
                Name = index < 0 ? file.Filename : file.Filename.Substring(index + 1);
                var dot = Name.LastIndexOf('.');
                Extension = dot < 0 ? string.Empty : Name.Substring(dot + 1).ToLowerInvariant();
            }

            public string Filename { get; }
            public string Directory { get; }
            public string Name { get; }
            public string Extension { get; }
            public long Size { get; }
            public char Separator { get; }
        }
    }
}
