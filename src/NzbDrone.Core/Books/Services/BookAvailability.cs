using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NzbDrone.Core.MediaFiles;

namespace NzbDrone.Core.Books
{
    public class BookAvailability
    {
        public bool CanRead { get; set; }
        public bool CanListen { get; set; }
        public List<string> FileFormats { get; set; } = new List<string>();
        public List<string> Languages { get; set; } = new List<string>();
    }

    public static class BookAvailabilityCalculator
    {
        private static readonly HashSet<string> AudioFormats = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "m4b", "m4a", "mp3", "flac", "opus", "ogg", "aac", "wav", "wma", "aax", "aaxc"
        };

        private static readonly HashSet<string> EbookFormats = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "epub", "pdf", "azw", "azw3", "mobi", "kfx", "cbz", "cbr", "cb7", "djvu", "fb2", "lit", "txt", "rtf", "docx"
        };

        public static BookAvailability Calculate(Book book)
        {
            var result = new BookAvailability();

            if (book == null)
            {
                return result;
            }

            var files = (book.BookFiles ?? new List<BookFile>()).Where(f => f != null).ToList();
            var formats = new SortedSet<string>(StringComparer.Ordinal);

            foreach (var file in files)
            {
                var ext = Path.GetExtension(file.Path ?? string.Empty).TrimStart('.').ToLowerInvariant();
                if (ext.Length == 0)
                {
                    continue;
                }

                if (AudioFormats.Contains(ext))
                {
                    result.CanListen = true;
                }
                else if (EbookFormats.Contains(ext))
                {
                    result.CanRead = true;
                }
                else
                {
                    continue;
                }

                formats.Add(ext);
            }

            result.FileFormats = formats.ToList();

            var languages = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var edition in book.Editions ?? new List<Edition>())
            {
                var code = edition?.Language;
                if (!string.IsNullOrWhiteSpace(code))
                {
                    languages.Add(code.Trim().ToLowerInvariant());
                }
            }

            if (!languages.Any() && !string.IsNullOrWhiteSpace(book.LanguageCode))
            {
                languages.Add(book.LanguageCode.Trim().ToLowerInvariant());
            }

            result.Languages = languages.ToList();
            return result;
        }
    }
}
