# Native package channels

The GitHub Release archive is the common application payload for native
packages. It is built from the same ChaptarrNG source and migration utility as
the standard container; package managers add only their service and filesystem
integration. There is no compatibility build.

The standard image remains `ghcr.io/snapetech/chaptarrng` after the GitHub
repository transfer to `snapetech-org`. The existing public `chaptarrng` and
`bookshelfng` container packages are not linked to a repository; grant the
ChaptarrNG and BookshelfNG repositories Actions write access to their
respective packages before enabling standard image publication or legacy
alias updates.

The new-install package identities are:

| Channel | New-install identity | Release asset |
| --- | --- | --- |
| Arch AUR | `chaptarrng-bin` | `ChaptarrNG-<version>-linux-x64.tar.gz` |
| Fedora COPR | `chaptarrng` | `ChaptarrNG-<version>-linux-x64.tar.gz` |
| Chocolatey Community Repository | `chaptarrng` | `ChaptarrNG-<version>-win-x64.zip` |

The AUR and Chocolatey files in this tree are release templates. A release
workflow must fill in the exact version and SHA-256 of the public release asset
before generating `.SRCINFO` or the `.nupkg`. Do not upload templates with
unresolved values. The RPM source package must likewise verify the archive
checksum before submitting to COPR.

Candidate package validation used a locally checksum-verified
`ChaptarrNG-0.9.969-linux-x64.tar.gz`. Both AUR identities built with
`makepkg` in an Arch container, and the package metadata/path checks passed.
Both COPR source RPMs were generated with SHA-256 sidecars; the standard
`chaptarrng` and legacy `bookshelfng` RPMs rebuilt successfully from their
SRPMs. Both installed into an isolated Fedora container with scriptlets and
dependency resolution disabled; the legacy package retained its command,
service, and environment-file paths. These tests do not exercise service
upgrades or database migration and do not use a published standard release
asset. RPM reports nonfatal duplicate build-ID warnings for bundled .NET app
and updater files.

No general native package has been published from ChaptarrNG yet. The release
workflow now builds and validates both the new-install package identities and
these legacy identity updates from the same ordinary ChaptarrNG release assets:

| Existing BookshelfNG install | Update identity | Preserved service and data path |
| --- | --- | --- |
| AUR | `bookshelfng-bin` | `bookshelfng.service`, `/var/lib/bookshelfng` |
| COPR | `bookshelfng` in `slskdn/bookshelfng` | `bookshelfng.service`, `/var/lib/bookshelfng` |
| Chocolatey | `bookshelfng` | `BookshelfNG`, `%ProgramData%\BookshelfNG\data` |

These are package-identity bridges, not another application build. They keep
the old data location so the standard ChaptarrNG startup migration sees the
existing `readarr.db`, converts supported migration-158 SQLite data, and
retains the original database backup. Older or unsupported schemas fail closed
and remain a migration blocker.

The package workflow publishes only when credentials and registrations are
ready. Standard ChaptarrNG releases publish only the ChaptarrNG package IDs.
Publishing existing BookshelfNG package IDs also requires the
`CHAPTARRNG_MIGRATION_BRIDGE_ENABLED` repository variable to be exactly
`true`; its unset default prevents old package managers from redirecting
before the migration, rollback, and channel gates pass. The same variable
controls legacy mutable image tags and the BookshelfNG repository bridge.
Prefer one organization Actions variable scoped to both repositories;
otherwise set the same value in both. Do not enable only one side of the
handoff.
The new package identities are AUR `chaptarrng-bin`, COPR
`slskdn/chaptarrng`, and Chocolatey `chaptarrng`. Configure the ChaptarrNG
publisher secrets without putting their values in chat:

- `AUR_SSH_KEY`: configured in ChaptarrNG Actions; the maintainer account must
  own or be authorized to update both `chaptarrng-bin` and the existing
  `bookshelfng-bin` recipe.
- `COPR_WEBHOOK_URL`: configured package-scoped custom webhook for the new
  ChaptarrNG COPR package.
- `BOOKSHELFNG_COPR_WEBHOOK_URL`: custom webhook for the existing
  `slskdn/bookshelfng` package.
- `CHOCOLATEY_API_KEY`: publisher key that can push both Chocolatey IDs.

