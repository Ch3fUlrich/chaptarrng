# Multi-Version Wanted Spec (per-language ebook rows + audiobook rows)

## 1. Summary

Goal: every wanted version of a work — each wanted ebook language plus the
audiobook — can be downloaded, and a bundle status reports available / partial /
missing **over the wanted versions only**. An external request ledger names the
wanted (media, language) pairs per work and stays the source of truth for which
versions are wanted; Chaptarr stores one Book row per `(BaseBookId, media,
language)` variant and derives bundle status from those rows.

Non-goals: bundling multiple physical copies (UnitKeyHash clones); choosing
which versions are wanted inside Chaptarr UI; no automatic back-catalogue
monitoring (§8); changing narration-variant behaviour beyond what
generalisation requires (§7).

## 2. Wanted-version model

| Concept | Where | Anchor |
|---|---|---|
| Row media type `Audiobook=0, Ebook=1` | `Book.MediaType` | src/NzbDrone.Core/Books/Model/Book.cs:14-18, :100 |
| Shared work key across ebook/audiobook rows | `Book.BaseBookId` | Book.cs:66 |
| Per-edition language string | `Edition.Language` | src/NzbDrone.Core/Books/Model/Edition.cs:39 |
| Exactly one monitored edition per row (invariant) | EditionSelector repair + housekeeper | src/NzbDrone.Core/Books/Services/EditionSelector.cs:200-253; src/NzbDrone.Core/Housekeeping/Housekeepers/FixMultipleMonitoredEditions.cs:19-27 |
| Language whitelist (CSV, `unknown`/`null` token) | `MetadataProfile.AllowedLanguages` | src/NzbDrone.Core/Profiles/Metadata/MetadataProfile.cs:25; src/NzbDrone.Core/Books/Services/EditionMetadataProfileFilter.cs:114-150 |

Variant row identity: `BaseBookId` + `MediaType` + monitored `Edition.Language`,
plus ISBN/ASIN on the monitored edition when present (the key ladder used
today: Hardcover > Goodreads > OpenLibrary > ASIN > ISBN13/10 > foreign,
BookService.cs:2378-2426 — src/NzbDrone.Core/Books/Services/BookService.cs).
The "wanted-version variant" stamp is today implicit: `TitleSlug =
"<base>_wanted_<editionId>"` + `AddOptions.AddType == Manual` + no
`UnitKeyHash` (BookService.cs:2491-2521). The design keeps this stamp but makes
`IsSyncVariantRow` recognise it — that helper lives in BookService.cs, so it is
lane 2's change (§5 R4, §6 L2).

"Wanted" per row = the row's `Monitored`/`AudiobookMonitored`/`EbookMonitored`
flags (Book.cs:100 and media-type helpers Book.cs:366-455); which rows exist is
decided by the external ledger, created via the API in §3. Language lives on the
edition (Edition.cs:39) and on the row (`Book.LanguageCode/LanguageName`,
Book.cs:70-71) — lane 2 must stamp the row values at creation and lane 3 must
keep both in sync across refresh, which overwrites the row values from the
remote copy (§5 R2).

## 3. API contract (lane 2)

Endpoint today: `POST /api/v1/book/{id}/editions/wanted`
(src/Chaptarr.Api.V1/Books/BookController.cs:2004-2036) →
`BookService.AddWantedEdition(bookId, editionId)` (BookService.cs:77, :2323).
Request today has only `EditionId` + `SearchForNewBook`
(src/Chaptarr.Api.V1/Books/AddWantedEditionRequest.cs:3-10).

Extended request (lane 2):

| Field | Semantics |
|---|---|
| `EditionId` (kept, optional if Language/Isbn/Asin given) | explicit edition selector |
| `Language` (new) | wanted language; matched against editions' `Language` |
| `Isbn` / `Asin` (new, optional) | edition selectors when no EditionId |
| `MediaType` (new) | `ebook` allowed; drop the audiobook-only throw at BookService.cs:2331-2334 |
| `SearchForNewBook` | keep, default false (BookController.cs:2017-2020) |
| controller guard (lane 2 must relax) | BookController.cs:2007-2010 returns `BadRequest("EditionId is required")` whenever `EditionId <= 0`, so a Language/Isbn/Asin-only request is rejected today; the guard must accept any one of EditionId/Language/Isbn/Asin |

