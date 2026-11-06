using System.Linq;
using NUnit.Framework;
using NzbDrone.Core.Parser;

namespace NzbDrone.Core.Test.Parser
{
    [TestFixture]
    public class IllustratedRealSampleFixture
    {
        [TestCase("comics", "(Kraven's Last Hunt 01) Web Of Spider-Man 031 (1987) (digital)/(Kraven's Last Hunt 01) Web Of Spider-Man 031 (1987) (digital).cbr")]
        [TestCase("comics", "(Kraven's Last Hunt 04) Web Of Spider-Man 032 (1987) (digital)/(Kraven's Last Hunt 04) Web Of Spider-Man 032 (1987) (digital).cbr")]
        [TestCase("comics", "008.Kraven s.Last.Hunt.(Event).(Digital).(TPB).Comic.eBook-BettyBoop/008.Kraven_s.Last.Hunt.(Event).(Digital).(TPB).Comic.eBook-BettyBoop.cbz")]
        [TestCase("comics", "0178  A Contract with God (October 1978) [Pyramid]/A Contract with God (October 1978) [Pyramid].cbz")]
        [TestCase("comics", "100.Bullets.-.Book.01.(2024).(digital).(Son.of.Ultron-Empire).Comic.eBook-BettyBoop/100.Bullets.-.Book.01.(2024).(digital).(Son.of.Ultron-Empire).Comic.eBook-BettyBoop.cbr")]
        [TestCase("comics", "102.Arkham.Asylum.-.A.Serious.House.on.a.Serious.Earth.(1987).(Digital).(TPB).Comic.eBook-BettyBoop/102.Arkham.Asylum.-.A.Serious.House.on.a.Serious.Earth.(1987).(Digital).(TPB).Comic.eBook-BettyBoop.cbz")]
        [TestCase("comics", "130.Immortal.Hulk.-.The.Threshing.Place.(2020).(Digital).(TPB).Comic.eBook-BettyBoop/130.Immortal.Hulk.-.The.Threshing.Place.(2020).(Digital).(TPB).Comic.eBook-BettyBoop.cbz")]
        [TestCase("comics", "300 (1999) (digital-Empire)/300 (1999) (digital-Empire).cbr")]
        [TestCase("comics", "300 2 of 5/300 2 of 5 .cbr")]
        [TestCase("comics", "300.-.Avengers.v03.020.(1999).Comic.eBook-BettyBoop/300.-.Avengers.v03.020.(1999).Comic.eBook-BettyBoop.cbz")]
        [TestCase("comics", "300.01.The.Amazing.Spider-Man.V1.300.(Digital).Comic.eBook-BettyBoop/300.01.The.Amazing.Spider-Man.V1.#300.(Digital).Comic.eBook-BettyBoop.cbz")]
        [TestCase("comics", "300.02.Thor.V1.391.(Scan).Comic.eBook-BettyBoop/300.02.Thor.V1.#391.(Scan).Comic.eBook-BettyBoop.cbr")]
        [TestCase("comics", "300.03.The.Spectacular.Spider-Man.V1.137.(Scan).Comic.eBook-BettyBoop/300.03.The.Spectacular.Spider-Man.V1.#137.(Scan).Comic.eBook-BettyBoop.cbr")]
        [TestCase("comics", "300.05.Web.of.Spider-Man.V1.039.(Scan).Comic.eBook-BettyBoop/300.05.Web.of.Spider-Man.V1.#039.(Scan).Comic.eBook-BettyBoop.cbz")]
        [TestCase("comics", "Abrams.-.The.Best.We.Could.Do.An.Illustrated.Memoir.2017.HYBRID.COMIC/Abrams.-.The.Best.We.Could.Do.An.Illustrated.Memoir.2017.HYBRID.COMIC.pdf")]
        [TestCase("comics", "Acme Novelty Library 001 [1993] Jimmy Corrigan - The Smartest Kid On Earth/Acme Novelty Library 001 [1993] Jimmy Corrigan - The Smartest Kid On Earth.cbz")]
        [TestCase("comics", "Acme Novelty Library 018 [2007] Building Stories/Acme Novelty Library 018 [2007] Building Stories.cbz")]
        [TestCase("comics", "Acme Novelty Library 019 [2008] Rusty Brown/Acme Novelty Library 019 [2008] Rusty Brown.cbz")]
        [TestCase("comics", "Alias 010 (2002) (Digital) (Zone-Empire)/Alias 010 (2002) (Digital) (Zone-Empire).cbr")]
        [TestCase("comics", "Alias 025 (2003) (Digital) (Zone-Empire)/Alias 025 (2003) (Digital) (Zone-Empire).cbr")]
        [TestCase("manga", "033.-.All.Aces.Battle.Royale.Comic.eBook-BettyBoop/033.-.All.Aces.Battle.Royale.Comic.eBook-BettyBoop.cbr")]
        [TestCase("manga", "13DL.APP Cardcaptor.Sakura.Clear.Card.Arc.vol.01-16/13DL.APP_Cardcaptor Sakura Clear Card Arc v01-03")]
        [TestCase("manga", "13DL.APP Fujiko.F.Fujio.Daizenshu.Doraemon.vol.01-20/13DL.APP_Fujiko F. Fujio Daizenshu Doraemon v01-05")]
        [TestCase("manga", "20th Century Boys - Band 22 (Panini, 2009) (c2c)(memphis)/20th Century Boys - Band 22 (Panini, 2009) (c2c)(memphis).cbz")]
        [TestCase("manga", "AKIRA 01 - DIE ALTE STADT/AKIRA 01 - DIE ALTE STADT.cbr")]
        [TestCase("manga", "AKIRA 02 - TETSUOS MACHT/AKIRA 02 - TETSUOS MACHT.cbr")]
        [TestCase("manga", "AKIRA 11 - SCHLACHT UM MIYAKOS TEMPEL/AKIRA 11 - SCHLACHT UM MIYAKOS TEMPEL.cbr")]
        [TestCase("manga", "AKIRA 13 - VISION IM UNTERGRUND/AKIRA 13 - VISION IM UNTERGRUND.cbr")]
        [TestCase("manga", "AKIRA 15 - DIE KRAFTPROBE/AKIRA 15 - DIE KRAFTPROBE.cbr")]
        [TestCase("manga", "AKIRA 16 - DIE TRANSMUTATION/AKIRA 16 - DIE TRANSMUTATION.cbr")]
        [TestCase("manga", "Air Gear Omnibus v03 (F) (2016) (Digital) (Shadowcat-Empire)/Air Gear Omnibus v03 (F) (2016) (Digital) (Shadowcat-Empire).cbz")]
        [TestCase("manga", "Ajin - Demi Human v07 2016 Digital Hexer-Empire/Ajin - Demi Human v07 (2016) (Digital) (Hexer-Empire).cbz")]
        [TestCase("manga", "Ajin - Demi-Human 073 (2019) (Digital) (danke-Empire)/Ajin - Demi-Human 073 (2019) (Digital) (danke-Empire).cbz")]
        [TestCase("manga", "Akame ga Kill! 04/Akame ga Kill! 04.cbz")]
        [TestCase("manga", "Akame ga Kill! 05/Akame ga Kill! 05.cbz")]
        [TestCase("manga", "Akame ga Kill! 06/Akame ga Kill! 06.cbz")]
        [TestCase("manga", "Akame ga Kill! 07/Akame ga Kill! 07.cbz")]
        [TestCase("manga", "Akame ga Kill! 08/Akame ga Kill! 08.cbz")]
        [TestCase("manga", "Akame ga Kill! 09/Akame ga Kill! 09.cbz")]
        [TestCase("manga", "Akane-banashi/Akane-banashi v01 (2023) (Digital) (1r0n).cbz")]
        [TestCase("magazines", "-[RANDOM SORT COOKBOOKS]-/Bon Appetit - January 2015.pdf")]
        [TestCase("magazines", "1989 Tangle [Bulbous Monocle Reissue 2025] [vinyl 24-96]/Thinking Fellers Union Local 282 - Tangle - BM-07liners-web.pdf")]
        [TestCase("magazines", "2001/Little Caesar - Francis Edwards Faragoh {supplement to the July 2001 issue of Sight And Sound}.pdf")]
        [TestCase("magazines", "All About History - Issue 163/All About History - Issue 163.pdf")]
        [TestCase("magazines", "All About History - Issue 164/All_About_History_-_Issue_164.pdf")]
        [TestCase("magazines", "All.About.Space-Issue.33.2014/All.About.Space-Issue.33.2014.pdf")]
        [TestCase("magazines", "All.About.Space-Issue.98.2019/All.About.Space-Issue.98.2019.pdf")]
        [TestCase("magazines", "All.About.Space.UK-Issue.13.2013/All.About.Space.UK-Issue.13.2013.pdf")]
        [TestCase("magazines", "Amateur Photographer - 21 10 2025/Amateur Photographer - 21.10.2025.pdf")]
        [TestCase("magazines", "American.Scientist-January.February.2023/American.Scientist-January.February.2023.pdf")]
        [TestCase("magazines", "Astronomy Magazine 2012-06/Astnomy201206.pdf")]
        [TestCase("magazines", "BBC Good Food (UK) - 07-2020.pdf.pdf/BBC Good Food (UK) - 07-2020.pdf.pdf")]
        [TestCase("magazines", "BBC History Magazine 2013-04/BBC History Magazine 2013-04.pdf")]
        [TestCase("magazines", "BBC Science Focus 08 2025/BBC Science Focus 08.2025.pdf")]
        [TestCase("magazines", "BBC.History.Magazine-April.2023/BBC.History.Magazine-April.2023.pdf")]
        [TestCase("magazines", "BBC.History.Magazine-January.2023/BBC.History.Magazine-January.2023.pdf")]
        [TestCase("magazines", "BBC.Sky.at.Night-January.2023/BBC.Sky.at.Night-January.2023.pdf")]
        [TestCase("magazines", "Black.And.White.Photography.For.Beginners.No.22.2025.HYBRID.MAGAZINE.eBook-21A1/Black.And.White.Photography.For.Beginners.No.22.2025.HYBRID.MAGAZINE.eBook-21A1.pdf")]
        [TestCase("magazines", "Black.And.White.Photography.For.Beginners.No.23.2026.HYBRID.MAGAZINE.eBook-21A1/Black.And.White.Photography.For.Beginners.No.23.2026.HYBRID.MAGAZINE.eBook-21A1.pdf")]
        [TestCase("magazines", "Bloomberg Businessweek (USA) - 03.08.2020.pdf/Bloomberg Businessweek (USA) - 03.08.2020.pdf")]
        public void lists_real_sample_name_and_library_type(string type, string path)
        {
            var entry = IllustratedLibraryListing.List(path, type);
            Assert.That(entry.Name, Is.EqualTo(path.Split('/').Last()));
            Assert.That(entry.Type, Is.EqualTo(type));
        }

