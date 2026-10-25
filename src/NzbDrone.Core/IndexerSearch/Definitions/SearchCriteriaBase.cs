using System.Collections.Generic;
using System.Text.RegularExpressions;
using NzbDrone.Common.EnsureThat;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Books;

namespace NzbDrone.Core.IndexerSearch.Definitions
{
    public abstract class SearchCriteriaBase
    {
        private static readonly Regex NonWord = new Regex(@"[^\w`'’]", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex BeginningThe = new Regex(@"^the\s", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly char[] UmlautChars = { '\u00e4', '\u00f6', '\u00fc', '\u00c4', '\u00d6', '\u00dc', '\u00df', '\u1e9e' };

        public virtual bool MonitoredBooksOnly { get; set; } = true;
        public virtual bool UserInvokedSearch { get; set; }
        public virtual bool InteractiveSearch { get; set; }

        public Author Author { get; set; }
        public List<Book> Books { get; set; }
        public List<Book> AuthorCatalog { get; set; }

        public string AuthorQuery => GetQueryTitle(Author.Name);
        /// <summary>Author query with German umlauts transliterated (ae/oe/ue/ss); null when the author name has none.</summary>
        public string AuthorQueryTransliterated => Author?.Name == null ? null : GetTransliteratedQueryTitle(Author.Name);

        public static string GetQueryTitle(string title)
        {
            return CleanQueryTitle(title, transliterate: false);
        }

        /// <summary>Title query with German umlauts transliterated (ae/oe/ue/ss); null when the title has none.</summary>
        public static string GetTransliteratedQueryTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return null;
            }

            // Decomposed (NFD) umlauts would slip past the precomposed checks below.
            title = title.Normalize(System.Text.NormalizationForm.FormC);

            if (title.IndexOfAny(UmlautChars) < 0)
            {
                return null;
            }

            return CleanQueryTitle(title, transliterate: true);
        }

        private static string CleanQueryTitle(string title, bool transliterate)
        {
            Ensure.That(title, () => title).IsNotNullOrWhiteSpace();

            // Most VA books are listed as VA, not Various Authors
            // TODO: Needed in Chaptarr??
            if (title == "Various Authors")
            {
                title = "VA";
            }

            var cleanTitle = BeginningThe.Replace(title, string.Empty);

            cleanTitle = cleanTitle.Replace(" & ", " ");
            cleanTitle = cleanTitle.Replace(".", " ");
            cleanTitle = NonWord.Replace(cleanTitle, "+");

            //remove any repeating +s
            cleanTitle = Regex.Replace(cleanTitle, @"\+{2,}", "+");

            if (transliterate)
            {
                cleanTitle = cleanTitle.Replace("ä", "ae")
                                       .Replace("ö", "oe")
                                       .Replace("ü", "ue")
                                       .Replace("Ä", "Ae")
                                       .Replace("Ö", "Oe")
                                       .Replace("Ü", "Ue")
                                       .Replace("ß", "ss");
                cleanTitle = cleanTitle.Replace("\u1e9e", "SS");
            }

            cleanTitle = cleanTitle.RemoveAccent();
            cleanTitle = cleanTitle.Trim('+', ' ');

            return cleanTitle.Length == 0 ? title : cleanTitle;
        }
    }
}
