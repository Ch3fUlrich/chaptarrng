# Move a BookshelfNG installation to ChaptarrNG

> **Release status (2026-10-17).** Use the standard ChaptarrNG `v0.9.976` release or a
> later stable version for the published BookshelfNG `0.4.21.71` release. That
> BookshelfNG release includes an optional source-side helper that creates a
> full appdata archive before invoking ChaptarrNG's preview, apply, and
> rollback commands; see the
> [BookshelfNG operator guide](https://github.com/snapetech-org/bookshelfng/blob/main/docs/migrate-from-bookshelfng-to-chaptarrng.md).
> For an immutable Docker deployment, use
> `ghcr.io/snapetech-org/chaptarrng:0.9.976`.
> Conversion supports the exact migration-158 SQLite schema. Pinned images,
> native packages, and updater channels need separate upgrade steps; database
> conversion alone does not update an installation channel.
> See [BookshelfNG retirement status](BOOKSHELFNG_RETIREMENT.md) for live
> package/channel state and the remaining acceptance gates.

Fresh and migrated installations use the same standard ChaptarrNG application
and container build. The `Chaptarr` executable provides an
explicit preview and apply command. Normal startup also detects and converts a
supported BookshelfNG database automatically.

The converter currently supports BookshelfNG migration-158 SQLite databases.
It makes a consistent snapshot, stages a fresh native Chaptarr database,
accounts for every source-table row, checks SQLite integrity and foreign keys,
retains the original database as a backup, and only then promotes the result.
Unsupported schemas fail closed without changing the source.

If BookshelfNG is older than `0.4.21.71`, update it to that final release and
let BookshelfNG finish its own database upgrade before switching applications.
Stop the service after it reaches an idle state, then run ChaptarrNG preview.
Do not point ChaptarrNG at an older schema and expect it to perform the
BookshelfNG upgrade itself. PostgreSQL BookshelfNG databases are not supported
by this converter.

Environment variables are outside the database conversion. Preserve the
BookshelfNG diagnostics variables if that optional module was explicitly
enabled; ChaptarrNG honors that choice only when the archived appdata also
contains the old diagnostics module marker. See the
[optional diagnostics guide](OPTIONAL_DIAGNOSTICS.md).

The conversion reuses the existing data directory and `config.xml`, preserving
the saved HTTP port, URL base, API key, and server settings. Keep the same media
and download mounts and host/container port mappings. This preserves existing
SeerrNG and reverse-proxy endpoints when those services used the old address.

## Preview and apply

Stop BookshelfNG first and make an independent copy of the entire data
directory. Run the preview with the **same regular ChaptarrNG release** that
will run the migrated service. Preview performs the full conversion into a
temporary staged database, prints the row disposition for every source table,
and leaves the source database untouched.

```sh
docker run --rm \
  -v /host/path/to/bookshelfng-config:/config \
  ghcr.io/snapetech-org/chaptarrng:0.9.976 \
  dotnet /app/Chaptarr.dll -preview-bookshelf-migration -data=/config
```

Some source records have no edition rows, so their media type cannot be
determined from the database. A monitored mixed-format book can also have no
selected edition. With no media map, preview and startup now preserve each
unresolved book in both ChaptarrNG catalogs and leave both records unmonitored.
This keeps the migration moving without starting speculative searches in the
wrong format. It can create a temporary duplicate title across the ebook and
audiobook catalogs; no files are assigned or moved for that book. The fallback
also applies to a monitored mixed-format book with no selected edition: its
existing editions and files remain, but neither format is monitored until the
operator reviews it.

The converter writes
`readarr.db.bookshelf-media-review.json` beside the database and keeps it after
automatic conversion. It lists the source book IDs, titles, authors, original
monitoring state, reason, and the `both-unmonitored` fallback. On Unix it is
created with owner-only permissions because it contains library metadata. Use
the review file after startup to correct the media type and monitoring choice
in ChaptarrNG. The normal startup log gives the review file path. Automatic
preservation prevents a conversion stop; it does not infer the book's true
format.

When one audiobook edition and one ebook edition are selected for the same
BookshelfNG work, conversion preserves each selection on its matching
ChaptarrNG record. If two or more editions of the same format are selected,
conversion stops before promotion because ChaptarrNG has one selected-edition
field per format record. Correct that selection in BookshelfNG and preview
again; the source database remains available for another attempt.

To choose formats during conversion, set every `mediaType` to `audiobook`,
`ebook`, `both`, or `both-unmonitored`, then save the completed document as
`readarr.db.bookshelf-media-map.json` beside `readarr.db`. For a book without
editions, the map creates the corresponding ChaptarrNG media record or records.
For a monitored mixed-format book without a selected edition, the map controls
which existing formats are monitored while preserving all source editions and
files. `both-unmonitored` retains both formats without starting searches. Run
preview again with the map in place. Normal startup uses the same map and
converter, so no separate migration build is needed.

The map is tied to source book IDs and the current ambiguous-record set. If the
database changes after review, preview rejects missing entries and stale
choices. It also rejects map entries for books that are no longer ambiguous,
including when the current source needs no map; remove stale entries or use the
updated review file, then preview again instead of guessing.

After preview succeeds, either let normal startup apply conversion or run the
explicit apply command once:

```sh
docker run --rm \
  -v /host/path/to/bookshelfng-config:/config \
  ghcr.io/snapetech-org/chaptarrng:0.9.976 \
  dotnet /app/Chaptarr.dll -migrate-bookshelf -confirm-bookshelf-service-stopped -data=/config
```

Apply retains a backup beside the original database. Keep the independent
directory copy and generated database backup until you have checked the
library, root paths, profiles, indexers, download clients, notifications, and
SeerrNG connection. The explicit stopped-service flag makes the required
quiescent data directory an operator-confirmed precondition.

## Update the local MCP connection

If `snapetech-mcp` is configured for BookshelfNG, change the MCP host to use
the ChaptarrNG app entry and URL after migration:

```text
SNAPETECH_MCP_APPS=chaptarrng
SNAPETECH_MCP_CHAPTARRNG_URL=<ChaptarrNG base URL>
SNAPETECH_MCP_CHAPTARRNG_API_KEY=<dedicated ChaptarrNG service key>
```

Configure that credential in ChaptarrNG as
`CHAPTARR__AUTH__SEERRAPIKEY`, then restart the MCP process. The BookshelfNG
entry uses a media-type selector; ChaptarrNG serves both media types through
one entry, so remove `SNAPETECH_MCP_BOOKSHELFNG_MEDIA_TYPE`. The current MCP
tools read status, paged books, bounded catalog search, queue, and history;
catalog search may contact the configured metadata providers. See the
[SnapeTech MCP guide](integrations/snapetech-mcp.md) for access and data
boundaries.

## Roll back after ChaptarrNG has started

Stop every process using the data directory. First validate the retained
BookshelfNG backup without changing either database:

```sh
docker run --rm \
  -v /host/path/to/bookshelfng-config:/config \
  ghcr.io/snapetech-org/chaptarrng:0.9.976 \
  dotnet /app/Chaptarr.dll -preview-bookshelf-restore \
    -bookshelf-backup=/config/readarr.db.bookshelfng-backup-YYYYMMDDTHHMMSSfffZ-ID.db \
    -data=/config
```

After preview passes, apply the restore while all services remain stopped:

```sh
docker run --rm \
  -v /host/path/to/bookshelfng-config:/config \
  ghcr.io/snapetech-org/chaptarrng:0.9.976 \
  dotnet /app/Chaptarr.dll -restore-bookshelf \
    -confirm-bookshelf-service-stopped \
    -bookshelf-backup=/config/readarr.db.bookshelfng-backup-YYYYMMDDTHHMMSSfffZ-ID.db \
    -data=/config
```

Rollback validates the current ChaptarrNG database and exact supported
BookshelfNG migration-158 backup before changing files. It preserves the
current ChaptarrNG database, including writes made since conversion, as
`readarr.db.chaptarrng-backup-<timestamp>-<id>.db`; the original
`readarr.db.bookshelfng-backup-*` remains untouched. Start only BookshelfNG
after restore. If promotion validation fails, the tool restores the prior
ChaptarrNG database and preserves the failed restore candidate for inspection.

## Unraid

Stop BookshelfNG and back up the full appdata directory. In **Apps > Previous
Apps**, apply the updated BookshelfNG template. It points to
`ghcr.io/snapetech-org/chaptarrng:latest`, the same image used by fresh ChaptarrNG
installs, while retaining the old `/config`, `/data`, `/download`, port,
PUID/PGID, timezone, and Hardcover settings. Starting the container runs the
regular ChaptarrNG app and its automatic migration. The old `config.xml` keeps
the saved app port and URL base.

Wait for the container health check, open the existing URL, and inspect
**System > Logs**. The backup filename starts with
`readarr.db.bookshelfng-backup-`. Check the SeerrNG service connection and run
its diagnostic if the migration changed any network address or API key.

If startup fails, keep both the original copy and the retained source backup.
Stop ChaptarrNG, restore the independent appdata copy, then reinstall the old
BookshelfNG image. Do not start BookshelfNG against a ChaptarrNG-converted
database.

## Docker Compose

Change the image to the standard ChaptarrNG tag and retain the old host paths,
container mount targets, and port mapping. For an installation that used the
default BookshelfNG port, the shape is:

```yaml
services:
  chaptarrng:
    image: ghcr.io/snapetech-org/chaptarrng:latest
    container_name: chaptarrng
    environment:
      PUID: "99"
      PGID: "100"
      TZ: Etc/UTC
    volumes:
      - /host/path/to/bookshelfng-config:/config
      - /host/path/to/media:/data
      - /host/path/to/downloads:/download
    ports:
      - "8787:8787"
    restart: unless-stopped
```

The saved BookshelfNG `config.xml` keeps the internal port at `8787`; the
published ChaptarrNG image still defaults to `8789` for fresh installs. If the
old instance used a different port, preserve that port in both its container
configuration and host mapping.

## Historical populated-database rehearsal (2026-10-14)

The ordinary Linux/amd64 ChaptarrNG image was exercised against an isolated
copy of a real BookshelfNG migration-158 SQLite database. Preview accounted
for all 39 source tables and their rows and left the source database hash
unchanged. At the time, that earlier candidate required a complete per-book
media map before preview could pass. The same standard image then completed
explicit apply on one copy and normal first-start conversion on another. No
compatibility image or build was used, and the live BookshelfNG service was
not changed.

The applied database reached native migration v119. Its retained backup
matched all 39 source table definitions and rows exactly; both databases
passed SQLite integrity and foreign-key checks. The importer retained source
file rows with `EditionId=0`, BookshelfNG's intentional unmapped-file
sentinel, without inferring an edition, book, or media type. A focused fixture
also verifies that explicit sidecar and history links to such a file remain
attached correctly. Volatile command queue/execution, provider health/cursor,
scheduled-task, and source `VersionInfo` state is deliberately recreated;
acquisition history is migrated.

Normal startup kept the saved port, URL base, API key, and SSL settings,
advertised Docker image-pull updates, retained a source backup, served the
existing port's ping and UI routes, and returned all four configured indexers
with their runtime-derived RSS and search capabilities. The conversion logs
and database copies were kept in a private VM scratch directory.

The real database exposed 81 books whose media choice could not be derived
from source relationships. Earlier test maps used explicit technical choices
only; those choices were not reviewed as correct library classification. The
published `v0.9.976` release instead keeps unresolved records in both catalogs
without monitoring and retains a private review file. Operators still need to
confirm the true format and monitoring intent for those records. A map can
apply that review before conversion; otherwise correct the records after
startup. The fallback removes the startup blocker, but it does not establish
semantic parity for those books.

This proves one populated migration-158 SQLite path on Linux/amd64. It does not
prove older BookshelfNG schemas, PostgreSQL conversion, every provider or
notification setting, representative M4B conversion, Windows-native
conversion, package-channel upgrades, rollback against a real populated deployment, populated SeerrNG
requests, or complete feature parity. Feature, installation, and distribution
channels remain separate continuity checks.

An earlier hosted Windows run of the migration candidate failed four
promotion/rollback cases. The Linux image checks below do not establish a
passing native Windows migration run.

## Published image smoke test (2026-10-15)

The public standard `v0.9.971` Linux/amd64 image
([release](https://github.com/snapetech-org/chaptarrng/releases/tag/v0.9.971))
was exercised against a populated synthetic database built from the checked-in
BookshelfNG migration-158 schema and importer fixture: one author, one source
book, two editions, two files, and two history rows.

Preview left the source database bytes unchanged and created no backup. The
explicit apply command converted the data to native migration v120, retained
the v158 source backup, and passed SQLite integrity and foreign-key checks.
After adding a configuration row as the image's default service user (UID 99),
rollback restored the v158 source and retained the newer ChaptarrNG database,
including that write, in its ChaptarrNG backup.

On a separate copy, the ordinary container startup command detected and
converted the same source automatically, retained its backup, passed the
container health check, and passed SQLite integrity and foreign-key checks.
A v157 preview was rejected with the source bytes unchanged and no backup or
promotion. These checks exercise the published Linux image and synthetic
fixture; they do not replace validation against an operator's own data or prove
support for another schema or database engine.

A separate fixture included a monitored source book with no editions. Preview
stopped without changing the database and wrote the review file with mode
`0600`, owned by the container's default UID 99. After the operator map chose
`both`, preview passed and apply created audiobook and ebook records with the
source monitoring state, removed the review file, retained the source backup,
and passed SQLite integrity and foreign-key checks.

## Published image smoke test (2026-10-17)

The public `ghcr.io/snapetech-org/chaptarrng:0.9.974` image was exercised
against three isolated copies of the checked-in migration-158 fixture (39
source tables). The tested `latest` tag had the same per-platform manifest
digests as `0.9.974` and reported application version `v0.9.974.0` at release
commit `53a265f0`.

Preview left the source database hash unchanged and created no backup. Explicit
apply completed and retained the BookshelfNG backup. Rollback preview passed.
After writing a setting to the converted ChaptarrNG database, rollback restored
the BookshelfNG v158 source while retaining the later ChaptarrNG write in a
separate backup. On a third copy, normal startup detected `readarr.db`,
converted it to the native Chaptarr schema, and started the service.

The fixture has no `/library` mount or desktop browser, so normal startup also
logged the expected missing-root and headless-browser warnings; those did not
stop conversion or service startup. This validates the current public image's
migration, rollback, and automatic startup paths against the synthetic fixture.
It does not establish behavior for older schemas, PostgreSQL, live operator
data, every BookshelfNG feature, or every package and update channel.

## Limits and update channels

The converter accepts SQLite databases at exactly BookshelfNG migration 158.
It rejects older or newer schemas and does not convert PostgreSQL. Supported
databases can be migrated by normal ChaptarrNG startup or with the explicit
preview/apply commands above. Both paths retain the BookshelfNG source backup;
rollback is available while the original backup remains intact.

The public ChaptarrNG `v0.9.976` release contains the converter and standalone
archives. Current package and image-channel status is tracked in
[`packaging/README.md`](../packaging/README.md). The migration bridge is
disabled: existing BookshelfNG package IDs and mutable image aliases do not
currently redirect to ChaptarrNG. Pinned tags, digests, offline installs, and
third-party deployment definitions always require an operator update. A
mutable tag only affects a later pull or container recreation.

BookshelfNG's in-app updater still uses its existing upstream update service;
Snapetech cannot redirect that service. The practical handoff paths are a
manual image/package change or a separately validated legacy-identity bridge.
The bridge must preserve each channel's app-data path and service settings and
must be tested as an upgrade, including startup conversion and rollback, before
it is enabled.

Database conversion is only one retirement requirement. ChaptarrNG does not
yet provide full BookshelfNG behavior parity. Follow the separate
[BookshelfNG parity roadmap](https://github.com/snapetech-org/bookshelfng/blob/main/docs/maintainers/bookshelfng-parity-roadmap.md)
for open feature, integration, and distribution work, including SeerrNG and
client behavior. Do not treat successful database conversion as proof that a
BookshelfNG deployment can be retired without losing behavior.
