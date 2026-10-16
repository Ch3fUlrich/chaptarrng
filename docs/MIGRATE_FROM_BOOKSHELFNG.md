# Move a BookshelfNG installation to ChaptarrNG

> **Not available in the current published release.** ChaptarrNG PR #100 is
> still under review. The published `v0.9.969` image and packages do not contain
> the converter, and no BookshelfNG update channel has been redirected. Keep
> BookshelfNG installed until this guide is updated to name a released,
> migration-enabled ChaptarrNG version and its tested channel-specific upgrade.

The candidate uses one standard ChaptarrNG application and container build for
fresh and migrated installations. The same `Chaptarr` executable provides an
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
database changes after review, preview rejects missing or stale entries and
generates an updated review instead of guessing.

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
notification setting, representative M4B conversion, hosted Windows/package
upgrades, rollback against a real populated deployment, populated SeerrNG
requests, or complete feature parity. Synthetic fixture coverage now exercises
preview, rollback after a post-migration write, preservation of the current
ChaptarrNG database, and rejection of a wrongly identified backup. The
candidate remains unpublished, and every
feature, installation, and distribution channel is still an independent
BookshelfNG retirement blocker.

Hosted validation exposed two cross-platform issues on earlier candidates:
Windows failed four migration promotion/rollback tests, and macOS Bash 3.2
rejected the Unraid branch-guard script's Bash 4 uppercase expansion. The
branch-guard now uses `tr`, and migration promotion uses `File.Replace`; the
focused migration fixtures pass 23/23 locally. The latest hosted Windows run
failed four migration/rollback tests. The new PR #100 receipt records only
safe exception types, the first Chaptarr application method, and SQLite's
numeric primary error code; exception text and paths remain redacted. A new exact-head Windows rerun is queued to capture that detail.
PR #130 contains the Bash 3.2 branch-guard fix and its local tests pass; the
current native macOS hosted run remains pending. The validation-engine tests
for PR #100 pass 26/26 locally. Neither hosted blocker is
closed.

## Limits and update channels

The ordinary ChaptarrNG release builds one image. The candidate release
workflow in PR #100 dispatches that image's exact multi-architecture digest to
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
