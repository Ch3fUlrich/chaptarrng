# Native package channels

Status checked 2026-10-18. ChaptarrNG `v0.9.976` is the latest published
release; the older-schema support in `main` is pending `v0.9.977`. GitHub Release archives
are the payload for native packages; the standard ChaptarrNG container is built
separately from the same application source and migration code. Package recipes
add service and filesystem integration. See the [ChaptarrNG v0.9.976
release](https://github.com/snapetech-org/chaptarrng/releases/tag/v0.9.976)
for the published archives, checksums, and source RPMs.

For current BookshelfNG migration and legacy-channel acceptance status, see
[BookshelfNG retirement status](../docs/BOOKSHELFNG_RETIREMENT.md).

## Package identities

| Channel | New ChaptarrNG install | Existing BookshelfNG identity | Data path during upgrade |
| --- | --- | --- | --- |
| Arch AUR | `chaptarrng-bin` | `bookshelfng-bin` | `/var/lib/bookshelfng` |
| Fedora COPR | `slskdn/chaptarrng` | `slskdn/bookshelfng` | `/var/lib/bookshelfng` |
| Chocolatey | `chaptarrng` | `bookshelfng` | `%ProgramData%\BookshelfNG\data` |
| Docker | `ghcr.io/snapetech-org/chaptarrng` | Legacy image aliases | Existing `/config` mount |

Legacy package recipes keep the old package identity, service name, and data
path so a package upgrade can start the normal ChaptarrNG database migration.
They are a bridge candidate, not proof that every existing install can be
upgraded safely.

## Public status

| Channel | ChaptarrNG | BookshelfNG | Remaining check |
| --- | --- | --- | --- |
| AUR | `chaptarrng-bin` `0.9.976-1` is published | `bookshelfng-bin` `0.4.21.71-1` remains on the BookshelfNG payload | Test an old-package upgrade through conversion and rollback |
| COPR | `chaptarrng 0.9.976-1.fc44` and `0.9.976-1.fc46` are published for x86_64 | Legacy trigger skipped | Test the old package identity in a Fedora upgrade before enabling its bridge |
| Chocolatey | `0.9.976` built and passed upgrade smoke, but its push returned HTTP 403; public ChaptarrNG remains `0.9.973` | `0.4.21.70` remains | Resolve feed authorization, publish `.976`, and test package-manager rollback |
| Docker | `ghcr.io/snapetech-org/chaptarrng:0.9.976` is published | Mutable legacy aliases unchanged | Verify write access and digest equality before enabling the bridge |

Core release validation passed on Linux, macOS, Windows, and PostgreSQL. The
Windows Chocolatey build and upgrade smoke also passed: it installs the
published BookshelfNG package, upgrades through the ChaptarrNG legacy recipe,
checks service startup and automatic database conversion, confirms that the
backup and data remain, and verifies uninstall preserves data. The candidate
BookshelfNG package itself was not published.

The `v0.9.976` release and standard image were published successfully. The
Chocolatey package built and passed its upgrade smoke, but Chocolatey rejected
the publish with HTTP 403. The workflow reported publisher credentials as
configured; the feed rejection still needs to be resolved before the package
is available. No credential value is recorded here.

The release workflow validated source RPMs for both identities and triggered
the standard ChaptarrNG COPR webhook for `0.9.976-1`. The `chaptarrng`
packages are now visible in the Fedora 44 and Rawhide x86_64 repositories. The
legacy BookshelfNG COPR trigger was skipped because the migration bridge is
disabled. The ChaptarrNG AUR
identity was published at `0.9.976-1`; the BookshelfNG AUR identity was not
updated. The `CHAPTARRNG_MIGRATION_BRIDGE_ENABLED` Actions variable is `false`,
so old package IDs and image aliases do not redirect to ChaptarrNG.

## Migration behavior and limits

The default ChaptarrNG app-data location differs from BookshelfNG's
native-package locations. A legacy package recipe preserves the old location
so the standard ChaptarrNG app can find `readarr.db` and run its migration.
The published `v0.9.976` converter accepts BookshelfNG migration-158 SQLite
databases. The `main` candidate embeds the BookshelfNG SQLite migration chain
and upgrades recognized source schemas on a disposable copy before conversion.
The raw snapshot remains available for rollback. A synthetic v22 fixture
passes upgrade, conversion, rollback, and non-mutating preview. Hosted CI,
publication, and real old-schema data remain to be checked. Unknown or newer
schemas, PostgreSQL sources, and two populated source databases fail closed.
The importer keeps records with unresolved formats in both catalogs
without monitoring, writes a private review file, retains the source backup,
and supports validated rollback.

Docker Compose, Unraid, and other container users can change the image to
`ghcr.io/snapetech-org/chaptarrng` and retain `/config`, media/download mounts,
ports, UID/GID, and environment settings. Unraid has a ChaptarrNG template.
Only mutable aliases can be repointed; pinned versions and image digests need
an operator change. A repointed tag does not change a running container.

BookshelfNG's in-app updater still checks its existing upstream update service,
which ChaptarrNG cannot redirect. Automatic migration must therefore use a
validated image/package identity bridge, or operators must change their install
source. Debian/PPA, Flatpak/AppImage, standalone installer definitions, Helm,
third-party catalogs, and pinned/offline installs need an explicit handoff or
retirement decision.

## Work required before enabling the legacy bridge

- Test AUR, COPR, and Chocolatey old-identity upgrades in disposable systems.
  Preserve service settings and data paths, convert the migration-158 fixture,
  retain the source backup, and verify rollback without losing later writes.
- Resolve Chocolatey HTTP 403 authorization, then confirm `0.9.976` publishes and
  its public checksum matches the tested package.
- Verify old GHCR/Docker Hub destinations, credentials, and per-platform
  manifest digests before changing any mutable image aliases.
- Complete the separate [BookshelfNG feature-parity roadmap](https://github.com/snapetech-org/bookshelfng/blob/main/docs/maintainers/bookshelfng-parity-roadmap.md),
  or explicitly accept and document the behavior that will not move.
- Decide which BookshelfNG install channels will be retired without a redirect,
  especially its upstream in-app updater and unsupported third-party channels.

The database conversion and published-image migration smoke test are complete
for the supported schema. The published BookshelfNG identity bridges and full
feature/channel retirement are not.
