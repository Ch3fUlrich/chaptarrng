using System.Collections.Generic;
using Chaptarr.Api.V1.Books;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.MediaFiles;

namespace Chaptarr.Core.Test.Books
{
    [TestFixture]
    public class BookResourceAvailabilityFixture
    {
        [Test]
        public void should_expose_availability_when_files_loaded()
        {
            var book = new Book
            {
                MediaType = BookMediaType.Audiobook,
                Editions = new List<Edition> { new Edition { Language = "eng", Monitored = true } },
                BookFiles = new List<BookFile> { new BookFile { Path = "/x/a.m4b" } }
            };

            var resource = book.ToResource(BookResourceMappingOptions.Lean());

            Assert.That(resource.Availability, Is.Not.Null);
            Assert.That(resource.Availability.CanListen, Is.True);
            Assert.That(resource.Availability.CanRead, Is.False);
            Assert.That(resource.Availability.FileFormats, Is.EqualTo(new[] { "m4b" }));
            Assert.That(resource.Availability.Languages, Is.EqualTo(new[] { "eng" }));
        }

        [Test]
        public void should_expose_ebook_availability()
        {
            var book = new Book
            {
                MediaType = BookMediaType.Ebook,
                Editions = new List<Edition> { new Edition { Language = "ger", Monitored = true } },
                BookFiles = new List<BookFile> { new BookFile { Path = "/x/a.epub" } }
            };

            var resource = book.ToResource(BookResourceMappingOptions.Lean());

            Assert.That(resource.Availability.CanRead, Is.True);
            Assert.That(resource.Availability.CanListen, Is.False);
        }

        [Test]
        public void should_leave_availability_null_when_files_not_loaded()
        {
            var book = new Book
            {
                MediaType = BookMediaType.Ebook,
                Editions = new List<Edition> { new Edition { Monitored = true } }
            };

            var resource = book.ToResource(BookResourceMappingOptions.Lean());

            Assert.That(resource.Availability, Is.Null);
        }
    }
}
