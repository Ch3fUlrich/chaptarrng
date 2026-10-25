using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Indexers.Soulseek
{
    public class SlskdIndexerSettingsValidator : AbstractValidator<SlskdIndexerSettings>
    {
        public SlskdIndexerSettingsValidator()
        {
            RuleFor(c => c.BaseUrl)
                .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                             (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
                             string.IsNullOrEmpty(uri.UserInfo))
                .WithMessage("'slskd URL' must be an http:// or https:// URL without embedded credentials");

            RuleFor(c => c.SearchTimeoutSeconds).InclusiveBetween(5, 120);
            RuleFor(c => c.MaximumResults).InclusiveBetween(1, 100);
            RuleFor(c => c.MinimumTitleMatchPercent).InclusiveBetween(1, 100);
            RuleFor(c => c.MaximumQueueLength).GreaterThanOrEqualTo(0);
            RuleFor(c => c.MinimumUploadSpeedKBps).GreaterThanOrEqualTo(0);
            RuleFor(c => c.MinimumAudiobookSizeMB).GreaterThanOrEqualTo(0);
        }
    }

    public class SlskdIndexerSettings : IIndexerSettings
    {
        private static readonly SlskdIndexerSettingsValidator Validator = new();

        public SlskdIndexerSettings()
        {
            SearchTimeoutSeconds = 15;
            MaximumResults = 15;
            MaximumQueueLength = 50;
            MinimumAudiobookSizeMB = 80;
            MinimumTitleMatchPercent = 50;
            RequireAuthorMatch = true;
            AllowUnknownLanguage = true;
        }

        [FieldDefinition(0, Label = "slskd URL", HelpText = "Base URL of your slskd instance, for example http://slskd:5030 (slskd 0.26.0 or newer).")]
        public string BaseUrl { get; set; }

        [FieldDefinition(1, Label = "API Key", Type = FieldType.Password, Privacy = PrivacyLevel.ApiKey, HelpText = "slskd API key (slskd.yml: web.authentication.api_keys). Leave empty only if slskd authentication is disabled.")]
        public string ApiKey { get; set; }

        [FieldDefinition(2, Type = FieldType.Number, Label = "Search Timeout", Unit = "seconds", HelpText = "How long one slskd network search may run. Every book search waits for it, so keep it short.", Advanced = true)]
        public int SearchTimeoutSeconds { get; set; }

        [FieldDefinition(3, Type = FieldType.Number, Label = "Maximum Results", HelpText = "Best-scoring releases returned per search; the best release of every detected language is always kept.", Advanced = true)]
        public int MaximumResults { get; set; }

        [FieldDefinition(4, Type = FieldType.Checkbox, Label = "Require Free Upload Slot", HelpText = "Only use peers that can start the transfer immediately.")]
        public bool RequireFreeSlot { get; set; }

        [FieldDefinition(5, Type = FieldType.Number, Label = "Maximum Peer Queue Length", HelpText = "Skip peers with a longer upload queue. 0 disables the filter.")]
        public int MaximumQueueLength { get; set; }

        [FieldDefinition(6, Type = FieldType.Number, Label = "Minimum Peer Upload Speed", Unit = "KB/s", HelpText = "Skip peers that report a slower upload speed. 0 disables the filter.")]
        public int MinimumUploadSpeedKBps { get; set; }

        [FieldDefinition(7, Type = FieldType.Number, Label = "Minimum Audiobook Size", Unit = "MB", HelpText = "Folders smaller than this are not offered as audiobooks.", Advanced = true)]
        public int MinimumAudiobookSizeMB { get; set; }

        [FieldDefinition(8, Type = FieldType.Number, Label = "Minimum Title Match", Unit = "%", HelpText = "Share of the book title words that must appear in the remote path.", Advanced = true)]
        public int MinimumTitleMatchPercent { get; set; }

        [FieldDefinition(9, Type = FieldType.Checkbox, Label = "Require Author In Path", HelpText = "The author's last name must appear in the remote path.", Advanced = true)]
        public bool RequireAuthorMatch { get; set; }

        [FieldDefinition(10, Label = "Languages", HelpText = "Comma separated language names to accept (for example German, English). Empty accepts every language.", Advanced = true)]
        public string Languages { get; set; }

        [FieldDefinition(11, Type = FieldType.Checkbox, Label = "Allow Unknown Language", HelpText = "Keep releases whose language cannot be told from the remote path.", Advanced = true)]
        public bool AllowUnknownLanguage { get; set; }

        [FieldDefinition(12, Type = FieldType.Number, Label = "Early Download Limit", Unit = "days", HelpText = "Time before release date Chaptarr will download from this indexer, empty is no limit", Advanced = true)]
        public int? EarlyReleaseLimit { get; set; }

        public NzbDroneValidationResult Validate()
        {
            return new NzbDroneValidationResult(Validator.Validate(this));
        }

        public IReadOnlyList<string> LanguageNames()
        {
            return (Languages ?? string.Empty)
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(name => name.Trim())
                .Where(name => name.Length > 0)
                .ToList();
        }
    }
}
