# Move a BookshelfNG installation to ChaptarrNG

> **Release status.** Use the standard ChaptarrNG `v0.9.973` release or a
> later stable version for the final BookshelfNG `0.4.21.70` source. That
> BookshelfNG release includes an optional source-side helper that creates a
> full appdata archive before invoking ChaptarrNG's preview, apply, and
> rollback commands; see the
> [BookshelfNG operator guide](https://github.com/snapetech-org/bookshelfng/blob/main/docs/migrate-from-bookshelfng-to-chaptarrng.md).
> Conversion supports the exact migration-158 SQLite schema. Pinned images,
> native packages, and updater channels need separate upgrade steps; database
> conversion alone does not update an installation channel.

Fresh and migrated installations use the same standard ChaptarrNG application
and container build. The `Chaptarr` executable provides an
explicit preview and apply command. Normal startup also detects and converts a
supported BookshelfNG database automatically.

The converter currently supports BookshelfNG migration-158 SQLite databases.
It makes a consistent snapshot, stages a fresh native Chaptarr database,
accounts for every source-table row, checks SQLite integrity and foreign keys,
retains the original database as a backup, and only then promotes the result.
Unsupported schemas fail closed without changing the source.

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
  ghcr.io/snapetech-org/chaptarrng:<migration-enabled-version> \
  dotnet /app/Chaptarr.dll -preview-bookshelf-migration -data=/config
```

Some source records have no edition rows, so their media type cannot be
determined from the database. A monitored mixed-format book can also have no
selected edition. In either case preview stops before promotion and writes
`readarr.db.bookshelf-media-review.json` beside the database. The file lists
the affected source book IDs, titles, authors, and reason. On Unix it is
created with owner-only permissions because it contains library metadata.

Set every `mediaType` to `audiobook`, `ebook`, or `both`, then save the
completed document as `readarr.db.bookshelf-media-map.json` beside
`readarr.db`. For a book without editions, the choice creates the corresponding
ChaptarrNG media record or records and preserves the source `Monitored` value.
For a monitored mixed-format book without a selected edition, the choice
controls which existing formats are monitored while preserving all source
editions and files. Run preview again with the map in place. Normal startup
uses the same map and converter, so no separate migration build is needed.

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
  ghcr.io/snapetech-org/chaptarrng:<migration-enabled-version> \
  dotnet /app/Chaptarr.dll -migrate-bookshelf -confirm-bookshelf-service-stopped -data=/config
```

Apply retains a backup beside the original database. Keep the independent
directory copy and generated database backup until you have checked the
library, root paths, profiles, indexers, download clients, notifications, and
SeerrNG connection. The explicit stopped-service flag makes the required
quiescent data directory an operator-confirmed precondition.

## Roll back after ChaptarrNG has started

Stop every process using the data directory. First validate the retained
BookshelfNG backup without changing either database:

```sh
docker run --rm \
  -v /host/path/to/bookshelfng-config:/config \
  ghcr.io/snapetech-org/chaptarrng:<migration-enabled-version> \
  dotnet /app/Chaptarr.dll -preview-bookshelf-restore \
    -bookshelf-backup=/config/readarr.db.bookshelfng-backup-YYYYMMDDTHHMMSSfffZ-ID.db \
    -data=/config
```

After preview passes, apply the restore while all services remain stopped:

```sh
docker run --rm \
  -v /host/path/to/bookshelfng-config:/config \
  ghcr.io/snapetech-org/chaptarrng:<migration-enabled-version> \
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

## Verified populated-database test (2026-10-14)

The ordinary Linux/amd64 ChaptarrNG image was exercised against an isolated
copy of a real BookshelfNG migration-158 SQLite database. Preview accounted
for all 39 source tables and their rows, left the source database hash
unchanged, and passed only after the explicit per-book media map was complete.
The same standard image then completed explicit apply on one copy and normal
first-start conversion on another. No compatibility image or build was used,
and the live BookshelfNG service was not changed.

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
from source relationships. The test map used explicit technical choices only
to exercise the converter; those choices were not reviewed as correct library
classification. A real migration must stop at preview and require the operator
to choose `audiobook`, `ebook`, or `both` for each listed record.

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

## Limits and update channels

The standard ChaptarrNG image includes conversion and rollback from `v0.9.970`.
The channel and package inventory below is an audit snapshot from 2026-10-14;
it describes the separate legacy handoff work at that time and is not a live
status feed. Use the current release information and the upgrade steps below
when deciding how to update a pinned image or package.

The ordinary ChaptarrNG release builds one image. The ChaptarrNG release
workflow dispatches that image's exact multi-architecture digest to
the public personal-account bridge repository
[`snapetech/bookshelfng-ghcr-release-bridge`](https://github.com/snapetech/bookshelfng-ghcr-release-bridge),
which republishes it as `ghcr.io/snapetech/bookshelfng:latest`, `:hardcover`,
and `:softcover`. The bridge performs no build, serializes alias updates, and
verifies that every destination digest matches the source. Its CI passed
on commit `7e2d1b0`. The candidate BookshelfNG workflow change in PR #130
removes the duplicate organization-owned GHCR publisher. Docker Hub
publication uses the same standard image digest for the ChaptarrNG and
BookshelfNG mutable tags when publisher credentials are configured.

The bridge repository is public, but its GitHub App installation on the
personal account and write access to the existing `bookshelfng` package are
not configured. The ChaptarrNG dispatch waits for the stable release to
publish, and the bridge refuses to replay a version that is no longer the
latest. No legacy tag has changed. Those access and digest checks must pass
alongside source mapping, rollback, feature parity, populated-client behavior,
release assets, and per-channel upgrade/rollback evidence before the migration
gate can open. The Docker Hub publisher secret is also unavailable.
Historical version tags and pinned digests remain unchanged. Mutable image
tags affect a later pull or recreation; they do not change a running
container.

The converter currently accepts only SQLite databases at BookshelfNG migration
158. Older database schemas and PostgreSQL are unsupported. The populated test
does not close the BookshelfNG feature-parity or distribution-channel
retirement gates by itself.

BookshelfNG's in-app updater checks the Readarr-owned update service, which
Snapetech cannot redirect. Candidate package bridges now keep the AUR
`bookshelfng-bin`, COPR `slskdn/bookshelfng`, and Chocolatey `bookshelfng`
identities while installing the same ChaptarrNG release asset used by fresh
installs. They preserve each channel's old data path and service name, allowing
the standard ChaptarrNG startup migration to run. These recipes have not yet
been published. Ordinary ChaptarrNG releases publish only the ChaptarrNG
package identities and image tags. Publishing the old BookshelfNG package and
container identities requires `CHAPTARRNG_MIGRATION_BRIDGE_ENABLED=true` in
the ChaptarrNG repository; the same unset-by-default gate controls the separate
BookshelfNG bridge workflow. Prefer one organization Actions variable scoped
to both repositories, or set the same value in both; do not enable only one
side. GHCR retagging is dispatched directly by the ChaptarrNG release workflow
to a small, personal-account repository. That repository uses a GitHub App
installation token scoped to its own contents and its own `GITHUB_TOKEN` for
package writes. The Pacman upgrade from the published BookshelfNG AUR package
to the candidate legacy package passed with config, data-marker, and service
identity retained; service restart and actual database conversion remain
untested through Pacman. Both AUR identities and both RPM identities build
from a locally checksum-verified archive, and both RPMs pass isolated
file-install checks. The public `v0.9.969` release has no standard archive,
Windows installer, SRPM, or checksum assets to install.
The new [COPR project](https://copr.fedorainfracloud.org/coprs/slskdn/chaptarrng/)
is created for Fedora 43, 44, 45, and Rawhide x86_64. Its `chaptarrng`
custom-source package is configured with auto-rebuild and the package-scoped
webhook is stored in ChaptarrNG Actions as `COPR_WEBHOOK_URL`; COPR has not
built or published a package yet. The AUR SSH key is configured, but
`chaptarrng-bin` is not registered. ChaptarrNG still lacks Chocolatey and
Docker Hub publisher secrets. The legacy `BOOKSHELFNG_COPR_WEBHOOK_URL` is
configured in ChaptarrNG Actions, and the matching BookshelfNG
`COPR_WEBHOOK_URL` was rotated to the same package-scoped endpoint. Neither
webhook has been exercised against a COPR build, and the old project's
external source bootstrap still needs its one-time update. The standard
ChaptarrNG image publish path succeeded in release run `38267985018`, but its
legacy BookshelfNG GHCR alias remains untested: the personal-account bridge
exists only in the local worktree and still needs publication, App
installation, and `bookshelfng` package write access. Chocolatey remains logged out in the
available browser; Proton Pass itself is not exposed through the browser
control. Secret values are not printed or documented. Pinned, offline,
third-party, Windows setup, Linux
archive, Docker, Helm, and catalog installs still need their own tested
handoff.
