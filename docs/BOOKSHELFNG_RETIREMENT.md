# BookshelfNG retirement status

**Reviewed:** 2026-10-18 after the older-schema migration work reached
ChaptarrNG `main`. This page is the current handoff summary for moving
BookshelfNG users to ChaptarrNG. The
detailed feature inventory remains in the
[BookshelfNG parity roadmap](https://github.com/snapetech-org/bookshelfng/blob/main/docs/maintainers/bookshelfng-parity-roadmap.md).

## Current releases

- ChaptarrNG `v0.9.976` is published with the standard image, migration tools,
  and standalone release archives.
- ChaptarrNG `main` now includes a bundled BookshelfNG SQLite migration chain
  for older recognized source schemas. The change is not in a public release
  yet; it is intended for `v0.9.977` after current-head CI and release checks.
- BookshelfNG `0.4.21.71` is published as the final source release.
- The BookshelfNG `.71` tag contains issue #114's optional new-author path
  setting. ChaptarrNG has equivalent new-author path initialization in
  `v0.9.974` and later. ChaptarrNG applies configured format defaults
  automatically; it has no separate toggle. Explicit author roots are kept,
  ambiguous or missing defaults stay blank, and existing authors or files are
  not changed.

## Database migration contract

The published ChaptarrNG `v0.9.976` converter accepts the BookshelfNG
migration-158 SQLite database. The `main` candidate adds the released
BookshelfNG SQLite migration chain: it upgrades a disposable copy of a
recognized source schema no newer than migration 158, then imports that copy.
The untouched snapshot remains the rollback source. A synthetic v22 fixture
passes upgrade, conversion, rollback, and byte-for-byte preview checks; schema
159 is rejected without changing the source. This is not real operator-data
evidence or proof for every historical release. Unknown schemas, PostgreSQL
source databases, and two populated BookshelfNG databases remain unsupported.
ChaptarrNG stages a native database, accounts for source rows, checks SQLite
integrity and foreign keys, and promotes only after validation. Preview does
not modify the source. Apply and rollback are explicit operations, and normal
startup can run the same converter when it finds a supported source database.

Some BookshelfNG books do not contain enough data to infer ebook versus
audiobook. Preview writes a private review file. Operators can provide a
complete, source-matched media map before conversion; with no map, conversion
keeps each unresolved work in both ChaptarrNG catalogs, unmonitored, and
leaves a private review file for classification after startup. This avoids
speculative searches and keeps conversion moving, but does not infer a work's
true format. A single monitored
edition of each format is preserved on its corresponding ChaptarrNG record;
multiple monitored editions of the same format still fail closed. The
populated-library rehearsal accounted for all 127,177 rows across 39 source
tables; 81 book classifications still required human review, and the test
choices were not reviewed as correct library decisions.

The public `v0.9.976` release does not yet accept older schemas; use the final
BookshelfNG release to upgrade to migration 158 until `v0.9.977` is published.
The candidate does not accept PostgreSQL databases or two populated
BookshelfNG databases at once. The BookshelfNG helper archives appdata, but
media/download volumes and settings stored outside appdata need separate
handling. Keep the BookshelfNG service stopped, retain an independent backup,
and follow the
[migration guide](MIGRATE_FROM_BOOKSHELFNG.md).

## Legacy install channels

| Channel | Published ChaptarrNG state | BookshelfNG handoff state |
| --- | --- | --- |
| Docker / GHCR | `ghcr.io/snapetech-org/chaptarrng:0.9.976` is published. | Legacy BookshelfNG aliases have not been redirected. A mutable tag only affects a later pull/recreate; pinned tags and digests need an operator change. |
| AUR | `chaptarrng-bin 0.9.976-1` is published. | `bookshelfng-bin 0.4.21.71-1` remains on the BookshelfNG payload; the old identity has not been upgraded to ChaptarrNG. |
| Fedora COPR | `chaptarrng 0.9.976-1.fc44` and `0.9.976-1.fc46` are published for x86_64. | The BookshelfNG bridge trigger was skipped. |
| Chocolatey | The `0.9.976` package built and passed its upgrade smoke, but publication returned HTTP 403; the public ChaptarrNG package remains `0.9.973`. | BookshelfNG `0.4.21.70` remains. Its identity was not included because the bridge is disabled. |
| Unraid | The ChaptarrNG template is maintained separately. | Existing BookshelfNG templates and containers still need an explicit image/template update. |
| Other historical channels | ChaptarrNG publishes its standard release artifacts. | PPA, Flatpak, AppImage, Helm, standalone installers, third-party catalogs, pinned/offline installs, and manually managed services do not all have a tested identity-preserving bridge. |

The compatibility package and image publishers are implemented behind the
`CHAPTARRNG_MIGRATION_BRIDGE_ENABLED` gate, which is `false`. The
scheduled BookshelfNG bridge workflow confirmed that it found a stable
ChaptarrNG release but skipped publishing because the gate is off. Do not turn
the gate on until the feature and channel acceptance items below are closed.

BookshelfNG's in-app updater checks the Readarr-owned update service, which
Snapetech cannot redirect. No single server-side change can move every pinned,
offline, third-party, or manually installed copy automatically.

## Retirement acceptance

BookshelfNG is not ready to retire solely because the converter and a
ChaptarrNG release exist. The remaining work is:

1. Close each BookshelfNG feature row with behavioral and converted-data
   evidence, or record an explicit maintainer decision to retire that behavior.
2. Publish and validate older-schema migration support, then validate
   old-identity AUR, COPR, Chocolatey, Helm, container, and Unraid
   upgrades through install, conversion, restart, backup retention, and
   rollback.
3. Resolve the Chocolatey HTTP 403 and independently verify the COPR build;
   verify each published package checksum or image digest against the tested
   artifact.
4. Decide and document handoffs for historical channels that cannot be
   redirected. Preserve a manual migration path for pinned and offline installs.
5. Run the final migration against representative operator data, review every
   ambiguous media choice, and verify external clients against the converted
   library before enabling automatic aliases.

The conversion preview, source-side archive helper, synthetic conversion and
rollback checks, and package templates are present. Full feature parity,
representative real-world rollback, all legacy package publications, and a
universal automatic update path are not established. See the
[BookshelfNG operator guide](https://github.com/snapetech-org/bookshelfng/blob/main/docs/migrate-from-bookshelfng-to-chaptarrng.md)
and the linked parity roadmap for the detailed evidence ledger.
