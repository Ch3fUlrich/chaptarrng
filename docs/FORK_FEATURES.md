# ChaptarrNG fork changes

ChaptarrNG is Snapetech's maintained fork of
[Chaptarr](https://github.com/Chaptarr/chaptarr), which is itself derived from
Readarr. The fork was created on August 31, 2026 from the parent repository's
`develop` branch at the
[shared fork-base commit](https://github.com/Chaptarr/chaptarr/commit/423b1bba1d47657d24ddfbf9c5418892972a1229).
This page summarizes the main user and operator changes carried in ChaptarrNG
through the published
[`v0.9.969` release](https://github.com/snapetech/chaptarrng/releases/tag/v0.9.969),
and compares them with Chaptarr's `develop` branch as reviewed on 2026-10-12
at [`e67374f`](https://github.com/Chaptarr/chaptarr/commit/e67374f). That
parent commit is in ChaptarrNG `main`'s history. The guide groups related
changes rather than listing every fix or dependency update; the
[changelog](../CHANGELOG.md) records releases and the
[repository history](https://github.com/snapetech/chaptarrng/commits/main/)
records each commit.

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
for full backup archives, fork-owned release images, and the fork's Unraid and
YunoHost packages. Chaptarr has its own encrypted quick-start settings backup;
the full-backup capability is the distinction recorded here.

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

## Direct ebook downloads maintained in ChaptarrNG

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

## Releases and packages

ChaptarrNG publishes versioned stable releases from its maintained `main`
branch and builds fork-owned container images for GHCR. Release notes and the
changelog summarize user-facing changes and required operator actions.

The fork also maintains an [Unraid Community Applications
package](https://github.com/snapetech/chaptarrng-unraid) and a [YunoHost
package](https://github.com/YunoHost-Apps/chaptarrng_ynh). Check each package's
instructions for supported architectures, paths, and upgrade steps.

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
