using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Books.Calibre;

namespace NzbDrone.Core.Books
{
    public class WorkVersion
    {
        public int BookId { get; set; }
        public string Title { get; set; }
        public BookMediaType MediaType { get; set; }

        // Canonical ISO 639-2 row language, null when the row carries none.
        public string Language { get; set; }
        public bool IsVariant { get; set; }
        public bool Monitored { get; set; }
        public bool HasFiles { get; set; }
    }

    public class WorkGroup
    {
        public string WorkKey { get; set; }
        public string Title { get; set; }
        public List<WorkVersion> Versions { get; set; } = new List<WorkVersion>();
    }

    // Groups the rows of one author into works: rows sharing a BaseBookId or a work-level provider id are one
    // work (ebook and audiobook rows, language variants, historical duplicates). Pure, so it is unit-testable.
    public static class WorkGrouping
    {
        public static List<WorkGroup> Group(IEnumerable<Book> books, ISet<int> bookIdsWithFiles = null)
        {
            var rows = (books ?? Enumerable.Empty<Book>()).Where(b => b != null).OrderBy(b => b.Id).ToList();
            var parent = Enumerable.Range(0, rows.Count).ToArray();

            int Find(int i)
            {
                while (parent[i] != i)
                {
                    parent[i] = parent[parent[i]];
                    i = parent[i];
                }

                return i;
            }

            void Union(int a, int b)
            {
                var ra = Find(a);
                var rb = Find(b);
                if (ra != rb)
                {
                    parent[Math.Max(ra, rb)] = Math.Min(ra, rb);
                }
            }

            var byBase = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < rows.Count; i++)
            {
                if (rows[i].BaseBookId.IsNotNullOrWhiteSpace())
                {
                    if (byBase.TryGetValue(rows[i].BaseBookId, out var first))
                    {
                        Union(first, i);
                    }
                    else
                    {
                        byBase[rows[i].BaseBookId] = i;
                    }
                }
            }

            // Work-level provider ids join rows whose BaseBookId differs (e.g. an audiobook row added separately).
            // Variant rows are tied to their base by BaseBookId only, so skip them here.
            var ordinary = Enumerable.Range(0, rows.Count).Where(i => !rows[i].IsWantedVariantRow()).ToList();
            for (var x = 0; x < ordinary.Count; x++)
            {
                for (var y = x + 1; y < ordinary.Count; y++)
                {
                    if (Find(ordinary[x]) != Find(ordinary[y]) && WorkIdMatcher.WorkProviderIdMatches(rows[ordinary[x]], rows[ordinary[y]]))
                    {
                        Union(ordinary[x], ordinary[y]);
                    }
                }
            }

            var groups = new Dictionary<int, WorkGroup>();
            var order = new List<int>();
            for (var i = 0; i < rows.Count; i++)
            {
                var root = Find(i);
                if (!groups.TryGetValue(root, out var group))
                {
                    group = new WorkGroup { WorkKey = rows[root].BaseBookId.IsNotNullOrWhiteSpace() ? rows[root].BaseBookId : "row:" + rows[root].Id };
                    groups[root] = group;
                    order.Add(root);
                }

                var row = rows[i];
                group.Versions.Add(new WorkVersion
                {
                    BookId = row.Id,
                    Title = row.Title,
                    MediaType = row.MediaType,
                    Language = row.IsWantedVariantRow() ? row.GetWantedVariantLanguage() : NormalizeLanguage(row.LanguageCode),
                    IsVariant = row.IsWantedVariantRow(),
                    Monitored = row.IsMonitored(),
                    HasFiles = bookIdsWithFiles != null && bookIdsWithFiles.Contains(row.Id)
                });
            }

            foreach (var group in groups.Values)
            {
                group.Versions = group.Versions
                    .OrderBy(v => v.IsVariant)
                    .ThenBy(v => v.MediaType == BookMediaType.Ebook ? 0 : 1)
                    .ThenBy(v => v.Language ?? string.Empty, StringComparer.Ordinal)
                    .ThenBy(v => v.BookId)
                    .ToList();
                group.Title = (group.Versions.FirstOrDefault(v => !v.IsVariant) ?? group.Versions[0]).Title;
            }

            return order.Select(r => groups[r]).ToList();
        }

        private static string NormalizeLanguage(string language)
        {
            return language.IsNullOrWhiteSpace() ? null : language.CanonicalizeLanguage();
        }
    }
}
