using System.Collections.Generic;
using Chaptarr.Http;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Download.GrabBudget;

namespace Chaptarr.Api.V1.GrabBudget
{
    [V1ApiController("grabbudget/batch")]
    public class GrabBudgetBatchController : Controller
    {
        private readonly IGrabBudgetService _grabBudgetService;

        public GrabBudgetBatchController(IGrabBudgetService grabBudgetService)
        {
            _grabBudgetService = grabBudgetService;
        }

        [HttpGet]
        public List<GrabBudgetBatchResource> GetLatestBatches()
        {
            return _grabBudgetService.GetLatestBatches(20)
                .ConvertAll(GrabBudgetBatchResourceMapper.MapToResource);
        }
    }
}
