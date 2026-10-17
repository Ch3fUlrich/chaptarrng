# Native package channels

Status checked 2026-10-17, before the `v0.9.975` release run. GitHub Release
archives are the payload for native packages; the standard ChaptarrNG container
is built separately from the same application source and migration code. Package
recipes add service and filesystem integration. See the [ChaptarrNG v0.9.974
release](https://github.com/snapetech-org/chaptarrng/releases/tag/v0.9.974) for
the latest published archive and checksums at the time of this check.

## Package identities

| Channel | New ChaptarrNG install | Existing BookshelfNG identity | Data path during upgrade |
| --- | --- | --- | --- |
| Arch AUR | `chaptarrng-bin` | `bookshelfng-bin` | `/var/lib/bookshelfng` |
| Fedora COPR | `slskdn/chaptarrng` | `slskdn/bookshelfng` | `/var/lib/bookshelfng` |
| Chocolatey | `chaptarrng` | `bookshelfng` | `%ProgramData%\BookshelfNG\data` |
| Docker | `ghcr.io/snapetech-org/chaptarrng` | Legacy image aliases | Existing `/config` mount |

Legacy package recipes keep the old package identity, service name, and data
path so a package upgrade can start the normal ChaptarrNG database migration.
They are a bridge candidate, not proof that an existing installation is safe
to upgrade.

## Public status

| Channel | ChaptarrNG | BookshelfNG | Remaining check |
| --- | --- | --- | --- |
| AUR | `chaptarrng-bin` `0.9.974-1` is published | `bookshelfng-bin` `0.4.21.70-1` remains | Test an old-package upgrade through conversion and rollback |
| COPR | Latest `0.9.974` builds failed | Legacy identity not bridged | Both fixed RPM specs built locally on Fedora 45 without debugsource subpackages; verify the next public build |
| Chocolatey | Public `0.9.973`; `0.9.974` push failed before the key was configured; key is now set and `0.9.975` is pending | `0.4.21.70` remains | Verify publish and Windows service upgrade; rollback remains untested |
| Docker | `ghcr.io/snapetech-org/chaptarrng:0.9.974` is published | Mutable legacy aliases unchanged | Verify write access and digest equality before enabling the bridge |

The standard release workflow has archives for Linux x64, Linux arm64, and
Windows x64, checksums, and source RPMs. Its Windows package job also installs
the last public BookshelfNG package, upgrades it through the candidate legacy
identity, and checks service startup, data retention, automatic database
conversion, backup creation, and uninstall behavior. The `CHAPTARRNG_MIGRATION_BRIDGE_ENABLED` Actions variable is unset, so
legacy package IDs and image aliases remain unchanged. Do not enable it until
the legacy upgrade paths and rollback checks below pass. Registry credentials and package names are maintained in GitHub
Actions; their values are not stored here.

## Migration behavior and limits

The default ChaptarrNG app-data location is different from BookshelfNG's
native-package locations. The legacy recipes preserve the old location and
start the standard ChaptarrNG app from there. The converter accepts only
BookshelfNG migration-158 SQLite databases. It previews every table, requires
operator decisions for ambiguous per-book media types, retains the source
backup, and offers a validated rollback. Older schemas and PostgreSQL are not
converted.

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

- Publish a release containing the RPM fix and confirm both COPR projects
  build from the published SRPMs.
- Retry Chocolatey publication and verify the public package version and
  checksums.
- Run a real upgrade using each existing package manager. Confirm it preserves
  the service identity, edited environment/configuration, permissions, and
  data path; converts the migration-158 fixture; retains a usable backup; and
  rolls back without losing writes made after conversion.
- Verify that the `v0.9.975` Windows release job passes its existing
  BookshelfNG-to-ChaptarrNG package upgrade smoke. It checks service startup,
  automatic conversion, backup creation, data retention, and uninstall, but
  does not exercise rollback.
- Verify the old GHCR/Docker Hub destinations, credentials, and per-platform
  manifest digests before changing any mutable image aliases.
- Complete the separate [BookshelfNG feature-parity roadmap](https://github.com/snapetech-org/bookshelfng/blob/main/docs/maintainers/bookshelfng-parity-roadmap.md),
  or explicitly accept and document the behavior that will not move.
- Decide which BookshelfNG install channels will be retired without a redirect,
  especially its upstream in-app updater and unsupported third-party channels.

The database conversion and published-image migration smoke test are complete
for the supported schema. BookshelfNG retirement is not complete until these
package, service, feature-parity, and channel decisions are resolved.
