<div align="center">

<img src="Logo/chaptarr.png" width="160" alt="ChaptarrNG logo">

# ChaptarrNG

A Snapetech-maintained Chaptarr fork for audiobook and eBook libraries.

[![License](https://img.shields.io/github/license/snapetech-org/chaptarrng)](https://github.com/snapetech-org/chaptarrng/blob/main/LICENSE)
[![.NET 10 LTS](https://img.shields.io/badge/.NET-10%20LTS-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/en-us/platform/support/policy)
[![Discord](https://img.shields.io/badge/Discord-join-5865F2?logo=discord&logoColor=white)](https://discord.gg/nqFGsGUug2)

</div>

Support ChaptarrNG development on [Ko-fi](https://ko-fi.com/snapetech).

> **ChaptarrNG is beta software.** It is under active development, and breaking changes are possible. Keep regular backups and avoid pointing it at a library you cannot afford to lose.

## What is ChaptarrNG?

ChaptarrNG is Snapetech's maintained fork of [Chaptarr](https://github.com/Chaptarr/chaptarr), which is itself an independent Readarr-derived audiobook and eBook library manager. ChaptarrNG keeps that standalone library-management experience while maintaining the request and format APIs used by SeerrNG.

We forked Chaptarr so SeerrNG can rely on a book backend with explicit ebook/audiobook routing and durable handling for imports that Chaptarr queues while preparing author metadata. Maintaining the API contract here lets SeerrNG track a request through that asynchronous work without depending on upstream changes landing first. ChaptarrNG runs on its own and does not require SeerrNG.

The application continues to identify itself as `Chaptarr` through its Readarr-compatible API so SeerrNG can detect it. **ChaptarrNG** is the name of this maintained repository, its container image, and its Unraid template.

### BookshelfNG consolidation status

BookshelfNG `0.4.21.70` is designated its final release. The release includes
a source-side helper that archives appdata before calling the standard
ChaptarrNG migration and rollback commands. The matching ChaptarrNG release is
[`v0.9.973`](https://github.com/snapetech-org/chaptarrng/releases/tag/v0.9.973);
it includes stricter rejection of incomplete or stale media maps and a frontend
dependency security fix. Read the
[BookshelfNG handoff guide](https://github.com/snapetech-org/bookshelfng/blob/main/docs/migrate-from-bookshelfng-to-chaptarrng.md)
before migrating.

The conversion scope is the exact BookshelfNG migration-158 SQLite schema.
Ambiguous book formats require operator review. The source helper backs up
appdata, but does not copy external media/download folders, translate every
external setting, or update pinned images and package channels. Older schemas,
PostgreSQL, full feature parity, and several real-world rollback and channel
paths remain unverified. The guarded BookshelfNG release bridge remains
disabled by default; it does not redirect live tags or packages. The
[combined retirement matrix](docs/FORK_FEATURES.md#chaptarrng-retirement-blockers)
tracks the remaining feature and channel gates.

ChaptarrNG is independently maintained and is not affiliated with the Servarr team or the Readarr, Sonarr, Radarr, Lidarr, or Prowlarr projects.

The backend is built on .NET 10 LTS. Docker images include the ASP.NET Core
runtime, while source builds require the .NET 10 SDK.

### What makes this fork different

- **SeerrNG contract:** SeerrNG can discover provider IDs, format routes, paged library scans, and pending-import operations through `GET /api/v1/system/capabilities`. Both format services can use one ChaptarrNG instance while requests retain the selected format and provider identity.
- **Pending-import integration:** Chaptarr's current development branch also has pending author imports. ChaptarrNG exposes that asynchronous work through the SeerrNG capability contract and keeps retry/cancel behavior safe when multiple requests share an import.
- **Restricted service access:** ChaptarrNG `0.9.941` and later can use a SeerrNG-only API key for supported book requests, searches, library status, and pending imports without granting global administration or destructive access.
- **Standalone additions maintained here:** An optional built-in indexer and download client can search configured ebook sources, use API-key downloads, and optionally fall back to browser-assisted links. Full backup archives can use passphrase-based authenticated encryption; the direct Chaptarr project separately offers encryption for quick-start settings backups.
- **Safer list and file workflows:** Preview an import list's next sync, inspect durable per-list sync history, and review manual-import matching evidence. Book-file downloads are limited to configured roots; imports check destinations before writing, extract supported archives, and can merge or convert multipart audiobooks with progress and cancellation. New multi-part releases can remain as separate book files.
- **More metadata and library connections:** RanobeDB adds light-novel ebook metadata; Audible searches can use a selected marketplace. Metadata profiles can rank edition terms and choose preferred series. AudioBookShelf, Calibre, Grimmory, and BookLore BookDrop provide optional library connections, while Emby/Jellyfin can refresh after an import.
- **Search and library maintenance:** Indexers can run optional recurring searches for missing monitored books. MyAnonaMouse health reports explain when stale account status blocks grabs. Operators can preview and backfill missing author root folders without moving files.
- **Operations and automation:** An optional MQTT gateway publishes library snapshots and lifecycle events, including Home Assistant discovery. A separate Prometheus listener exposes low-cardinality metrics. The full-library API supports paging, large library views use paged reads, and long author deletions run in the background.
- **Localization:** The interface and help text have catalogs for 12 additional languages, including Indonesian and Traditional Chinese, with English fallback for untranslated strings.
- **Fork-owned distribution:** ChaptarrNG publishes its own images and maintains an Unraid package. Its [fork feature guide](docs/FORK_FEATURES.md) compares these additions with the parent project.

The [fork feature guide](docs/FORK_FEATURES.md) covers the additions and their
limits. Dedicated guides explain [import-list preview and history](docs/IMPORT_LIST_SYNC.md),
[external library connections](docs/EXTERNAL_LIBRARY_INTEGRATIONS.md),
[MQTT and canonical events](docs/MQTT_GATEWAY.md), and
[Prometheus metrics](docs/PROMETHEUS_METRICS.md). The
[changelog](CHANGELOG.md) records changes by released version.
The feature guide describes the current source checkout; some newer work may
not be included in the latest published image yet.

The [ChaptarrNG retirement matrix](docs/FORK_FEATURES.md#chaptarrng-retirement-blockers)
defines the cumulative upstream gate: every BookshelfNG feature carried over
and every ChaptarrNG feature introduced or maintained through `v0.9.973`
blocks retirement as a whole and remains an individual acceptance gate.
Carrying over a BookshelfNG capability adds to this set; it does not replace
or reduce ChaptarrNG's own blockers.

See the [SeerrNG Bookshelf backend guide](https://github.com/snapetech-org/seerrng/blob/main/docs/using-seerr/bookshelf-backend.md) for setup and the integration contract.

## Key Features

### Audiobooks
* **Narrator Aware** - Can help recognize and organize your books with narrator info
* **Format-scoped books and requests** - Track ebook and audiobook records
  separately while one ChaptarrNG instance serves both; SeerrNG can route
  each request to its matching format
* **Publisher Aware** - Special handling for dramatized audiobooks and multi-part releases
* **Audio Formats** - Handles M4B, MP3 chapters, and multi-file audiobooks
* **MP3 → M4B Conversion** - Convert new downloads or books already in the library into a single chaptered M4B, with chapter preservation or insertion if missing. Conversion shows progress and can be cancelled (powered by [m4b-tool](https://github.com/sandreas/m4b-tool))
* **Multipart Audiobooks** - Import numbered parts side by side with `{ReleasePart}` and `{ReleaseParts}` filename tokens, or optionally merge eligible downloads into one M4B after checking the final destination

### Organization
* **Dual Media Libraries** - You can have separate root folders for audiobooks and eBooks or choose to have your eBooks placed alongside your audiobooks
* **Matching** - Match audiobook and eBook files using tags, folders, and release metadata
* **Series Management** - Automatically organize books by series
* **Metadata Profiles** - Control allowed languages, rank preferred edition terms, and choose the series name used in filenames and generated metadata. Stronger match evidence remains authoritative.
* **Genre Filtering** - Exclude configured genres, such as comics or graphic novels, through metadata profiles
* **Flexible Renaming** - Customizable file naming with audiobook-specific tokens

### Integration & Automation
* **Standard *arr Integrations** - Works with the usual *arr-family download clients and indexer protocols
* **External Libraries** - Connect AudioBookShelf, Calibre Content Server, and Grimmory for supported refresh, import, metadata, cover, and edit-forwarding workflows; see the [integration guide](docs/EXTERNAL_LIBRARY_INTEGRATIONS.md) for the direction and limits of each connection
* **BookLore BookDrop** - Optionally upload imported files to BookLore's BookDrop intake; BookLore is an external app and is not required to run ChaptarrNG. See the [BookLore integration guide](docs/BOOKLORE_INTEGRATION.md) for setup, supported file handling, and the unverified BookOrbit transition.
* **Emby and Jellyfin** - Refresh the media-server library after a grabbed release is imported
* **MQTT and Webhooks** - Publish snapshots and canonical lifecycle events to MQTT, with optional Home Assistant discovery and opt-in canonical events for existing webhooks
* **Import Lists** - Preview the next sync and review durable per-list history before relying on automatic-add behavior

### Quality & Upgrades
* **Automatic Upgrades** - Replace lower quality versions automatically
* **Profile Flexibility** - Create custom quality profiles for different libraries

## Metadata

ChaptarrNG is not compatible with Readarr's metadata sources. It uses Chaptarr's modular pipeline to resolve entities across metadata providers and aggregate their data through automated refinement and consensus. Metadata work remains an ongoing effort.

For light-novel eBooks, ChaptarrNG can search RanobeDB explicitly and store its
work and release IDs alongside the normal eBook records. RanobeDB's public API
is currently v0 and is for non-commercial use; ChaptarrNG applies a conservative
local request throttle below the API's 60-per-minute guidance. ChaptarrNG links
results back to RanobeDB and does not import its descriptions or cover images,
which have separate licensing conditions. If several instances or other clients
share an IP address, keep their combined requests below the service limit.
See the [RanobeDB API documentation](https://ranobedb.org/api/docs/v0) and
[database licensing notes](https://ranobedb.org/about) before enabling this
source.

Audible search and ASIN lookups can use a configured marketplace for the US,
UK, France, Germany, Italy, Spain, Canada, Australia, India, Japan, or Brazil.
Results use that storefront's language headers and link back to the selected
marketplace.

## Getting Started

> **Docker is currently the only supported way to run ChaptarrNG.** The fork is developed and tested in Docker. Releases do not include native install packages; native support is not currently offered.

ChaptarrNG uses ImageSharp to resize cover images. ImageSharp 4 requires a
valid Six Labors license when building this direct dependency from source.
Official ChaptarrNG images contain a licensed build and do not need a build key
configured at runtime. All use remains subject to Six Labors' license terms.
See the [ImageSharp build-license guide](docs/development/imagesharp-license.md)
for eligibility, acquisition, and build setup.

### Docker

Pull the image:
```bash
docker pull ghcr.io/snapetech-org/chaptarrng:latest
```

Run with Docker:
```bash
docker run -d \
  --name chaptarrng \
  -p 127.0.0.1:8789:8789 \
  -e PUID=1000 \
  -e PGID=1000 \
  -v /path/to/config:/config \
  -v /path/to/audiobooks:/audiobooks \
  -v /path/to/ebooks:/ebooks \
  -v /path/to/downloads:/downloads \
  --restart unless-stopped \
  ghcr.io/snapetech-org/chaptarrng:latest
```

Note: if `PUID`/`PGID` are not set, the image defaults to `99:100`. If `/path/to/config` doesn't exist, Docker will create it as `root:root`. Create it first (or fix ownership) so it matches `PUID`/`PGID`. Avoid setting `user:` in Compose; it bypasses the entrypoint permission setup. On Unraid, media folders commonly use `99:100`, so use `PUID=99` and `PGID=100` unless your share is owned differently. If multiple containers/users share the same media group, add `-e UMASK=002`. When testing permissions with `docker exec`, test as the app user, not root, for example: `docker exec -u 99:100 chaptarrng sh -c 'id; touch /audiobooks/.chaptarr-write-test && rm /audiobooks/.chaptarr-write-test'`.

The host port is bound to loopback by default. To expose ChaptarrNG directly on
your LAN, change the mapping to `-p 8789:8789` and restrict access with your
firewall or reverse proxy. If SeerrNG runs in another container, attach both
containers to a shared Docker network and use `http://chaptarrng:8789`; do not
use `localhost` from inside SeerrNG.

Or use Docker Compose:
```bash
wget https://raw.githubusercontent.com/snapetech-org/chaptarrng/main/docker-compose.yml
# Edit paths in docker-compose.yml
docker compose up -d
```

#### Memory limits for large libraries

The Compose example does not set a memory limit because the right value depends
on the host and library. For a large initial scan, set a container limit sized
for the memory the host can spare. Without a cgroup limit, .NET may size its
garbage-collection heap from all the memory visible to the process, including
the full VM or host. Docker Compose's [`mem_limit`](https://docs.docker.com/reference/compose-file/services/#mem_limit)
sets a container memory limit; .NET uses that limit when sizing its heap. On a
Docker-in-LXC setup, make sure the Docker container itself has a limit visible
inside the container rather than relying only on the LXC allocation.

For example, add this under the `chaptarrng` service and choose a value that
fits your host and workload:

```yaml
mem_limit: 4g # Example only; size this for your host and library.
```

When a container memory limit is visible, .NET's default GC heap hard limit is
75% of that limit. If you choose to override it with
`DOTNET_GCHeapHardLimitPercent`, note that .NET parses the environment-variable
value as hexadecimal: `55` (or `0x55`) means 85%, while `85` means 133%.
Consider the memory needed outside the GC heap before increasing the percentage.
See Microsoft's [.NET GC configuration reference](https://learn.microsoft.com/dotnet/core/runtime-config/garbage-collector#heap-hard-limit-percent)
for details.

### PostgreSQL (Optional)

ChaptarrNG supports using an external PostgreSQL database instead of the default SQLite `chaptarr.db` file. To enable it, set at least `Chaptarr__Postgres__Host` (and credentials).

Environment variables:
- `Chaptarr__Postgres__Host`
- `Chaptarr__Postgres__Port` (default: `5432`)
- `Chaptarr__Postgres__User`
- `Chaptarr__Postgres__Password`
- `Chaptarr__Postgres__MainDb` (default: `chaptarr-main`)
- `Chaptarr__Postgres__LogDb` (default: `chaptarr-log`)
- `Chaptarr__Postgres__CacheDb` (default: `chaptarr-cache`)
- `Chaptarr__Postgres__SslMode` (optional: `disable`, `allow`, `prefer`, `require`, `verify-ca`, or `verify-full`; when unset the Npgsql default applies)
- `Chaptarr__Postgres__RootCert` (optional: path to a PEM root certificate used to verify the server for `verify-ca` and `verify-full`; startup fails with a clear message if the file is missing)
- `Chaptarr__Postgres__NoResetOnClose` (default: `true`; skips the session reset round-trip when a pooled connection is returned, set to `false` to restore it)

Note: ChaptarrNG does not create PostgreSQL databases automatically; create the databases and grant the configured user access.

### Direct Download sources

Direct Download is an optional, built-in ebook indexer and download client. It does not provide audiobook searches, recent-item feeds, or automatic discovery of arbitrary sources. Configure it only with sources you are allowed to access and use.

#### Configure the indexer

1. Add the **Direct Download** indexer.
2. Enter one absolute `http://` or `https://` URL per line in **URLs**.
3. Enter an **API Key** when the source supports fast downloads, or enable **Enable Slow-Download Browser Fallback** when you need browser-assisted links. At least one download method is required.
4. Save the indexer and select the **Direct Download** client for it.

Blank lines are ignored. Duplicate URLs are removed case-insensitively, and trailing slashes are normalized. The first occurrence remains in the configured order. Chaptarr never reorders the list or stores probe health as part of the settings. The provider Test action validates the URL list and, when an API key is configured, checks it against the first source without requesting a real file. A successful Test does not search for a book or prove that a title is available.

The API Key field is masked. A saved key is preserved when the indexer is edited or included in a settings backup, while API responses, logs, and validation errors must not reveal its value. Do not put credentials in a URL. If different URLs use different credentials, create separate indexers rather than placing them in one fallback list. A key must not be copied into a fallback URL or sent to a host that was not selected for the request.

#### API and browser-assisted grabs

When you grab a result from a supported catalog source, Chaptarr first uses
the source's fast-download API when an API key is configured. If that cannot
provide a file URL, the optional slow-download browser fallback can resolve a
link in a headless browser during the background transfer. Browser fallback
can take longer than an API grab. Direct Download is built in; it does not
require qBittorrent, Transmission, or another external download client.
The official Docker image includes the Playwright Chromium runtime on AMD64
and ARM64. ARMv7 images omit Chromium, so browser fallback is unavailable on
ARMv7; API-key-resolved downloads still work there. A custom image must include
a matching browser runtime for browser fallback to work.

The built-in client reports transfer progress and keeps its state and staged
files across restarts. Once a supported ebook file is complete, Chaptarr can
send it through the normal import flow. See the [fork feature guide](docs/FORK_FEATURES.md#direct-ebook-downloads)
for the behavior and safety limits.

#### Ordering, probing, and fallback

During an ebook search, Chaptarr checks the configured URLs from top to bottom. It prefers the book ISBN when one is available, then falls back to the monitored edition title. A URL is selected only after it returns a supported response with usable ebook results. Chaptarr moves to the next URL when the current one is unsafe, unavailable, times out, returns an unsupported response, or produces no usable result. It stops after the first successful URL and does not probe later entries for that search.

Each request has a bounded timeout and response-size limit. Redirects are followed only after every destination passes the same URL safety checks, and redirect chains are bounded. Search results are limited to recognized ebook file types and carry the normalized title, format, size, ISBN when available, and a direct download URL. A search fails with an actionable error when every configured URL fails or returns no usable result.

#### Ebook-only limitation

Direct Download accepts ebook searches only. Audiobook searches and audiobook releases are rejected before a Direct download is started. Supported ebook formats are limited to the formats recognized by the importer. A source that returns a page, an empty response, an unsupported format, or a non-ebook file is not a valid Direct release.

#### Staging, restart, and cleanup

The Direct Download client requires a configured **Staging Folder**. Create the folder first and make sure the Chaptarr process user can read, write, create subfolders, rename files, and delete files there. Keep the staging folder on persistent storage, not a temporary container filesystem. Do not point it at a library root or a folder shared with another downloader.

Each download gets its own state record and working directory. Data is written to a `.part` file and promoted to the final staged file only after a non-empty response completes. The client reports queued, downloading, completed, and failed states. Transient connection, timeout, and server failures are retried up to three attempts. An interrupted partial file is discarded before a retry, so it is not treated as a completed ebook.

On restart, Chaptarr reloads Direct state from the staging folder. Queued and in-progress items are reconciled and started again, an existing completed file is reused, and a missing completed file becomes failed. Keep the state files and staged files together until the item has been imported or deliberately removed.

Cleanup is explicit. Removing an item with delete-data enabled removes its state, partial file, completed staged file, and empty per-download directory. Removing it without delete-data removes the state but preserves downloaded data for manual inspection or recovery. After an import, use the normal completed-download cleanup action and choose whether staged data should be deleted. Do not delete staged files manually while an item is queued or being imported.

#### Security and operational requirements

Only absolute HTTP and HTTPS URLs are accepted. URLs with embedded credentials are rejected. Chaptarr blocks localhost, loopback, private, link-local, carrier-grade NAT, and cloud metadata targets, including targets reached through redirects. TLS certificate validation remains enabled. Keep source URLs and keys private, restrict access to the Chaptarr web interface, and never paste a key into an issue, log excerpt, backup shared with others, or shell history.

The feature is not a general-purpose web downloader. It depends on a source returning a supported, bounded response and a usable direct file URL. Access controls, rate limits, expiring links, changed page formats, unavailable mirrors, and licensing restrictions can cause a previously working URL to fail. A provider Test cannot detect those runtime conditions. Monitor the queue and failed items, keep backups of the configuration and library, and remove stale staging data after confirming that no queued or import-pending item references it.

### Unraid Community Applications

ChaptarrNG's Unraid Community Apps template is [templates/chaptarrng.xml](templates/chaptarrng.xml),
with repository metadata in [ca_profile.xml](ca_profile.xml). It installs the
maintained fork image, maps the web interface on port `8789`, and provides
separate paths for appdata, audiobooks, ebooks, and downloads. The template
tracks the validated `main` image and uses `PUID=99` and `PGID=100` by default;
change those values to match the ownership of your media shares.

To make ChaptarrNG searchable in Community Applications, submit the package
repository URL `https://github.com/snapetech-org/chaptarrng` through Unraid's
[Community Apps submission flow](https://ca.unraid.net/submit/new) and complete
its Validate and Scan steps. The template's `TemplateURL` points to its raw XML
in this repository. The `latest` GHCR tag remains available for stable Docker
installs; the Unraid template follows the validated `main` image.

## Building from Source
Building from source requires the .NET 10 SDK, Node.js, and Yarn. When running ChaptarrNG natively, install FFmpeg for your operating system and make sure both `ffmpeg` and `ffprobe` are available on the `PATH` used to start the application. Normal source builds do not bundle them automatically; the ChaptarrNG Docker image already includes them.

**Linux / macOS:**
```bash
# Clone the repository
git clone https://github.com/snapetech-org/chaptarrng.git
cd chaptarrng

# Build the backend
dotnet publish src/NzbDrone.Console/Chaptarr.Console.csproj -c Release -f net10.0 -o _output/publish

# Build the frontend
yarn install
yarn build
cp -r _output/UI _output/publish/UI

# Run ChaptarrNG
dotnet _output/publish/Chaptarr.dll
```

**Windows (Command Prompt):**
```cmd
:: Clone the repository
git clone https://github.com/snapetech-org/chaptarrng.git
cd chaptarrng

:: Build the backend
dotnet publish src/NzbDrone.Console/Chaptarr.Console.csproj -c Release -f net10.0 -o _output/publish

:: Build the frontend
yarn install
yarn build
xcopy _output\UI _output\publish\UI /E /I

:: Run ChaptarrNG
dotnet _output/publish/Chaptarr.Console.dll
```

Note: the inherited assembly is named `Chaptarr.dll` on Linux/macOS and `Chaptarr.Console.dll` on Windows.

## Documentation
- [UI localization roadmap](docs/localization-roadmap.md): current locale order, translated high-use strings, and remaining catalog work

- Default ChaptarrNG web UI: http://localhost:8789
- Default username/password: Set on first launch
- [Fork changes and capabilities](docs/FORK_FEATURES.md): feature overview and links to detailed setup and API references
- [Import-list preview and history](docs/IMPORT_LIST_SYNC.md): dry-run behavior, saved sync outcomes, retention, and refresh settings
- [External library integrations](docs/EXTERNAL_LIBRARY_INTEGRATIONS.md): setup and synchronization boundaries for AudioBookShelf, Calibre, Grimmory, and Emby/Jellyfin
- [MQTT Gateway](docs/MQTT_GATEWAY.md): broker settings, Home Assistant discovery, snapshots, events, and canonical webhook requests
- [Prometheus metrics](docs/PROMETHEUS_METRICS.md): listener configuration, scrape setup, metric names, and network exposure
- [ImageSharp build-license guide](docs/development/imagesharp-license.md): why source builds need a Six Labors license, how to obtain one, and where to configure it
- [Validation engine guide](docs/development/validation-engine.md): local and CI validation tasks, progress reporting, and receipt checks
- [Changelog](CHANGELOG.md): release-by-release changes shipped by the fork
- [SeerrNG Bookshelf backend guide](https://github.com/snapetech-org/seerrng/blob/main/docs/using-seerr/bookshelf-backend.md): setup and request lifecycle
- [SeerrNG integration contract](docs/SEERRNG_INTEGRATION.md): capabilities, service-key setup, and integration smoke test
- [API identity and lifecycle](docs/API_IDENTITY_AND_LIFECYCLE.md): compatibility details for maintainers

## Contributing

ChaptarrNG is a community project, and we welcome contributions! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

## Bug Reports & Features

Found a bug or have a feature request? Please open an issue on GitHub with:
- Clear description of the issue/feature
- Steps to reproduce (for bugs)
- Logs if applicable
- Your environment details

## Security

ChaptarrNG inherits the Readarr-derived operational model and security work from Chaptarr. Its current protections include:

- Constant-time API-key comparison
- Login brute-force throttling
- Inbound security headers including HSTS, CSP, X-Frame-Options, X-Content-Type-Options, and Referrer-Policy
- Image proxy target validation before fetching remote images
- API responses redact provider secrets
- No analytics or crash reporting is enabled
- Optional passphrase-encrypted Quickstart Settings Backups
- Optional authenticated encryption for full backup archives
- Update binaries are verified by SHA256 before install

CI runs CodeQL and Trivy dependency/secret scans. Published container images
include a BuildKit SBOM and provenance metadata, plus a GitHub build-provenance
attestation. The Compose example also enables `no-new-privileges` and binds
the published web port to loopback by default.

The frontend build uses a local `braces` parser depth limit while its upstream
package has no published fix for GHSA-vfj7-8cjw-p6xm. See the
[dependency security notes](docs/dependency-security.md) for the patch details,
audit commands, and removal criteria.

Use the `X-Api-Key` header for API clients. ChaptarrNG still accepts the legacy
`apikey` query parameter for compatibility, so reverse proxies should redact
that parameter from access logs.

Traditional full backups contain the database and config file, including credentials. Without the setting below they are stored as plain ZIP files; protect them as secrets.

To encrypt scheduled, update, and manual full backups, store a randomly
generated passphrase with at least 16 non-whitespace characters in a secret
file, mount it read-only in the ChaptarrNG container, and set
`CHAPTARR_BACKUP_ENCRYPTION_KEY_FILE` to its in-container path (for example,
`/run/secrets/chaptarr_backup_passphrase`). The Compose example includes a
commented Docker secret setup. Backups are then written as authenticated,
chunk-encrypted `.zip.enc` files; existing `.zip` backups remain restorable.
Mount the same secret file on a replacement instance before restoring an
encrypted backup. Keep the secret outside `Config.xml` and the backup archive;
losing it makes those encrypted backups unrecoverable. Enabling encryption does
not convert existing `.zip` backups, so protect or remove those separately.
With the file path unset, new backups retain the legacy plain ZIP format.
Protect the secret file through your container secret manager, and retain old
secret files if you need to restore archives made with them.
On POSIX systems, backup staging, temporary archives, and restore extraction
directories are restricted to the application owner while the files are in use.

The Compose example publishes its web port on `127.0.0.1` by default. Set
`CHAPTARRNG_BIND_ADDRESS=0.0.0.0` in the host environment to publish it on all
host interfaces, or use a shared Docker network for container-to-container
access without publishing the port.

## Privacy

ChaptarrNG uses `api2.chaptarr.com` for metadata and matching. Metadata requests may include provider IDs, search text, media type, selected audio/eBook tags, and the file name being matched. Full file paths, user identity, and indexer or download-client credentials are not sent. Update checks send version, OS, architecture, and runtime information.

Please see [SECURITY.md](SECURITY.md) for reporting security vulnerabilities.

## Acknowledgments

ChaptarrNG is a fork of [Chaptarr](https://github.com/Chaptarr/chaptarr), which is itself an independent fork of [Readarr](https://github.com/Readarr/Readarr), from the [Servarr](https://wiki.servarr.com/) project family. This project builds on work by Chaptarr, Readarr, and the Servarr contributors to Readarr, Sonarr, Radarr, Lidarr, and Prowlarr. Thank you.

Audiobook conversion is powered by [m4b-tool](https://github.com/sandreas/m4b-tool) (by sandreas), which builds on [FFmpeg](https://ffmpeg.org/) and [mp4v2](https://github.com/enzo1982/mp4v2). These are bundled in the ChaptarrNG Docker image under their respective licenses.

See [COPYRIGHT.md](COPYRIGHT.md) for full attribution and copyright details.

## Disclaimer

ChaptarrNG is independently maintained. It is **not affiliated with, endorsed by, or supported by** the Servarr team or the Readarr, Sonarr, Radarr, Lidarr, or Prowlarr projects — please don't send ChaptarrNG support requests their way.

ChaptarrNG is provided under the GNU GPL v3 with no warranty (see the [License](#license) section below).

## AI Development Disclosure

ChaptarrNG is developed and maintained with the assistance of AI tools.

## License

- [GNU GPL v3](https://www.gnu.org/licenses/gpl.html)
- Copyright © 2026 Snapetech and SeerrNG contributors for fork-specific changes
- Portions copyright © 2010–2026 the Servarr team and contributors

See the [LICENSE](LICENSE) file for the full license text and [COPYRIGHT.md](COPYRIGHT.md) for attribution details.
