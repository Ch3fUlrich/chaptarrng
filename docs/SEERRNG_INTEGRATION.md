# SeerrNG integration

ChaptarrNG exposes its Bookshelf API to SeerrNG using the Readarr-compatible
API shape. The integration capability contract is available at
`GET /api/v1/system/capabilities`; SeerrNG uses it to select the provider-ID
dialect, format-scoped routes, paged library endpoint, edition identity rules,
pending author-import support, restricted-key support, and format-specific
availability statistics.

For a broader overview of maintained behavior beyond the shared Chaptarr base,
see [ChaptarrNG fork changes and upstream replacement criteria](./FORK_FEATURES.md).
Chaptarr's current development branch has its own pending-import subsystem;
this guide focuses on the format, identity, capability, and service-key
contract SeerrNG uses.

## Configure a dedicated service key

ChaptarrNG `0.9.941` and later can use a dedicated credential for SeerrNG.
Configure `CHAPTARR__AUTH__SEERRAPIKEY` in the ChaptarrNG container or secret
manager, then use its value as the API key in SeerrNG under **Settings →
Services → Bookshelf**. Keep the regular ChaptarrNG API key separate; it keeps
its full administrative access.

The SeerrNG credential can inspect book libraries, profiles, queue and history,
look up books and authors, add books, update an individual book, run a single
book search, read pending author imports, and cancel a pending author import.
It can access the native `/api/v1` routes and the advertised
`/readarr/{dialect}/{mediaType}/api/v1` compatibility routes. Media covers are
available so the SeerrNG UI can display book artwork.

The credential cannot change global settings, delete books or authors, delete
files, run other commands, or use author media-move operations. Those routes
remain available only through the regular API key or an administrator session.
SeerrNG's standard request, search, status, and pending-import workflows use
the restricted route set.

For older ChaptarrNG releases, use the regular API key. The separate service
key is an additive option and does not change existing authentication.

## Verify a local integration build

The SeerrNG repository includes `scripts/chaptarrng-integration-smoke.mjs`.
Build its server first with `pnpm build:server`, then run the integration
Compose file from the ChaptarrNG repository with `SEERRNG_SOURCE` set to the
absolute SeerrNG checkout path and distinct random values for
`CHAPTARR_GLOBAL_API_KEY` and `CHAPTARR_SEERR_API_KEY`:

```sh
SEERRNG_SOURCE=/path/to/seerrng \
CHAPTARR_GLOBAL_API_KEY='use-a-separate-long-random-key' \
CHAPTARR_SEERR_API_KEY='use-another-long-random-key' \
docker compose -f integration/seerrng-smoke.compose.yml \
  up --build --no-attach chaptarrng \
  --abort-on-container-exit --exit-code-from smoke
```

The smoke test connects from a separate container, confirms SeerrNG can read
ChaptarrNG status and the paged book library through its format-scoped API,
and confirms the service key is rejected for administrative and unrelated
command routes. Remove the test stack afterward with `docker compose -f
integration/seerrng-smoke.compose.yml down -v`.
