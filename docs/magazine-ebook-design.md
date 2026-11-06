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
dedicated IssueIdentifier/catalog title. Never compare dates as floating-point series numbers.
Identity is (series, issue identifier), so equal dates in different series do not
collide. Combined/special editions need explicit catalog entries and identifiers.

Complete means every explicitly expected, monitored issue through an operator
chosen cutoff has an accepted Ebook file. Gaps are expected identifiers minus
owned identifiers, per series, not inferred missing days between publication dates.
The parity fixture demonstrates this set comparison for two numbered series.
Cadence inference, publisher metadata feeds, automatic issue catalog creation,
archive extraction/tag writing, UI completeness dashboards and cutover are out of
scope. Names-only inventory parity does not establish full import parity.

## D-4157 validation and limits

The names-only 2026-11-06 handoff is embedded verbatim in
`IllustratedRealSampleFixture.cs`: 20 comics, 20 manga, 20 magazines.
`IllustratedLibraryListing.List` preserves each filename and its operator-provided
library type; this is an inventory, not inferred genre or metadata identification.
The two-series test uses the actual All About History and All About Space paths
and production expected-minus-owned computation, including a deliberately missing issue.
No live host or NFS was contacted.

Entries that cannot supply individual issue identity:
- `13DL.APP_Cardcaptor Sakura Clear Card Arc v01-03`: extensionless multi-volume bundle.
- `13DL.APP_Fujiko F. Fujio Daizenshu Doraemon v01-05`: extensionless multi-volume bundle.
- `Thinking Fellers Union Local 282 - Tangle - BM-07liners-web.pdf`: album liner notes, not a magazine issue.
- `Little Caesar - Francis Edwards Faragoh {supplement to the July 2001 issue of Sight And Sound}.pdf`: supplement, not an independent magazine issue.
Other dated/title-only PDFs remain inventory entries requiring canonical catalog
metadata; no permissive filename pattern fabricates identities for them.

Migration 124 uses frozen definitions, rejects conflicting reserved IDs, preserves
custom settings, skips malformed/null JSON, and adds missing formats disabled.
Its SQLite integration fixture covers grouped JSON, invalid quality values,
non-ebook profiles, unchanged complete profiles and custom settings.

Review finding rejected: MediaFileExtensions already derives TextExtensions and
IsSingleFileBookContainer from the extension map. A CBZ is one on-disk book
container (as is EPUB), not a claim that it contains only one image. Assertions retained.
Full import/catalog matching, cadence inference, and deployment remain out of scope.
