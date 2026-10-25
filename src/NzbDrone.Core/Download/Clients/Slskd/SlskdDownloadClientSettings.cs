using System;
using FluentValidation;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.ThingiProvider;
using NzbDrone.Core.Validation;
using NzbDrone.Core.Validation.Paths;

namespace NzbDrone.Core.Download.Clients.Slskd
{
    public class SlskdDownloadClientSettingsValidator : AbstractValidator<SlskdDownloadClientSettings>
    {
        public SlskdDownloadClientSettingsValidator()
        {
            RuleFor(c => c.BaseUrl)
                .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                             (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
                             string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment))
                .WithMessage("'slskd URL' must be an http:// or https:// URL without credentials, query or fragment");

            RuleFor(c => c.DownloadDirectory).IsValidPath();
        }
    }

    public class SlskdDownloadClientSettings : IProviderConfig
    {
        private static readonly SlskdDownloadClientSettingsValidator Validator = new();

        [FieldDefinition(0, Label = "slskd URL", HelpText = "Base URL of your slskd instance, for example http://slskd:5030 (slskd 0.26.0 or newer).")]
        public string BaseUrl { get; set; }

        [FieldDefinition(1, Label = "API Key", Type = FieldType.Password, Privacy = PrivacyLevel.ApiKey, HelpText = "slskd API key (slskd.yml: web.authentication.api_keys). Leave empty only if slskd authentication is disabled.")]
        public string ApiKey { get; set; }

        [FieldDefinition(2, Label = "Download Directory", Type = FieldType.Path, HelpText = "slskd's download directory as Chaptarr sees it (the same folder, often mounted at a different path). Chaptarr imports from chaptarr-<id> sub folders and may delete them after import.")]
        public string DownloadDirectory { get; set; }

        public NzbDroneValidationResult Validate()
        {
            return new NzbDroneValidationResult(Validator.Validate(this));
        }
    }
}
