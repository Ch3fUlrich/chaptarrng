using System.Collections.Generic;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.MediaFiles;

namespace Chaptarr.Core.Test.Books
{
    [TestFixture]
    public class BookAvailabilityFixture
    {
        private static Book MakeBook(BookMediaType type, string[] paths, params string[] languages)
        {
            var files = new List<BookFile>();
            foreach (var p in paths)
            {
                files.Add(new BookFile { Path = p });
            }

            var editions = new List<Edition>();
            foreach (var l in languages)
            {
                editions.Add(new Edition { Language = l });
            }

            return new Book { MediaType = type, BookFiles = files, Editions = editions };
        }

        [Test]
        public void ebook_only_is_read_not_listen()
        {
            var a = BookAvailabilityCalculator.Calculate(MakeBook(BookMediaType.Ebook, new[] { "/x/a.EPUB", "/x/a.pdf" }, "eng"));

            Assert.That(a.CanRead, Is.True);
            Assert.That(a.CanListen, Is.False);
            Assert.That(a.FileFormats, Is.EqualTo(new[] { "epub", "pdf" }));
        }

        [Test]
        public void audio_only_is_listen_not_read()
        {
            var a = BookAvailabilityCalculator.Calculate(MakeBook(BookMediaType.Audiobook, new[] { "/x/a.m4b" }, "eng"));

            Assert.That(a.CanListen, Is.True);
            Assert.That(a.CanRead, Is.False);
            Assert.That(a.FileFormats, Is.EqualTo(new[] { "m4b" }));
        }

        [Test]
        public void both_kinds_of_files_are_read_and_listen()
        {
            var a = BookAvailabilityCalculator.Calculate(MakeBook(BookMediaType.Audiobook, new[] { "/x/a.mp3", "/x/a.epub" }, "eng"));

            Assert.That(a.CanListen, Is.True);
            Assert.That(a.CanRead, Is.True);
        }

        [Test]
        public void no_files_is_neither()
        {
            var a = BookAvailabilityCalculator.Calculate(MakeBook(BookMediaType.Ebook, new string[0], "eng"));

            Assert.That(a.CanListen, Is.False);
            Assert.That(a.CanRead, Is.False);
            Assert.That(a.FileFormats, Is.Empty);
        }

        [Test]
        public void multi_language_editions_are_distinct_sorted_lowercase()
        {
            var a = BookAvailabilityCalculator.Calculate(MakeBook(BookMediaType.Ebook, new[] { "/x/a.epub" }, "ger", "ENG", "eng"));

            Assert.That(a.Languages, Is.EqualTo(new[] { "eng", "ger" }));
        }

        [Test]
        public void falls_back_to_book_language_code()
        {
            var book = MakeBook(BookMediaType.Ebook, new[] { "/x/a.epub" });
            book.LanguageCode = "fre";

            Assert.That(BookAvailabilityCalculator.Calculate(book).Languages, Is.EqualTo(new[] { "fre" }));
        }

        [Test]
        public void null_book_is_empty()
        {
            Assert.That(BookAvailabilityCalculator.Calculate(null).CanRead, Is.False);
        }
    }
}
