using NUnit.Framework;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Soulseek;

namespace Chaptarr.Core.Test.Soulseek
{
    [TestFixture]
    public class SlskdTextMatcherFixture
    {
        [TestCase("Märchen", "maerchen", "marchen")]
        [TestCase("Straße", "strasse", "strase")]
        [TestCase("Über", "ueber", "uber")]
        [TestCase("Café", "cafe", "cafe")]
        public void should_fold_umlauts_both_ways(string word, string digraph, string plain)
        {
            Assert.That(SlskdTextMatcher.Fold(word, true), Is.EqualTo(digraph));
            Assert.That(SlskdTextMatcher.Fold(word, false), Is.EqualTo(plain));
        }

        [TestCase("Die Märchen der Brüder Grimm", "Hörbuch\\Grimm\\Maerchen der Brueder Grimm", 1.0)]
        [TestCase("Die Märchen der Brüder Grimm", "Hörbuch\\Grimm\\Marchen der Bruder Grimm", 1.0)]
        [TestCase("Dune", "Frank Herbert\\Dune Messiah.epub", 1.0)]
        [TestCase("Das Jesus Video", "Eschbach\\Jesus Video", 1.0)]
        [TestCase("Harry Potter Stein der Weisen", "Rowling\\Harry Potter 1", 0.5)]
        [TestCase("Dune", "Music\\Something Else", 0.0)]
        public void should_compute_title_match_ratio(string title, string path, double expected)
        {
            Assert.That(SlskdTextMatcher.TitleMatchRatio(title, path), Is.EqualTo(expected).Within(0.001));
        }

        [Test]
        public void should_not_count_substrings_as_token_matches()
        {
            Assert.That(SlskdTextMatcher.TitleMatchRatio("Stein", "Steinway\\Collection"), Is.EqualTo(0).Within(0.001));
            Assert.That(SlskdTextMatcher.PathContainsAuthor("Anna Stein", "Music\\Steinway"), Is.False);
        }

        [TestCase("Dune", "Frank Herbert", "Dune", 0)]
        [TestCase("Dune", "Frank Herbert", "Dune (1965) [retail] epub", 0)]
        [TestCase("Dune", "Frank Herbert", "Frank Herbert - Dune", 0)]
        [TestCase("Dune", "Frank Herbert", "Dune Messiah", 1)]
        [TestCase("Dune", "Frank Herbert", "Dune Messiah Children of Dune", 2)]
        [TestCase("Märchen", "Hans Müller", "Maerchen Hoerbuch Mueller 01", 0)]
        public void should_count_extra_significant_words(string title, string author, string name, int expected)
        {
            Assert.That(SlskdTextMatcher.ExtraTokenCount(new[] { title }, author, name), Is.EqualTo(expected));
        }

        [Test]
        public void should_match_author_surname_in_any_spelling()
        {
            Assert.That(SlskdTextMatcher.Surname("J. R. R. Tolkien"), Is.EqualTo("tolkien"));
            Assert.That(SlskdTextMatcher.Surname("Martin Luther King Jr."), Is.EqualTo("king"));
            Assert.That(SlskdTextMatcher.PathContainsAuthor("Hans Müller", "Hoerbuch\\Mueller, Hans\\Buch"), Is.True);
            Assert.That(SlskdTextMatcher.PathContainsAuthor("Hans Müller", "Hoerbuch\\Muller\\Buch"), Is.True);
            Assert.That(SlskdTextMatcher.PathContainsAuthor("Hans Müller", "Hoerbuch\\Schmidt\\Buch"), Is.False);
            Assert.That(SlskdTextMatcher.PathContainsAuthor(null, "anything"), Is.True);
        }

        [TestCase("Hörbücher\\Autor\\Titel [Deutsch]", "German")]
        [TestCase("Audio\\Autor\\Titel ungekuerzt", "German")]
        [TestCase("Hörbücher\\Autor\\Titel", "German")]
        [TestCase("hoerbuecher/autor/titel", "German")]
        [TestCase("Hörbuch\\Autor\\Titel", "German")]
        [TestCase("Audio\\Autor\\Titel [DE]", "German")]
        [TestCase("Audio\\Author\\Title (English, unabridged)", "English")]
        [TestCase("Audio\\Author\\Title [EN]", "English")]
        [TestCase("Audio\\Author\\Title", null)]
        [TestCase("Audio\\La casa de papel", null)]
        public void should_detect_language_from_path(string path, string expected)
        {
            var language = SlskdTextMatcher.DetectLanguage(path);
            Assert.That(language?.Name, Is.EqualTo(expected));
        }
    }
}
