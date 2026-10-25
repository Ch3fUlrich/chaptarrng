using Chaptarr.Http;
using Chaptarr.Http.Validation;
using FluentValidation;
using NzbDrone.Core.Configuration;

namespace Chaptarr.Api.V1.Config
{
    [V1ApiController("config/indexer")]
    public class IndexerConfigController : ConfigController<IndexerConfigResource>
    {
        public IndexerConfigController(IConfigService configService)
            : base(configService)
        {
            SharedValidator.RuleFor(c => c.MinimumAge)
                           .GreaterThanOrEqualTo(0);

            SharedValidator.RuleFor(c => c.MaximumSize)
                           .GreaterThanOrEqualTo(0);

            SharedValidator.RuleFor(c => c.Retention)
                           .GreaterThanOrEqualTo(0);

            SharedValidator.RuleFor(c => c.RssSyncInterval)
                           .IsValidRssSyncInterval();

            SharedValidator.RuleFor(c => c.MissingBookSearchInterval)
                           .IsValidMissingBookSearchInterval();

            SharedValidator.RuleFor(c => c.MissingSearchMaxRows).GreaterThanOrEqualTo(0);
            SharedValidator.RuleFor(c => c.MissingSearchCooldownHours).GreaterThanOrEqualTo(0);
            SharedValidator.RuleFor(c => c.MissingSearchMaxAttempts).GreaterThanOrEqualTo(0);
        }

        protected override IndexerConfigResource ToResource(IConfigService model)
        {
            return IndexerConfigResourceMapper.ToResource(model);
        }
    }
}
