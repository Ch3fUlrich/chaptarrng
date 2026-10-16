# ChaptarrNG fork changes

ChaptarrNG is Snapetech's maintained fork of
[Chaptarr](https://github.com/Chaptarr/chaptarr), which is itself derived from
Readarr. The fork was created on August 31, 2026 from the parent repository's
`develop` branch at the
[shared fork-base commit](https://github.com/Chaptarr/chaptarr/commit/423b1bba1d47657d24ddfbf9c5418892972a1229).
This guide covers the published feature set through
[`v0.9.973`](https://github.com/snapetech-org/chaptarrng/releases/tag/v0.9.973)
and the additional feature and maintenance work present in the current fork
checkout. The source comparison below was refreshed on 2026-10-16; release and
migration evidence is dated separately. The [changelog](../CHANGELOG.md) is
the record of released changes. The parent comparison is Chaptarr's `develop`
branch at
[`e67374f`](https://github.com/Chaptarr/chaptarr/commit/e67374f), which is in
ChaptarrNG `main`'s history.

**Migration update (2026-10-16).** The standard ChaptarrNG image includes the
BookshelfNG converter and rollback commands from
[`v0.9.970`](https://github.com/snapetech-org/chaptarrng/releases/tag/v0.9.970).
The public `v0.9.971` image passed preview, explicit apply, rollback after a
post-migration write, ordinary startup, and the operator media-map review loop
against populated synthetic migration-158 SQLite fixtures. `v0.9.972` adds
strict rejection of incomplete or stale media maps; `v0.9.973` bounds nested
brace-parser input in the frontend dependency tree. The final BookshelfNG
`0.4.21.70` release includes a source-side appdata archive helper; its
[operator guide](https://github.com/snapetech-org/bookshelfng/blob/main/docs/migrate-from-bookshelfng-to-chaptarrng.md)
documents use with the standard image. Conversion still supports only
migration-158 SQLite; older schemas and PostgreSQL remain unsupported, and
successful database conversion does not complete feature parity or legacy
channel handoff.

This guide groups behavior rather than listing every fix, dependency update,
or upstream report. Some items need reproduction or depend on an external
service, so this overview does not claim that every reported problem is
resolved.

> **Upstream retirement status (2026-10-16): paused and incomplete.** The
> matrix is a cumulative capability and acceptance inventory; it does not
> mean upstream issue/PR implementation is active or that the backlog is
> resolved.

At the 2026-10-16 source review, ChaptarrNG `main` was `76aad77847ce`
(`v0.9.971`) and upstream Chaptarr `develop` was
`e67374f4b3fed01bbf08f18672add944062fcb49`. ChaptarrNG was 629 commits ahead,
zero behind, with 1,652 changed paths. These counts describe source history,
not feature parity, and must be regenerated for any later retirement review.
Upstream PR [#114](https://github.com/Chaptarr/chaptarr/pull/114), which
preserves media type during scoped book lookup, remained open in that review.
The broader upstream issue/PR audit remains incomplete.

The BookshelfNG consolidation is now in ChaptarrNG `main`; its standard image
uses the same executable for fresh and migrated installs. At the 2026-10-16
review, BookshelfNG `main` was `029b0f9b`, with product PRs #128 and #130 still
open for its audit and guarded release bridge. The converter, metadata-profile
preferences, and validation changes are historical merged work, not evidence
that BookshelfNG feature parity, package handoff, or upstream Chaptarr
retirement is complete.

The cumulative blocker set now includes the features released through
`v0.9.973`: BookshelfNG migration and rollback, author-root backfill, managed
book-file downloads, recurring missing-book searches, Audible marketplace
selection, BookLore BookDrop uploads, metadata edition/series preferences,
import-list preview and history, new localization catalogs, paged full-library
API reads, PostgreSQL TLS, split-release part preservation, and the associated
matching, performance, and safety improvements. YunoHost publishing was
retired from source; old published assets and the external package repository
were left untouched. Unreviewed backlog requests are not accepted product
scope until maintainers adopt them.

The application keeps reporting its name as `Chaptarr` through its
Readarr-compatible API so existing clients can identify it. **ChaptarrNG**
names the maintained repository and its fork-owned releases and packages.
ChaptarrNG remains a standalone book manager and does not require SeerrNG.

## Why maintain ChaptarrNG

Chaptarr already provides the standalone combined ebook/audiobook manager,
narrator-aware metadata, and its own pending-author-import API. ChaptarrNG does
not claim those shared application features as NG-only. Its SeerrNG-specific
work is the capability-discovered contract: selected format and provider
identity, format-scoped API aliases, paged library reads, and a restricted
service key. Chaptarr's current `develop` source does not expose that SeerrNG
contract or key scope.

ChaptarrNG also maintains standalone additions that operators may use without
SeerrNG: the built-in Direct Download indexer/client, authenticated encryption
for full backup archives, fork-owned release images, and the fork's Unraid
package. Chaptarr has its own encrypted quick-start settings backup; the
full-backup capability is the distinction recorded here.
The sections below also cover the maintained import, metadata, external-library,
and developer-validation additions. They are separate from the SeerrNG contract
and can be used without SeerrNG.

This work also adds an optional MQTT Gateway for library
snapshots, lifecycle events, and Home Assistant discovery, plus opt-in
canonical lifecycle requests for existing Webhook connections. See the
[MQTT Gateway guide](./MQTT_GATEWAY.md) for topic and payload details.

Operators can also enable a separate, low-cardinality Prometheus metrics
listener for queue and health state, scheduled tasks, import-list syncs, and
download/import outcomes. See the [Prometheus metrics guide](./PROMETHEUS_METRICS.md)
for configuration, metric names, and network exposure guidance.

## Format-aware books and SeerrNG requests

Chaptarr supports ebook and audiobook formats in one standalone instance.
ChaptarrNG's integration contract carries the selected format through SeerrNG
lookups, searches, adds, monitoring, and paged library scans. Provider IDs are
retained for integrations; local database row IDs are not durable external
identities.

When an author is not ready in the metadata catalog, ChaptarrNG can accept a
book add as a pending import instead of losing the request. It retries author
preparation, keeps the selected format and requested book attached to that
work, then searches for the requested book when the catalog is ready.
Chaptarr's current development branch has a pending-import subsystem too.
ChaptarrNG's contribution is exposing its status and retry/cancel behavior to
SeerrNG through the capability contract. Retry and cancellation checks protect
imports shared by more than one active request.

SeerrNG discovers the supported API dialect, format routes, paged library
behavior, provider identity rules, and pending-import operations through
`GET /api/v1/system/capabilities`. ChaptarrNG keeps its compatible `Chaptarr`
app identity while exposing this additive contract.

The full-library book API also accepts optional `offset` and `limit` values
and returns the matching total in `X-Total-Count`, so clients can page through
large libraries without loading the entire response.

Since ChaptarrNG `0.9.941`, operators can configure a dedicated
`CHAPTARR__AUTH__SEERRAPIKEY` credential for SeerrNG. It supports the
integration's book, library, search, and pending-import operations without
granting global settings, deletes, file removal, or unrelated commands. Keep
the regular API key for administration and older clients.

Setup and behavior:

- [SeerrNG integration guide](./SEERRNG_INTEGRATION.md)
- [API identity and lifecycle contract](./API_IDENTITY_AND_LIFECYCLE.md)
- [SeerrNG Bookshelf backend guide](https://github.com/snapetech-org/seerrng/blob/main/docs/using-seerr/bookshelf-backend.md)

## Library identity and file organization

The API distinguishes durable provider identities from local database IDs and
can return the provider IDs associated with a book. When a mutation cannot
choose safely between multiple matching rows, Chaptarr reports the ambiguity
instead of silently changing a different book. The integration contract also
preserves the requested ebook or audiobook format during lookups and searches.

File-organization previews identify the exact file rows selected for a move or
retag. Author-folder moves use Chaptarr's stored author paths, and moving files
to a canonical author folder is opt-in. The [API identity and lifecycle
contract](./API_IDENTITY_AND_LIFECYCLE.md) documents identity, match evidence,
pending imports, and organize-preview fields.

Operators can download a book file from its file actions. The API restricts
the response to files under configured root folders. The API's
`POST /api/v1/author/editor/backfill-roots` endpoint can fill a missing
audiobook or ebook root from the matching default root. Add `?dryRun=true` to
preview counts; without it the endpoint queues the update. It skips hand-set
values, formats without a quality profile, and missing default roots, and
does not move files.

## Import-list preview and sync history

The **Preview Next Sync** action fetches one configured list and classifies its
entries against the current library without adding authors or books, queuing
searches, committing the provider cursor, or updating the list's normal sync
status. The preview uses read-only provider-cache and status scopes, and the
preview itself is saved with `dryRun=true` and `providerStateCommitted=false`.

Preview counts distinguish processed, new, existing, changed, excluded,
unresolved, and search-eligible entries. A snapshot can differ from a later
sync if the list or library changes in between. Per-list history also records
scheduled and manual runs, duration, outcome, failure stage, the same counts,
and whether the provider's state was committed. ChaptarrNG keeps at most 100
records per list and 5,000 overall.

Hardcover provider state is committed only after fetched list entries have
processed successfully, and the cursor is committed on the list instance that
was actually fetched. Goodreads bookshelf refresh defaults to 12 hours and is
configurable down to a 15-minute minimum.

See the [import-list preview and history guide](./IMPORT_LIST_SYNC.md) for the
UI action, API routes, retention, and refresh behavior.

## Direct ebook downloads

The optional Direct Download indexer searches configured ebook sources by ISBN
when available and falls back to the monitored edition title. URL order is
preserved and used for fallback between sources. Searches are ebook-only; the
built-in Direct Download client stages and tracks supported files and sends
completed downloads through Chaptarr's normal import flow.

For supported catalog results, the client first attempts a fast download
through the source API when an API key is configured. If no file URL is
available, operators can enable the slow-download browser fallback. That
fallback uses a headless browser during the background transfer, so a
browser-assisted grab may take longer. The API key is checked during the
indexer's Test action without requesting a real file.

The official Docker image includes the Playwright Chromium runtime needed for
browser fallback on AMD64 and ARM64. ARMv7 images omit Chromium, so browser
fallback is unavailable there; API-key-resolved downloads remain available. A
custom image must include a compatible Playwright browser runtime. The
fallback is optional; a source with a working API-key download does not need
it.

Direct Download requires a persistent staging folder. Its download state and
files survive application restarts, and operators can inspect progress, retry
interrupted transfers, and choose whether staged data is removed after import.
The client is built into ChaptarrNG; a separate qBittorrent, Transmission, or
similar service is not required for this path.

Configured links must use HTTP or HTTPS and must not embed credentials.
ChaptarrNG validates redirects and blocks loopback, private, link-local, and
cloud metadata destinations. Requests have bounded time and response sizes,
and TLS verification remains enabled.

Indexer settings can also enable a recurring search for all missing monitored
books. It is off by default and accepts intervals of at least 60 minutes.
System health names a MyAnonaMouse indexer when its unsatisfied-slot protection
has no fresh account status, explaining why grabs are blocked.

See [Direct Download source setup](../README.md#direct-download-sources) for the settings and staging-folder requirements.

## Imports, matching, and conversion

Import and conversion changes focus on preventing a bad match or occupied
destination from damaging an existing library. Automatic and manual imports
check the final destination before writing. Manual Import shows an existing
destination conflict in its preview; forced imports can bypass quality or
duplicate rules, but still cannot overwrite an untracked file or a file
belonging to another edition. Multi-file imports are handled as a batch so a
new part cannot accidentally replace another part from the same download.

When an archive contains no recognized media file at its top level,
ChaptarrNG can extract supported `.zip`, `.tgz`, and `.tar.gz` downloads and
continue through the normal import path. The importer still validates the
resulting files and destination. Manual-import matches can persist provenance
such as the match route, mode, and evidence values; this explains the historic
decision and is not reused as a future search or scoring signal.

Eligible multi-part audiobook downloads can be merged into a single M4B when
the quality profile enables merging. ChaptarrNG checks the merged destination
before conversion and preserves chapter offsets. An optional merge failure can
fall back to importing the parts separately. Conversion also works on books
already in the library, stays in the existing book folder, reports progress,
and can be cancelled. Cancellation stops the next item in a bulk operation
from starting.

Separate audiobook releases such as `(1 of 2)` and `(2 of 2)` now import side
by side instead of replacing one another. The `{ReleasePart}` and
`{ReleaseParts}` naming tokens can include the part information in filenames;
this protection applies to new imports and does not re-identify files already
in the library. Retagging an M4B also preserves its existing chapter markers.

Matching and quality handling also preserve distinct audiobook narrations and
dramatized editions when their casts overlap, use strong same-work identity
when retargeting duplicates, and keep ebook/audiobook monitoring and
availability scoped to the selected format. These rules avoid merging
different audio products or expanding a single-book request into an author's
whole catalogue.

## Metadata and profile additions

RanobeDB adds light-novel ebook search and metadata lookup using its read-only
API. ChaptarrNG stores RanobeDB work and release IDs, limits request frequency,
uses digital releases, and does not ingest separately licensed descriptions
or cover images. The [README metadata section](../README.md#metadata) links to
the API and licensing terms.

Metadata profiles can exclude configured genres, for example comics or
graphic novels, rank preferred edition terms, and select a preferred series
for filenames, ebook tags, Calibre, and AudioBookShelf metadata. The profile
editor offers genre autocomplete from the local library and identifier filters
for editions missing ISBN and/or ASIN values. When a language-profile change
allows more languages, Chaptarr queues a forced metadata refresh so newly
permitted editions can be discovered without a library rescan. Audible search
and ASIN lookup can use the US, UK, French, German, Italian, Spanish, Canadian,
Australian, Indian, Japanese, or Brazilian marketplace. Goodreads bookshelf
refresh intervals are operator-configurable with a safe minimum, as described
in the import-list guide.

## External library connections

The current checkout adds or extends connections for AudioBookShelf, Grimmory,
Calibre Content Server, BookLore BookDrop, and Emby/Jellyfin. Their scope
differs: AudioBookShelf can receive covers and compatible forwarded metadata;
Grimmory can refresh, publish, and forward sidecar edits; Calibre-backed roots
route ebook imports through Calibre while the separate Content Server
connection can mirror ChaptarrNG changes; BookLore can receive imported books
through a streamed BookDrop upload; and Emby/Jellyfin can refresh after a
grabbed release is imported.

Ignore-tag settings are evaluated against tags on the external item and are
case-insensitive. Automatic pushes skip ignored items, while the documented
manual push action can override that choice. Sync-loop suppression and field
ownership checks prevent routine pushes from overwriting locked fields or
being forwarded back to their source.

See the [external library integration guide](./EXTERNAL_LIBRARY_INTEGRATIONS.md)
for connection setup, data direction, and per-provider limits.

## Operations, observability, and validation

MQTT can publish a versioned library snapshot, availability state, and
canonical lifecycle events. Home Assistant discovery is optional; existing
Webhook connections can separately opt into the same canonical event envelope
without changing their legacy notification payloads. See the
[MQTT guide](./MQTT_GATEWAY.md) for topic names, payload fields, delivery
behavior, and discovery cleanup.

Prometheus metrics use a separate, disabled-by-default HTTP listener. The
exported gauges, counters, and histogram cover health, queue state, scheduled
tasks, import-list syncs, and download/import outcomes. Labels use fixed
application enums and exclude user-controlled values. See the
[metrics guide](./PROMETHEUS_METRICS.md) before exposing the unauthenticated
listener on a network.

Large library screens use server-paged book results and load bookshelf books
as rows become visible. Backend work batches queries and avoids repeated
whole-author refreshes. Author deletions of more than 200 books are queued in
the background; smaller deletions remain synchronous. Conversion and other
long-running commands expose progress and cancellation.

PostgreSQL connections can require TLS with a configurable SSL mode and PEM
root certificate. The connection pool no longer resets the session after each
query. UI routes load their code on demand, while React Query manages server
state; both changes reduce startup work for large installations.

The [validation engine guide](./development/validation-engine.md) documents
source-bound local and CI test runs, platform receipts, and live task progress.
It complements the normal release changelog and does not imply that every
backlog report has passed final validation.

## Backups and service security

Quickstart Settings Backups support optional passphrase encryption. Full
backup archives can use authenticated encryption when
`CHAPTARR_BACKUP_ENCRYPTION_KEY_FILE` points to a mounted secret containing a
strong passphrase.

Existing ZIP backups remain restorable, but enabling full-backup encryption
does not convert them. Keep the decryption secret outside the backup and retain
it for as long as encrypted archives must remain recoverable.

The fork also maintains API-key handling and response redaction, login
throttling, request security headers, image-proxy target validation, and
verification of update binaries. The SeerrNG service key is separately scoped
to its supported routes. See the [README security guidance](../README.md#security)
for configuration details.

## Releases and distribution

ChaptarrNG publishes versioned stable releases from its maintained `main`
branch and builds fork-owned container images for GHCR. Release notes and the
changelog summarize user-facing changes and required operator actions.

The standard image is published at
`ghcr.io/snapetech-org/chaptarrng`; the previous personal-account image path is
not a release destination. BookshelfNG compatibility aliases remain behind
explicit migration gates. YunoHost package and release publishing have been
retired from source, while already-published assets and the external package
repository remain untouched.

The fork also maintains an [Unraid Community Applications
package](https://github.com/snapetech-org/chaptarrng-unraid). Check its instructions
for supported architectures, paths, and upgrade steps.

## ChaptarrNG retirement gates

Retiring BookshelfNG into ChaptarrNG and retiring ChaptarrNG into upstream
Chaptarr are separate decisions. The first gate is the complete BookshelfNG
feature, data, and distribution inventory in the
[BookshelfNG parity roadmap](https://github.com/snapetech-org/bookshelfng/blob/main/docs/maintainers/bookshelfng-parity-roadmap.md).
BookshelfNG stays active until every capability it added or materially
extended is present in ChaptarrNG or has a tested, documented replacement.

The later ChaptarrNG-to-Chaptarr gate is cumulative. Upstream Chaptarr must
preserve the union of **all BookshelfNG capabilities carried into ChaptarrNG**
and **all features ChaptarrNG introduced or maintains**. The whole union
blocks ChaptarrNG retirement; every capability, source-change group, and
installation channel is also an individual blocker. Similar names or partial
implementations do not close a blocker. Features count before release when
maintainers accept them into scope. Upstream implementation is paused, so this
inventory is not an active work queue.

Porting a BookshelfNG capability closes a BookshelfNG-to-ChaptarrNG parity row
only after its own acceptance checks pass; it simultaneously adds that
capability to ChaptarrNG's upstream-retirement blocker set. It does not replace
or reduce ChaptarrNG's own released and maintained blockers.

| Blocker set | Scope to preserve | Evidence required to close |
| --- | --- | --- |
| BookshelfNG capabilities carried into ChaptarrNG | Every capability and material enhancement in the BookshelfNG parity roadmap, including its settings, persisted data, identities, clients, and integrations | Demonstrate equivalent behavior on the supported upstream release, including converted data and existing client configurations; pass each row's acceptance checks. |
| ChaptarrNG released and maintained features | Every capability ChaptarrNG introduced, materially extended, or maintains through `v0.9.973`, including the SeerrNG contract, Direct Download, full-backup encryption, migration tools, new operator workflows, security behavior, and fork-specific integrations | Show the behavior in a supported upstream release and validate the relevant APIs, security boundaries, operator workflows, and clients. |
| Current upstream blocker and source inventory | At the 2026-10-16 review, ChaptarrNG `main` was `76aad77847ce`, 629 commits ahead of and zero behind Chaptarr `develop` at `e67374f4`; it had 1,652 changed paths. Chaptarr PR #114 remained open. BookshelfNG `main` was `029b0f9b`; its audit and guarded bridge PRs #128 and #130 remained open. | Preserve or replace every accepted capability and source change in a supported upstream release and pass its acceptance checks. The broader upstream issue/PR audit remains paused and incomplete; refresh this inventory before any retirement decision. |
| Remaining fork-only source changes | Every post-fork source change not already accounted for by the capability rows, including fixes, migrations, APIs, security, build, and platform behavior | Disposition every changed path as retained, ported, or replaced by a verified equivalent. A path count is not feature parity. |
| Release and installation channels | Each ChaptarrNG release, image, package, updater, and supported upgrade path, plus any channel carrying a migrated BookshelfNG installation | Test the exact published upgrade, configuration/data migration, rollback, and client/update continuity for each channel. |

No gate closes because a feature is merely present in source or because its
name appears in upstream documentation. The acceptance evidence must target
the exact supported upstream release and cover behavior, data, configuration,
security, and affected clients. This inventory records accepted capabilities;
the broader upstream issue/PR implementation remains paused and incomplete.

### Itemized ChaptarrNG capability blockers

The rows below are individual blockers inside the cumulative gate above.
Every row is **open** until a supported upstream release preserves the behavior
or an equivalent is tested against the same cases. The broader implementation
initiative is paused and incomplete. The complete source-change inventory
remains an additional blocker, so this feature list does not waive review of
fixes, migrations, build changes, or deletions.

| Capability or change | Current ChaptarrNG scope | Evidence required before upstream retirement |
| --- | --- | --- |
| BookshelfNG conversion and rollback | Standard image preview, apply, automatic startup conversion, retained source backup, rollback, and operator-selected media mapping for migration-158 SQLite | Preserve source rows, identities, settings, backup and rollback guarantees; test supported schemas, real populated rollback, and each installation channel. |
| RanobeDB light-novel search and metadata | Read-only catalog search, stored work/release IDs, request pacing, and source-content limits | Preserve catalog behavior, identity, rate limits, licensing boundaries, and failure handling. |
| Audible marketplace selection | US, UK, France, Germany, Italy, Spain, Canada, Australia, India, Japan, and Brazil storefronts for search and ASIN lookup | Preserve marketplace configuration, defaults, language headers, and result identity. |
| Metadata profile filters and edition ranking | Genre exclusions/autocomplete; ISBN/ASIN missing-value filters; weighted edition terms; preferred series; language-change refresh; and filename, tag, Calibre, and AudioBookShelf consumers | Preserve profile migration/serialization, ranking order, missing-evidence behavior, refresh semantics, series consumers, and manual-review boundaries. |
| Catalog and edition search recovery | Retry alternate edition-title searches and retain the strongest credible match | Test title, identifier, ambiguity, and no-false-match cases on the supported release. |
| Configurable Goodreads refresh | Operator-configured bookshelf refresh interval with a safe minimum | Preserve the setting, default, minimum, and retry/rate-limit behavior. |
| Missing-book search and indexer health | Optional scheduled search for missing monitored books; MyAnonaMouse health names stale account status when slot protection blocks grabs | Preserve the off-by-default behavior, interval minimum, indexer identity, and health warnings. |
| Author root backfill and book-file download | Dry-run/real backfill of missing author roots from default roots; skips hand-set values and formats without a quality profile; direct file downloads restricted to configured roots | Preserve no-file-move behavior, hand-set-root/profile protection, dry-run counts, and root containment. |
| Manual import provenance | Persist the route, mode, and evidence behind an explicit manual match | Keep provenance auditable and ensure it is not reused as an automatic scoring signal. |
| Safe existing-file adoption and import destinations | Adopt compatible existing files; avoid replacing untracked destinations; skip unnecessary hashing when the safety setting permits | Preserve no-overwrite, ownership, cancellation, and destination-race guarantees. |
| Archive extraction and audiobook conversion | Extract supported archives; merge eligible multipart audio, retain chapter offsets, report progress, and support cancellation/fallback | Validate formats, metadata, ordering, chapter preservation, source retention, disk failures, and recovery. |
| Split audiobook release parts | Import numbered parts side by side; expose `{ReleasePart}` and `{ReleaseParts}` naming tokens; preserve chapters when retagging M4B files | Verify new imports do not replace sibling parts and older records remain unchanged; check token values and chapter preservation. |
| Existing torrent adoption and seeding | Reuse tracked download data without breaking the seed | Prove correct category, wrong category, completed-download, and seeding-state behavior. |
| Import-list preview and durable sync history | Dry-run preview, per-list outcome history, and provider cursor commits only after successful processing | Preserve read-only preview semantics, counters, cursor safety, retention, and restart behavior. |
| External library connections | AudioBookShelf, Grimmory, Calibre roots/Content Server, BookLore BookDrop uploads, and Emby/Jellyfin refresh/synchronization | Preserve each provider's direction, field ownership, loop suppression, ignored-item rules, identifiers, streaming behavior, and failure recovery. |
| MQTT Gateway and canonical lifecycle events | Versioned library snapshots, lifecycle requests, and optional Home Assistant discovery | Preserve topic/payload contracts, opt-in behavior, retained-state cleanup, and existing webhook compatibility. |
| Prometheus metrics | Separate optional listener with low-cardinality queue, health, task, and import metrics | Preserve disabled-by-default exposure, metric names/types, and bounded labels. |
| Large-library and background-operation behavior | Paged/virtualized views and full-library API paging with totals, bounded query work, batched reconciliation, fair download processing, per-folder scan progress, and cancellable background commands | Run scale fixtures and verify page completeness, query bounds, concurrency, progress, cancellation, and consistent state. |
| Frontend performance and usability | React Query, route code splitting, mobile search/toolbar adjustments, and upstream bundle optimizations | Preserve loading/error behavior, keyboard/mobile access, route boundaries, and browser tests. |
| Localization and locale-aware UI | Twelve additional language catalogs, including Indonesian and Traditional Chinese; AudioBookShelf root-mapping and audiobook-mode translations; locale direction metadata; English fallback | Preserve all supported strings, fallback behavior, and locale selection; complete native-speaker review and rendered-UI checks before treating translation quality as closed. |
| PostgreSQL and data compatibility | Configurable PostgreSQL TLS/SSL mode and root certificate; pooled-session behavior; API/database migration compatibility | Preserve certificate validation, failure behavior, pool correctness, and supported upgrade/rollback paths. |
| Release and distribution behavior | GHCR image at `ghcr.io/snapetech-org/chaptarrng`, Unraid packaging, gated BookshelfNG compatibility aliases, and retired YunoHost publishing | Disposition every supported channel change; test package/image/service/data identity and upgrade or rollback. Previously published YunoHost assets and the external package repository remain untouched. |
| Security, migrations, and compatibility fixes | API, import, authentication, database, dependency, container, and platform changes in the source history | Review every security boundary and migration; run the required platform and rollback checks. |
| CI and validation changes | Hosted-runner isolation, validation contracts, and bounded validation tasks | Retain source-bound validation, clean hosted evidence, and reproducible maintainer workflows. |
| Remaining post-fork changes | All other ChaptarrNG source-history changes not already represented above | Disposition every changed path as retained, ported, or replaced by verified behavior; a path count alone is not parity. |

The metadata-preference changes require their profile migration, serialization,
ranking order, missing-evidence behavior, and filename, tag, Calibre, and
AudioBookShelf consumers to remain equivalent upstream. Their implementation
does not close the retirement blocker until those behaviors and the remaining
data-migration requirements pass against a supported upstream release.

## Release history

The [changelog](../CHANGELOG.md) records the maintained release line through
`v0.9.973`, including format-scoped requests, fork identity and Unraid
distribution, capability discovery, the restricted SeerrNG service key,
encrypted full backups, and the Direct Download indexer/client with
browser-assisted fallback. The `v0.9.969` container includes headless Chromium
on AMD64 and ARM64; ARMv7 retains API-key downloads without browser fallback.

`v0.9.970` added BookshelfNG migration and rollback, root-folder backfill,
managed book-file downloads, missing-book search scheduling, Audible
marketplaces, BookLore BookDrop, metadata edition/series preferences,
import-list preview and history, 12 additional language catalogs, full-library
API paging, and PostgreSQL TLS settings. It also improved split-audiobook
handling, manual-import evidence, alternate-edition search, scan progress, and
large-screen startup work. `v0.9.971` fixed delay-profile reordering;
`v0.9.972` rejects stale media maps; `v0.9.973` bounds nested brace-parser
input in the frontend toolchain. The changelog includes operator actions.

<a id="chaptarrng-retirement-blockers"></a>

## Cumulative BookshelfNG and ChaptarrNG retirement matrix

Retiring ChaptarrNG into upstream Chaptarr has a cumulative scope. It is a
separate gate from retiring BookshelfNG into ChaptarrNG. Upstream must retain
or replace the complete union of (1) every BookshelfNG capability carried into
the consolidated product and (2) every ChaptarrNG feature introduced or
maintained through `v0.9.973`. This union blocks ChaptarrNG retirement as a
whole, and every row below is an independent blocker. The broader upstream
issue/PR implementation remains paused and incomplete. A matching feature
name, a README claim, or similar code does not close a row; the supported
upstream release must pass behavioral, data, security, and packaging checks.

| Combined capability family | ChaptarrNG features to retain | BookshelfNG contribution to the consolidated product | Upstream Chaptarr retirement gate |
| --- | --- | --- | --- |
| SeerrNG API and request lifecycle | Capability discovery; format/provider identity; format-scoped routes; paged library scans; pending-import status/retry/cancel; restricted SeerrNG key; scoped lookup media-type fix in upstream PR #114 (still open on 2026-10-16) | Readarr-compatible format routes, format-specific availability, and existing client settings and identities | Release an equivalent stable contract and scoped credential; merge and release PR #114, then run unchanged SeerrNG request, polling, availability, pending-work, and authorization probes. Its existing pending-import engine alone is not the contract. |
| Metadata providers and identity | Chaptarr's resolver, V5 pipeline, and provider-specific identities; RanobeDB light-novel provider and Audible marketplace selection | Native Hardcover client and controls; selectable supplemental catalogs; per-field source choices; Goodreads/Readarr continuity and qualified IDs | Keep both identity models resolvable or provide tested migration/adapter behavior; preserve provider choice, credentials, caches, rate-limit handling, and refresh policy. |
| Matching, recovery, and edition preferences | Narrator/publisher-aware matching; metadata-profile filters, weighted edition terms, preferred series, and language-change refresh; alternate-edition fallback, manual-match provenance, and import recovery | Cross-catalog unmapped recovery; normalized title/identifier search; configurable thresholds; weighted edition terms; preferred series; narrator tags and evidence | Preserve candidate ranking, identity constraints, missing-data neutrality, ambiguity handling, manual review, and no-false-match/no-delete behavior with the original edge cases. |
| Format records, profiles, and storage | ChaptarrNG's media-specific models, roots, and standalone ebook/audiobook workflows | Multiple compatible formats on a book; format-specific folders and profile overrides; previewable moves and extra-file handling | Preserve file-to-book relationships, profile fallback, paths, move previews, collisions, and sibling-format isolation; convert existing data without moving media unexpectedly. |
| Audiobook processing and tags | Native conversion, multipart handling, part-number tokens, preserved M4B chapters, and AudioBookShelf metadata/cover propagation | Opt-in M4B merge behavior, chapter/part semantics, and BookshelfNG's byte-preserving MPEG-4 `chpl` TagLib patch | Prove chapter preservation, order, tags, source retention, conversion failure recovery, and representative M4B compatibility. |
| Acquisition and download safety | Built-in Direct Download indexer/client, staged state, API-key downloads, browser fallback, redirect validation, scheduled missing-book search, MyAnonaMouse account-health warnings, and safe import recovery | Existing-torrent adoption/seeding preservation and BookshelfNG-specific source ordering, transfer recovery, and client compatibility | Keep both acquisition paths and their security boundaries; test wrong-category and seeding cases, destination ownership, URL order, private-network redirect rejection, interrupted transfer recovery, and supported architectures. |
| Standalone integrations and privacy | ChaptarrNG standalone app; Calibre Content Server conversion/push, AudioBookShelf and Grimmory metadata propagation, BookLore BookDrop upload, and Emby/Jellyfin library refresh integrations | BookLore BookDrop, Calibre, MyAnonamouse, Audiobookshelf sidecars, and optional aggregate-only diagnostics | Preserve every integration and its settings/data contract; verify BookDrop streaming and recovery; diagnostics must remain a separate explicit opt-in and must not be enabled by migration. |
| Operational events, observability, and import-list history | Optional MQTT/Home Assistant gateway and canonical lifecycle events; low-cardinality Prometheus metrics; durable import-list sync preview/history | Optional aggregate-only diagnostics and existing release/validation workflows | Preserve opt-in defaults, event contracts, metric cardinality/security, import-list cursors, preview semantics, and historical records; verify restart and failure behavior. |
| Backup, authentication, and security | Encrypted full-backup archives; API-key controls, login throttling, response redaction, update verification, and request hardening | Encrypted portable backups; legacy API/query-key aliases; trusted-proxy and request-limit behavior; Bookshelf secrets and existing credentials | Restore existing ChaptarrNG and Bookshelf archives; preserve credentials and rollback; keep restrictions at least as strong and never log secrets. |
| Library browsing, performance, and operator workflow | Server-paged views and full-library API paging with totals; large-library query and cache improvements; root-folder backfill; managed file downloads; mobile accessibility; per-folder progress; cancellation safeguards | Previewable media moves, bulk moves, and extra-file handling | Preserve page completeness and order, root containment, no-file-move backfill, bounded memory/query behavior, responsive workflows, cancellation, and progress on representative large libraries. |
| Release, package, and update channels | ChaptarrNG image at `ghcr.io/snapetech-org/chaptarrng`, Unraid identity, gated BookshelfNG aliases, and retired YunoHost publishing | BookshelfNG GHCR/Docker Hub tags, Unraid template, native installers, Linux package channels, Helm, and legacy update behavior | Preserve each retained channel or record an explicit maintainer decision to retire it; test historical installs, upgrades, package/image/service/data identities, and rollback. No single redirect reaches every installed copy. |
| Localization | Twelve additional language catalogs, including Indonesian and Traditional Chinese, AudioBookShelf translations, locale metadata, English fallback, and locale-aware workflows | No separate BookshelfNG locale feature was identified; inherited keys remain in the source-path audit | Keep all supported strings, fallback behavior, and locale selection; complete native-speaker review and run translation-key and rendered-UI checks against the released upstream build. |
| Data, database, and API compatibility | Existing ChaptarrNG databases, configuration, local IDs, SeerrNG links, migration-158 SQLite import/rollback, paged library API, and PostgreSQL TLS settings | BookshelfNG databases/configuration and their provider identities, local IDs, settings, histories, and files | Keep each source recoverable; preserve identities and references; prove rollback, upgrade, restart, TLS validation, and populated-database behavior. |
| Complete fork history and retirement scope | ChaptarrNG `main` `76aad77847ce` at `v0.9.971` (629 commits ahead, zero behind, 1,652 changed paths); upstream `develop` `e67374f4`; open upstream PR #114 | BookshelfNG `main` `029b0f9b`; product PRs #128 and #130 remain open, along with dependency updates | Regenerate exact source-to-upstream inventories for `main` and review all current upstream issues and PRs before retirement. Retain, port, or prove equivalence for every behavior; this historical matrix does not indicate active implementation. |

The data row has structural differences: BookshelfNG separates `Authors` and
`AuthorMetadata`, and one source book may split into multiple media-specific
Chaptarr books. The migration-158 importer now maps those records, preserves
download and user-visible history separately, qualifies blocklist provider
IDs, and retains extra-file rows whose source `BookFileId` is absent or
unmapped. The importer fixtures cover these mappings. Semantic parity across
all provider, client, and settings combinations remains open.

The retirement gates remain open until tested against a supported upstream
release. The upstream comparison is pinned above; it does not prove equivalence
for later upstream commits. BookshelfNG's inventory and migration evidence are
recorded in its
[consolidation report](https://github.com/snapetech-org/bookshelfng/blob/main/docs/maintainers/chaptarr-consolidation.md)
and [feature matrix](https://github.com/snapetech-org/bookshelfng/blob/main/docs/maintainers/bookshelfng-parity-roadmap.md).
The pre-release PR #100 migration was exercised against a separate real
populated migration-158 SQLite database; its detailed row accounting and
operator-selected media map are recorded in the migration guide. On
2026-10-15, the public `v0.9.971` image passed an additional synthetic populated
fixture run: preview preserved the source bytes; explicit apply reached native
migration v120 and passed integrity checks; rollback after a service-user write
restored v158 and retained the ChaptarrNG database with that write; ordinary
startup converted another copy and passed the container health check; and v157
was rejected without changing the source. A separate no-edition book required
a media map: the review file was private to UID 99, and the `both` choice
created monitored audiobook and ebook records. The primary fixture contains
one author, one source book, two editions, two files, and two history rows.

The converter is now publicly available in the standard image, beginning with
[`v0.9.970`](https://github.com/snapetech-org/chaptarrng/releases/tag/v0.9.970).
It supports migration-158 SQLite only. This evidence does not prove older
schema or PostgreSQL conversion, real-populated rollback, every
provider/settings/client contract, semantic feature parity, or compatibility
with every installation channel. Each capability and applicable upgrade,
rollback, and redirect path remains an independent replacement gate.

The release workflows enforce that distinction: standard ChaptarrNG releases
publish the new ChaptarrNG identities, while old BookshelfNG package IDs and
mutable image aliases remain untouched unless the shared
`CHAPTARRNG_MIGRATION_BRIDGE_ENABLED` repository variable is explicitly set
to `true` after the retirement gates pass.

## What would allow replacement

ChaptarrNG can be retired into upstream Chaptarr only when a supported upstream
release satisfies every row in the combined matrix, the complete post-fork
commit/path inventory is retained, ported, or proven equivalent, and the exact
release passes the ChaptarrNG and SeerrNG validation suites plus the data and
packaging upgrade tests. Chaptarr already contains a pending-import engine, so
SeerrNG needs matching exposed semantics rather than a second queue
implementation. Keeping only the SeerrNG API contract while dropping
standalone or BookshelfNG capabilities does not satisfy the retirement gate.
