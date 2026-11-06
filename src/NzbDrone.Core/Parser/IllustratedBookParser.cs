using System.IO;
using System.Text.RegularExpressions;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Parser
{
    // Filename evidence only: metadata still supplies the author and persistent series link.
    public static class IllustratedBookParser
    {
        private static readonly Regex Numbered = new(@"^(?<series>.+?)\s+(?:(?:v|vol\.?|band)\s*)?(?<number>\d{1,3})(?:\s+Ch\.?\s*(?<chapter>\d+))?(?:\s*\((?<year>\d{4})\))?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex Dated = new(@"^(?<series>.+?)\s+(?<date>\d{4}-\d{2}-\d{2})$", RegexOptions.Compiled);

        public static ParsedBookInfo Parse(string title)
        {
            var quality = QualityParser.ParseQuality(title);
            // Do not intercept ordinary numbered ebook titles in the author parser.
            if (quality.Quality != Quality.CBZ && quality.Quality != Quality.CBR && quality.Quality != Quality.CB7)
            {
                return null;
            }

            var name = Path.GetFileName(title);
            name = Regex.Replace(name, @"(?:\.(?:cbz|cbr|cb7|pdf)|\s+\[?(?:CBZ|CBR|CB7|PDF)\]?)$", "", RegexOptions.IgnoreCase);
            var dated = Dated.Match(name);
            var numbered = Numbered.Match(name);
            if (!dated.Success && !numbered.Success)
            {
                return null;
            }

            var match = dated.Success ? dated : numbered;
            var position = dated.Success ? match.Groups["date"].Value : int.Parse(match.Groups["number"].Value).ToString(System.Globalization.CultureInfo.InvariantCulture);
            return new ParsedBookInfo
            {
                BookTitle = name,
                SeriesTitle = match.Groups["series"].Value,
                SeriesPosition = position,
                ChapterNumber = numbered.Groups["chapter"].Success ? numbered.Groups["chapter"].Value : null,
                ReleaseDate = dated.Success ? position : match.Groups["year"].Value,
                Quality = quality,
                ReleaseTitle = title
            };
        }
    }
}
