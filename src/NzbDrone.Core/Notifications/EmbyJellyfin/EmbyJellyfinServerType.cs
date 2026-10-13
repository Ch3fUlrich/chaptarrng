using NzbDrone.Core.Annotations;

namespace NzbDrone.Core.Notifications.EmbyJellyfin
{
    public enum EmbyJellyfinServerType
    {
        [FieldOption(Label = "Jellyfin")]
        Jellyfin = 0,

        [FieldOption(Label = "Emby")]
        Emby = 1
    }
}
