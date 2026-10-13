# ChaptarrNG fork changes

ChaptarrNG is Snapetech's maintained fork of
[Chaptarr](https://github.com/Chaptarr/chaptarr), which is itself derived from
Readarr. The fork was created on August 31, 2026 from the parent repository's
`develop` branch at the
[shared fork-base commit](https://github.com/Chaptarr/chaptarr/commit/423b1bba1d47657d24ddfbf9c5418892972a1229).
This guide covers the published feature set through
[`v0.9.969`](https://github.com/snapetech/chaptarrng/releases/tag/v0.9.969)
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
- [SeerrNG Bookshelf backend guide](https://github.com/snapetech/seerrng/blob/main/docs/using-seerr/bookshelf-backend.md)

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
package](https://github.com/snapetech/chaptarrng-unraid). Check its instructions
for supported architectures, paths, and upgrade steps.

## Release history

The [changelog](../CHANGELOG.md) records the maintained release line from
`0.9.936` through `0.9.969`: format-scoped requests, fork identity and Unraid
distribution, capability discovery, the restricted SeerrNG service key,
encrypted full backups, and the Direct Download indexer/client with
browser-assisted fallback. The `v0.9.969` container includes headless Chromium
on AMD64 and ARM64; ARMv7 retains API-key downloads without browser fallback.

## What would allow replacement

ChaptarrNG could be replaced for SeerrNG when a supported Chaptarr release
advertises the required capability contract, keeps provider identity and format
scoped through lookup/add/status, exposes paged library reads and safe
pending-import read/retry/cancel operations, and offers a credential limited to
those routes. Chaptarr already contains a pending-import engine, so SeerrNG
needs matching exposed semantics rather than a second queue implementation.
Keep Direct Download and full-backup encryption available upstream or replace
those standalone needs before retiring the fork. Verify the exact upstream
release with SeerrNG before switching.
