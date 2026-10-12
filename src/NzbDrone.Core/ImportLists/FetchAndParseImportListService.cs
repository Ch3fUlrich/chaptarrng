using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Common.TPL;
using NzbDrone.Core.ImportLists.Hardcover.Library;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.ImportLists
{
    public interface IFetchAndParseImportList
    {
        List<ImportListItemInfo> Fetch();
        List<ImportListItemInfo> FetchSingleList(ImportListDefinition definition);
        ImportListFetchResult FetchWithState() => new ImportListFetchResult(Fetch(), new List<IImportList>());
        ImportListFetchResult FetchSingleListWithState(ImportListDefinition definition) => new ImportListFetchResult(FetchSingleList(definition), new List<IImportList>());
    }

    public sealed class ImportListFetchResult
    {
        public List<ImportListItemInfo> Items { get; }
        public List<IImportList> FetchedLists { get; }

        public ImportListFetchResult(List<ImportListItemInfo> items, List<IImportList> fetchedLists)
        {
            Items = items ?? new List<ImportListItemInfo>();
            FetchedLists = fetchedLists ?? new List<IImportList>();
        }
    }

    public class FetchAndParseImportListService : IFetchAndParseImportList
    {
        private readonly IImportListFactory _importListFactory;
        private readonly IImportListStatusService _importListStatusService;
        private readonly Logger _logger;

        public FetchAndParseImportListService(IImportListFactory importListFactory, IImportListStatusService importListStatusService, Logger logger)
        {
            _importListFactory = importListFactory;
            _importListStatusService = importListStatusService;
            _logger = logger;
        }

        private static object BuildDedupeKey(ImportListItemInfo item)
        {
            if (item == null)
            {
                return null;
            }

            var authorProviderId = ImportListProviderIdHelper.Normalize(item.AuthorProviderId, "gr");
            var bookProviderId = ImportListProviderIdHelper.Normalize(item.BookProviderId, "gr");
            var editionProviderId = ImportListProviderIdHelper.Normalize(item.EditionProviderId, "gr");

            var fallbackAuthor = item.Author?.Trim();
            var fallbackBook = item.Book?.Trim();

            // Never dedupe across import lists: per-list config (monitoring/profiles/root folder) can differ.
            return new
            {
                item.ImportListId,
                Author = authorProviderId ?? fallbackAuthor,
                Book = bookProviderId ?? editionProviderId ?? fallbackBook,
                Edition = editionProviderId,
                item.HardcoverReadingFormatId
            };
        }

        public List<ImportListItemInfo> Fetch()
        {
            var result = FetchWithState();
            CompleteFetch(result);
            return result.Items;
        }

        public ImportListFetchResult FetchWithState()
        {
            var result = new List<ImportListItemInfo>();
            var fetchedLists = new List<IImportList>();

            var importLists = _importListFactory.AutomaticAddEnabled();

            if (!importLists.Any())
            {
                _logger.Debug("No enabled import lists, skipping.");
                return new ImportListFetchResult(result, fetchedLists);
            }

            _logger.Debug("Available import lists {0}", importLists.Count);

            var taskList = new List<Task>();
            var taskFactory = new TaskFactory(TaskCreationOptions.LongRunning, TaskContinuationOptions.None);

            foreach (var importList in importLists)
            {
                var importListLocal = importList;

                // Hardcover Library import lists are synced by a dedicated scheduled task.
                if (importListLocal is HardcoverLibraryImportList)
                {
                    continue;
                }

                var importListStatus = _importListStatusService.GetLastSyncListInfo(importListLocal.Definition.Id);

                if (importListStatus.HasValue)
                {
                    var importListNextSync = importListStatus.Value + importListLocal.MinRefreshInterval;

                    if (DateTime.UtcNow < importListNextSync)
                    {
                        _logger.Trace("Skipping refresh of Import List {0} ({1}) due to minimum refresh interval. Next sync after {2}", importList.Name, importListLocal.Definition.Name, importListNextSync);
                        continue;
                    }
                }

                var task = taskFactory.StartNew(() =>
                     {
                         try
                         {
                             var importListReports = importListLocal.Fetch();

                             lock (result)
                             {
                                 _logger.Debug("Found {0} reports from {1} ({2})", importListReports.Count, importList.Name, importListLocal.Definition.Name);

                                 result.AddRange(importListReports);
                                 fetchedLists.Add(importListLocal);
                             }

                         }
                         catch (Exception e)
                         {
                             _logger.Error(e, "Error during Import List Sync of {0} ({1})", importList.Name, importListLocal.Definition.Name);
                         }
                     }).LogExceptions();

                taskList.Add(task);
            }

            Task.WaitAll(taskList.ToArray());

            result = result
                .Where(r => r != null)
                .DistinctBy(BuildDedupeKey)
                .ToList();

            _logger.Debug("Found {0} total reports from {1} lists", result.Count, importLists.Count);

            return new ImportListFetchResult(result, fetchedLists);
        }

        public List<ImportListItemInfo> FetchSingleList(ImportListDefinition definition)
        {
            var result = FetchSingleListWithState(definition);
            CompleteFetch(result);
            return result.Items;
        }

        public ImportListFetchResult FetchSingleListWithState(ImportListDefinition definition)
        {
            var result = new List<ImportListItemInfo>();
            var fetchedLists = new List<IImportList>();

            var importList = _importListFactory.GetInstance(definition);

            if (importList == null || !definition.EnableAutomaticAdd)
            {
                _logger.Debug("Import List {0} is not enabled or could not be loaded, skipping.", definition?.Name ?? definition?.Id.ToString());
                return new ImportListFetchResult(result, fetchedLists);
            }

            var taskList = new List<Task>();
            var taskFactory = new TaskFactory(TaskCreationOptions.LongRunning, TaskContinuationOptions.None);

            var importListLocal = importList;

            var task = taskFactory.StartNew(() =>
            {
                try
                {
                    var importListReports = importListLocal.Fetch();

                    lock (result)
                    {
                        _logger.Debug("Found {0} reports from {1} ({2})", importListReports.Count, importList.Name, importListLocal.Definition.Name);

                        result.AddRange(importListReports);
                        fetchedLists.Add(importListLocal);
                    }

                }
                catch (Exception e)
                {
                    _logger.Error(e, "Error during Import List Sync of {0} ({1})", importList.Name, importListLocal.Definition.Name);
                }
            }).LogExceptions();

            taskList.Add(task);

            Task.WaitAll(taskList.ToArray());

            result = result
                .Where(r => r != null)
                .DistinctBy(BuildDedupeKey)
                .ToList();

            return new ImportListFetchResult(result, fetchedLists);
        }

        private void CompleteFetch(ImportListFetchResult result)
        {
            foreach (var importList in result?.FetchedLists ?? new List<IImportList>())
            {
                try
                {
                    importList.CommitState();
                    _importListStatusService.UpdateListSyncStatus(importList.Definition.Id);
                }
                catch (Exception e)
                {
                    _logger.Error(e, "Error committing state for Import List {0} ({1})", importList.Name, importList.Definition.Name);
                }
            }
        }
    }
}
