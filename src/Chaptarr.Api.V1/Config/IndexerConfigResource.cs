using Chaptarr.Http.REST;
using NzbDrone.Core.Configuration;

namespace Chaptarr.Api.V1.Config
{
    public class IndexerConfigResource : RestResource
    {
        public int MinimumAge { get; set; }
        public int MaximumSize { get; set; }
        public int Retention { get; set; }
        public int RssSyncInterval { get; set; }
        public int MissingBookSearchInterval { get; set; }
        public int MissingSearchMaxRows { get; set; }
        public int MissingSearchCooldownHours { get; set; }
        public int MissingSearchMaxAttempts { get; set; }
    }

    public static class IndexerConfigResourceMapper
    {
        public static IndexerConfigResource ToResource(IConfigService model)
        {
            return new IndexerConfigResource
            {
                MinimumAge = model.MinimumAge,
                MaximumSize = model.MaximumSize,
                Retention = model.Retention,
                RssSyncInterval = model.RssSyncInterval,
                MissingBookSearchInterval = model.MissingBookSearchInterval,
                MissingSearchMaxRows = model.MissingSearchMaxRows,
                MissingSearchCooldownHours = model.MissingSearchCooldownHours,
                MissingSearchMaxAttempts = model.MissingSearchMaxAttempts,
            };
        }
    }
}
