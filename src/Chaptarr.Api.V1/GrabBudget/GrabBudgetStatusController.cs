using System;
using Chaptarr.Http;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download.GrabBudget;

namespace Chaptarr.Api.V1.GrabBudget
{
    [V1ApiController("grabbudget/status")]
    public class GrabBudgetStatusController : Controller
    {
        private readonly IGrabBudgetService _grabBudgetService;
        private readonly IConfigService _configService;

        public GrabBudgetStatusController(IGrabBudgetService grabBudgetService, IConfigService configService)
        {
            _grabBudgetService = grabBudgetService;
            _configService = configService;
        }

        [HttpGet]
        public GrabBudgetStatusResource GetStatus()
        {
            return new GrabBudgetStatusResource
            {
                Enabled = _configService.GrabBudgetEnabled,
                DryRun = _configService.GrabBudgetDryRun,
                GrabsInLastDay = _grabBudgetService.GrabsInLastDay(DateTime.UtcNow),
                MaxPerDay = _configService.GrabBudgetMaxPerDay,
                MaxPerRun = _configService.GrabBudgetMaxPerRun,
                MaxActiveQueue = _configService.GrabBudgetMaxActiveQueue
            };
        }
    }
}
