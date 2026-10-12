using FluentValidation.Results;
using NzbDrone.Core.MediaFiles;

namespace NzbDrone.Core.Notifications.EmbyJellyfin
{
    public class EmbyJellyfin : NotificationBase<EmbyJellyfinSettings>
    {
        private readonly IEmbyJellyfinProxy _proxy;

        public EmbyJellyfin(IEmbyJellyfinProxy proxy)
        {
            _proxy = proxy;
        }

        public override string Link => "https://jellyfin.org/docs/";

        public override string Name => "Emby / Jellyfin";

        public override ValidationResult Test()
        {
            return _proxy.Test(Settings);
        }

        public override void OnReleaseImport(BookDownloadMessage message)
        {
            _proxy.RefreshLibrary(Settings);
        }
    }
}