Response: the variant Book row — `id`, `baseBookId`, `mediaType`
(mapped `audiobook`/`ebook`, BookResource.cs:242), monitored edition id,
edition `language`, plus a `created|existing` flag.

Idempotency: match on `(BaseBookId, MediaType, language)` and return the
existing row. Today the clone-per-retry only happens **once the base row has
files**: the no-files branch (BookService.cs:2347-2376) pins the edition on the
same row and returns before the dedupe at :2428-2477, so a file-less base row
absorbs repeats rather than cloning. The dedupe itself scans only **audiobook +
Manual** sibling rows (filter at :2434-2435), so retrying an ebook request
against a filed base row clones a new row each time (§5 B2). R1 removes that
no-files early return for language/variant requests, so the dedupe must also
cover the path it used to exit through.

Caller ordering: add author/book → wait until book + editions are stable
(refresh finished) → `editions/wanted` per wanted version → only then
BookSearch. BookSearch for a variant row stays **held** until lane 4 (language
gate) exists (§5 B4).

Errors: unknown/unparseable language → 422, no row created; no edition of the
requested language/ISBN/ASIN → 422; edition belongs to another book →
`InvalidOperationException`→400 (BookService.cs:2342-2344; mapping
BookController.cs:2028-2035 — lane 2 switches the two "no such version" cases
to 422); media mismatch between request and target editions → 422.
**422 has no precedent**: the grep
`grep -rn "422|UnprocessableEntity" src/Chaptarr.Api.V1` → 0 hits, so nothing
in the API maps any exception to 422 today. Lane 2 therefore introduces a
dedicated exception (e.g.
`WantedVersionNotFoundException`) and adds a `catch` for it in
BookController.cs:2028-2035 next to the existing
`ArgumentException`/`InvalidOperationException`→400 mapping, returning 422. See
§8 (default: 422 via a dedicated exception; lane 2 may fall back to 400 if it
prefers no new mapping).

## 4. bundleStatus state table

Computed over **wanted versions only** = variant rows that exist per §2/§3.
One rule, stated once: **n counts every variant row that exists for the work,
monitored or not** — an unmonitored row is still a wanted version, and counts
as missing when it has no file; **f counts the rows among them that have a
file**. "Unmonitored" is never "absent", so `n = 0` means no rows exist at all
(no ledger entries), which is a different state from "rows exist, none filed".
Lane 5 must compute over rows that exist, not over the monitored-only SQL
predicate (AuthorStatisticsRepository.cs:102, :110-114), which today drops
unmonitored rows from both n and f.
A version "has file" = its row has any BookFile (file statistics join groups
`BookFiles` by `Editions.BookId`, so any edition of the row counts —
src/NzbDrone.Core/Books/BookFileStatisticsSql.cs:5-13;
src/NzbDrone.Core/AuthorStats/AuthorStatisticsRepository.cs:22-23).

| Variant rows that exist (n) | Row monitored | Rows with files (f) | bundleStatus |
|---|---|---|---|
| n > 0 | monitored | f = n | available |
| n > 0 | monitored | 1 ≤ f < n | partial |
| n > 0 | monitored | f = 0 | missing |
| n > 0 | **unmonitored** | no file | still counted in n → helps drive `missing` (default §8) |
| n > 0 | **unmonitored** | has file | counted in both n and f |
| 0 (no rows for the work) | — | — | `none` — no ledger entries; excluded from bundle UI (default §8) |

Where computed: extend the SQL select in
`AuthorStatisticsRepository.BuildStatisticsSelect`
(AuthorStatisticsRepository.cs:92-107) to also emit per-work wanted/filed
counts grouped by `BaseBookId`; expose via `BookStatisticsResource`
(src/Chaptarr.Api.V1/Books/BookStatisticsResource.cs:9-23, `PercentOfBooks` at
:13-23); render in `getProgressBarKind`
(frontend/src/Utilities/Author/getProgressBarKind.js:3-11),
`BookStatusCell` (frontend/src/Book/Index/Table/BookStatusCell.js:17-29),
`BookIndexProgressBar` (frontend/src/Book/Index/ProgressBar/BookIndexProgressBar.js:27).

