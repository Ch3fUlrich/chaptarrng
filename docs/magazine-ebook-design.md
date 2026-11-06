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

Validated locally on 2026-11-06 using the authorized inline build license.
The affected files were IllustratedEbookFixture.cs, ParserTitleParsingFixture.cs,
QualityParserTitleFormatEvidenceFixture.cs, MediaFileExtensionsFixture.cs and
QualityProfileControllerFixture.cs. Result: 131 passed, zero failed/skipped.
The illustrated parser now leaves ordinary PDF/EPUB/MOBI/AZW3 titles to the
existing parser. Schema assertions include the three new archive qualities.

The 60 synthetic cases cover parsing/classification/container acceptance, not
sandbox library listing or the full metadata identification/import service.
No real-library sample list was supplied in this run; obtain it through L1,
never by contacting a live service or NFS. CH-COMICS-SUPPORT is NOT done.
The two-series magazine test demonstrates synthetic set parity only; it does
not establish real-library parity. Migration 124 still needs a database
integration test (including grouped profile JSON and preservation of custom
settings). L1-managed review seats remain pending. Do not deploy or claim
end-to-end library parity from this patch.
