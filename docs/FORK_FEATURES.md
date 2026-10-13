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

The published release is the comparison baseline, not the limit of the
retirement scope. ChaptarrNG's later commits and active changes must also be
reviewed. The BookshelfNG consolidation adds another required feature set; it
does not replace or reduce ChaptarrNG's existing maintained features.

At this review, the shared upstream comparison commit `e67374f` to ChaptarrNG
`dc53d0a` spans 427 commits and 960 net-changed paths. The active consolidation
worktree has 15 tracked changes: five product files, eight Core test fixtures,
and two documentation files. These counts describe source history, not feature
parity, and must be regenerated against the exact upstream and candidate
revisions at any later retirement review. **Confidence:** high for these
pinned repository counts, moderate for the feature-level upstream comparison,
and unknown for semantic equivalence until the matrix checks pass.

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

## ChaptarrNG retirement blockers

Retiring ChaptarrNG into upstream Chaptarr has a cumulative scope. Upstream
must retain or replace **both** (1) ChaptarrNG's own maintained features and
active changes and (2) every BookshelfNG feature carried into the consolidated
ChaptarrNG product. The combined set blocks retirement as a whole, and every
row below is an independent gate. A matching feature name, a README claim, or
similar code does not close a row; the supported upstream release must pass
behavioral, data, security, and packaging checks.

| Combined capability family | ChaptarrNG features to retain | BookshelfNG contribution to the consolidated product | Upstream Chaptarr retirement gate |
| --- | --- | --- | --- |
| SeerrNG API and request lifecycle | Capability discovery; format/provider identity; format-scoped routes; paged library scans; pending-import status/retry/cancel; restricted SeerrNG key | Readarr-compatible format routes, format-specific availability, and existing client settings and identities | Upstream must expose an equivalent stable contract and scoped credential; run unchanged SeerrNG request, polling, availability, pending-work, and authorization probes. Its existing pending-import engine alone is not the contract. |
| Metadata providers and identity | Chaptarr's resolver, V5 pipeline, and provider-specific identities | Native Hardcover client and controls; selectable supplemental catalogs; per-field source choices; Goodreads/Readarr continuity and qualified IDs | Keep both identity models resolvable or provide tested migration/adapter behavior; preserve provider choice, credentials, caches, rate-limit handling, and refresh policy. |
| Matching, recovery, and edition preferences | Narrator/publisher-aware matching and native import behavior | Cross-catalog unmapped recovery; normalized title/identifier search; configurable thresholds; weighted edition terms; preferred series; narrator tags and evidence | Preserve candidate ranking, identity constraints, missing-data neutrality, ambiguity handling, manual review, and no-false-match/no-delete behavior with the original edge cases. |
| Format records, profiles, and storage | ChaptarrNG's media-specific models, roots, and standalone ebook/audiobook workflows | Multiple compatible formats on a book; format-specific folders and profile overrides; previewable moves and extra-file handling | Preserve file-to-book relationships, profile fallback, paths, move previews, collisions, and sibling-format isolation; convert existing data without moving media unexpectedly. |
| Audiobook processing and tags | Native conversion, multipart handling, and Chaptarr metadata | Opt-in M4B merge behavior, chapter/part semantics, and BookshelfNG's byte-preserving MPEG-4 `chpl` TagLib patch | Prove chapter preservation, order, tags, source retention, conversion failure recovery, and representative M4B compatibility. |
| Acquisition and download safety | Built-in Direct Download indexer/client, staged state, API-key downloads, browser fallback, redirect validation | Existing-torrent adoption/seeding preservation and BookshelfNG-specific source ordering, transfer recovery, and client compatibility | Keep both acquisition paths and their security boundaries; test wrong-category and seeding cases, URL order, private-network redirect rejection, interrupted transfer recovery, and supported architectures. |
| Standalone integrations and privacy | ChaptarrNG standalone app and its supported integrations | BookLore BookDrop, Calibre, MyAnonamouse, Audiobookshelf sidecars, and optional aggregate-only diagnostics | Preserve every integration and its settings/data contract; diagnostics must remain a separate explicit opt-in and must not be enabled by migration. |
| Backup, authentication, and security | Encrypted full-backup archives; API-key controls, login throttling, response redaction, update verification, and request hardening | Encrypted portable backups; legacy API/query-key aliases; trusted-proxy and request-limit behavior; Bookshelf secrets and existing credentials | Restore existing ChaptarrNG and Bookshelf archives; preserve credentials and rollback; keep restrictions at least as strong and never log secrets. |
| Release, package, and update channels | ChaptarrNG GHCR releases, Unraid/YunoHost identities, and its supported update path | BookshelfNG's GHCR/Docker Hub tags, Unraid template, native installers, Linux package channels, Helm, and legacy update behavior | Provide tested channel-specific transition artifacts, retain package/image/service/data identities where required, and demonstrate rollback. No single redirect reaches every installed copy. |
| Data and API compatibility | Existing ChaptarrNG databases, configuration, local IDs, and SeerrNG links | BookshelfNG databases/configuration and their provider identities, local IDs, settings, histories, and files | Keep each source recoverable; perform explicit staged conversion; preserve or reconcile identities and references; prove rollback, upgrade, and restart behavior for populated fixtures. |
| Complete fork history and active changes | Every ChaptarrNG commit after the shared fork base, including current in-progress changes | Every BookshelfNG commit/path in the pinned consolidation inventory | Generate and review the exact source-to-upstream commit/path inventory at retirement time. Retain, port, or prove equivalence for every behavior; a proposed removal remains blocked until an explicit maintainer decision. |

All gates are open until tested against a supported upstream release. The
current upstream comparison is pinned above; it does not prove equivalence for
later upstream commits. BookshelfNG's current inventory and migration evidence
are recorded in its
[consolidation report](https://github.com/snapetech/bookshelfng/blob/main/docs/maintainers/chaptarr-consolidation.md)
and [feature matrix](https://github.com/snapetech/bookshelfng/blob/main/docs/maintainers/bookshelfng-parity-roadmap.md).
The candidate contains some ports and passes the recorded Chaptarr Core suite,
but the published `v0.9.969` release has no populated BookshelfNG converter or
channel bridge. Until conversion, parity, consumer probes, and the applicable
channel upgrades pass, neither BookshelfNG nor ChaptarrNG can be described as
seamlessly replaceable.

## What would allow replacement

ChaptarrNG can be retired into upstream Chaptarr only when a supported upstream
release satisfies every row in the combined matrix, the complete post-fork
commit/path inventory is retained, ported, or proven equivalent, and the exact
release passes the ChaptarrNG and SeerrNG validation suites plus the data and
packaging upgrade tests. Chaptarr already contains a pending-import engine, so
SeerrNG needs matching exposed semantics rather than a second queue
implementation. Keeping only the SeerrNG API contract while dropping
standalone or BookshelfNG capabilities does not satisfy the retirement gate.
