# BookshelfNG retirement status

**Reviewed:** 2026-10-17. This page is the current handoff summary for moving
BookshelfNG users to ChaptarrNG. The detailed feature inventory remains in the
[BookshelfNG parity roadmap](https://github.com/snapetech-org/bookshelfng/blob/main/docs/maintainers/bookshelfng-parity-roadmap.md).

## Current releases

- ChaptarrNG `v0.9.975` is published with the standard image, migration tools,
  and standalone release archives.
- BookshelfNG `0.4.21.71` is tagged as the final source release. Its GitHub
  release is still a draft while the standalone archives are being built; the
  latest published BookshelfNG release is `0.4.21.70`.
- The BookshelfNG `.71` tag contains issue #114's optional new-author path
  setting. ChaptarrNG has equivalent new-author path initialization in
  `v0.9.974` and later. ChaptarrNG applies configured format defaults
  automatically; it has no separate toggle. Explicit author roots are kept,
  ambiguous or missing defaults stay blank, and existing authors or files are
  not changed.

## Database migration contract

ChaptarrNG accepts the BookshelfNG migration-158 SQLite database. It snapshots
the source, stages a native ChaptarrNG database, accounts for source rows,
checks SQLite integrity and foreign keys, retains the source database, and
promotes the result only after validation. Preview does not modify the source.
Apply and rollback are explicit operations, and ordinary ChaptarrNG startup
can apply the same converter when it finds a supported source database.

Some BookshelfNG books do not contain enough data to infer ebook versus
audiobook. Preview writes a private review file and waits for a complete,
source-matched media map. The populated-library rehearsal accounted for all
127,177 rows across 39 source tables; 81 book classifications still required
human choices, and the test choices were not reviewed as correct library
decisions. Operators must review their own map before applying.

The converter does not currently accept older BookshelfNG schema versions,
PostgreSQL databases, or two populated BookshelfNG databases at once. The
BookshelfNG helper archives appdata, but media/download volumes and settings
stored outside appdata need separate handling. Keep the BookshelfNG service
stopped, retain an independent backup, and follow the
[migration guide](MIGRATE_FROM_BOOKSHELFNG.md).

## Legacy install channels

| Channel | Published ChaptarrNG state | BookshelfNG handoff state |
| --- | --- | --- |
| Docker / GHCR | `ghcr.io/snapetech-org/chaptarrng:0.9.975` is published. | Legacy BookshelfNG aliases have not been redirected. A mutable tag only affects a later pull/recreate; pinned tags and digests need an operator change. |
| AUR | `chaptarrng-bin 0.9.975-1` is published. | `bookshelfng-bin 0.4.21.70-1` remains; the old identity has not been upgraded to the ChaptarrNG payload. |
| Fedora COPR | The standard `slskdn/chaptarrng` `0.9.975-1` build succeeded. | The BookshelfNG bridge build was not triggered. |
| Chocolatey | ChaptarrNG `0.9.975` publication returned HTTP 403; the public package remains `0.9.973`. | BookshelfNG `0.4.21.70` remains. The Windows upgrade smoke passed, but the migration package has not been published. |
| Unraid | The ChaptarrNG template is maintained separately. | Existing BookshelfNG templates and containers still need an explicit image/template update. |
| Other historical channels | ChaptarrNG publishes its standard release artifacts. | PPA, Flatpak, AppImage, Helm, standalone installers, third-party catalogs, pinned/offline installs, and manually managed services do not all have a tested identity-preserving bridge. |

The compatibility package and image publishers are implemented behind the
`CHAPTARRNG_MIGRATION_BRIDGE_ENABLED` gate, which remains disabled. The
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
2. Validate old-identity AUR, COPR, Chocolatey, Helm, and container upgrades
   through install, conversion, restart, backup retention, and rollback.
3. Resolve the Chocolatey publisher authorization failure and verify every
   package's published checksum or image digest against the tested artifact.
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