        [TestCase("All About History", "163", "164")]
        [TestCase("All About Space", "33", "98")]
        public void real_series_parity_uses_production_gap_detection(string series, string first, string second)
        {
            var paths = series == "All About History"
                ? new[] { "All About History - Issue 163/All About History - Issue 163.pdf", "All About History - Issue 164/All_About_History_-_Issue_164.pdf" }
                : new[] { "All.About.Space-Issue.33.2014/All.About.Space-Issue.33.2014.pdf", "All.About.Space-Issue.98.2019/All.About.Space-Issue.98.2019.pdf" };
            var owned = paths.Select(path => IllustratedLibraryListing.List(path, "magazines"))
                .Select(entry => (entry.Series, entry.Issue)).ToArray();
            var expected = new[] { (series, first), (series, second) };
            Assert.That(IllustratedLibraryListing.MissingIssues(expected, owned), Is.Empty);
            Assert.That(IllustratedLibraryListing.MissingIssues(expected, owned.Take(1)), Is.EqualTo(new[] { (series, second) }));
        }

        [TestCase("Series 2026-123.cbz", "2026-123")]
        [TestCase("Series 2026-01-01.cbz", "2026-01-01")]
        public void issue_identity_is_not_a_numeric_position(string title, string identifier)
        {
            var parsed = NzbDrone.Core.Parser.Parser.ParseBookTitle(title);
            Assert.That(parsed.IssueIdentifier, Is.EqualTo(identifier));
            Assert.That(parsed.SeriesPosition, Is.Null);
        }

