# Soulseek (slskd) indexer and download client

Status: design for branch `homelab/ng-slskd` (operator order D-2750). Pinned to
slskd 0.26.2 (API version `v0`); API shapes below were read from the slskd 0.26.2
sources (`SearchesController`, `TransfersController`, `Transfer`, `Search`,
`Response`, `File`, `Batch`, `Application State`), not from memory.

## Goal

Soulseek as a last-resort source: Usenet/torrent indexers are tried first, slskd
only when they produce nothing acceptable. Chaptarr searches slskd (via its REST
API, Chaptarr never speaks the Soulseek protocol), turns peer folders/files into
releases, enqueues the chosen release as one slskd batch, polls it and hands the
finished folder to the normal completed-download import.

## Files and classes

| File | Class | Role |
|---|---|---|
| `Indexers/DownloadProtocol.cs` | `DownloadProtocol.Soulseek = 4` | new protocol; clients are routed per protocol |
| `Soulseek/SlskdModels.cs` | DTOs | JSON shapes of slskd (camelCase, case-insensitive) |
| `Soulseek/SlskdApiClient.cs` | `ISlskdApiClient` | thin typed HTTP wrapper, `X-API-Key` header, no business logic |
| `Soulseek/SlskdTextMatcher.cs` | static | token/umlaut-folded title and author matching, language hints |
| `Soulseek/SlskdReleaseBuilder.cs` | static | search responses -> `ReleaseInfo` (gates, scoring, caps) |
| `Soulseek/SlskdDownloadToken.cs` | static | encode/decode what an enqueue needs into `ReleaseInfo.DownloadUrl` |
| `Indexers/Soulseek/SlskdIndexer.cs` + `SlskdIndexerSettings.cs` | `SlskdIndexer` | search orchestration, test |
| `Download/Clients/Slskd/SlskdDownloadClient.cs` + `SlskdDownloadClientSettings.cs` | `SlskdDownloadClient` | enqueue, poll, import path, remove, test |

## Invariants

1. The indexer never downloads; the client never searches.
2. A release is self-describing: `DownloadUrl = slskd://soulseek/<base64url(json)>`
   carries username, remote directory and `(relative name, size)` of every file. No
   search id is needed at grab time (slskd search results expire).
3. One release = one slskd batch = one destination folder. Ebook releases are a
   single file; audiobook releases are a folder (multi-disc subfolders merge into
   their parent folder).
4. `DownloadId` is a GUID that Chaptarr generates and passes as the batch `Id`;
   the client keeps no state of its own and survives restarts: it lists slskd
   transfers and keeps batches whose `options.externalId` starts with
   `chaptarr|<client id>|`. The external id also carries the release title.
5. The destination folder is deterministic: `<download dir>/chaptarr-<id N-format>`;
   `Options.Destination` is sent explicitly so slskd's own subdirectory pattern
   does not matter. The download dir setting is the path as Chaptarr sees it
   (slskd and Chaptarr usually mount it at different paths).
6. A batch is only `Completed` when every transfer succeeded and the destination
   folder exists with at least one file. Any failed/rejected/cancelled/timed-out
   transfer fails the whole item (Chaptarr blocklists the release, next one is tried).
7. Locked files and zero-size files are never offered. Paths containing `..` are
   rejected on both sides (slskd also rejects them).
8. Requires slskd >= 0.26.0 (batch endpoints); the client test checks the version.
9. No secret is logged; the API key field uses `PrivacyLevel.ApiKey`.

## Search flow (slice 1)

1. `POST /api/v0/searches` `{id, searchText, searchTimeout (ms), fileLimit,
   filterResponses, minimumResponseFileCount, maximumPeerQueueLength,
   minimumPeerUploadSpeed}`. slskd allows ONE concurrent search request (429
   otherwise): the indexer serialises its own searches with a semaphore and treats
   429/409 as "no result this time".
2. Poll `GET /api/v0/searches/{id}` until `isComplete` or deadline
   (timeout + 10 s); then `GET /api/v0/searches/{id}/responses`.
3. `DELETE /api/v0/searches/{id}` always (finally).
4. `SlskdReleaseBuilder` maps responses to releases: gates, language, score, cap.

### Input table: slskd search API

| Call | Success | Failure/edge | Behaviour |
|---|---|---|---|
| POST /searches | 200 `Search` (`id`, `state`, `isComplete`, ...) | 400 bad text; 409 invalid operation (not connected); 429 another search running; 5xx | 400/409/429 -> empty result + warn, indexer status not penalised for 429; 5xx -> failure |
| GET /searches/{id} | 200 `Search` | 404 | 404 -> treat as lost, empty |
| GET /searches/{id}/responses | 200 `[Response]` | 404 | as above |
| Response | `username`, `hasFreeUploadSlot`, `uploadSpeed` (bytes/s), `queueLength`, `files[]`, `lockedFiles[]` | `files` empty | skipped |
| File | `filename` (remote path, backslash or slash separated), `size`, `extension`, `bitRate`, `length`, `isLocked` | `size` 0, no extension | extension derived from `filename`; size 0 skipped |
| DELETE /searches/{id} | 204 | 404 | ignored |
| GET /application | 200 state: `version.current`, `server.state` (e.g. `Connected, LoggedIn`) | 401 bad key | used by Test |

