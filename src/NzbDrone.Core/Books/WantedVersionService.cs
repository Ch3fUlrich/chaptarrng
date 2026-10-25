using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;

namespace NzbDrone.Core.Books
{
    public class WantedVersionCellResult
    {
        public string MediaType { get; set; }
        public string Language { get; set; }

        // created | existing | no_version
        public string Status { get; set; }
        public string Reason { get; set; }
        public int? BookId { get; set; }
    }

    public interface IWantedVersionService
    {
        List<string> GetWantedLanguages();
        List<string> GetWantedFormats(BookMediaType mediaType);
        List<WantedVersionCellResult> RequestAllVersions(int bookId, BookMediaType? onlyMediaType = null);
    }

    public class WantedVersionService : IWantedVersionService
    {
        private readonly IConfigService _configService;
        private readonly IBookService _bookService;
        private readonly Logger _logger;

        public WantedVersionService(IConfigService configService, IBookService bookService, Logger logger)
        {
            _configService = configService;
            _bookService = bookService;
            _logger = logger;
        }

        public static List<string> ParseCsv(string csv)
        {
            return (csv ?? string.Empty)
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(v => v.Trim().ToLowerInvariant())
                .Where(v => v.Length > 0)
                .Distinct()
                .ToList();
        }

        public List<string> GetWantedLanguages()
        {
            return ParseCsv(_configService.WantedVersionLanguages);
        }

        public List<string> GetWantedFormats(BookMediaType mediaType)
        {
            return ParseCsv(mediaType == BookMediaType.Ebook
                ? _configService.WantedVersionEbookFormats
                : _configService.WantedVersionAudiobookFormats);
        }

        // One work -> every wanted (medium x language) variant row, monitored. Cells with no matching
        // edition are reported (reason code) rather than failing the whole request; rerunning is idempotent
        // because AddWantedEdition returns the existing row of a cell.
        public List<WantedVersionCellResult> RequestAllVersions(int bookId, BookMediaType? onlyMediaType = null)
        {
            var results = new List<WantedVersionCellResult>();
            var languages = GetWantedLanguages();

            foreach (var mediaType in new[] { BookMediaType.Ebook, BookMediaType.Audiobook })
            {
                if (onlyMediaType.HasValue && onlyMediaType.Value != mediaType)
                {
                    continue;
                }

                foreach (var language in languages)
                {
                    var cell = new WantedVersionCellResult { MediaType = mediaType.ToString().ToLowerInvariant(), Language = language };
                    try
                    {
                        var result = _bookService.AddWantedEdition(bookId, new WantedEditionSelection { Language = language, MediaType = mediaType });
                        cell.BookId = result.Book.Id;
                        cell.Status = result.Created ? "created" : "existing";
                        _bookService.SetMonitoredForMediaType(new[] { result.Book.Id }, cell.MediaType, true);
                    }
                    catch (WantedVersionNotFoundException ex)
                    {
                        cell.Status = "no_version";
                        cell.Reason = ex.Reason;
                    }

                    results.Add(cell);
                }
            }

            _logger.Debug("Requested {0} wanted version cell(s) for book {1}", results.Count, bookId);
            return results;
        }
    }
}
