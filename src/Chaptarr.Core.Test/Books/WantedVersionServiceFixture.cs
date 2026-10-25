using System;
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
    public class WantedVersionServiceFixture
    {
        public class ConfigProxy : DispatchProxy
        {
            public Dictionary<string, string> Values { get; set; } = new();

            protected override object Invoke(MethodInfo targetMethod, object[] args)
            {
                if (targetMethod.Name.StartsWith("get_") && Values.TryGetValue(targetMethod.Name.Substring(4), out var v))
                {
                    return targetMethod.ReturnType == typeof(bool) ? bool.Parse(v) : v;
                }

                throw new NotImplementedException(targetMethod.Name);
            }
        }

        public class BookProxy : DispatchProxy
        {
            public List<(int Id, BookMediaType Media, string Language)> Requests { get; } = new();
            public List<(int Id, string Media)> Monitored { get; } = new();
            public HashSet<string> Missing { get; set; } = new();
            public HashSet<string> Existing { get; set; } = new();

            protected override object Invoke(MethodInfo targetMethod, object[] args)
            {
                if (targetMethod.Name == nameof(IBookService.AddWantedEdition) && args.Length == 2 && args[1] is WantedEditionSelection sel)
                {
                    var key = $"{sel.MediaType}:{sel.Language}";
                    Requests.Add(((int)args[0], sel.MediaType.Value, sel.Language));
                    if (Missing.Contains(key))
                    {
                        throw new WantedVersionNotFoundException("none", WantedVersionNotFoundReason.NoEditionInLanguage, (int)args[0], sel.MediaType.ToString(), sel.Language);
                    }

                    return new WantedEditionResult { Book = new Book { Id = 100 + Requests.Count }, Created = !Existing.Contains(key) };
                }

                if (targetMethod.Name == nameof(IBookService.SetMonitoredForMediaType))
                {
                    foreach (var id in (IEnumerable<int>)args[0])
                    {
                        Monitored.Add((id, (string)args[1]));
                    }

                    return null;
                }

                throw new NotImplementedException(targetMethod.Name);
            }
        }

        private static (WantedVersionService Subject, BookProxy Books) Build(string languages, string ebook = "", string audio = "")
        {
            var config = DispatchProxy.Create<IConfigService, ConfigProxy>();
            ((ConfigProxy)(object)config).Values = new()
            {
                ["WantedVersionLanguages"] = languages,
                ["WantedVersionEbookFormats"] = ebook,
                ["WantedVersionAudiobookFormats"] = audio
            };
            var books = DispatchProxy.Create<IBookService, BookProxy>();
            return (new WantedVersionService(config, books, LogManager.GetCurrentClassLogger()), (BookProxy)(object)books);
        }

        [Test]
        public void ParseCsv_normalises_case_whitespace_and_duplicates()
        {
            Assert.That(WantedVersionService.ParseCsv(" DEU, eng;deu ,,"), Is.EqualTo(new[] { "deu", "eng" }));
        }

        [Test]
        public void Formats_are_read_per_medium()
        {
            var (subject, _) = Build("deu", "epub,AZW3", "m4b");
            Assert.That(subject.GetWantedFormats(BookMediaType.Ebook), Is.EqualTo(new[] { "epub", "azw3" }));
            Assert.That(subject.GetWantedFormats(BookMediaType.Audiobook), Is.EqualTo(new[] { "m4b" }));
        }

        [Test]
        public void RequestAllVersions_fans_out_to_every_medium_x_language_and_monitors_each()
        {
            var (subject, books) = Build("deu,eng");

            var cells = subject.RequestAllVersions(7);

            Assert.That(cells.Count, Is.EqualTo(4));
            Assert.That(cells.All(c => c.Status == "created"));
            Assert.That(books.Requests.Select(r => (r.Media, r.Language)).ToHashSet(), Is.EquivalentTo(new[]
            {
                (BookMediaType.Ebook, "deu"), (BookMediaType.Ebook, "eng"), (BookMediaType.Audiobook, "deu"), (BookMediaType.Audiobook, "eng")
            }));
            Assert.That(books.Monitored.Count, Is.EqualTo(4));
            Assert.That(books.Monitored.Count(m => m.Media == "ebook"), Is.EqualTo(2));
        }

        [Test]
        public void RequestAllVersions_reports_missing_cells_and_keeps_going()
        {
            var (subject, books) = Build("deu,eng");
            books.Missing.Add("Audiobook:deu");
            books.Existing.Add("Ebook:eng");

            var cells = subject.RequestAllVersions(7);

            Assert.That(cells.Single(c => c.MediaType == "audiobook" && c.Language == "deu").Status, Is.EqualTo("no_version"));
            Assert.That(cells.Single(c => c.MediaType == "audiobook" && c.Language == "deu").Reason, Is.EqualTo(WantedVersionNotFoundReason.NoEditionInLanguage));
            Assert.That(cells.Single(c => c.MediaType == "ebook" && c.Language == "eng").Status, Is.EqualTo("existing"));
            Assert.That(books.Monitored.Count, Is.EqualTo(3));
        }

        [Test]
        public void RequestAllVersions_without_languages_does_nothing()
        {
            var (subject, books) = Build("");
            Assert.That(subject.RequestAllVersions(7), Is.Empty);
            Assert.That(books.Requests, Is.Empty);
        }
    }
}
