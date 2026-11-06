using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NzbDrone.Core.MediaFiles;

namespace NzbDrone.Core.Parser
{
    // A names-only inventory, not metadata identification or permission to import.
    public static class IllustratedLibraryListing
    {
        public sealed record Entry(string Name, string Type, string Series, string Issue, string Limitation);

        public static bool IsExtensionlessBundle(string path)
        {
            return !MediaFileExtensions.TextExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase) &&
                Regex.IsMatch(Path.GetFileName(path), @"(?:v|vol\.?| )\d+-\d+$", RegexOptions.IgnoreCase);
        }

        public static string Classify(string path)
        {
            var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            foreach (var type in new[] { "comics", "manga", "magazines" })
            {
                if (parts.Take(parts.Length - 1).Contains(type, StringComparer.OrdinalIgnoreCase)) return type;
            }

            var name = Path.GetFileName(path);
            if (IsExtensionlessBundle(name)) return "manga";
            return string.Equals(Path.GetExtension(name), ".pdf", StringComparison.OrdinalIgnoreCase) ? "magazines" : "comics";
        }

        public sealed record IssueIdentity(string Series, string Issue);
        public sealed record Inventory(IReadOnlyList<Entry> Entries, IReadOnlyList<IssueIdentity> MissingIssues);

        public static Entry List(string relativePath, string libraryType)
        {
            if (libraryType != "comics" && libraryType != "manga" && libraryType != "magazines")
            {
                throw new ArgumentException("Unknown illustrated library type", nameof(libraryType));
            }

            var name = Path.GetFileName(relativePath);
            if (!MediaFileExtensions.TextExtensions.Contains(Path.GetExtension(name), StringComparer.OrdinalIgnoreCase))
            {
                return new Entry(name, libraryType, null, null, IsExtensionlessBundle(name)
                    ? "Extensionless bundle: no individual media file or issue identity supplied"
                    : "Unrecognized file: no known media extension or volume range");
            }

            var normalized = Regex.Replace(name, @"[._]", " ");
            var numbered = Regex.Match(normalized, @"^(?<series>.+?)[ -]+(?:Issue|No)\s+(?<issue>\d+)\b", RegexOptions.IgnoreCase);
            return new Entry(name, libraryType,
                numbered.Success ? numbered.Groups["series"].Value.Trim() : null,
                numbered.Success ? NormalizeIssue(numbered.Groups["issue"].Value) : null,
                numbered.Success || libraryType != "magazines" ? null : "No unambiguous numbered magazine identity; catalog metadata required");
        }

        public static IReadOnlyList<(string Series, string Issue)> MissingIssues(
            IEnumerable<(string Series, string Issue)> expected,
            IEnumerable<(string Series, string Issue)> owned)
        {
            return expected.Select(issue => (issue.Series, NormalizeIssue(issue.Issue)))
                .Except(owned.Select(issue => (issue.Series, NormalizeIssue(issue.Issue)))).ToList();
        }

        public static string NormalizeIssue(string issue)
        {
            if (issue == null)
            {
                return null;
            }

            if (Regex.IsMatch(issue, @"^\d{4}-\d{2}-\d{2}$"))
            {
                return issue;
            }

            return Regex.Replace(issue, @"\d+", match => match.Value.TrimStart('0') is var value && value.Length > 0 ? value : "0");
        }
    }
}
