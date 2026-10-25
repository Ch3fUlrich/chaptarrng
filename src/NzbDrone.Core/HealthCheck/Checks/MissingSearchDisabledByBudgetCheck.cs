using NzbDrone.Core.Configuration;
using NzbDrone.Core.Configuration.Events;
using NzbDrone.Core.Localization;

namespace NzbDrone.Core.HealthCheck.Checks
{
    [CheckOn(typeof(ConfigSavedEvent))]
    public class MissingSearchDisabledByBudgetCheck : HealthCheckBase
    {
        private readonly IConfigService _configService;

        public MissingSearchDisabledByBudgetCheck(IConfigService configService, ILocalizationService localizationService)
            : base(localizationService)
        {
            _configService = configService;
        }

        public override HealthCheck Check()
        {
            if (_configService.MissingBookSearchInterval > 0 && !_configService.GrabBudgetEnabled)
            {
                return new HealthCheck(GetType(), HealthCheckResult.Warning, _localizationService.GetLocalizedString("MissingSearchDisabledByBudgetMessage"));
            }

            return new HealthCheck(GetType());
        }
    }
}
