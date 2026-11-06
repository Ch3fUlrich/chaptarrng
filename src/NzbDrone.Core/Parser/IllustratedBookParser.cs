using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Parser
{
    // Filename evidence only: metadata still supplies the author and persistent series link.
    public static class IllustratedBookParser
    {
        private static readonly Regex Numbered = new(@"^(?<series>.+?)\s+(?:(?:v|vol\.?|band)\s*|#)?(?<number>\d+)(?:\s+Ch\.?\s*(?<chapter>\d+))?(?:\s*\((?<year>\d{4})\))?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex Dated = new(@"^(?<series>.+?)\s+(?<date>\d{4}-(?:\d{2}-\d{2}|\d{3}))$", RegexOptions.Compiled);

        public static ParsedBookInfo Parse(string title)
        {
            var quality = QualityParser.ParseQuality(title);
            // Do not intercept ordinary numbered ebook titles in the author parser.
            if (quality.Quality != Quality.CBZ && quality.Quality != Quality.CBR && quality.Quality != Quality.CB7)
            {
                return null;
            }

            var name = Path.GetFileName(title);
            name = Regex.Replace(name, @"(?:\.(?:cbz|cbr|cb7)|\s+[\[(]?(?:CBZ|CBR|CB7)[\])]?(?:\s*[\[(][^\])]*[\])])*)$", "", RegexOptions.IgnoreCase);
            var dated = Dated.Match(name);
            var numbered = Numbered.Match(name);
            if (!dated.Success && Regex.IsMatch(name, @"\s\d{4}-\d+(?:-\d+)?$"))
            {
                return null;
            }
            var date = dated.Groups["date"].Value;
            var realDate = date.Length == 10 && DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
            if (dated.Success && date.Length == 10 && !realDate)
            {
                return null;
            }
            if (!dated.Success && !numbered.Success)
            {
                return null;
            }

            var match = dated.Success ? dated : numbered;
            var position = dated.Success ? null : IllustratedLibraryListing.NormalizeIssue(match.Groups["number"].Value);
            return new ParsedBookInfo
            {
                BookTitle = name,
                SeriesTitle = match.Groups["series"].Value,
                SeriesPosition = position,
                IssueIdentifier = dated.Success ? match.Groups["date"].Value : null,
                ChapterNumber = numbered.Groups["chapter"].Success ? numbered.Groups["chapter"].Value : null,
                ReleaseDate = realDate ? date : null,
                Quality = quality,
                ReleaseTitle = title
            };
        }
    }
}