        [TestCase("Action Comics #1000.cbz", "1000")]
        public void large_issue_number(string title, string position)
        {
            Assert.That(NzbDrone.Core.Parser.Parser.ParseBookTitle(title).SeriesPosition, Is.EqualTo(position));
        }

        [TestCase("Author - Ordinary Book 12.pdf")]
        [TestCase("Author - Ordinary Book 12.epub")]
        [TestCase("Author - Ordinary Book 12.mobi")]
        [TestCase("Author - Ordinary Book 12.azw3")]
        [TestCase("Author - Ordinary Book 12.mp3")]
        [TestCase("Author - Ordinary Book 12.m4b")]
        public void ordinary_title_preserves_author_and_book(string title)
        {
            var parsed = NzbDrone.Core.Parser.Parser.ParseBookTitle(title);
            // This title is rejected by the pre-existing author parser for every format.
            Assert.That(parsed, Is.Null);
        }

        [TestCase("epub")]
        [TestCase("pdf")]
        [TestCase("mobi")]
        [TestCase("azw3")]
        [TestCase("mp3")]
        [TestCase("m4b")]
        public void ordinary_positive_control_preserves_author_and_book(string format)
        {
            var parsed = NzbDrone.Core.Parser.Parser.ParseBookTitle($"Freida McFadden - Want to Know a Secret ({format})");
            Assert.That(parsed, Is.Not.Null);
            Assert.That(parsed.AuthorName, Is.EqualTo("Freida McFadden"));
            Assert.That(parsed.BookTitle, Is.EqualTo("Want to Know a Secret"));
            Assert.That(parsed.SeriesTitle, Is.Null);
        }

