# ChaptarrNG fork changes

ChaptarrNG is Snapetech's maintained fork of
[Chaptarr](https://github.com/Chaptarr/chaptarr), which is itself derived from
Readarr. The fork was created on August 31, 2026 from the parent repository's
`develop` branch at the
[shared fork-base commit](https://github.com/Chaptarr/chaptarr/commit/423b1bba1d47657d24ddfbf9c5418892972a1229).
This guide covers the published feature set through
[`v0.9.969`](https://github.com/snapetech-org/chaptarrng/releases/tag/v0.9.969)
and the additional feature and maintenance work present in the current fork
checkout. Some additions described here may not yet be in a published image;
the [changelog](../CHANGELOG.md) is the record of released changes. The parent
comparison is Chaptarr's `develop` branch as reviewed on 2026-10-12 at
[`e67374f`](https://github.com/Chaptarr/chaptarr/commit/e67374f), which is in
ChaptarrNG `main`'s history.

This guide groups behavior rather than listing every fix, dependency update,
or upstream report. The
[upstream issue and PR ledger](./maintainers/upstream-backlog.json) provides
the item-by-item audit snapshot and its evidence. Some items remain
unreviewed, need reproduction, or depend on an external service; the ledger
and this overview do not claim that the full upstream backlog is resolved.

The published release is the comparison baseline, not the limit of the
retirement scope. The later ChaptarrNG gate covers the cumulative union of
every BookshelfNG capability carried into ChaptarrNG and every feature
ChaptarrNG maintains or is actively adding, including unpublished work in
progress at the time of review. The union blocks retirement as a whole, and
each capability remains an individual blocker until its evidence passes.

At this review, upstream `develop` is `e67374f4b3fed01bbf08f18672add944062fcb49`
and ChaptarrNG `main` is `6731a436d890e2b56ebdb0875726e6250e720080`; the fork is
606 commits ahead and zero behind, with 1,485 net-changed paths. The current
Bookshelf consolidation candidate, PR #100 at `561bf395`, is 25 commits ahead
of `main`, with 359 changed paths. These counts describe source history, not
feature parity, and must be regenerated against the exact upstream and
candidate revisions at any later retirement review. Open product, security,
and validation PRs include #78, #100, #103, and #105–#109, in addition to open
dependency updates; all count before merge or publication. PR #100 adds the
converter to ordinary ChaptarrNG startup and includes native packaging and
release-workflow changes. Its focused migration fixtures pass 23/23, and the
local full Core suite passed 4,065 tests with 0 failures and 3 skipped on the
same application sources as the current head. The latest hosted Windows run
failed four migration/rollback tests. The validation receipt now records only
safe exception types, the first Chaptarr application method, and SQLite's
numeric primary error code; exception text and paths remain redacted. A fresh
exact-head Windows run is required to capture that added diagnostic. All of
this work belongs in the cumulative blocker set.
**Confidence:** high for these pinned repository counts and open PR states,
moderate for the feature-level upstream comparison, and unknown for semantic
equivalence until the matrix checks pass.

The current integration candidate carries the metadata-profile edition and
series preferences, their consumers, the BookshelfNG database/configuration
migration, and the current upstream-backlog changes. It uses the ordinary
ChaptarrNG executable and image for both fresh installs and migrated installs;
there is no compatibility build. PR #100 does not complete the upstream
issue/PR audit or either retirement gate.

The earlier pinned backlog snapshot at
[`c26dd7f`](https://github.com/snapetech-org/chaptarrng/tree/c26dd7f) was 306
commits ahead and 13 behind ChaptarrNG `main`, with 680 changed paths (45,693
insertions and 6,178 deletions). Its
[ledger](https://github.com/snapetech-org/chaptarrng/blob/c26dd7f/docs/maintainers/upstream-backlog.json)
records 305 upstream issue and pull-request items: 213 implemented items
awaiting final validation and 79 unreviewed. That snapshot adds or extends
RanobeDB and Audible metadata, audiobook conversion and metadata, safe
existing-file adoption, import matching and recovery, Calibre, AudioBookShelf,
Grimmory, Emby/Jellyfin, MQTT/Home Assistant events, Prometheus metrics,
import-list preview/history, and large-library browsing, performance, and
mobile workflows. The integration candidate has since added validation and
merge fixes. The YunoHost publisher and package have been removed; previously
published assets and the external package repository were left untouched.
These capabilities and proposed changes join the cumulative retirement blocker
set. Unreviewed backlog requests are not treated as accepted product scope
until maintainers adopt them.

The fork's Indonesian and Traditional Chinese localization, maintainer guidance,
and YunoHost publisher retirement are already present in `main`. Upstream
PR #114 currently carries media-type preservation for scoped book lookup and
remains a release dependency. All active feature work counts before it is
published.

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

The upstream-backlog work also adds an optional MQTT Gateway for library
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
graphic novels. The profile editor offers genre autocomplete from the local
library. Audible search and metadata can use a configured marketplace rather
than assuming the US catalog. Goodreads bookshelf refresh intervals are
operator-configurable with a safe minimum, as described in the import-list
guide.

## External library connections

The current checkout adds or extends connections for AudioBookShelf, Grimmory,
Calibre Content Server, and Emby/Jellyfin. Their scope differs: AudioBookShelf
can receive covers and compatible forwarded metadata; Grimmory can refresh,
publish, and forward sidecar edits; Calibre-backed roots route ebook imports
through Calibre while the separate Content Server connection can mirror
ChaptarrNG changes; and Emby/Jellyfin can refresh after a grabbed release is
imported.

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

The fork also maintains an [Unraid Community Applications
package](https://github.com/snapetech-org/chaptarrng-unraid). Check its instructions
for supported architectures, paths, and upgrade steps.
## ChaptarrNG active retirement gates

Retiring BookshelfNG into ChaptarrNG and retiring ChaptarrNG into upstream
Chaptarr are separate decisions. The first gate is the complete BookshelfNG
feature, data, and distribution inventory in the
[BookshelfNG parity roadmap](https://github.com/snapetech-org/bookshelfng/blob/main/docs/maintainers/bookshelfng-parity-roadmap.md).
BookshelfNG stays active until every capability it added or materially
extended is present in ChaptarrNG or has a tested, documented replacement.

The later ChaptarrNG-to-Chaptarr gate is cumulative. Upstream Chaptarr must
preserve the union of **all BookshelfNG capabilities carried into ChaptarrNG**
and **all features ChaptarrNG maintains or actively develops**. The whole
union blocks ChaptarrNG retirement; every capability, source-change group,
and installation channel is also an individual blocker. Similar names or
partial implementations do not close a blocker. Work counts before release,
including unpublished work on active branches, open feature PRs, and changes
already being developed locally.

Porting a BookshelfNG capability closes a BookshelfNG-to-ChaptarrNG parity row
only after its own acceptance checks pass; it simultaneously adds that
capability to ChaptarrNG's upstream-retirement blocker set. It does not replace
or reduce ChaptarrNG's own released or actively developed blockers.

| Blocker set | Scope to preserve | Evidence required to close |
| --- | --- | --- |
| BookshelfNG capabilities carried into ChaptarrNG | Every capability and material enhancement in the BookshelfNG parity roadmap, including its settings, persisted data, identities, clients, and integrations | Demonstrate equivalent behavior on the supported upstream release, including converted data and existing client configurations; pass each row's acceptance checks. |
| ChaptarrNG released features | Every capability ChaptarrNG introduced, materially extended, or maintains in this guide and the released changelog, including the SeerrNG contract, Direct Download, full-backup encryption, security behavior, and fork-specific operations | Show the behavior in a supported upstream release and validate the relevant APIs, security boundaries, operator workflows, and clients. |
| ChaptarrNG active and unreleased features | At `main` `6731a436`, open product, security, and validation work includes PR #78, PR #100 (BookshelfNG converter), PR #103, and PRs #105–#109, plus open dependency updates. The consolidation candidate is PR #100 at `561bf395`. | Preserve or replace every released and actively developed capability in a supported upstream release and pass its acceptance checks. Keep the ledger current as active work changes. |
| Remaining fork-only source changes | Every post-fork source change not already accounted for by the capability rows, including fixes, migrations, APIs, security, build, and platform behavior | Disposition every changed path as retained, ported, or replaced by a verified equivalent. A path count is not feature parity. |
| Release and installation channels | Each ChaptarrNG release, image, package, updater, and supported upgrade path, plus any channel carrying a migrated BookshelfNG installation | Test the exact published upgrade, configuration/data migration, rollback, and client/update continuity for each channel. |

No gate closes because a feature is merely present in source or because its
name appears in upstream documentation. The acceptance evidence must target
the exact supported upstream release and cover behavior, data, configuration,
security, and affected clients. Any new feature enters the cumulative set as
soon as ChaptarrNG takes it into active development.

### Itemized active and unreleased ChaptarrNG blockers

The rows below are individual blockers inside the cumulative gate above.
Every row is **open** until a supported upstream release preserves the behavior
or an equivalent is tested against the same cases. The complete source-change
inventory remains an additional blocker, so this feature list does not waive
review of fixes, migrations, build changes, or deletions.

| Active capability or change | Current ChaptarrNG scope | Evidence required before upstream retirement |
| --- | --- | --- |
| RanobeDB light-novel search and metadata | Read-only catalog search, stored work/release IDs, request pacing, and source-content limits | Preserve catalog behavior, identity, rate limits, licensing boundaries, and failure handling. |
| Audible marketplace selection | Operator-selected marketplace for search and metadata | Preserve marketplace configuration, defaults, and result identity. |
| Metadata profile filters and edition ranking | Genre exclusions/autocomplete, weighted edition terms, preferred series, audiobook evidence, and profile-aware filename and metadata consumers | Preserve profile migration/serialization, ranking order, missing-evidence behavior, series consumers, and manual-review boundaries. |
| Catalog and edition search recovery | Retry alternate edition-title searches and retain the strongest credible match | Test title, identifier, ambiguity, and no-false-match cases on the supported release. |
| Configurable Goodreads refresh | Operator-configured bookshelf refresh interval with a safe minimum | Preserve the setting, default, minimum, and retry/rate-limit behavior. |
| Manual import provenance | Persist the route, mode, and evidence behind an explicit manual match | Keep provenance auditable and ensure it is not reused as an automatic scoring signal. |
| Safe existing-file adoption and import destinations | Adopt compatible existing files; avoid replacing untracked destinations; skip unnecessary hashing when the safety setting permits | Preserve no-overwrite, ownership, cancellation, and destination-race guarantees. |
| Archive extraction and audiobook conversion | Extract supported archives; merge eligible multipart audio, retain chapter offsets, report progress, and support cancellation/fallback | Validate formats, metadata, ordering, chapter preservation, source retention, disk failures, and recovery. |
| Existing torrent adoption and seeding | Reuse tracked download data without breaking the seed | Prove correct category, wrong category, completed-download, and seeding-state behavior. |
| Import-list preview and durable sync history | Dry-run preview, per-list outcome history, and provider cursor commits only after successful processing | Preserve read-only preview semantics, counters, cursor safety, retention, and restart behavior. |
| External library connections | AudioBookShelf, Grimmory, Calibre roots/Content Server, and Emby/Jellyfin refresh/synchronization | Preserve each provider's direction, field ownership, loop suppression, ignored-item rules, identifiers, and failure recovery. |
| MQTT Gateway and canonical lifecycle events | Versioned library snapshots, lifecycle requests, and optional Home Assistant discovery | Preserve topic/payload contracts, opt-in behavior, retained-state cleanup, and existing webhook compatibility. |
| Prometheus metrics | Separate optional listener with low-cardinality queue, health, task, and import metrics | Preserve disabled-by-default exposure, metric names/types, and bounded labels. |
| Large-library and background-operation behavior | Paged/virtualized views, bounded query work, batched reconciliation, fair download processing, and cancellable background commands | Run scale fixtures and verify query bounds, concurrency, progress, cancellation, and consistent state. |
| Frontend performance and usability | React Query, route code splitting, mobile search/toolbar adjustments, and upstream bundle optimizations | Preserve loading/error behavior, keyboard/mobile access, route boundaries, and browser tests. |
| Localization and locale-aware UI | Indonesian and Traditional Chinese, AudioBookShelf root-mapping and audiobook-mode translations, language/resource lookup updates, locale-direction metadata, and the localization roadmap | Keep these additions in the cumulative blocker set. Complete language review and browser/layout acceptance for affected locales before treating localization parity as closed. |
| Release and distribution behavior | Fork-owned releases/images, Unraid packaging, platform build support, and the retired YunoHost publishing automation and package | Disposition every supported channel change; previously published YunoHost assets and the external package repository were left untouched. |
| Security, migrations, and compatibility fixes | API, import, authentication, database, dependency, container, and platform changes in the active source tree | Review every security boundary and migration; run the required platform and rollback checks. |
| CI, validation, and maintainer workflow changes | Hosted-runner isolation, validation contracts, and the maintainer guidance added by merged PR #71 | Retain source-bound validation, clean hosted evidence, and reproducible maintainer workflows. |
| Remaining post-fork changes | All other active-branch changes not already represented above | Disposition every changed path as retained, ported, or replaced by verified behavior; a path count alone is not parity. |

The metadata-preference changes require their profile migration, serialization,
ranking order, missing-evidence behavior, and filename, tag, Calibre, and
AudioBookShelf consumers to remain equivalent upstream. Their implementation
does not close the retirement blocker until those behaviors and the remaining
data-migration requirements pass against a supported upstream release.

## Release history

The [changelog](../CHANGELOG.md) records the maintained release line from
`0.9.936` through `0.9.969`: format-scoped requests, fork identity and Unraid
distribution, capability discovery, the restricted SeerrNG service key,
encrypted full backups, and the Direct Download indexer/client with
browser-assisted fallback. The `v0.9.969` container includes headless Chromium
on AMD64 and ARM64; ARMv7 retains API-key downloads without browser fallback.

<a id="chaptarrng-retirement-blockers"></a>

## Cumulative BookshelfNG and ChaptarrNG retirement matrix

Retiring ChaptarrNG into upstream Chaptarr has a cumulative scope. It is a
separate gate from retiring BookshelfNG into ChaptarrNG. Upstream must retain
or replace the complete union of (1) every BookshelfNG capability carried into
the consolidated product and (2) every ChaptarrNG feature that is maintained
or actively being added, including work not yet in a published release. This
union blocks ChaptarrNG retirement as a whole, and every row below is an
independent blocker. A matching feature name, a README claim, or similar code
does not close a row; the supported upstream release must pass behavioral,
data, security, and packaging checks.

| Combined capability family | ChaptarrNG features to retain | BookshelfNG contribution to the consolidated product | Upstream Chaptarr retirement gate |
| --- | --- | --- | --- |
| SeerrNG API and request lifecycle | Capability discovery; format/provider identity; format-scoped routes; paged library scans; pending-import status/retry/cancel; restricted SeerrNG key; active scoped-lookup media-type fix in upstream PR #114 | Readarr-compatible format routes, format-specific availability, and existing client settings and identities | Upstream must expose an equivalent stable contract and scoped credential; merge and release the scoped-lookup fix, then run unchanged SeerrNG request, polling, availability, pending-work, and authorization probes. Its existing pending-import engine alone is not the contract. |
| Metadata providers and identity | Chaptarr's resolver, V5 pipeline, and provider-specific identities; active RanobeDB light-novel provider and Audible marketplace selection | Native Hardcover client and controls; selectable supplemental catalogs; per-field source choices; Goodreads/Readarr continuity and qualified IDs | Keep both identity models resolvable or provide tested migration/adapter behavior; preserve provider choice, credentials, caches, rate-limit handling, and refresh policy. |
| Matching, recovery, and edition preferences | Narrator/publisher-aware matching; metadata-profile controls for weighted edition terms, ignored genres, and preferred series; profile-aware filename, tag, Calibre, and AudioBookShelf series selection; active alternate-edition fallback, manual-match provenance, and import-recovery work | Cross-catalog unmapped recovery; normalized title/identifier search; configurable thresholds; weighted edition terms; preferred series; narrator tags and evidence | Preserve candidate ranking, identity constraints, missing-data neutrality, ambiguity handling, manual review, and no-false-match/no-delete behavior with the original edge cases. |
| Format records, profiles, and storage | ChaptarrNG's media-specific models, roots, and standalone ebook/audiobook workflows | Multiple compatible formats on a book; format-specific folders and profile overrides; previewable moves and extra-file handling | Preserve file-to-book relationships, profile fallback, paths, move previews, collisions, and sibling-format isolation; convert existing data without moving media unexpectedly. |
| Audiobook processing and tags | Native conversion, multipart handling, and Chaptarr metadata; active multi-part M4B conversion, Audible naming, audiobook fallback, and AudioBookShelf metadata/cover propagation | Opt-in M4B merge behavior, chapter/part semantics, and BookshelfNG's byte-preserving MPEG-4 `chpl` TagLib patch | Prove chapter preservation, order, tags, source retention, conversion failure recovery, and representative M4B compatibility. |
| Acquisition and download safety | Built-in Direct Download indexer/client, staged state, API-key downloads, browser fallback, redirect validation; active safe adoption of identical existing destinations and archive-aware import | Existing-torrent adoption/seeding preservation and BookshelfNG-specific source ordering, transfer recovery, and client compatibility | Keep both acquisition paths and their security boundaries; test wrong-category and seeding cases, destination ownership, URL order, private-network redirect rejection, interrupted transfer recovery, and supported architectures. |
| Standalone integrations and privacy | ChaptarrNG standalone app; active Calibre Content Server conversion/push, AudioBookShelf and Grimmory metadata propagation, and Emby/Jellyfin library refresh integrations | BookLore BookDrop, Calibre, MyAnonamouse, Audiobookshelf sidecars, and optional aggregate-only diagnostics | Preserve every integration and its settings/data contract; diagnostics must remain a separate explicit opt-in and must not be enabled by migration. |
| Operational events, observability, and import-list history | Active optional MQTT/Home Assistant gateway and canonical lifecycle events; low-cardinality Prometheus metrics; durable import-list sync preview/history | Optional aggregate-only diagnostics and existing release/validation workflows | Preserve opt-in defaults, event contracts, metric cardinality/security, import-list cursors, preview semantics, and historical records; verify restart and failure behavior. |
| Backup, authentication, and security | Encrypted full-backup archives; API-key controls, login throttling, response redaction, update verification, and request hardening | Encrypted portable backups; legacy API/query-key aliases; trusted-proxy and request-limit behavior; Bookshelf secrets and existing credentials | Restore existing ChaptarrNG and Bookshelf archives; preserve credentials and rollback; keep restrictions at least as strong and never log secrets. |
| Library browsing, performance, and operator workflow | Active server-paged book views, large-library query and cache improvements, mobile search/accessibility, cancellation/progress safeguards, and responsive import tools | Previewable media moves, bulk moves, and extra-file handling | Preserve page completeness and order, bounded memory/query behavior, responsive workflows, cancellation, and progress reporting on representative large libraries. |
| Release, package, and update channels | ChaptarrNG GHCR releases and Unraid identity; YunoHost publishing and packaging have been retired from source | BookshelfNG's GHCR/Docker Hub tags, Unraid template, native installers, Linux package channels, Helm, and legacy update behavior | Preserve each retained channel or record an explicit maintainer decision to retire it; test historical installs, upgrades, package/image/service/data identities, and rollback. No single redirect reaches every installed copy. |
| Localization | Indonesian and Traditional Chinese UI, AudioBookShelf translations, locale metadata, and locale-aware workflows | No separate BookshelfNG locale feature was identified; inherited keys remain in the source-path audit | Keep all supported strings, fallback behavior, and locale selection; run translation-key and rendered-UI checks against the released upstream build. |
| Data and API compatibility | Existing ChaptarrNG databases, configuration, local IDs, and SeerrNG links | BookshelfNG databases/configuration and their provider identities, local IDs, settings, histories, and files | Keep each source recoverable; perform explicit staged conversion; preserve or reconcile identities and references; prove rollback, upgrade, and restart behavior for populated fixtures. |
| Complete fork history and active changes | ChaptarrNG main at `6731a436`, the current BookshelfNG consolidation candidate at PR #100 `561bf395`, and all active ChaptarrNG product, security, validation, and dependency branches, including PRs #78, #103, and #105–#109 | BookshelfNG main at `5d696fb0` has 426 reachable post-fork commits and 1,045 net-changed paths; active BookshelfNG PRs #128 and #130 are tracked separately | Generate and review exact source-to-upstream inventories for `main`, each active branch, and every open feature PR at retirement time. Retain, port, or prove equivalence for every behavior; a proposed removal remains blocked until an explicit maintainer decision. |

The data row has known structural blockers: BookshelfNG separates `Authors`
and `AuthorMetadata`, and one source book may split into multiple
media-specific Chaptarr books. Source `DownloadHistory` has no `BookId`; the
target uses `BookId = 0` for events with no known book, so conversion can retain
it as author/download state if `AuthorId`, event order, download IDs, release,
and data are preserved and replay behavior is tested. The user-visible `History`
table is separate and needs its own event and ID mapping. Source blocklists use
local IDs while Chaptarr requires provider-qualified IDs, and source extra-file
rows may have no `BookFileId` although the target schema requires one.
Conversion must define durable, lossless mappings for each case and test them
with populated fixtures before the data-compatibility blocker can close.

All gates are open until tested against a supported upstream release. The
current upstream comparison is pinned above; it does not prove equivalence for
later upstream commits. BookshelfNG's current inventory and migration evidence
are recorded in its
[consolidation report](https://github.com/snapetech-org/bookshelfng/blob/main/docs/maintainers/chaptarr-consolidation.md)
and [feature matrix](https://github.com/snapetech-org/bookshelfng/blob/main/docs/maintainers/bookshelfng-parity-roadmap.md).
The unpublished PR #100 candidate passed preview, explicit apply, and ordinary
startup on separate copies of one real populated BookshelfNG migration-158
SQLite database using the standard Linux/amd64 ChaptarrNG image. The preview
left the source hash unchanged and accounted for all 127,177 rows; the retained
backup matched all 39 source table definitions and every source row; the v119
target passed SQLite integrity and foreign-key checks. The run also preserved
the saved port, URL base, API key, SSL setting, configured indexers, UI, and
Docker image-pull update behavior. Eighty-one books needed an operator-selected
media mapping; technical test choices were not reviewed as correct
classifications, so this is not general migration approval.

The latest focused importer/converter fixtures passed 23/23 on the application
sources in ChaptarrNG PR #100 at `561bf395`. The local full Core suite passed
4,065 tests with 0 failures and 3 skipped; the validator-only commits since
that application test changed no .NET sources. The latest hosted Windows run
failed four migration/rollback cases. The current Windows rerun is queued with a receipt that captures only the safe
exception type, first application method, and SQLite primary code. A current
exact-head run must pass before merge. The validation-engine tests pass 26/26.
Earlier synthetic rollback runs remain
historical evidence, not a substitute for current exact-head checks. This
proves one real SQLite input and focused conversion invariants, not older
schemas, PostgreSQL, real-populated rollback, semantic feature parity, or
compatibility with other databases.

This is one exact real SQLite input, not proof for older schema lineages,
PostgreSQL, every provider/settings/client contract, rollback on a real
populated deployment, or the complete feature matrix. The candidate remains
unpublished; exact-head hosted checks and package/image bridges have not
completed. The published
`v0.9.969` release has no BookshelfNG converter or channel bridge. Until each
capability and data path passes its own checks and every applicable upgrade,
rollback, and redirect path is proven, neither fork can be called seamlessly
replaceable.

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
