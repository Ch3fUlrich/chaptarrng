# Book-list provenance: round 2 (D-5502)

The previous report was incorrect: the unfiltered controller list loads lean books,
then hydrates files for availability. That second load is now explicitly display-only.
No default projection, schema, migration, or stored data was changed.

## GetFilesByBooks call-site audit (API and Core)

Paths below are relative to `src`. Interface defaults and service/repository forwarding
are not independent consumers; they remain full by default, with the existing explicit
display entry point forwarding to the selective projection.

| Consumer | Decision | Reason |
| --- | --- | --- |
| Chaptarr.Api.V1/Books/BookController.HydrateFilesForAvailability | Switch | Unfiltered list, paged list, and bookshelf only attach rows for resource availability; no saves. |
| Chaptarr.Api.V1/Books/BookController.GetWorks | Switch | Only builds a set of book IDs with files for work/version display. |
| Chaptarr.Api.V1/Books/BookController.GetBookSiblings (both calls) | Switch | Delete-preview resources contain paths, sizes, formats and counts; this GET does not delete or save rows. |
| Chaptarr.Api.V1/Author/AuthorController.LinkNextPreviousBooks | Switch | PreloadAuthorIndexBooks attaches files to next/last books for resource mapping only. |
| NzbDrone.Core/Books/Services/BookService.GetBooksForDisplay | Keep existing display load | Author-specific list attaches files for list-resource mapping. |
| NzbDrone.Core/Books/Services/BookService.HydrateBooksForDeleteEvent | Keep full | Delete-event operational model hydration, not list display. |
| NzbDrone.Core/Books/Services/BookService.GetBook | Keep full | Detail/operational model, also consumed by update workflows. |
| NzbDrone.Core/Books/Services/BookService.UpdateMany | Keep full | Edition-selection/relinking and update workflow. |
| NzbDrone.Core/Books/Services/RefreshAuthorService (localBookIds unmerge load) | Keep full | Refresh/unmerge edition re-home decisions, not display. |
| NzbDrone.Core/Books/Services/RefreshAuthorService (bookIds hydration load) | Keep full | Refresh operational model hydration, not display-only. |
| NzbDrone.Core/Books/Services/BookDurationService.UpdateAllBookDurations | Keep full | Explicit repair updates books; outside display/availability scope. |
| NzbDrone.Core/MediaFiles/AudioTagService.HydrateEditionBookFiles | Keep full | Shared tag-writing workflow, not display-only. |
| NzbDrone.Core/MediaFiles/EbookTagService.HydrateEditionBookFiles | Keep full | Shared tag-writing workflow, not display-only. |
| NzbDrone.Core/Notifications/MqttGateway/CanonicalEventGatewayService | Keep full | Series-completion notification decisions, not list display. |

## Other multi-book file query surfaces

Searched API/Core for BookFile queries, joins, batch/path/author loaders and lazy
mapping. These shared query surfaces must not be globally converted to display loads:

| Query surface | Decision and reason |
| --- | --- |
| MediaFileRepository.GetFilesByAuthor / service GetFilesByAuthorAndMediaType | Keep full: conversion, rename, retag, author updates, parsing, metadata and extras workflows share this loader. BookFileController also exposes full file details, including provenance. |
| MediaFileRepository.GetFileWithPath(List<string>) | Keep full: DiskScanService persistence/reconciliation and MediaFileService.FilterUnchangedFiles; not display models. |
| MediaFileRepository.GetFilesWithBasePath (both overloads) | Keep full: file moves, cleanup, deletion, import/staging, unmapped refresh and library notifications. |
| BasicRepository<BookFile>.All/Get(IEnumerable<int>), MediaFileRepository.Query/Builder | Keep full: generic operational/detail/write API; never globally omit provenance. BookFileController multi-book GET iterates GetFilesByBook and maps full file resources. |
| TableMapping Book.LazyBookFiles / edition lazy mapping | Keep full: general-purpose lazy models, not restricted to lists. Book-list hydration overrides the book's files before availability mapping. |
| MediaFileRepository.GetUnmappedFiles overloads | Keep full: unmatched file review/retry workflows, not files belonging to many books. |
| MediaFileRepository.GetFileStatsWithBasePath | Keep existing narrow projection: scan/stat evidence only. |
| MediaFileRepository.GetMappedFilePathEvidenceByAuthor | Keep existing narrow projection: refresh path evidence only. |
| MediaFileRepository.GetReplicaPathsWithBasePath/GetLatestImportDate | Keep scalar projections; no provenance materialization. |
| BookRepository BookFile joins (GetBooksByFileIds, without-files, cutoff, GetAuthorBooksWithFiles, EXISTS predicates) | Keep: filtering/selecting books, not projecting BookFile documents. |
| BookFileStatisticsSql, AuthorStatisticsRepository, SystemStatisticsRepository | Keep aggregate/stat projections; no full BookFile documents. |

## Mapping and writes

HydrateFilesForAvailability callers immediately map resources; they never save the
attached files. BookAvailabilityCalculator reads paths/extensions and edition/book
languages, not provenance. BookResourceMapper does not populate BookResource.BookFiles;
the unfiltered list therefore has `bookFiles: null`, **not nested files with misleading
`matchProvenance: null`**. EditionResource maps file counts, not file resources.
The author index mapper likewise uses ordinary book-resource mapping. Sibling previews
use a separate file DTO without provenance; work lists only use file presence.

Searched API PUT/POST handlers and resource conversion: BookController add/import/update
and bulk update convert BookResource without consuming its BookFiles property;
BookResource.ToModel (both new and existing model overloads) ignores it. BookEditorController
accepts IDs/settings, not nested files. BookFileController quality PUT reloads full rows
by ID and changes quality only; its editor accepts file IDs/quality and reloads rows.
There is no nested-book-file update path from book-list payloads, so no client-side
change is needed to protect omitted provenance.

## Regression coverage and limitations

`BookControllerAuthorListFixture.unfiltered_book_list_should_hydrate_availability_using_only_display_files`
calls the actual unfiltered GetBooks branch. The strict media-service proxy implements
only GetFilesByBooksForDisplay: any full load or write throws. It checks requested IDs,
one display load, attachment of the partial row, readable EPUB availability and absence
of nested file resources. Empty statistics deliberately exercise the stale/missing-stat
hydration case. The existing author-list test remains separately named and exercised.
Existing repository tests, including the 20,000-row selective-projection test, are retained.

`dotnet` is absent in this environment. No build or tests could be run; production OOM,
serialization under a live server, and database-provider runtime behavior were not
reproduced. Static diff whitespace validation is the available check.
