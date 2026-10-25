using Chaptarr.Http;
using NzbDrone.Core.Configuration;

namespace Chaptarr.Api.V1.Config
{
    [V1ApiController("config/wantedversions")]
    public class WantedVersionsConfigController : ConfigController<WantedVersionsConfigResource>
    {
        public WantedVersionsConfigController(IConfigService configService)
            : base(configService)
        {
        }

        protected override WantedVersionsConfigResource ToResource(IConfigService model)
        {
            return WantedVersionsConfigResourceMapper.ToResource(model);
        }
    }
}
