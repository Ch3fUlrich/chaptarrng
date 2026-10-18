# Embedded BookshelfNG SQLite migrations

The migration classes in this directory are copied from
BookshelfNG `main-v0.4.21.71` at commit
`3fad9427d167ed69d30ca6ed55f40262c2846487` under GPL-3.0. They run only on a
disposable copy of a BookshelfNG SQLite database, before the ChaptarrNG data
importer runs. The migration controller filters to this namespace, so these
version numbers cannot run against a ChaptarrNG database.

The converter keeps the consistent source snapshot untouched for rollback,
copies it to a temporary migration file, applies the released BookshelfNG
migration chain there, and requires the resulting schema to pass the importer’s
migration-158 checks. Preview removes both temporary files without changing
the source. Apply retains the untouched original schema as the rollback backup.

The copied migrations include PostgreSQL timestamp-type changes. Those type
changes are skipped for the SQLite snapshot because SQLite stores these values
with dynamic typing and the released BookshelfNG SQLite schema already uses
`DATETIME` affinity. New timestamp columns use that same SQLite-compatible
type. The isolated migration runner also includes BookshelfNG’s SQLite table
rebuild processor for its older alter, rename, and drop-column migrations.

Only a BookshelfNG database with the expected Bookshelf lineage and a schema
version no newer than migration 158 is eligible. The migration runner upgrades
the disposable copy; the importer then validates its final table set and
version. Unknown/newer schemas and PostgreSQL sources fail closed. The
automatic compatibility path does not rewrite or normalize the original
database before it is retained.

When updating this embedded migration chain, keep the original source tag and
commit recorded above, review SQLite-specific behavior, and extend the
older-schema conversion, non-mutating preview, rollback, and current-schema
fixtures in `src/Chaptarr.Core.Test/Datastore/BookshelfDatabaseImporterFixture.cs`.
