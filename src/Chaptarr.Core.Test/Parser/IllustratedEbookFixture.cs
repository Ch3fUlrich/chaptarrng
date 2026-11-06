using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Test.Parser
{
    [TestFixture]
    public class IllustratedEbookFixture
    {
        public static IEnumerable<TestCaseData> SyntheticItems()
        {
            foreach (var type in new[] { "comics", "manga", "magazines" })
            {
                foreach (var language in new[] { "en", "de" })
                {
                    for (var i = 1; i <= 10; i++)
                    {
                        var extension = new[] { "cbz", "cbr", "cb7" }[(i - 1) % 3];
                        var series = language == "de" ? "Synthetische Bücher" : "Synthetic Books";
                        var suffix = type == "magazines" ? $"2026-01-{i:00}" : type == "manga" ? (language == "de" ? $"Band {i}" : $"Vol. {i} Ch. 88") : (i % 2 == 0 ? $"{i:000}" : $"v{i:00} (2019)");
                        yield return new TestCaseData($"/books/{type}/{language}/{series} {suffix}.{extension}", series, type == "magazines" ? suffix : i.ToString(), extension).SetName($"Synthetic_{type}_{language}_{i:00}");
                    }
                }
            }
        }

        [TestCaseSource(nameof(SyntheticItems))]
        public void synthetic_item_is_parsed_and_classified(string path, string series, string position, string extension)
        {
            var parsed = NzbDrone.Core.Parser.Parser.ParseBookTitle(path);
            Assert.That(parsed, Is.Not.Null);
            Assert.That(parsed.SeriesTitle, Is.EqualTo(series));
            Assert.That(parsed.IssueIdentifier ?? parsed.SeriesPosition, Is.EqualTo(position));
            Assert.That(parsed.Quality.Quality.Name, Is.EqualTo(extension.ToUpperInvariant()));
            Assert.That(QualityMediaTypeHelper.GetKnownMediaType(parsed.Quality.Quality), Is.EqualTo(BookMediaType.Ebook));
            Assert.That(MediaFileExtensions.TextExtensions.Contains("." + extension), Is.True);
            Assert.That(MediaFileExtensions.IsSingleFileBookContainer("." + extension), Is.True);
        }

        [TestCase("CBZ")]
        [TestCase("CBR")]
        [TestCase("CB7")]
        public void release_title_quality(string format)
        {
            Assert.That(NzbDrone.Core.Parser.QualityParser.ParseQuality($"Synthetic Series v03 [{format}]").Quality.Name, Is.EqualTo(format));
        }

        [TestCase("Author - Ordinary Book 12.pdf")]
        [TestCase("Author - Ordinary Book 12.epub")]
        [TestCase("Author - Ordinary Book 12.mobi")]
        [TestCase("Author - Ordinary Book 12.azw3")]
        public void ordinary_ebook_does_not_use_illustrated_parser(string title)
        {
            Assert.That(NzbDrone.Core.Parser.IllustratedBookParser.Parse(title), Is.Null);
        }

        [Test]
        public void dated_magazine_parity_detects_gap_per_series()
        {
            var expected = new[] { ("Synthetic Science", "2026-01-01"), ("Synthetic Science", "2026-02-01"), ("Synthetisches Wissen", "2026-01-01"), ("Synthetisches Wissen", "2026-02-01") };
            var owned = expected.Where(issue => issue != ("Synthetic Science", "2026-02-01"))
                .Select(issue => NzbDrone.Core.Parser.Parser.ParseBookTitle($"/books/magazines/en/{issue.Item1} {issue.Item2}.cbz"))
                .Select(parsed => (parsed.SeriesTitle, parsed.IssueIdentifier));
            Assert.That(NzbDrone.Core.Parser.IllustratedLibraryListing.MissingIssues(expected, owned), Is.EqualTo(new[] { ("Synthetic Science", "2026-02-01") }));
        }
    }
}
