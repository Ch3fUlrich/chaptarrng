# Illustrated publications (D-3454 option b)

Comics, manga and magazines remain Ebook books/editions/files. CBZ, CBR and
CB7 are container qualities, not media types or conversion targets. Filename
series/position evidence does not create authors or metadata records; operators
must supply matching catalog metadata before automatic identification/import.
Volumes (`v03`, `Vol. 12`, `Band 12`) and bare issue numbers become series
positions. A manga chapter is separate evidence, never a decimal volume number.

## Magazine completeness contract

One magazine is a series; one issue is a book. Use an operator-curated canonical
issue identifier: `YYYY-NNN` for numbered issues, or ISO `YYYY-MM-DD` for dated
issues. Keep the publication date on the book and the identifier in the series
position/catalog title. Never compare dates as floating-point series numbers.
Identity is (series, issue identifier), so equal dates in different series do not
collide. Combined/special editions need explicit catalog entries and identifiers.

Complete means every explicitly expected, monitored issue through an operator
chosen cutoff has an accepted Ebook file. Gaps are expected identifiers minus
owned identifiers, per series, not inferred missing days between publication dates.
The parity fixture demonstrates this set comparison for two dated series.
Cadence inference, publisher metadata feeds, automatic issue catalog creation,
archive extraction/tag writing, UI completeness dashboards and cutover are out of
scope. Real-library parity remains pending: all fixture paths are synthetic.

## Validation status

Implementation is incomplete/unverified. Local .NET 10.0.400 restore succeeded;
the test-project build under `systemd-run --user --scope -p MemoryMax=4G` failed
because ImageSharp 4.1.3 requires a Six Labors license. No secret paths were
accessed and no license check was bypassed. The 64 new cases have not run.
The 60 synthetic cases cover parsing/classification/container acceptance, not
the full metadata identification/import service. Migration 124 still needs a
database integration test (including grouped profile JSON and preservation of
custom settings). Do not deploy or claim end-to-end library parity from this patch.
