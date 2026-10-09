using System;
using Chaptarr.Http.REST;
using NzbDrone.Core.Download.GrabBudget;

namespace Chaptarr.Api.V1.GrabBudget
{
    public class GrabBudgetBatchResource : RestResource
    {
        public DateTime StartedAt { get; set; }
        public int Grabbed { get; set; }
        public int Skipped { get; set; }
        public int Failed { get; set; }
        public string StopReason { get; set; }
        public string Details { get; set; }
    }

    public static class GrabBudgetBatchResourceMapper
    {
        public static GrabBudgetBatchResource MapToResource(this GrabBudgetBatch model)
        {
            if (model == null)
            {
                return null;
            }

            return new GrabBudgetBatchResource
            {
                Id = model.Id,
                StartedAt = model.StartedAt,
                Grabbed = model.Grabbed,
                Skipped = model.Skipped,
                Failed = model.Failed,
                StopReason = model.StopReason.ToString(),
                Details = model.Details
            };
        }
    }
}
