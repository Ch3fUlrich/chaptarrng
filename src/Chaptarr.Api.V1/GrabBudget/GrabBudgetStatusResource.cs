using Chaptarr.Http.REST;

namespace Chaptarr.Api.V1.GrabBudget
{
    public class GrabBudgetStatusResource : RestResource
    {
        public bool Enabled { get; set; }
        public bool DryRun { get; set; }
        public int GrabsInLastDay { get; set; }
        public int MaxPerDay { get; set; }
        public int MaxPerRun { get; set; }
        public int MaxActiveQueue { get; set; }
    }
}
