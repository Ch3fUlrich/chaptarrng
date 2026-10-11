using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.IndexerSearch.Definitions;

namespace Chaptarr.Core.Test.IndexerSearch
{
    [TestFixture]
    public class TransliterationFixture
    {
        [TestCase("Ernährungskompass", "Ernaehrungskompass")]
        [TestCase("Bäst", "Baest")]
        [TestCase("Größe", "Groesse")]
        [TestCase("Straße", "Strasse")]
        [TestCase("Ärger", "Aerger")]
        [TestCase("Öffnung", "Oeffnung")]
        [TestCase("Über", "Ueber")]
        public void should_transliterate_german_umlauts_and_eszett(string input, string expected)
        {
            Assert.That(SearchCriteriaBase.GetTransliteratedQueryTitle(input), Is.EqualTo(expected));
        }

        [TestCase("Bas Kast")]
        [TestCase("The Great Gatsby")]
        [TestCase("Animal Farm")]
        [TestCase("1984")]
        public void should_return_null_when_title_has_no_german_umlauts(string input)
        {
            Assert.That(SearchCriteriaBase.GetTransliteratedQueryTitle(input), Is.Null);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void should_return_null_when_title_is_null_or_whitespace(string input)
        {
            Assert.That(SearchCriteriaBase.GetTransliteratedQueryTitle(input), Is.Null);
        }

        [Test]
        public void get_query_title_should_remain_unchanged_for_umlaut_titles()
        {
            Assert.That(SearchCriteriaBase.GetQueryTitle("Ernährungskompass"), Is.EqualTo("Ernahrungskompass"));
            Assert.That(SearchCriteriaBase.GetQueryTitle("Der Ernährungskompass"), Is.EqualTo("Der+Ernahrungskompass"));
            Assert.That(SearchCriteriaBase.GetQueryTitle("Bäst"), Is.EqualTo("Bast"));
        }

        [Test]
        public void author_query_transliterated_should_return_transliterated_author_name_when_umlauts_exist()
        {
            var criteria = new AuthorSearchCriteria
            {
                Author = new Author { Name = "Bäst" }
            };

            Assert.That(criteria.AuthorQuery, Is.EqualTo("Bast"));
            Assert.That(criteria.AuthorQueryTransliterated, Is.EqualTo("Baest"));
        }

        [Test]
        public void author_query_transliterated_should_return_null_for_ascii_author_name()
        {
            var criteria = new AuthorSearchCriteria
            {
                Author = new Author { Name = "Bas Kast" }
            };

            Assert.That(criteria.AuthorQuery, Is.EqualTo("Bas+Kast"));
            Assert.That(criteria.AuthorQueryTransliterated, Is.Null);
        }

        [Test]
        public void author_query_transliterated_should_return_null_when_author_is_null()
        {
            var criteria = new AuthorSearchCriteria
            {
                Author = null
            };

            Assert.That(criteria.AuthorQueryTransliterated, Is.Null);
        }

        [Test]
        public void book_query_transliterated_should_return_transliterated_book_title_when_umlauts_exist()
        {
            var criteria = new BookSearchCriteria
            {
                Author = new Author { Name = "Bas Kast" },
                BookTitle = "Ernährungskompass"
            };

            Assert.That(criteria.BookQuery, Is.EqualTo("Ernahrungskompass"));
            Assert.That(criteria.BookQueryTransliterated, Is.EqualTo("Ernaehrungskompass"));
        }

        [Test]
        public void book_query_transliterated_should_return_transliterated_title_for_der_ernaehrungskompass()
        {
            var criteria = new BookSearchCriteria
            {
                Author = new Author { Name = "Bas Kast" },
                BookTitle = "Der Ernährungskompass"
            };

            Assert.That(criteria.BookQuery, Is.EqualTo("Der+Ernahrungskompass"));
            Assert.That(criteria.BookQueryTransliterated, Is.EqualTo("Der+Ernaehrungskompass"));
        }

        [Test]
        public void book_query_transliterated_should_return_null_for_ascii_book_title()
        {
            var criteria = new BookSearchCriteria
            {
                Author = new Author { Name = "Frank Herbert" },
                BookTitle = "Dune"
            };

            Assert.That(criteria.BookQuery, Is.EqualTo("Dune"));
            Assert.That(criteria.BookQueryTransliterated, Is.Null);
        }
    }
}