        [TestCase("13DL.APP_Cardcaptor Sakura Clear Card Arc v01-03", "manga", "Extensionless bundle: no individual media file or issue identity supplied")]
        [TestCase("13DL.APP_Fujiko F. Fujio Daizenshu Doraemon v01-05", "manga", "Extensionless bundle: no individual media file or issue identity supplied")]
        [TestCase("Thinking Fellers Union Local 282 - Tangle - BM-07liners-web.pdf", "magazines", "No unambiguous numbered magazine identity; catalog metadata required")]
        [TestCase("Little Caesar - Francis Edwards Faragoh {supplement to the July 2001 issue of Sight And Sound}.pdf", "magazines", "No unambiguous numbered magazine identity; catalog metadata required")]
        public void real_sample_limitations_are_explicit(string name, string type, string limitation)
        {
            var entry = IllustratedLibraryListing.List(name, type);
            Assert.That(entry.Series, Is.Null);
            Assert.That(entry.Issue, Is.Null);
            Assert.That(entry.Limitation, Is.EqualTo(limitation));
        }

        [TestCase("033", "33")]
        [TestCase("2026-001", "2026-1")]
        [TestCase("000", "0")]
        public void gap_comparison_normalizes_both_sides(string padded, string canonical)
        {
            Assert.That(IllustratedLibraryListing.MissingIssues(new[] { ("Series", padded) }, new[] { ("Series", canonical) }), Is.Empty);
            Assert.That(IllustratedLibraryListing.MissingIssues(new[] { ("Series", canonical) }, new[] { ("Series", padded) }), Is.Empty);
        }

        [TestCase("2026-12")]
        [TestCase("2026-123456")]
        [TestCase("2026-02-30")]
        public void invalid_issue_shapes_are_not_illustrated_identity(string issue)
        {
            Assert.That(IllustratedBookParser.Parse($"Series {issue}.cbz"), Is.Null);
        }

        [TestCase("Series 2026-001.cbz", null)]
        [TestCase("Series v00.cbz", null)]
        [TestCase("Series v03 (2019).cbz", null)]
        [TestCase("Series 2026-01-01.cbz", "2026-01-01")]
        public void release_date_requires_a_real_date(string title, string date)
        {
            var parsed = IllustratedBookParser.Parse(title);
            Assert.That(parsed, Is.Not.Null);
            Assert.That(parsed.ReleaseDate, Is.EqualTo(date));
            if (title.Contains("v00"))
            {
                Assert.That(parsed.SeriesPosition, Is.EqualTo("0"));
            }
        }

        [TestCase("Author [CBR] - Book.epub", "EPUB")]
        [TestCase("Author [CBZ] - Book.mp3", "MP3")]
        public void archive_tokens_inside_titles_do_not_override_format(string title, string quality)
        {
            Assert.That(QualityParser.ParseQuality(title).Quality.Name, Is.EqualTo(quality));
        }
    }
}
