using System.Collections.Generic;
using NUnit.Framework;
using NzbDrone.Core.Books;

namespace Chaptarr.Core.Test.Books
{
    [TestFixture]
    public class WantedSlugFixture
    {
        private static Book Row(string slug, string editionLanguage = "deu", string rowLanguage = null, BookAddType addType = BookAddType.Manual, string unitKeyHash = null)
        {
            return new Book
            {
                TitleSlug = slug,
                LanguageCode = rowLanguage,
                UnitKeyHash = unitKeyHash,
                AddOptions = new AddBookOptions { AddType = addType },
                Editions = new List<Edition> { new Edition { Monitored = true, Language = editionLanguage } }
            };
        }

        [TestCase("dune_wanted_ebook_deu_1", true)]
        [TestCase("DUNE_WANTED_5", true)]
        [TestCase("dune", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void HasWantedSlug_matches_marker_case_insensitively(string slug, bool expected)
        {
            Assert.That(Book.HasWantedSlug(slug), Is.EqualTo(expected));
        }

        [Test]
        public void BuildWantedSlug_round_trips_through_HasWantedSlug()
        {
            var slug = Book.BuildWantedSlug("dune", BookMediaType.Audiobook, "deu", 7);

            Assert.That(slug, Is.EqualTo("dune_wanted_audiobook_deu_7"));
            Assert.That(Book.HasWantedSlug(slug), Is.True);
            Assert.That(Book.BuildWantedSlug("dune", BookMediaType.Ebook, null, 7), Is.EqualTo("dune_wanted_ebook_und_7"));
        }

        [Test]
        public void GetWantedVariantLanguage_returns_monitored_edition_language()
        {
            Assert.That(Row("dune_wanted_ebook_deu_1").GetWantedVariantLanguage(), Is.EqualTo("deu"));
        }

        [Test]
        public void GetWantedVariantLanguage_falls_back_to_row_language_code()
        {
            Assert.That(Row("dune_wanted_ebook_deu_1", editionLanguage: null, rowLanguage: "deu").GetWantedVariantLanguage(), Is.EqualTo("deu"));
        }

        [Test]
        public void GetWantedVariantLanguage_is_null_for_ordinary_or_synced_rows()
        {
            Assert.That(Row("dune").GetWantedVariantLanguage(), Is.Null);
            Assert.That(Row("dune_wanted_ebook_deu_1", addType: BookAddType.Automatic).GetWantedVariantLanguage(), Is.Null);
            Assert.That(Row("dune_wanted_ebook_deu_1", unitKeyHash: "abc").GetWantedVariantLanguage(), Is.Null);
        }
    }
}
