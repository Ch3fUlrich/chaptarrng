using Chaptarr.Http.REST;
using NzbDrone.Core.Configuration;

namespace Chaptarr.Api.V1.Config
{
    public class WantedVersionsConfigResource : RestResource
    {
        // CSV, ISO 639-2 codes (e.g. "deu,eng")
        public string WantedVersionLanguages { get; set; }

        // CSV, lowercase format names (ebook: epub,azw3,mobi,pdf; audiobook: m4b,mp3,flac). Empty = any.
        public string WantedVersionEbookFormats { get; set; }
        public string WantedVersionAudiobookFormats { get; set; }

        // Monitoring a work (author "monitor all") also creates the wanted version rows.
        public bool WantedVersionsFanOutOnMonitor { get; set; }
    }

    public static class WantedVersionsConfigResourceMapper
    {
        public static WantedVersionsConfigResource ToResource(IConfigService model)
        {
            return new WantedVersionsConfigResource
            {
                WantedVersionLanguages = model.WantedVersionLanguages,
                WantedVersionEbookFormats = model.WantedVersionEbookFormats,
                WantedVersionAudiobookFormats = model.WantedVersionAudiobookFormats,
                WantedVersionsFanOutOnMonitor = model.WantedVersionsFanOutOnMonitor
            };
        }
    }
}
