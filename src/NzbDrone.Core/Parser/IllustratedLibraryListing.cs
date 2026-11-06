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

        public static Entry List(string relativePath, string libraryType)
        {
            if (libraryType != "comics" && libraryType != "manga" && libraryType != "magazines")
            {
                throw new ArgumentException("Unknown illustrated library type", nameof(libraryType));
            }

            var name = Path.GetFileName(relativePath);
            if (!MediaFileExtensions.TextExtensions.Contains(Path.GetExtension(name)))
            {
                return new Entry(name, libraryType, null, null, "Extensionless bundle: no individual media file or issue identity supplied");
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
