# Agent instructions - ChaptarrNG

## Communication and authority

- Be direct and accurate. Correct mistaken premises when evidence supports it, and say when something is unknown.
- Use explicit confidence levels for claims and estimates: high, moderate, low, or unknown.
- Distinguish implemented behavior, automated checks, visual evidence, and live integration evidence.
- Preserve upstream attribution and follow CONTRIBUTING.md and the repository license.
- Do not publish, merge, deploy, or create a release without maintainer authorization. A request for a named action does not authorize unrelated changes.

## Required project reading

- Read CONTRIBUTING.md, README.md, and docs/FORK_FEATURES.md before product changes.
- Read docs/SEERRNG_INTEGRATION.md and docs/API_IDENTITY_AND_LIFECYCLE.md before changing the SeerrNG capability contract, provider identity, format-specific routes, pending imports, or file organization.
- Read docs/development/validation-engine.md before choosing or reporting validation.
- Read docs/development/imagesharp-license.md before building from source or changing the ImageSharp dependency.
- Read release-notes/README.md before preparing a pull request for changes to user-facing behavior, security, operations, or user-facing documentation.

## ChaptarrNG boundaries and invariants

- ChaptarrNG is a standalone combined ebook and audiobook manager descended from Chaptarr and Readarr. SeerrNG remains an optional integration.
- Treat the original Chaptarr and Readarr repositories as read-only references. Keep existing upstream copyright headers on upstream files and use the documented fork attribution for new fork-owned files.
- Preserve the compatible Chaptarr API identity used by existing clients. ChaptarrNG is the repository and release identity; do not rename compatibility identifiers without a documented compatibility decision.
- Keep the selected ebook or audiobook format and durable metadata-provider identity attached through lookup, search, add, monitoring, pending import, and library reads. Provider IDs are external identities; local database row IDs are not.
- Pending author preparation may be shared by multiple active requests. Retry or cancel only when the operation is still safe for every request that depends on it; do not remove another request's work.
- Keep the SeerrNG service key limited to the integration routes documented in the capability contract. Do not expose administrator settings, deletes, file removal, or unrelated commands through that credential.
- Direct Download must keep staging durable, preserve the normal import path, validate redirects, reject unsafe destinations, and retain TLS verification. Do not use live download sources or real API keys in tests.
- Treat database migrations and backup restoration as data-compatibility work. Use disposable databases and fixtures; never test against an operator's library or production database.
- Source builds require a valid Six Labors license. Keep the license file and SIXLABORS_LICENSE_KEY out of commits, logs, validation receipts, and pull requests; unset the variable before starting the validation engine as documented.
- A post-commit package hook can sync and push packaging/unraid to its companion repository. Inspect the hook before commits that touch package source, and do not allow an unrelated commit to publish package changes as a side effect.

## Validation and UI evidence

- Use Node 26.10.0 and .NET SDK 10.0.401 as declared in docs/development/validation-engine.md.
- Follow the repository validation engine for plan, tests-only, and full runs. A plan discovers tasks but does not execute tests. Report the actual tasks, native counts, skips, exclusions, and any unavailable prerequisite.
- Use the disposable PostgreSQL migration smoke only through the documented workflow fixtures. Do not point tests at live services or databases.
- CONTRIBUTING.md requires screenshots for UI changes. Capture the changed interface running and include the evidence in the pull request; tests and type checks do not prove visual behavior.
- If the ImageSharp license is unavailable, report the blocked build accurately rather than bypassing licensing or claiming a complete validation pass.

## Release notes

Use the fragment schema, immutable-history rules, and preview command in release-notes/README.md. For an internal-only pull request, select the internal-only option in the pull request template. Do not add a user-facing fragment for a change that has no user-visible effect.
