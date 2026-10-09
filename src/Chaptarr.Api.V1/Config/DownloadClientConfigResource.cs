using Chaptarr.Http.REST;
using NzbDrone.Core.Configuration;

namespace Chaptarr.Api.V1.Config
{
    public class DownloadClientConfigResource : RestResource
    {
        public string DownloadClientWorkingFolders { get; set; }

        public bool EnableCompletedDownloadHandling { get; set; }
        public bool AutoRedownloadFailed { get; set; }
        public bool AutoRedownloadFailedFromInteractiveSearch { get; set; }

        public bool GrabBudgetEnabled { get; set; }
        public int GrabBudgetMaxPerRun { get; set; }
        public int GrabBudgetMaxPerDay { get; set; }
        public int GrabBudgetMaxActiveQueue { get; set; }
        public bool GrabBudgetApplyToInteractive { get; set; }
    }

    public static class DownloadClientConfigResourceMapper
    {
        public static DownloadClientConfigResource ToResource(IConfigService model)
        {
            return new DownloadClientConfigResource
            {
                DownloadClientWorkingFolders = model.DownloadClientWorkingFolders,

                EnableCompletedDownloadHandling = model.EnableCompletedDownloadHandling,
                AutoRedownloadFailed = model.AutoRedownloadFailed,
                AutoRedownloadFailedFromInteractiveSearch = model.AutoRedownloadFailedFromInteractiveSearch,

                GrabBudgetEnabled = model.GrabBudgetEnabled,
                GrabBudgetMaxPerRun = model.GrabBudgetMaxPerRun,
                GrabBudgetMaxPerDay = model.GrabBudgetMaxPerDay,
                GrabBudgetMaxActiveQueue = model.GrabBudgetMaxActiveQueue,
                GrabBudgetApplyToInteractive = model.GrabBudgetApplyToInteractive
            };
        }
    }
}
