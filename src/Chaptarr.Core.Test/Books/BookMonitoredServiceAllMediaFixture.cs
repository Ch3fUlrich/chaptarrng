using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NLog;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.Configuration;

namespace Chaptarr.Core.Test.Books
{
    [TestFixture]
    public class BookMonitoredServiceAllMediaFixture
    {
        public class VersionProxy : DispatchProxy
        {
            public List<(int Id, BookMediaType? Media)> Requested { get; } = new();

            protected override object Invoke(MethodInfo targetMethod, object[] args)
            {
                if (targetMethod.Name == nameof(IWantedVersionService.GetWantedLanguages))
                {
                    return new List<string> { "deu", "eng" };
                }

                if (targetMethod.Name == nameof(IWantedVersionService.RequestAllVersions))
                {
                    Requested.Add(((int)args[0], (BookMediaType?)args[1]));
                    return new List<WantedVersionCellResult>();
                }

                throw new System.NotImplementedException(targetMethod.Name);
            }
        }

        private static List<Book> Library()
        {
            return new List<Book>
            {
                new Book { Id = 1, AuthorId = 9, BaseBookId = "w1", MediaType = BookMediaType.Ebook },
                new Book { Id = 2, AuthorId = 9, BaseBookId = "w1", MediaType = BookMediaType.Audiobook },
                new Book { Id = 3, AuthorId = 9, BaseBookId = "w2", MediaType = BookMediaType.Ebook },
                new Book { Id = 4, AuthorId = 9, BaseBookId = "w1", MediaType = BookMediaType.Ebook, TitleSlug = "w1_wanted_ebook_deu_5", AddOptions = new AddBookOptions { AddType = BookAddType.Manual } }
            };
        }

        private static (BookMonitoredService Subject, List<Book> Books, VersionProxy Versions) Build(bool fanOut)
        {
            var books = Library();
            var bookService = DispatchProxy.Create<IBookService, BookMonitoredServiceSpecificBookFixture.BookServiceProxy>();
            ((BookMonitoredServiceSpecificBookFixture.BookServiceProxy)(object)bookService).Books = books;
            var authorService = DispatchProxy.Create<IAuthorService, BookMonitoredServiceSpecificBookFixture.AuthorServiceProxy>();
            var versions = DispatchProxy.Create<IWantedVersionService, VersionProxy>();
            var config = DispatchProxy.Create<IConfigService, WantedVersionServiceFixture.ConfigProxy>();
            ((WantedVersionServiceFixture.ConfigProxy)(object)config).Values = new() { ["WantedVersionsFanOutOnMonitor"] = fanOut.ToString() };

            var subject = new BookMonitoredService(authorService, bookService, versions, config, LogManager.GetCurrentClassLogger());
            return (subject, books, (VersionProxy)(object)versions);
        }

        [Test]
        public void Monitor_all_without_media_type_monitors_both_media()
        {
            var (subject, books, _) = Build(false);

            subject.SetBookMonitoredStatus(new Author { Id = 9, Name = "A" }, new MonitoringOptions { Monitor = MonitorTypes.All });

            Assert.That(books.Where(b => b.MediaType == BookMediaType.Ebook).All(b => b.EbookMonitored));
            Assert.That(books.Single(b => b.Id == 2).AudiobookMonitored, Is.True);
        }

        [Test]
        public void Monitor_all_ebooks_leaves_audiobooks_alone()
        {
            var (subject, books, _) = Build(false);

            subject.SetBookMonitoredStatus(new Author { Id = 9, Name = "A" }, new MonitoringOptions { Monitor = MonitorTypes.All, MediaType = BookMediaType.Ebook });

            Assert.That(books.Single(b => b.Id == 1).EbookMonitored, Is.True);
            Assert.That(books.Single(b => b.Id == 2).AudiobookMonitored, Is.False);
        }

        [Test]
        public void Monitor_all_audiobooks_leaves_ebooks_alone()
        {
            var (subject, books, _) = Build(false);

            subject.SetBookMonitoredStatus(new Author { Id = 9, Name = "A" }, new MonitoringOptions { Monitor = MonitorTypes.All, MediaType = BookMediaType.Audiobook });

            Assert.That(books.Single(b => b.Id == 2).AudiobookMonitored, Is.True);
            Assert.That(books.Single(b => b.Id == 1).EbookMonitored, Is.False);
        }

        [Test]
        public void Fan_out_requests_each_work_once_when_enabled_and_skips_variant_rows()
        {
            var (subject, _, versions) = Build(true);

            subject.SetBookMonitoredStatus(new Author { Id = 9, Name = "A" }, new MonitoringOptions { Monitor = MonitorTypes.All });

            Assert.That(versions.Requested.Select(r => r.Id), Is.EquivalentTo(new[] { 1, 3 }));
            Assert.That(versions.Requested.All(r => r.Media == null));
        }

        [Test]
        public void Fan_out_is_limited_to_the_chosen_medium()
        {
            var (subject, _, versions) = Build(true);

            subject.SetBookMonitoredStatus(new Author { Id = 9, Name = "A" }, new MonitoringOptions { Monitor = MonitorTypes.All, MediaType = BookMediaType.Ebook });

            Assert.That(versions.Requested.All(r => r.Media == BookMediaType.Ebook));
        }

        [Test]
        public void Fan_out_is_off_by_default_and_for_other_monitor_modes()
        {
            var (off, _, offVersions) = Build(false);
            off.SetBookMonitoredStatus(new Author { Id = 9, Name = "A" }, new MonitoringOptions { Monitor = MonitorTypes.All });
            Assert.That(offVersions.Requested, Is.Empty);

            var (on, _, onVersions) = Build(true);
            on.SetBookMonitoredStatus(new Author { Id = 9, Name = "A" }, new MonitoringOptions { Monitor = MonitorTypes.Missing });
            Assert.That(onVersions.Requested, Is.Empty);
        }
    }
}
