using System;

namespace NzbDrone.Core.Books
{
    public static class WantedVersionNotFoundReason
    {
        public const string UnknownLanguage = "unknown_language";
        public const string EditionNotFound = "edition_not_found";
        public const string EditionMediaMismatch = "edition_media_mismatch";
        public const string EditionLanguageMismatch = "edition_language_mismatch";
        public const string NoEditionInLanguage = "no_edition_in_language";
    }

    public class WantedVersionNotFoundException : Exception
    {
        public WantedVersionNotFoundException(string message, string reason = null, int? bookId = null, string mediaType = null, string language = null)
            : base(message)
        {
            Reason = reason ?? WantedVersionNotFoundReason.NoEditionInLanguage;
            BookId = bookId;
            MediaType = mediaType;
            Language = language;
        }

        // Stable machine-readable code (WantedVersionNotFoundReason) the caller can log or branch on.
        public string Reason { get; }
        public int? BookId { get; }
        public string MediaType { get; }
        public string Language { get; }
    }
}
