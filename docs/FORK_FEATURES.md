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
retirement scope. The later ChaptarrNG gate covers the cumulative union of
every BookshelfNG capability carried into ChaptarrNG and every feature
ChaptarrNG maintains or is actively adding, including unpublished work in
progress at the time of review. The union blocks retirement as a whole, and
each capability remains an individual blocker until its evidence passes.

At this review, the shared upstream comparison commit `e67374f` to ChaptarrNG
`4582c13` spans 436 commits and 964 net-changed paths. The consolidation task
commit contains 16 changed files: six product files, eight Core test fixtures,
and two documentation files. These counts describe source history, not feature
parity, and must be regenerated against the exact upstream and candidate
revisions at any later retirement review. Beyond that pinned task commit, the
current unpublished ChaptarrNG worktree also adds metadata-profile edition and
series preferences, consumer wiring, database migration 115, API/UI changes,
and focused tests. Those active changes belong in ChaptarrNG's cumulative
retirement blocker set even before publication. **Confidence:** high for these
pinned repository counts, moderate for the feature-level upstream comparison,
and unknown for semantic equivalence until the matrix checks pass.

The active consolidation PR #73 adds metadata-profile edition and series
preferences, consumer wiring, database migration 115, API/UI changes, and
focused tests. Separately, the pushed but unreleased
[`codex/upstream-backlog-20261012` branch at `c26dd7f`](https://github.com/snapetech/chaptarrng/tree/codex/upstream-backlog-20261012)
is 306 commits ahead and 13 behind ChaptarrNG `main`, with 680 changed paths
(45,693 insertions and 6,178 deletions). Its
[backlog ledger](https://github.com/snapetech/chaptarrng/blob/c26dd7f/docs/maintainers/upstream-backlog.json)
contains a snapshot of 305 upstream issue and pull-request items; it marks 213
implemented items as awaiting final validation and 79 as unreviewed. The branch
adds or extends RanobeDB and Audible metadata, audiobook conversion and
metadata, safe existing-file adoption, import matching and recovery, Calibre,
AudioBookShelf, Grimmory, Emby/Jellyfin, MQTT/Home Assistant events,
Prometheus metrics, import-list preview/history, and large-library browsing,
performance, and mobile workflows. The branch also removes ChaptarrNG's
YunoHost publisher and package, which remains a channel disposition to review.
These unreleased capabilities and proposed changes join the cumulative
retirement blocker set. Unreviewed backlog requests are not treated as accepted
product scope until maintainers adopt them.

Open ChaptarrNG PR #72 adds Indonesian and Traditional Chinese localization.
Open PRs #69 and #71 change validation infrastructure and maintainer guidance;
they remain in the complete source-history disposition. Upstream PR #114
currently carries media-type preservation for scoped book lookup and remains
a release dependency. All active feature work counts before it is merged or
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
| Release, package, and update channels | ChaptarrNG GHCR releases and Unraid identity; the published line includes YunoHost while the active backlog branch removes its publisher and package | BookshelfNG's GHCR/Docker Hub tags, Unraid template, native installers, Linux package channels, Helm, and legacy update behavior | Preserve each retained channel or record an explicit maintainer decision to retire it; test historical installs, upgrades, package/image/service/data identities, and rollback. No single redirect reaches every installed copy. |
| Localization | Active Indonesian and Traditional Chinese UI work in ChaptarrNG PR #72 | No separate BookshelfNG locale feature was identified; inherited keys remain in the source-path audit | Keep all supported strings, fallback behavior, and locale selection; run translation-key and rendered-UI checks against the released upstream build. |
| Data and API compatibility | Existing ChaptarrNG databases, configuration, local IDs, and SeerrNG links | BookshelfNG databases/configuration and their provider identities, local IDs, settings, histories, and files | Keep each source recoverable; perform explicit staged conversion; preserve or reconcile identities and references; prove rollback, upgrade, and restart behavior for populated fixtures. |
| Complete fork history and active changes | Every ChaptarrNG commit after the shared fork base, including the 306-commit active backlog branch at `c26dd7f`, consolidation PR #73, localization PR #72, and validation/maintainer-workflow PRs #69 and #71 | Every BookshelfNG commit/path in the pinned consolidation inventory | Generate and review exact source-to-upstream inventories for `main`, each active branch, and every open feature PR at retirement time. Retain, port, or prove equivalence for every behavior; a proposed removal remains blocked until an explicit maintainer decision. |

The data row has known structural blockers: BookshelfNG separates `Authors`
and `AuthorMetadata`, and one source book may split into multiple
media-specific Chaptarr books; source download history has no `BookId` although
the target requires one; source blocklists use local IDs while Chaptarr requires
provider-qualified IDs; and source extra-file rows may have no `BookFileId`
although the target schema requires one. Conversion must define durable,
lossless mappings for each case and test them with populated fixtures before
the data-compatibility blocker can close.

All gates are open until tested against a supported upstream release. The
current upstream comparison is pinned above; it does not prove equivalence for
later upstream commits. BookshelfNG's current inventory and migration evidence
are recorded in its
[consolidation report](https://github.com/snapetech/bookshelfng/blob/main/docs/maintainers/chaptarr-consolidation.md)
and [feature matrix](https://github.com/snapetech/bookshelfng/blob/main/docs/maintainers/bookshelfng-parity-roadmap.md).
The unpublished consolidation worktree passes 3,966 of 3,969 Chaptarr Core
tests (3 skipped), including 36 focused preference and matching tests. That
validates code paths, not full feature parity. The published `v0.9.969` release
has no populated BookshelfNG converter or channel bridge. Until conversion,
parity, consumer probes, and the applicable channel upgrades pass, neither
BookshelfNG nor ChaptarrNG can be described as seamlessly replaceable.

## What would allow replacement

ChaptarrNG can be retired into upstream Chaptarr only when a supported upstream
release satisfies every row in the combined matrix, the complete post-fork
commit/path inventory is retained, ported, or proven equivalent, and the exact
release passes the ChaptarrNG and SeerrNG validation suites plus the data and
packaging upgrade tests. Chaptarr already contains a pending-import engine, so
SeerrNG needs matching exposed semantics rather than a second queue
implementation. Keeping only the SeerrNG API contract while dropping
standalone or BookshelfNG capabilities does not satisfy the retirement gate.