Double-count risk: every variant row is one more `Books` row, so per-row
statistics add `+1` to `TotalBookCount`/`BookCount` per wanted version and
`PercentOfBooks = Available/BookCount*100` (:22) deflates as versions are
added. Bundle status must group rows by work, not average row percentages;
`HasFiles(book,…)` (BookController.cs:1582-1593) is per-row and safe as-is.
The two denominators also diverge per version: `TotalBookCount` (:102) is only
gated on the monitored predicate, while `BookCount` (:104-106) adds a
release-date gate (`effectiveReleaseDate` null, `<= @currentDate`, or the row
already has files), so a **future-dated** variant row adds to `TotalBookCount`
but not to `BookCount` until its date passes or a file lands.

## 5. Invariants lanes 2-5 must keep

| # | Invariant | Anchor that makes it necessary |
|---|---|---|
| R1 | `AddWantedEdition`'s no-files branch pins the edition on the SAME row and returns. For a language/variant request it must ALWAYS spawn a new row (otherwise the first wanted version absorbs the rest). | BookService.cs:2347-2376 |
| R2 | Refresh must keep N language rows: all map to one remote work+media row; only the filed/manual row becomes `existingChild`, extra rows risk merge-delete; the row language is NOT preserved across refresh — see anchor; re-home keys are `work::media` with no language component. | RefreshAuthorService.cs:891, :1081 (keys); :1689 (media filter), :1719-1736 (existingChild pick + dup merge), :2013-2019 (merged rows skipped for refresh); **overwrite site** Book.cs:271-272 — inside `UseMetadataFrom` (:232) the remote copy replaces the row's language (`other.LanguageCode ?? LanguageCode`, same for `LanguageName`), so a refresh substitutes the remote's language whenever the remote carries one, and `UseDbFieldsFrom` (Book.cs:333-354) copies **no** language field, so nothing re-stamps it afterwards; markers RefreshAuthorService.cs:1533-1557 |
| R3 | Import must never steal across languages: the identity group is media+BaseBookId/provider-tokens without language, and an empty foreign-language row can be adopted and stamped with the unit's `UnitKeyHash`, turning a wanted-variant row into a clone. | src/NzbDrone.Core/MediaFiles/BookImport/Services/BookUnitDestinationService.cs:284-311 (group), :250-282 (empty-target adoption), :272-276 (stamp); src/NzbDrone.Core/MediaFiles/BookImport/FileMatchingService.cs — parent dir of `Services/`, **not** the same dir as BookUnitDestinationService.cs |
| R4 | Sync fan-out must ignore variants: `IsSyncVariantRow` keys off `UnitKeyHash`/`_copy_` only, so `_wanted_` rows are sync participants; `DisableFormat`/`EnsureOneMonitoredOnFormat` can flip a language row, and `DeleteBook(applyToBothFormats)` deletes provider-matched siblings. Lane 2 must add the wanted stamp to `IsSyncVariantRow` (BookService.cs:1252-1266) — it lives in BookService.cs, which L2 owns, so it goes with L2's API work; L3 stays on import/refresh only and only consumes the resulting behaviour. | BookService.cs:1259-1266, :1369-1384, :1386-1395, call sites :1453-1465; DeleteBook :381-413 |
| R5 | Release language gate must be per variant row: `ReleaseLanguageSpecification` is author+media PROFILE scoped, unknown-language releases pass, and nothing compares release language to the row's edition language; release search sends only monitored-edition title + year, criteria carry no language. | ReleaseLanguageSpecification.cs:57-82, :111-119 (profile scope), :76-80 (unknown passes); ReleaseSearchService.cs:131-141; BookSearchCriteria.cs:9-11 (src/NzbDrone.Core/IndexerSearch/Definitions/) |
| B1 | Caller idempotency: match an existing variant by `(BaseBookId, MediaType, edition language/ISBN/ASIN)` from `GET /book?authorId=` (BookController.cs:584-591) before calling wanted. | BookController.cs:584-591 |
| B2 | No per-retry clones for ebook: the dedupe covers only audiobook Manual rows, and is reached only when the base row already has files (§3); lane 2 must extend it to `(BaseBookId, MediaType, language)` and cover the path R1 frees. | BookService.cs:2347-2376, :2428-2477 |
| B3 | Ordering: ledger add → stable editions → editions/wanted → BookSearch. | BookController.cs:2017-2020 |
| B4 | Hold BookSearch for variant rows until R5's language gate exists. | ReleaseLanguageSpecification.cs:76-80 |
| B5 | Bulk monitoring and repair must not break N rows: `PUT /book/monitor` flips whole rows; "exactly one monitored edition per row" repair and housekeeper must run per row, and row `PUT` must keep its `SingleOrDefault` monitored-edition assumption valid per row (it does — one row, one monitored edition). | BookController.cs:1995-2002, :1862, :1872, :1880; EditionSelector.cs:200-253; RefreshBookService.cs:1108-1122 |

