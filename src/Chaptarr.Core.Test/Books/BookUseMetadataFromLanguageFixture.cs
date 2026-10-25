using NUnit.Framework;
using NzbDrone.Core.Books;

namespace Chaptarr.Core.Test.Books
{
    [TestFixture]
    public class BookUseMetadataFromLanguageFixture
    {
        private static Book Remote()
        {
            return new Book
            {
                Title = "Remote",
                TitleSlug = "test-book",
                LanguageCode = "eng",
                LanguageName = "English"
            };
        }

        [Test]
        public void UseMetadataFrom_WantedVariantRowKeepsOwnLanguage()
        {
            var variant = new Book
            {
                Title = "Test Book",
                TitleSlug = "test-book_wanted_audiobook_deu_77",
                LanguageCode = "deu",
                LanguageName = "German",
                AddOptions = new AddBookOptions { AddType = BookAddType.Manual }
            };

            variant.UseMetadataFrom(Remote());

            Assert.That(variant.LanguageCode, Is.EqualTo("deu"));
            Assert.That(variant.LanguageName, Is.EqualTo("German"));
            Assert.That(variant.TitleSlug, Is.EqualTo("test-book_wanted_audiobook_deu_77"));
        }

        [Test]
        public void UseMetadataFrom_NormalRowTakesRemoteLanguage()
        {
            var row = new Book
            {
                Title = "Test Book",
                TitleSlug = "test-book",
                LanguageCode = "deu",
                LanguageName = "German",
                AddOptions = new AddBookOptions { AddType = BookAddType.Manual }
            };

            row.UseMetadataFrom(Remote());

            Assert.That(row.LanguageCode, Is.EqualTo("eng"));
            Assert.That(row.LanguageName, Is.EqualTo("English"));
        }
    }
}
