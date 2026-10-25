using System;
using System.Collections.Generic;

namespace NzbDrone.Core.Soulseek
{
    // JSON shapes of slskd 0.26.x (see docs/slskd.md). Deserialised case-insensitively;
    // unknown properties are ignored so newer slskd versions keep working.

    public class SlskdSearchRequest
    {
        public Guid Id { get; set; }
        public string SearchText { get; set; }

        // milliseconds
        public int SearchTimeout { get; set; }
        public int FileLimit { get; set; }
        public bool FilterResponses { get; set; }
        public int MinimumResponseFileCount { get; set; }
        public int MaximumPeerQueueLength { get; set; }
        public int MinimumPeerUploadSpeed { get; set; }
    }

    public class SlskdSearch
    {
        public Guid Id { get; set; }
        public string SearchText { get; set; }
        public string State { get; set; }
        public bool IsComplete { get; set; }
        public int ResponseCount { get; set; }
        public int FileCount { get; set; }
        public List<SlskdSearchResponse> Responses { get; set; } = new();
    }

    public class SlskdSearchResponse
    {
        public string Username { get; set; }
        public bool HasFreeUploadSlot { get; set; }

        // bytes per second
        public int UploadSpeed { get; set; }
        public long QueueLength { get; set; }
        public int FileCount { get; set; }
        public List<SlskdFile> Files { get; set; } = new();
        public List<SlskdFile> LockedFiles { get; set; } = new();
    }

    public class SlskdFile
    {
        public string Filename { get; set; }
        public long Size { get; set; }
        public string Extension { get; set; }
        public int? BitRate { get; set; }
        public int? Length { get; set; }
        public bool IsLocked { get; set; }
    }

    public class SlskdApplicationState
    {
        public SlskdVersionState Version { get; set; }
        public SlskdServerState Server { get; set; }
    }

    public class SlskdVersionState
    {
        public string Current { get; set; }
    }

    public class SlskdServerState
    {
        // Soulseek.NET flags rendered as text, for example "Connected, LoggedIn"
        public string State { get; set; }

        public bool IsLoggedIn => State != null && State.Contains("LoggedIn", StringComparison.OrdinalIgnoreCase);
    }

    public class SlskdConnection
    {
        public string BaseUrl { get; set; }
        public string ApiKey { get; set; }
    }

    public class SlskdApiException : Exception
    {
        public int StatusCode { get; }

        public SlskdApiException(int statusCode, string message)
            : base(message)
        {
            StatusCode = statusCode;
        }
    }
}
