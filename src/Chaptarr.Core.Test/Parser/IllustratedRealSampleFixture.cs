using System.Linq;
using NUnit.Framework;
using NzbDrone.Core.Parser;

namespace NzbDrone.Core.Test.Parser
{
    [TestFixture]
    public class IllustratedRealSampleFixture
    {
        [TestCase("comics", "(Velqor's Glass Voyage 01) Loom Of Cinder-Moth 031 (1987) (digital)/(Velqor's Glass Voyage 01) Loom Of Cinder-Moth 031 (1987) (digital).cbr")]
        [TestCase("comics", "(Velqor's Glass Voyage 04) Loom Of Cinder-Moth 032 (1987) (digital)/(Velqor's Glass Voyage 04) Loom Of Cinder-Moth 032 (1987) (digital).cbr")]
        [TestCase("comics", "008.Velqor s.Glass.Voyage.(Event).(Digital).(TPB).Comic.eBook-VexliQuop/008.Velqor_s.Glass.Voyage.(Event).(Digital).(TPB).Comic.eBook-VexliQuop.cbz")]
        [TestCase("comics", "0178  The Lantern beneath Quartz (October 1978) [Zelmavor]/The Lantern beneath Quartz (October 1978) [Zelmavor].cbz")]
        [TestCase("comics", "100.Quorvets.-.Book.01.(2024).(digital).(Kin.of.Zelqor-Velmora).Comic.eBook-VexliQuop/100.Quorvets.-.Book.01.(2024).(digital).(Kin.of.Zelqor-Velmora).Comic.eBook-VexliQuop.cbr")]
        [TestCase("comics", "102.Velnok.Atrium.-.The.Copper.Stair.in.a.Frozen.Cloud.(1987).(Digital).(TPB).Comic.eBook-VexliQuop/102.Velnok.Atrium.-.The.Copper.Stair.in.a.Frozen.Cloud.(1987).(Digital).(TPB).Comic.eBook-VexliQuop.cbz")]
        [TestCase("comics", "130.Opaline.Velk.-.A.Clockwork.Marsh.(2020).(Digital).(TPB).Comic.eBook-VexliQuop/130.Opaline.Velk.-.A.Clockwork.Marsh.(2020).(Digital).(TPB).Comic.eBook-VexliQuop.cbz")]
        [TestCase("comics", "742 (1999) (digital-Velmora)/742 (1999) (digital-Velmora).cbr")]
        [TestCase("comics", "742 2 of 5/742 2 of 5 .cbr")]
        [TestCase("comics", "300.-.Quorvants.v03.020.(1999).Comic.eBook-VexliQuop/300.-.Quorvants.v03.020.(1999).Comic.eBook-VexliQuop.cbz")]
        [TestCase("comics", "300.01.The.Opaline.Cinder-Moth.V1.300.(Digital).Comic.eBook-VexliQuop/300.01.The.Opaline.Cinder-Moth.V1.#300.(Digital).Comic.eBook-VexliQuop.cbz")]
        [TestCase("comics", "300.02.Zelk.V1.391.(Scan).Comic.eBook-VexliQuop/300.02.Zelk.V1.#391.(Scan).Comic.eBook-VexliQuop.cbr")]
        [TestCase("comics", "300.03.The.Prismatic.Cinder-Moth.V1.137.(Scan).Comic.eBook-VexliQuop/300.03.The.Prismatic.Cinder-Moth.V1.#137.(Scan).Comic.eBook-VexliQuop.cbr")]
        [TestCase("comics", "300.05.Loom.of.Cinder-Moth.V1.039.(Scan).Comic.eBook-VexliQuop/300.05.Loom.of.Cinder-Moth.V1.#039.(Scan).Comic.eBook-VexliQuop.cbz")]
        [TestCase("comics", "Quelmix.-.A.Bell.Beyond.Our.Reach.An.Illustrated.Journey.2017.HYBRID.COMIC/Quelmix.-.A.Bell.Beyond.Our.Reach.An.Illustrated.Journey.2017.HYBRID.COMIC.pdf")]
        [TestCase("comics", "Velnix Curiosity Archive 001 [1993] Orvin Zelquill - A Copper Map Beneath Clouds/Velnix Curiosity Archive 001 [1993] Orvin Zelquill - A Copper Map Beneath Clouds.cbz")]
        [TestCase("comics", "Velnix Curiosity Archive 018 [2007] Quartzbound Fables/Velnix Curiosity Archive 018 [2007] Quartzbound Fables.cbz")]
        [TestCase("comics", "Velnix Curiosity Archive 019 [2008] Vellum Quorn/Velnix Curiosity Archive 019 [2008] Vellum Quorn.cbz")]
        [TestCase("comics", "Zelquix 010 (2002) (Digital) (Veln-Qorvax)/Zelquix 010 (2002) (Digital) (Veln-Qorvax).cbr")]
        [TestCase("comics", "Zelquix 025 (2003) (Digital) (Veln-Qorvax)/Zelquix 025 (2003) (Digital) (Veln-Qorvax).cbr")]
        [TestCase("manga", "033.-.Seven.Quills.Copper.Carnival.Comic.eBook-VexliQuop/033.-.Seven.Quills.Copper.Carnival.Comic.eBook-VexliQuop.cbr")]
        [TestCase("manga", "47QX.ZUP Lanternkeeper.Velni.Glass.Moon.Arc.vol.01-16/47QX.ZUP_Lanternkeeper Velni Glass Moon Arc v01-03")]
        [TestCase("manga", "47QX.ZUP Quelvo.R.Zelvin.Omnivaria.Plorvex.vol.01-20/47QX.ZUP_Quelvo R. Zelvin Omnivaria Plorvex v01-05")]
        [TestCase("manga", "20th Meridian Quorvets - Band 22 (Velqira, 2009) (c2c)(qelvrix)/20th Meridian Quorvets - Band 22 (Velqira, 2009) (c2c)(qelvrix).cbz")]
        [TestCase("manga", "ZELVU 01 - DAS GLASNE MEER/ZELVU 01 - DAS GLASNE MEER.cbr")]
        [TestCase("manga", "ZELVU 02 - VELNORS UHR/ZELVU 02 - VELNORS UHR.cbr")]
        [TestCase("manga", "ZELVU 11 - REISE ZU QELVINS TURM/ZELVU 11 - REISE ZU QELVINS TURM.cbr")]
        [TestCase("manga", "ZELVU 13 - FLUESTERN IM KUPFERWALD/ZELVU 13 - FLUESTERN IM KUPFERWALD.cbr")]
        [TestCase("manga", "ZELVU 15 - DER MONDANKER/ZELVU 15 - DER MONDANKER.cbr")]
        [TestCase("manga", "ZELVU 16 - DAS QUARZGEFLECHT/ZELVU 16 - DAS QUARZGEFLECHT.cbr")]
        [TestCase("manga", "Quartz Loom Omnibus v03 (F) (2016) (Digital) (Quillvex-Velmora)/Quartz Loom Omnibus v03 (F) (2016) (Digital) (Quillvex-Velmora).cbz")]
        [TestCase("manga", "Velq - Glass Warden v07 2016 Digital Zelvor-Velmora/Velq - Glass Warden v07 (2016) (Digital) (Zelvor-Velmora).cbz")]
        [TestCase("manga", "Velq - Glass-Warden 073 (2019) (Digital) (queln-Velmora)/Velq - Glass-Warden 073 (2019) (Digital) (queln-Velmora).cbz")]
        [TestCase("manga", "Velni ru Quell! 04/Velni ru Quell! 04.cbz")]
        [TestCase("manga", "Velni ru Quell! 05/Velni ru Quell! 05.cbz")]
        [TestCase("manga", "Velni ru Quell! 06/Velni ru Quell! 06.cbz")]
        [TestCase("manga", "Velni ru Quell! 07/Velni ru Quell! 07.cbz")]
        [TestCase("manga", "Velni ru Quell! 08/Velni ru Quell! 08.cbz")]
        [TestCase("manga", "Velni ru Quell! 09/Velni ru Quell! 09.cbz")]
        [TestCase("manga", "Quelve-zorani/Quelve-zorani v01 (2023) (Digital) (7q2v).cbz")]
        [TestCase("magazines", "-[RANDOM SORT COOKBOOKS]-/Velquane Table - January 2015.pdf")]
        [TestCase("magazines", "1989 Quorlune [Quorven Lantern Reissue 2025] [vinyl 24-96]/Copper Weavers Guild Sector 282 - Quorlune - QL-07liners-web.pdf")]
        [TestCase("magazines", "2001/Copper Orchard - Velnor Quillian Zorveth {supplement to the July 2001 issue of Quartz And Vellum}.pdf")]
        [TestCase("magazines", "Quills Beyond Copper - Issue 163/Quills Beyond Copper - Issue 163.pdf")]
        [TestCase("magazines", "Quills Beyond Copper - Issue 164/Quills_Beyond_Copper_-_Issue_164.pdf")]
        [TestCase("magazines", "Lanterns.Beyond.Quartz-Issue.33.2014/Lanterns.Beyond.Quartz-Issue.33.2014.pdf")]
        [TestCase("magazines", "Lanterns.Beyond.Quartz-Issue.98.2019/Lanterns.Beyond.Quartz-Issue.98.2019.pdf")]
        [TestCase("magazines", "Lanterns.Beyond.Quartz.UK-Issue.13.2013/Lanterns.Beyond.Quartz.UK-Issue.13.2013.pdf")]
        [TestCase("magazines", "Quorven Lenskeeper - 21 10 2025/Quorven Lenskeeper - 21.10.2025.pdf")]
        [TestCase("magazines", "Velquian.Naturalist-January.February.2023/Velquian.Naturalist-January.February.2023.pdf")]
        [TestCase("magazines", "Quorvology Magazine 2012-06/Qorvlogy201206.pdf")]
        [TestCase("magazines", "QVN Copper Table (UK) - 07-2020.pdf.pdf/QVN Copper Table (UK) - 07-2020.pdf.pdf")]
        [TestCase("magazines", "QVN Vellum Magazine 2013-04/QVN Vellum Magazine 2013-04.pdf")]
        [TestCase("magazines", "QVN Quartz Inquiry 08 2025/QVN Quartz Inquiry 08.2025.pdf")]
        [TestCase("magazines", "QVN.Vellum.Magazine-April.2023/QVN.Vellum.Magazine-April.2023.pdf")]
        [TestCase("magazines", "QVN.Vellum.Magazine-January.2023/QVN.Vellum.Magazine-January.2023.pdf")]
        [TestCase("magazines", "QVN.Lamps.at.Dusk-January.2023/QVN.Lamps.at.Dusk-January.2023.pdf")]
        [TestCase("magazines", "Copper.And.Quartz.Lenscraft.For.Novices.No.22.2025.HYBRID.MAGAZINE.eBook-73Q8/Copper.And.Quartz.Lenscraft.For.Novices.No.22.2025.HYBRID.MAGAZINE.eBook-73Q8.pdf")]
        [TestCase("magazines", "Copper.And.Quartz.Lenscraft.For.Novices.No.23.2026.HYBRID.MAGAZINE.eBook-73Q8/Copper.And.Quartz.Lenscraft.For.Novices.No.23.2026.HYBRID.MAGAZINE.eBook-73Q8.pdf")]
        [TestCase("magazines", "Quelnavor Ledgerwheel (USA) - 03.08.2020.pdf/Quelnavor Ledgerwheel (USA) - 03.08.2020.pdf")]
        public void lists_real_sample_name_and_library_type(string type, string path)
        {
            var entry = IllustratedLibraryListing.List(path, type);
            Assert.That(entry.Name, Is.EqualTo(path.Split('/').Last()));
            Assert.That(entry.Type, Is.EqualTo(type));
        }

