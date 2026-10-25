using System.Collections.Generic;
using System.Linq;
using FluentValidation.Results;
using NzbDrone.Common.Extensions;

namespace NzbDrone.Core.Notifications.BookLore
{
    public class BookLore : NotificationBase<BookLoreSettings>
    {
        private readonly IBookLoreProxy _bookLoreProxy;

        public BookLore(IBookLoreProxy bookLoreProxy)
        {
            _bookLoreProxy = bookLoreProxy;
        }

        public override string Link => "https://booklore.org/";

        public override void OnReleaseImport(BookDownloadMessage message)
        {
            // BookLore only handles ebooks; audiobook imports are ignored. A throw here is intended: the host's
            // notification runner (NotificationService) catches it, logs a warning and records the failure for the
            // health check, so the import itself is never aborted.
            var files = message.BookFiles ?? new List<MediaFiles.BookFile>();
            var ebookPaths = files.Where(IsEbook).Select(bookFile => bookFile.Path).ToList();

            var skipped = files.Count - ebookPaths.Count;
            if (skipped > 0)
            {
                Logger.Debug("BookLore: skipped {0} non-ebook file(s) of {1} imported", skipped, files.Count);
            }

            _bookLoreProxy.UploadFiles(Settings, ebookPaths);
        }

        private static readonly NLog.Logger Logger = NzbDrone.Common.Instrumentation.NzbDroneLogger.GetLogger(typeof(BookLore));

        private static readonly string[] EbookExtensions = { ".epub", ".azw3", ".azw", ".mobi", ".pdf", ".kepub", ".fb2", ".cbz" };

        // MediaType is the authority; files without one (legacy rows) fall back to the file extension.
        private static bool IsEbook(MediaFiles.BookFile bookFile)
        {
            if (!string.IsNullOrWhiteSpace(bookFile.MediaType))
            {
                return string.Equals(bookFile.MediaType, "ebook", System.StringComparison.OrdinalIgnoreCase);
            }

            return EbookExtensions.Contains(System.IO.Path.GetExtension(bookFile.Path ?? string.Empty), System.StringComparer.OrdinalIgnoreCase);
        }

        public override string Name => "BookLore";

        public override ValidationResult Test()
        {
            var failures = new List<ValidationFailure>();
            var settingsValidation = Settings.Validate();

            if (!settingsValidation.IsValid)
            {
                failures.AddRange(settingsValidation.Errors);
            }
            else
            {
                failures.AddIfNotNull(_bookLoreProxy.Test(Settings));
            }

            return new ValidationResult(failures);
        }
    }
}
