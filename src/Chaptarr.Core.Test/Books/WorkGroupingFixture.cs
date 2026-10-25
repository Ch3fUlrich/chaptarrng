using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using NzbDrone.Core.Books;

namespace Chaptarr.Core.Test.Books
{
    [TestFixture]
    public class WorkGroupingFixture
    {
        private static Book Row(int id, string baseId, BookMediaType media, string title = "T", string language = null, bool variant = false)
        {
            return new Book
            {
                Id = id,
                BaseBookId = baseId,
                MediaType = media,
                Title = title,
                LanguageCode = language,
                TitleSlug = variant ? $"{baseId}_wanted_{media.ToString().ToLowerInvariant()}_{language}_{id}" : $"slug{id}",
                AddOptions = variant ? new AddBookOptions { AddType = BookAddType.Manual } : new AddBookOptions()
            };
        }

        [Test]
        public void Rows_sharing_a_base_id_form_one_work_with_variants_after_ordinary_rows()
        {
            var groups = WorkGrouping.Group(new[]
            {
                Row(5, "w1", BookMediaType.Ebook, language: "deu", variant: true),
                Row(1, "w1", BookMediaType.Ebook, "Der Titel", "eng"),
                Row(2, "w1", BookMediaType.Audiobook, "Der Titel"),
                Row(3, "w2", BookMediaType.Ebook, "Other")
            });

            Assert.That(groups.Count, Is.EqualTo(2));
            var work = groups.Single(g => g.WorkKey == "w1");
            Assert.That(work.Title, Is.EqualTo("Der Titel"));
            Assert.That(work.Versions.Select(v => v.BookId), Is.EqualTo(new[] { 1, 2, 5 }));
            Assert.That(work.Versions.Last().IsVariant, Is.True);
            Assert.That(work.Versions.Last().Language, Is.EqualTo("deu"));
        }

        [Test]
        public void Duplicate_ebook_and_audio_rows_with_different_base_ids_group_by_work_provider_id()
        {
            var ebook = Row(1, "a", BookMediaType.Ebook);
            ebook.HardcoverBookId = "hc:42";
            var audio = Row(2, "b", BookMediaType.Audiobook);
            audio.HardcoverBookId = "hc:42";
            var other = Row(3, "c", BookMediaType.Ebook);
            other.HardcoverBookId = "hc:99";

            var groups = WorkGrouping.Group(new[] { ebook, audio, other });

            Assert.That(groups.Count, Is.EqualTo(2));
            Assert.That(groups[0].Versions.Select(v => v.BookId), Is.EqualTo(new[] { 1, 2 }));
        }

        [Test]
        public void Rows_without_any_identity_stay_separate_and_files_flag_is_applied()
        {
            var groups = WorkGrouping.Group(new[] { Row(1, null, BookMediaType.Ebook), Row(2, null, BookMediaType.Ebook) }, new HashSet<int> { 2 });

            Assert.That(groups.Count, Is.EqualTo(2));
            Assert.That(groups[1].Versions.Single().HasFiles, Is.True);
            Assert.That(groups[0].Versions.Single().HasFiles, Is.False);
        }
    }
}