        [TestCase("Quills Beyond Copper", "163", "164")]
        [TestCase("Lanterns Beyond Quartz", "33", "98")]
        public void real_series_parity_uses_production_gap_detection(string series, string first, string second)
        {
            var paths = series == "Quills Beyond Copper"
                ? new[] { "Quills Beyond Copper - Issue 163/Quills Beyond Copper - Issue 163.pdf", "Quills Beyond Copper - Issue 164/Quills_Beyond_Copper_-_Issue_164.pdf" }
                : new[] { "Lanterns.Beyond.Quartz-Issue.33.2014/Lanterns.Beyond.Quartz-Issue.33.2014.pdf", "Lanterns.Beyond.Quartz-Issue.98.2019/Lanterns.Beyond.Quartz-Issue.98.2019.pdf" };
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

        [TestCase("Quorvex Chronicles #1000.cbz", "1000")]
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
            var parsed = NzbDrone.Core.Parser.Parser.ParseBookTitle($"Velrina Quorvale - Where the Copper Lantern Sleeps ({format})");
            Assert.That(parsed, Is.Not.Null);
            Assert.That(parsed.AuthorName, Is.EqualTo("Velrina Quorvale"));
            Assert.That(parsed.BookTitle, Is.EqualTo("Where the Copper Lantern Sleeps"));
            Assert.That(parsed.SeriesTitle, Is.Null);
        }

        [TestCase("47QX.ZUP_Lanternkeeper Velni Glass Moon Arc v01-03", "manga", "Extensionless bundle: no individual media file or issue identity supplied")]
        [TestCase("47QX.ZUP_Quelvo R. Zelvin Omnivaria Plorvex v01-05", "manga", "Extensionless bundle: no individual media file or issue identity supplied")]
        [TestCase("Copper Weavers Guild Sector 282 - Quorlune - QL-07liners-web.pdf", "magazines", "No unambiguous numbered magazine identity; catalog metadata required")]
        [TestCase("Copper Orchard - Velnor Quillian Zorveth {supplement to the July 2001 issue of Quartz And Vellum}.pdf", "magazines", "No unambiguous numbered magazine identity; catalog metadata required")]
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
