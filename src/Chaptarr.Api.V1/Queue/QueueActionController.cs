using System.Threading.Tasks;
using Chaptarr.Http;
using Chaptarr.Http.REST;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.GrabBudget;
using NzbDrone.Core.Download.Pending;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.Queue;
using HttpStatusCode = System.Net.HttpStatusCode;

namespace Chaptarr.Api.V1.Queue
{
    [V1ApiController("queue")]
    public class QueueActionController : Controller
    {
        private readonly IPendingReleaseService _pendingReleaseService;
        private readonly IDownloadService _downloadService;
        private readonly IGrabBudgetService _grabBudgetService;
        private readonly IQueueService _queueService;

        public QueueActionController(IPendingReleaseService pendingReleaseService,
                                     IDownloadService downloadService,
                                     IGrabBudgetService grabBudgetService = null,
                                     IQueueService queueService = null)
        {
            _pendingReleaseService = pendingReleaseService;
            _downloadService = downloadService;
            _grabBudgetService = grabBudgetService;
            _queueService = queueService;
        }

        // Manual grab of a pending release: same interactive budget rule as the release grab endpoint.
        private async Task GrabPending(NzbDrone.Core.Parser.Model.RemoteBook remoteBook)
        {
            var budget = _grabBudgetService?.CheckInteractive(_queueService?.GetQueue()?.Count ?? 0);
            if (budget != null && !budget.Allowed)
            {
                throw new NzbDroneClientException(HttpStatusCode.TooManyRequests, budget.Reason);
            }

            await _downloadService.DownloadReport(remoteBook, null);
            _grabBudgetService?.RecordInteractiveGrab();
        }

        [HttpPost("grab/{id:int}")]
        public async Task<object> Grab(int id)
        {
            var pendingRelease = _pendingReleaseService.FindPendingQueueItem(id);

            if (pendingRelease == null)
            {
                throw new NotFoundException();
            }

            await GrabPending(pendingRelease.RemoteBook);

            return new { };
        }

        [HttpPost("grab/bulk")]
        [Consumes("application/json")]
        public async Task<object> Grab([FromBody] QueueBulkResource resource)
        {
            foreach (var id in resource.Ids)
            {
                var pendingRelease = _pendingReleaseService.FindPendingQueueItem(id);

                if (pendingRelease == null)
                {
                    throw new NotFoundException();
                }

                await GrabPending(pendingRelease.RemoteBook);
            }

            return new { };
        }
    }
}
