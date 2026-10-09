using System;
using System.Collections.Generic;
using Chaptarr.Http;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download.GrabBudget;

namespace Chaptarr.Api.V1.GrabBudget
{
    [V1ApiController("grabbudget/batch")]
    public class GrabBudgetBatchController : Controller
    {
        private readonly IGrabBudgetService _grabBudgetService;
        private readonly IConfigService _configService;

        public GrabBudgetBatchController(IGrabBudgetService grabBudgetService, IConfigService configService)
        {
            _grabBudgetService = grabBudgetService;
            _configService = configService;
        }

        [HttpGet]
        public List<GrabBudgetBatchResource> GetLatestBatches()
        {
            return _grabBudgetService.GetLatestBatches(20)
                .ConvertAll(GrabBudgetBatchResourceMapper.MapToResource);
        }

        [HttpGet("/api/v1/grabbudget/status")]
        [HttpGet("~/api/v1/grabbudget/status")]
        [HttpGet("status")]
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
