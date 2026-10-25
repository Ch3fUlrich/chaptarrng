using NLog;
using NzbDrone.Core.Books;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.DecisionEngine.Specifications
{
    // Settings > Versions: per-medium list of wanted file formats. An empty list leaves the medium unrestricted.
    public class WantedFormatSpecification : IDecisionEngineSpecification
    {
        private readonly IWantedVersionService _wantedVersionService;
        private readonly Logger _logger;

        public WantedFormatSpecification(IWantedVersionService wantedVersionService, Logger logger)
        {
            _wantedVersionService = wantedVersionService;
            _logger = logger;
        }

        public SpecificationPriority Priority => SpecificationPriority.Default;
        public RejectionType Type => RejectionType.Permanent;

        public Decision IsSatisfiedBy(RemoteBook subject, SearchCriteriaBase searchCriteria)
        {
            var quality = subject.ParsedBookInfo?.Quality?.Quality;
            if (quality == null)
            {
                return Decision.Accept();
            }

            BookMediaType? mediaType = null;
            if (quality == Quality.PDF || quality == Quality.MOBI || quality == Quality.EPUB || quality == Quality.AZW3)
            {
                mediaType = BookMediaType.Ebook;
            }
            else if (quality == Quality.M4B || quality == Quality.MP3 || quality == Quality.FLAC || quality == Quality.UnknownAudio)
            {
                mediaType = BookMediaType.Audiobook;
            }

            if (mediaType == null)
            {
                return Decision.Accept();
            }

            var wanted = _wantedVersionService.GetWantedFormats(mediaType.Value);
            if (wanted.Count == 0 || wanted.Contains(quality.Name.ToLowerInvariant()))
            {
                return Decision.Accept();
            }

            _logger.Trace("Rejecting {0}: format {1} is not in the wanted {2} formats", subject.Release?.Title, quality.Name, mediaType);
            return Decision.RejectSoftFilter($"Format {quality.Name} is not in the wanted {mediaType.ToString().ToLowerInvariant()} formats", "Format");
        }
    }
}