Today's variant-row behaviour (all anchors in BookService.cs): throws unless
`MediaType==Audiobook` (:2331-2334); clones only `ReadingFormatId==2` editions
and errors if none (:2527-2531, :2589-2592); new row `MediaType` hardcoded
`Audiobook` (:2505); no `UnitKeyHash` on the row (:2491-2521); selected edition
cloned with `Language` verbatim (:2547) and `Monitored`/`ManualAdd` set
(:2583-2584).

## 6. Per-lane done-means and tests

| Lane | Owned files | Done means | Named tests (fail-first → green) | Invariants |
|---|---|---|---|---|
| L2 variant API | BookService.cs, BookController.cs, AddWantedEditionRequest.cs, Chaptarr.Core.Test/Books | language/ebook request spawns or returns one variant row; relaxes the `EditionId <= 0` guard (BookController.cs:2007-2010) so Language/Isbn/Asin-only requests are accepted; 422s per §3; idempotent per `(BaseBookId,MediaType,language)` including the path freed by R1; stamps `LanguageCode`/`LanguageName` on the new row at creation (BookService.cs:2491-2521 builds the row without them, so today it stays un-stamped until a first refresh); owns `IsSyncVariantRow` (BookService.cs:1252-1266) wanted-stamp recognition | `dotnet test --filter FullyQualifiedName~BookServiceWantedEditionLanguageFixture` (`…_CreatesEbookVariantRow`, `…_ReturnsExistingOnRetry`, `…_UnknownLanguage422`, `…_NoEditionOfLanguage422`); ~BookServiceSyncVariantFixture (`…_DisableFormatSkipsWantedStamp`); existing ~BookServiceWantedNarratorSeriesLinksFixture stays green | R1, R4, B1, B2, B3 |
| L3 import guard + refresh | src/NzbDrone.Core/MediaFiles/BookImport/Services/BookUnitDestinationService.cs, src/NzbDrone.Core/MediaFiles/BookImport/FileMatchingService.cs (parent dir of `Services/`), RefreshAuthorService.cs | N language rows survive a full refresh; import of language X never adopts or UnitKeyHash-stamps language Y's empty variant; no `BookService.cs` edits — the wanted stamp in `IsSyncVariantRow` is L2's | ~RefreshAuthorServiceLanguageRowsFixture (`…_KeepsNLanguageRows`, `…_ExistingChildIsFiledRow`); ~BookUnitDestinationServiceLanguageGuardFixture (`…_NoCrossLanguageAdoption`) | R2, R3 |
| L4 search language gate | ReleaseSearchService.cs, ReleaseLanguageSpecification.cs, BookSearchCriteria | gate compares release language to the row's monitored edition language; unknown-language release rejects when row language is explicit; BookSearch held for variants until gate green | ~ReleaseLanguageSpecificationVariantFixture (`…_RejectsLanguageMismatch`, `…_UnknownReleaseRejectedForExplicitRowLanguage`); ~ReleaseSearchServiceLanguageCriteriaFixture (`…_CriteriaCarriesLanguage`) | R5, B4 |
| L5 bundle status | AuthorStatisticsRepository.cs, BookStatisticsResource.cs, BookRepository.cs (missing/wanted :407-418, :731-819), frontend BookStatusCell / BookIndexProgressBar / getProgressBarKind / Missing | per-work wanted/filed counts; available/partial/missing per §4; percent no longer deflated by variant rows | ~AuthorStatisticsRepositoryBundleFixture (`…_CountsWantedPerWork`); ~BookStatisticsResourceFixture extended; frontend: **no test runner exists** (no jest config, no `test` script in package.json, no `*.test.js` under frontend/) — L5 adds none; verify by lint + build (`yarn lint`, `yarn build` as defined in package.json `scripts`) and by the backend statistics fixtures | B5, §4 |

