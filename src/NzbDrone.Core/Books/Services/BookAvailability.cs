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
            "m4b", "m4a", "mp3", "flac", "opus", "ogg", "aac", "wav", "wma"
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

                formats.Add(ext);

                if (AudioFormats.Contains(ext))
                {
                    result.CanListen = true;
                }
                else
                {
                    result.CanRead = true;
                }
            }

            // Files with no usable extension fall back to the book's media type.
            if (files.Any() && !result.CanListen && !result.CanRead)
            {
                if (book.MediaType == BookMediaType.Audiobook)
                {
                    result.CanListen = true;
                }
                else
                {
                    result.CanRead = true;
                }
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
