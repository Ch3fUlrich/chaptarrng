using System.Collections.Generic;
using System.Linq;
using FluentValidation.Results;
using NUnit.Framework;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Notifications;
using NzbDrone.Core.Notifications.BookLore;

namespace Chaptarr.Core.Test.Notifications
{
    [TestFixture]
    public class BookLoreNotificationFixture
    {
        private class FakeProxy : IBookLoreProxy
        {
            public List<string> Uploaded { get; } = new();

            public ValidationFailure Test(BookLoreSettings settings) => null;

            public bool Fail { get; set; }

            public void UploadFiles(BookLoreSettings settings, IEnumerable<string> filePaths)
            {
                Uploaded.AddRange(filePaths);

                if (Fail)
                {
                    throw new System.AggregateException("BookLore upload failed", new System.Exception("boom"));
                }
            }
        }

        [Test]
        public void should_upload_only_ebook_files_on_import()
        {
            var proxy = new FakeProxy();
            var notification = new BookLore(proxy) { Definition = new NotificationDefinition { Settings = new BookLoreSettings() } };

            notification.OnReleaseImport(new BookDownloadMessage
            {
                BookFiles = new List<BookFile>
                {
                    new BookFile { Path = "a.epub", MediaType = "ebook" },
                    new BookFile { Path = "b.m4b", MediaType = "audiobook" }
                }
            });

            Assert.That(proxy.Uploaded, Is.EqualTo(new[] { "a.epub" }));
        }

        [Test]
        public void should_not_upload_audiobook_only_imports()
        {
            var proxy = new FakeProxy();
            var notification = new BookLore(proxy) { Definition = new NotificationDefinition { Settings = new BookLoreSettings() } };

            Assert.DoesNotThrow(() => notification.OnReleaseImport(new BookDownloadMessage
            {
                BookFiles = new List<BookFile> { new BookFile { Path = "b.m4b", MediaType = "audiobook" } }
            }));

            Assert.That(proxy.Uploaded, Is.Empty);
        }

        [Test]
        public void should_use_the_file_extension_when_media_type_is_missing()
        {
            var proxy = new FakeProxy();
            var notification = new BookLore(proxy) { Definition = new NotificationDefinition { Settings = new BookLoreSettings() } };

            notification.OnReleaseImport(new BookDownloadMessage
            {
                BookFiles = new List<BookFile>
                {
                    new BookFile { Path = "legacy.epub", MediaType = null },
                    new BookFile { Path = "legacy.m4b", MediaType = "" }
                }
            });

            Assert.That(proxy.Uploaded, Is.EqualTo(new[] { "legacy.epub" }));
        }

        // The failure must reach the host runner (it logs and records the notification failure for the health check)
        // without any file being modified by the notification itself.
        [Test]
        public void should_surface_upload_failure_to_the_notification_runner()
        {
            var proxy = new FakeProxy { Fail = true };
            var notification = new BookLore(proxy) { Definition = new NotificationDefinition { Settings = new BookLoreSettings() } };

            Assert.Throws<System.AggregateException>(() => notification.OnReleaseImport(new BookDownloadMessage
            {
                BookFiles = new List<BookFile> { new BookFile { Path = "a.epub", MediaType = "ebook" } }
            }));
        }
    }
}
