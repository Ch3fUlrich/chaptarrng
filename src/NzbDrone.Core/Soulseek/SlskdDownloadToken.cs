using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace NzbDrone.Core.Soulseek
{
    public class SlskdTokenFile
    {
        public string Filename { get; set; }
        public long Size { get; set; }
    }

    // Everything the download client needs to enqueue a release, carried in ReleaseInfo.DownloadUrl
    // as slskd://soulseek/<base64url(json)>. Search results expire in slskd, the token does not.
    public class SlskdDownloadToken
    {
        public const string Prefix = "slskd://soulseek/";
        public const int MaxFiles = 500;

        public string Username { get; set; }
        public string Directory { get; set; }
        public char Separator { get; set; } = '\\';
        public List<SlskdTokenFile> Files { get; set; } = new();

        public static string ToUrl(SlskdDownloadToken token)
        {
            // Compact form: directory once, files relative to it.
            var payload = new Payload
            {
                U = token.Username,
                D = token.Directory,
                S = token.Separator.ToString(),
                F = token.Files.Select(file => new object[] { Relative(token, file.Filename), file.Size }).ToList()
            };

            var json = JsonSerializer.Serialize(payload);
            return Prefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        public static bool TryParse(string url, out SlskdDownloadToken token)
        {
            token = null;

            if (string.IsNullOrWhiteSpace(url) || !url.StartsWith(Prefix, StringComparison.Ordinal))
            {
                return false;
            }

            try
            {
                var encoded = url.Substring(Prefix.Length).Replace('-', '+').Replace('_', '/');
                encoded = encoded.PadRight(encoded.Length + ((4 - (encoded.Length % 4)) % 4), '=');
                var payload = JsonSerializer.Deserialize<Payload>(Encoding.UTF8.GetString(Convert.FromBase64String(encoded)));

                if (payload == null || string.IsNullOrWhiteSpace(payload.U) || payload.F == null || payload.F.Count == 0 || payload.F.Count > MaxFiles)
                {
                    return false;
                }

                var separator = string.IsNullOrEmpty(payload.S) ? '\\' : payload.S[0];
                if (separator != '\\' && separator != '/')
                {
                    return false;
                }

                var parsed = new SlskdDownloadToken { Username = payload.U, Directory = payload.D ?? string.Empty, Separator = separator };

                foreach (var entry in payload.F)
                {
                    if (entry == null || entry.Length != 2)
                    {
                        return false;
                    }

                    if (entry[0] is not JsonElement { ValueKind: JsonValueKind.String } name ||
                        entry[1] is not JsonElement { ValueKind: JsonValueKind.Number } number ||
                        !number.TryGetInt64(out var size))
                    {
                        return false;
                    }

                    var relative = name.GetString();
                    var filename = string.IsNullOrEmpty(parsed.Directory) ? relative : parsed.Directory + separator + relative;

                    if (string.IsNullOrWhiteSpace(relative) || size <= 0 || HasTraversal(filename) || HasTraversal(parsed.Directory))
                    {
                        return false;
                    }

                    parsed.Files.Add(new SlskdTokenFile { Filename = filename, Size = size });
                }

                token = parsed;
                return true;
            }
            catch (Exception ex) when (ex is FormatException || ex is JsonException || ex is InvalidOperationException || ex is ArgumentException)
            {
                return false;
            }
        }

        // True for anything that is not a plain relative-looking remote path: dot segments (also with trailing
        // dots/spaces), control characters or a drive letter. Both separators are always checked.
        public static bool HasTraversal(string path)
        {
            if (path == null)
            {
                return false;
            }

            if (path.Any(char.IsControl) || (path.Length >= 2 && char.IsLetter(path[0]) && path[1] == ':'))
            {
                return true;
            }

            return path.Split('\\', '/').Any(segment => segment.Length > 0 && segment.Trim('.', ' ').Length == 0);
        }

        private static string Relative(SlskdDownloadToken token, string filename)
        {
            return string.IsNullOrEmpty(token.Directory) || filename.Length <= token.Directory.Length + 1
                ? filename
                : filename.Substring(token.Directory.Length + 1);
        }

        private class Payload
        {
            public string U { get; set; }
            public string D { get; set; }
            public string S { get; set; }
            public List<object[]> F { get; set; }
        }
    }
}
