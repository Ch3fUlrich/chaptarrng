using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NzbDrone.Core.Languages;

namespace NzbDrone.Core.Soulseek
{
    // Matching of Soulseek paths against a wanted title/author. Soulseek paths are free-form,
    // so everything is token based and tolerant of the umlaut spellings used by German shares
    // (ä/ae/a, ö/oe/o, ü/ue/u, ß/ss).
    public static class SlskdTextMatcher
    {
        private static readonly Regex SplitRegex = new(@"[^\p{L}\p{N}]+", RegexOptions.Compiled);

        private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
        {
            "the", "a", "an", "of", "and", "or", "in", "on", "to", "der", "die", "das", "und", "ein", "eine", "von", "im", "am", "zu", "den", "dem"
        };

        private static readonly Regex GermanMarkers = new(
            @"(?<![\p{L}\p{N}])(deutsch|german|h(ö|oe|o)rbuch|ungek(ü|ue|u)rzt|gelesen\s+von)(?![\p{L}\p{N}])|[\[\(]de[\]\)]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex EnglishMarkers = new(
            @"(?<![\p{L}\p{N}])(english|unabridged)(?![\p{L}\p{N}])|[\[\(]en[\]\)]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        // The folded spellings of one word: digraph form (ä -> ae) and plain form (ä -> a).
        public static IReadOnlyList<string> Variants(string word)
        {
            var digraph = Fold(word, true);
            var plain = Fold(word, false);
            return digraph == plain ? new[] { digraph } : new[] { digraph, plain };
        }

        public static string Fold(string text, bool digraphs)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(text.Length + 4);
            foreach (var character in text.ToLowerInvariant())
            {
                switch (character)
                {
                    case 'ä':
                        builder.Append(digraphs ? "ae" : "a");
                        break;
                    case 'ö':
                        builder.Append(digraphs ? "oe" : "o");
                        break;
                    case 'ü':
                        builder.Append(digraphs ? "ue" : "u");
                        break;
                    case 'ß':
                        builder.Append(digraphs ? "ss" : "s");
                        break;
                    default:
                        builder.Append(character);
                        break;
                }
            }

            // Strip remaining diacritics (é -> e).
            var decomposed = builder.ToString().Normalize(NormalizationForm.FormD);
            var result = new StringBuilder(decomposed.Length);
            foreach (var character in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                {
                    result.Append(character);
                }
            }

            return result.ToString().Normalize(NormalizationForm.FormC);
        }

        public static List<string> Tokens(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new List<string>();
            }

            return SplitRegex.Split(text.ToLowerInvariant())
                .Where(token => token.Length > 0)
                .ToList();
        }

        public static List<string> TitleTokens(string title)
        {
            return Tokens(title)
                .Where(token => !StopWords.Contains(token) && (token.Length > 1 || char.IsDigit(token[0])))
                .ToList();
        }

        // Fraction (0..1) of the significant title tokens that appear in the path.
        public static double TitleMatchRatio(string title, string path)
        {
            var wanted = TitleTokens(title);
            if (wanted.Count == 0)
            {
                return 0;
            }

            var haystack = Tokens(path);
            var haystackForms = new HashSet<string>(haystack.SelectMany(Variants), StringComparer.Ordinal);
            var joined = string.Join(" ", haystackForms);
            var matched = 0;

            foreach (var token in wanted)
            {
                var variants = Variants(token);
                if (variants.Any(variant => haystackForms.Contains(variant) ||
                                            (variant.Length >= 5 && joined.Contains(variant, StringComparison.Ordinal))))
                {
                    matched++;
                }
            }

            return matched / (double)wanted.Count;
        }

        // The last name token of an author ("J. R. R. Tolkien" -> "tolkien").
        public static string Surname(string author)
        {
            var tokens = Tokens(author)
                .Where(token => token.Length > 1 && token != "jr" && token != "sr")
                .ToList();
            return tokens.Count == 0 ? null : tokens[tokens.Count - 1];
        }

        public static bool PathContainsAuthor(string author, string path)
        {
            var surname = Surname(author);
            if (surname == null)
            {
                return true;
            }

            var haystack = Tokens(path).SelectMany(Variants).ToList();
            return Variants(surname).Any(variant => haystack.Contains(variant) ||
                                                   (variant.Length >= 5 && string.Join(" ", haystack).Contains(variant, StringComparison.Ordinal)));
        }

        // Language hint from the remote path. Null means unknown.
        public static Language DetectLanguage(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            var german = GermanMarkers.IsMatch(path);
            var english = EnglishMarkers.IsMatch(path);

            if (german && !english)
            {
                return Language.German;
            }

            if (english && !german)
            {
                return Language.English;
            }

            return null;
        }
    }
}