The AUR key is configured in the ChaptarrNG repository and authenticated
against the existing keefshape AUR account. The `chaptarrng-bin` AUR package
is still absent and must wait for a public, checksummed standard release
archive. The public [COPR project](https://copr.fedorainfracloud.org/coprs/slskdn/chaptarrng/)
exists for Fedora 43, 44, 45, and Rawhide x86_64. Its `chaptarrng`
custom-source package uses `packaging/rpm/copr-source.sh`, has auto-rebuild
enabled, and its package-scoped webhook is configured in ChaptarrNG Actions as
`COPR_WEBHOOK_URL`. No COPR build has run yet: the public `v0.9.969` release
has no assets, including the SRPM and checksum this package requires.

ChaptarrNG still has no Chocolatey or Docker Hub publisher secrets. The
legacy `BOOKSHELFNG_COPR_WEBHOOK_URL` is now configured in ChaptarrNG Actions;
the matching `COPR_WEBHOOK_URL` in BookshelfNG Actions was rotated to the same
package-scoped endpoint so its existing publisher remains usable. Neither
webhook has been exercised against a COPR build, and the legacy project's
external source bootstrap still needs its one-time update. GitHub does not
reveal secret values, so they are not included here.

The public registry check found the existing AUR `bookshelfng-bin` package at
`0.4.21.69-1`; `chaptarrng-bin` is not registered. COPR has both the existing
`slskdn/bookshelfng` project and the new `slskdn/chaptarrng` project, but the
new project has no published builds yet.
Chocolatey has the existing `bookshelfng` package; the `chaptarrng` package
page returns 404. The public ChaptarrNG `v0.9.969` release has no standard
Linux or Windows archives, which these package recipes require. Registry
publication therefore remains blocked even though the local AUR and RPM
candidate builds passed.

The new COPR package uses [`packaging/rpm/copr-source.sh`](rpm/copr-source.sh),
with `bash`, `curl`, `cpio`, and `rpm` as source-script build dependencies.
Its package-scoped custom webhook receives the stable
`vMAJOR.MINOR.PATCH` release tag and fetches the matching, checksummed SRPM
from the public GitHub Release. The normal release workflow will trigger it
only after the release and its SRPM sidecar are public.

For the existing `slskdn/bookshelfng` project, update its custom-source script
to [`packaging/legacy/rpm/copr-bookshelfng-source.sh`](legacy/rpm/copr-bookshelfng-source.sh).
That script fetches the matching `bookshelfng-<version>-1.src.rpm` from the
ChaptarrNG release. Its old webhook is package-scoped and needs its own
`BOOKSHELFNG_COPR_WEBHOOK_URL` secret.

Linux packages run the self-contained application under systemd as the
`chaptarrng` service user and use `/var/lib/chaptarrng` for configuration and
database files. Add the service user to the group that owns media/download
directories if ChaptarrNG needs write access there. Install `ffmpeg` for
conversion features that use it. The package declares an external update
mechanism, so users update through pacman or dnf; package installation does not
enable the systemd unit automatically.

Chocolatey installs the self-contained Windows release as an NSSM service and
keeps persistent data under `%ProgramData%\ChaptarrNG\data`. Package upgrades
download and verify the archive before stopping a running service, stage the
new payload beside the current one, and restore the old files if the package
swap or service restart fails. They preserve the prior service state and leave
app data in place. Updates come from Chocolatey rather than the in-app updater.

After publication, pacman users install `chaptarrng-bin`, Fedora users enable
the COPR project and install `chaptarrng`, and Windows users install
`chaptarrng` with Chocolatey. Linux services are not enabled automatically;
enable `chaptarrng.service` after checking media-group access. Chocolatey
installs and starts its service automatically.

The legacy recipes and dual-publish workflow are candidates and have not been
published. The new COPR project and webhook are set up, but no package build
has run because the current public release has no source RPM or standard
archives. The AUR and RPM candidate builds pass against the local archive.
On the Arch VM, Pacman upgraded the public BookshelfNG 0.4.21.69-1 package to
the candidate 0.9.969-1 legacy package identity; the operator environment file
and synthetic /var/lib/bookshelfng data marker survived, and the
bookshelfng.service identity now runs the ChaptarrNG binary. Pacman retained
the edited environment file and wrote the package default as .pacnew. This
does not test service restart or a populated application database. The
Chocolatey old-package upgrade smoke is defined but hosted Windows has not
run. The public release asset, remaining publisher credentials, first-time
registry setup, and publication remain open.
The Docker workflow also has a candidate same-digest publisher for the old
mutable GHCR and Docker Hub `latest`, `hardcover`, and `softcover` image names.
The normal ChaptarrNG release leaves those aliases untouched unless
`CHAPTARRNG_MIGRATION_BRIDGE_ENABLED=true`; the BookshelfNG bridge uses the
same gate. It is not exercised against the live registries: GHCR package write
access and Docker Hub credentials must be set up in ChaptarrNG, then each
resulting digest must be checked. Historical version tags and pinned digests
stay unchanged. Windows setup, Debian/PPA, Flatpak/AppImage, Helm, catalogs,
and updater handoff still need separate tested bridges or an explicit
retirement decision. Package work
does not close the feature, data, SeerrNG, rollback, hosted-release, or full
distribution retirement gates.