## Release mapping

* Ebook: each file with extension `epub azw3 mobi pdf` (>= 20 KB) is a release
  titled `Author - Title [ext]`, `Container = ext`, protocol Soulseek.
* Audiobook: files with extension `mp3 m4b m4a flac ogg opus aac` grouped by
  directory (disc/part subfolders folded into the parent). Folder total >= setting
  (default 80 MB). Album guard: >= 8 files with average < 2.5 MB is rejected unless the
  author surname is in the path. `Container` = dominant extension (`m4b` preferred).
* Match gates (configurable): title token ratio >= 50 % (umlaut folded both ways:
  `ae/oe/ue/ss` and plain `a/o/u/s`), author surname in path (default required for
  audiobooks and ebooks, switchable).
* Language: German markers (`deutsch`, `german`, `hoerbuch`, `ungekuerzt`, `[de]`) /
  English markers set `ReleaseInfo.Languages`; allowed languages and
  "allow unknown language" are settings. The best release of each detected language
  is always kept when the result cap is applied.
* Free slot / queue / speed filters: `RequireFreeSlot`, `MaximumQueueLength`,
  `MinimumUploadSpeedKBps` are applied both in the slskd request and again in the
  builder.
* Score = title ratio * 100 + free slot 20 + m4b 10 (epub 5) - min(queue, 50) / 5
  + speed bonus (<= 5). Top `MaximumResults` (default 15) are returned.

## Download client flow (slice 2)

`Download(remoteBook)`: decode token, `POST /api/v0/transfers/downloads/batches`
`{id, username, files[{filename,size}], options:{destination, externalId}}`.
201/200 = ok, 207 = partial (fail: a partial folder is useless), 4xx/5xx ->
`ReleaseDownloadException`. Returns the batch id.

`GetItems()`: `GET /api/v0/transfers/downloads` (all users, grouped by user and
directory, `includeRemoved=false`), take transfers with a `batchId`, resolve each
distinct batch id once through `GET /transfers/downloads/batches/{id}` (cached;
foreign batches cached as foreign), keep ours, build one `DownloadClientItem` per batch.

### State table of a transfer (slskd `Transfer.state`, flag names comma separated)

| slskd state | Meaning | Item status |
|---|---|---|
| `Requested`, `Queued, Locally`, `Queued, Remotely`, `Initializing` | waiting | Queued (message shows queue position when known) |
| `InProgress` | transferring | Downloading |
| `Completed, Succeeded` | file done | counts towards completion |
| `Completed, Cancelled` / `TimedOut` / `Errored` / `Rejected` / `Aborted` | file failed | item Failed (message = state description / exception) |
| unknown / empty | defensive | Queued |

Batch rule: any failed transfer -> Failed; else all succeeded -> Completed (after
the folder check, invariant 6); else any InProgress -> Downloading; else Queued.
A batch with zero visible transfers (all removed in slskd) -> Warning with message.

`RemoveItem(item, deleteData)`: `DELETE /transfers/downloads/{username}/{transferId}?remove=true`
for each transfer of the batch (cancels when running); `deleteData` deletes the
destination folder via the disk provider (never anything outside the download dir).

`GetImportItem`: sets `OutputPath` = destination folder, `FilePaths` = files found,
`FileListConfidence = Authoritative`. `CanMoveFiles = false` (the folder is slskd's
share/download area; Chaptarr copies or hardlinks).

Settings: URL, API key, download dir as seen by Chaptarr, minimum free-slot/speed
filters are indexer-side (client has none that matter), `RemoveCompletedDownloads`
via the standard definition flags, test button (version >= 0.26.0, logged in,
download dir exists and readable).

## Fallback behaviour (slice 3)

Chaptarr already ranks competing releases by (title match, format, profile,
**protocol preference**, **indexer priority**, size, ...). With the delay profile
`PreferredProtocol = Usenet` (or torrent) every acceptable Usenet release outranks
every Soulseek release, so Soulseek is only grabbed when no Usenet release passes
the decision engine; set the slskd indexer's priority to 50 (higher number = lower
preference) to make this explicit. When a Usenet grab later fails and is
blocklisted, the next search finds only Soulseek releases and uses them. No extra
"fallback only after N empty searches" option is added because the priority
mechanism expresses it; the remaining cost (a slskd search runs on every search of
a wanted book) is bounded by `SearchTimeoutSeconds` and the single-search
semaphore. Interactive search still shows both.

Operator recipe: Settings > Profiles > Delay: preferred protocol Usenet; Indexers:
add "Soulseek (slskd)" with priority 50, "Enable RSS" off; Download Clients: add
"slskd" (protocol Soulseek).
