# Round 5 display-row audit

Scope: every production caller of `GetFilesByBooksForDisplay`. Paths below are
relative to `src/`. Crucially, `file.Edition` is a key-only joined object, NOT
an element of `book.Editions`. Metadata editions are loaded separately. No
consumer maps a display row through `BookFileResourceMapper`.

## Row consumers

| Consumer | Method / downstream helper | File:line | Properties read |
|---|---|---|---|
| Repository | display join / shared edition cache | NzbDrone.Core/MediaFiles/MediaFileRepository.cs:148-172 | BookFile.EditionId (join); Edition.Id, BookId (filter); Edition.Id (cache) |
| General book list | HydrateFilesForAvailability | Chaptarr.Api.V1/Books/BookController.cs:1732-1743 | BookFile.Edition; Edition.BookId |
| Works | GetWorks | Chaptarr.Api.V1/Books/BookController.cs:1035-1038 | BookFile.Edition; Edition.BookId; WorkGrouping receives only a set of book IDs, not files |
| Siblings | GetBookSiblings | Chaptarr.Api.V1/Books/BookController.cs:1089-1092 | BookFile.Edition, Path; Edition.BookId |
| Siblings | BuildBookDeleteDetail / BuildDeleteStatistics | Chaptarr.Api.V1/Books/BookController.cs:1110-1138 | BookFile.Path, Size; statistics sum mapped Size and count mapped files |
| Author next/last | LinkNextPreviousBooks / PreloadAuthorIndexBooks | Chaptarr.Api.V1/Author/AuthorController.cs:1381-1390,1488-1537 | BookFile.LazyEdition (loaded-state check), EditionId; joined Edition.BookId; separately loaded Edition.Id for fallback lookup |
| Author list | BookService.GetBooksForDisplay | NzbDrone.Core/Books/Services/BookService.cs:2681-2703 | BookFile.EditionId; separately loaded Edition.Id, BookId; attaches files to both book and metadata edition |
| General / author / next-last resources | Book.HasFiles / ToLocalInstanceResource | NzbDrone.Core/Books/Model/Book.cs:227; Chaptarr.Api.V1/Books/BookResource.cs:182-184,663-682 | File collection presence only; no file scalar |
| Author list filtering | GetBooks monitored filters | Chaptarr.Api.V1/Books/BookController.cs:745-754 | Edition.BookFiles.Any(); no file scalar |
| All book resource variants | ToAvailabilityResource / Calculate | Chaptarr.Api.V1/Books/BookResource.cs:276-291; NzbDrone.Core/Books/Services/BookAvailability.cs:38-82 | BookFile.Path; separately loaded Edition.Language, not joined file.Edition.Language |
| All book resource variants | GetDuration / GetDurationMinutes | Chaptarr.Api.V1/Books/BookResource.cs:607-636 | Edition.BookFiles presence; BookFile.DurationSeconds (positive values summed as long) |
| Edition resources | EditionResourceMapper.ToResource | Chaptarr.Api.V1/Books/EditionResource.cs:120 | Edition.BookFiles.Count only; no file scalar |
| Book / author statistics | MapToResource / AuthorStatisticsRepository / BookFileStatisticsSql | Chaptarr.Api.V1/Books/BookControllerWithSignalR.cs:98-118; NzbDrone.Core/AuthorStats/AuthorStatisticsRepository.cs:24,98-130; NzbDrone.Core/Books/BookFileStatisticsSql.cs:7-11,19-23 | Separate SQL aggregation of BookFile.Size, Id, EditionId and Edition.Id, BookId; does not consume display rows |

`BookFile.Id` is retained as model identity (and exercised by the round-trip
test), even though list resource mappers do not expose individual file IDs.
`MediaType` is removed from the projection: availability classifies Path's
extension, and resource MediaType comes from Book, not BookFile.

## Metadata editions (separate loads, not the joined projection)

Reading all of BookResourceMapper found these edition reads:

* `BookResource.cs:165-259,685-690`: Id, Monitored, Title, Ratings,
  ReleaseDate, PageCount, Disambiguation, Overview, Images, BookFiles;
  identity helpers at 208,213,217-219 also read ForeignEditionId,
  GoodreadsEditionId, HardcoverEditionId, OpenLibraryEditionId,
  GoogleBooksEditionId, Asin, AudibleASIN, Asins (see
  `BookEditionIdentity.cs:30-38,398-425,480-483` and
  `ReadarrFacadeEditionIdentity.cs:39-60`).
* `BookResource.cs:417-432,470-505,566-594`: ProviderUrls, Narrator,
  NarratorNames. `ToLocalInstanceResource:670-681` reuses selection/narrator
  helpers and reads Title and Disambiguation, with no additional file scalars.
* `BookAvailability.cs:68-74`: Language from book.Editions.
* `EditionResource.cs:84-134`: Id, BookId, ForeignEditionId (identity helper),
  TitleSlug, Isbn13, Isbn10, Asin, Asins, Title, Subtitle, Language, Overview,
  Format, IsEbook, Disambiguation, Publisher, PageCount, ReleaseDate, Images,
  Links, Ratings, Monitored, ManualAdd, GoodreadsEditionId, HardcoverEditionId,
  OpenLibraryEditionId, ReadingFormatId, EditionFormat, EditionInfo,
  AudibleASIN, GoogleBooksEditionId, BookFiles (count), Narrator, NarratorNames,
  DurationSeconds, ChapterCount, HasChapters, ReviewCount, Chapters.

These metadata/document consumers stay on their existing edition loads. They
must not be switched to file.Edition. The file join remains exactly Id, BookId.
AuthorResourceMapper itself does not read BookFiles; NextBook/LastBook use
`ToAuthorIndexBookResource` (`AuthorController.cs:1569-1579`), which invokes the
same lean BookResourceMapper. Author statistics use independent SQL aggregates.

Explicit property-name search and the full mapper reads found NO display-row
reads of Quality, Languages/Language (not BookFile properties), DateAdded,
Modified, PartCount, ReleaseGroup, SceneName, CalibreId, MediaType, AllTags,
MediaInfo, MatchProvenance, or any other file document. File-detail/lookup,
duration-update and write paths retain their existing full loads.

## Final projections and verification

* BookFile: Id, EditionId, Path, Size, DurationSeconds.
* Joined Edition: Id, BookId. Null edition guard restored before cache access.
* Author-list controller fixture now models the service attachment of display
  files to metadata editions and asserts Duration = 3723 seconds,
  DurationMinutes = 62.05, HasFiles, availability read/listen flags, formats,
  languages, and absence of individual BookFile resources.
* Repository fixture round-trips every selected scalar and both edition keys
  with non-default stored values. The omitted-column update tests now mutate
  omitted MediaType rather than newly loaded DurationSeconds; their before/after
  comparison still checks every stored column except intentionally changed Path.
* No schema, migration, or production data changes. Default loads unchanged.
* `git diff --check` passes. `dotnet` is absent: no compilation, test execution,
  allocation measurements, or database-copy HTTP probe was possible here.
