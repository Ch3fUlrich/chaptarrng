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

            public void UploadFiles(BookLoreSettings settings, IEnumerable<string> filePaths) => Uploaded.AddRange(filePaths);
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
    }
}