## 7. Upstream fit

Upstream Chaptarr PR #58 "Track multiple narrations of the same audiobook"
(open) adds an `AsNewVariant` flag to `AddWantedEdition`. The flag is **not
present in this checkout** (`grep -rn AsNewVariant src frontend` → 0 hits).
Our change should be a superset: reuse the name and semantics —
`AsNewVariant=true` ⇒ always spawn a row (R1) — and treat
`language`/ebook support as extra parameters alongside it, so the fork stays
mergeable.

Conflict points: AddWantedEdition signature (BookService.cs:77, :2323) and
body (:2347-2592: no-files branch, dedupe, clone block all shared);
`AddWantedEditionRequest` fields; the `EditionId <= 0` →
`BadRequest("EditionId is required")` guard (BookController.cs:2007-2010),
which lane 2 must relax for Language/Isbn/Asin-only requests; controller error
mapping (BookController.cs:2028-2035 — where any new 422 exception from §3 must
be caught). Keep our edits inside those same regions.

## 8. Open questions

| Question | Default | Why |
|---|---|---|
| Variant row title/slug scheme | `<base>_wanted_<media>_<lang>_<editionId>` | deterministic idempotency key; today `_wanted_<editionId>` (BookService.cs:2494) |
| Does bundleStatus count unmonitored wanted rows? | yes — a variant row that exists counts in n whether monitored or not, and is missing when it has no file (§4) | monitored-only is a SQL artifact (AuthorStatisticsRepository.cs:102, :110-114), not the wanted-set definition; lane 5 computes over rows that exist |
| Language code format | ISO 639-1 canonicalized, matched via `CanonicalizeLanguage` used by profile CSV | aligns row language with AllowedLanguages filter (EditionMetadataProfileFilter.cs:114-150) |
| Which exception type maps to 422? | default: 422 via a new dedicated exception caught in BookController.cs:2028-2035; lane 2 may instead return 400 if it prefers no new mapping | nothing in src/Chaptarr.Api.V1 emits 422 today (`grep -rn "422\|UnprocessableEntity"` → 0 hits), so there is no precedent to follow (§3) |
| No edition of requested language exists | 422, no row created | ledger must fix source metadata, not half-create |
| Zero wanted versions | status `none`, excluded from bundle rollup — means **no rows exist**, distinct from "rows exist, none filed" (§4) | absence ≠ missing |
| Grab budget multiplies with versions | keep caller-side cap | rows multiply search commands; budget belongs to the caller loop |
| Back-catalogue monitoring | no automatic back-catalogue monitoring; only explicitly requested versions exist as rows | ledger is the sole source of "wanted" (§1) |

## Verification notes

All `path:line` citations above were re-anchored against this checkout
(origin/develop e67374f4) at write time. Also verified in this checkout: the
422 grep in §3 (0 hits), the frontend test-runner absence in §6 L5 (no jest
config, no `test` script, no `*.test.js`), and that FileMatchingService.cs sits
in `src/NzbDrone.Core/MediaFiles/BookImport/` while BookUnitDestinationService.cs
sits in its `Services/` child. Unverified: the upstream PR #58 status
and its `AsNewVariant` design — [unverified: not fetched]; only the
absence of `AsNewVariant` in this checkout was grep-verified.
