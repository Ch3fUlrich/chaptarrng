# Changelog

ChaptarrNG release notes highlight user-facing changes and operating guidance.
Each release combines curated entries from release-notes/ with technical
history grouped from conventional commits.

## [0.9.969](https://github.com/snapetech/chaptarrng/compare/v0.9.968..v0.9.969) - 2026-10-12

### User-facing changes

#### Fixed

- **Metadata:** Goodreads searches for titles with umlauts or other special characters now complete successfully when multiple requests arrive at the same time.

### Bug Fixes
- *(http-cache)* Enforce unique cached response URLs - ([82b81bf](https://github.com/snapetech/chaptarrng/commit/82b81bf270c10cfadff53ee4a2b6b62f3a52f315))

## [0.9.968](https://github.com/snapetech/chaptarrng/compare/v0.9.967..v0.9.968) - 2026-10-12

### User-facing changes

#### Changed

- **Build:** **Breaking:** ImageSharp 4.1.3 requires a valid Six Labors license when ChaptarrNG is built from source. Official images already include a licensed build, so no build key must be configured at runtime. All use remains subject to Six Labors' license terms.
  - **Action required:** Configure SIXLABORS_LICENSE_KEY when building from source; official images need no build key at runtime.
- **Validation:** ChaptarrNG CI reconciles source-bound receipts across Linux, Windows, macOS, and disposable PostgreSQL runners before completing validation. Fingerprints remain consistent across Git-normalized Windows line endings.

#### Fixed

- **Validation:** Local validation selects macOS and Windows test tasks correctly, and hosted reconciliation preserves receipts from every runner.
- **Distribution:** Manual ChaptarrNG releases now forward the private ImageSharp build license to validation and image publishing, allowing both reusable build workflows to run under this repository's account settings.
- **Release Workflow:** Grant release validation the read-only Actions permission needed to reconcile its test receipts, allowing GitHub to start the reusable workflow.

#### Security

- **Image Processing:** ChaptarrNG updates the licensed ImageSharp dependency to v4.1.3 and removes the audit suppressions previously required for v3.2.0 advisories.

### Security
- *(release-notes)* Update ImageSharp security note - ([93718b9](https://github.com/snapetech/chaptarrng/commit/93718b92baf0bdc48995466b493a4f4554f0bffb))

### Features
- *(testing)* Add source-bound validation engine - ([7c3637d](https://github.com/snapetech/chaptarrng/commit/7c3637d025af7307bcc28040cccb3094400375e0))

### Bug Fixes
- *(deps)* Update ImageSharp to 4.1.3 - ([6613a51](https://github.com/snapetech/chaptarrng/commit/6613a51d35f60b75fec42d88d2df5224e05f74de))
- *(release)* Pass receipt read permission - ([9b180d5](https://github.com/snapetech/chaptarrng/commit/9b180d5818b42906eaab25d185f04bb1af105bf7))
- *(release)* Forward ImageSharp license explicitly - ([2b202d8](https://github.com/snapetech/chaptarrng/commit/2b202d884cff0b777b2adf0f8fd608b17373bb1c))
- *(security)* Update ImageSharp to patched 3.2.0 - ([f5096e3](https://github.com/snapetech/chaptarrng/commit/f5096e3323b86c3ff85d4a45316fef9151f098f2))
- *(validation)* Preserve runner receipts during reconciliation - ([5308ac7](https://github.com/snapetech/chaptarrng/commit/5308ac7a28c5686b76fa0c35750bf948bf8a4fa0))
- *(validation)* Map local platforms to CI node IDs - ([cf3e111](https://github.com/snapetech/chaptarrng/commit/cf3e11142ce493e55928c98f52b4847e54a647d6))
- *(validation)* Assign test tasks to CI nodes - ([fd59dd3](https://github.com/snapetech/chaptarrng/commit/fd59dd3666aca541c5c184007193f398295360e7))
- *(validation)* Normalize fingerprints across platforms - ([6ced970](https://github.com/snapetech/chaptarrng/commit/6ced9702b30abe58dd70fac46a1a4d20926d795d))

### Documentation
- *(imagesharp)* Clarify runtime license terms - ([91d63f5](https://github.com/snapetech/chaptarrng/commit/91d63f584d0022f98e583045dce2ba09f7868be3))
- *(imagesharp)* Explain build licensing - ([733a6de](https://github.com/snapetech/chaptarrng/commit/733a6dea7b16202a6f212cc93f2e1a98cce22f1c))
- *(security)* Clarify remaining ImageSharp advisories - ([a1754ee](https://github.com/snapetech/chaptarrng/commit/a1754ee2f37bcbc8a0fbbb094e9842e89ab7c01b))

### Testing
- *(validation)* Isolate local plan node fixtures - ([db048b5](https://github.com/snapetech/chaptarrng/commit/db048b54a0cdb62f808a4610ba4b5f9295b04348))

## [0.9.967](https://github.com/snapetech/chaptarrng/compare/v0.9.966..v0.9.967) - 2026-10-07

### User-facing changes

#### Added

- **Integration:** SeerrNG capability discovery now advertises the ebook and audiobook availability statistics already exposed by ChaptarrNG, so integrations can select the format-aware data reliably.

## [0.9.966](https://github.com/snapetech/chaptarrng/compare/v0.9.942..v0.9.966) - 2026-10-06

### User-facing changes

#### Fixed

- **Navigation:** Fresh installations now reach the sign-in screen instead of failing while React Router initializes.

#### Security

- **Logging:** Log messages now escape line separators and redact sensitive values from download titles, filenames, and browser errors. The build also uses patched CSS selector and source-map parsers.

### Bug Fixes
- *(security)* Cleanse remaining import log values - ([9495f80](https://github.com/snapetech/chaptarrng/commit/9495f804d80d9f57cd93376d4a2c28d12294a1fb))
- *(security)* Update vulnerable frontend parsers - ([54be4d9](https://github.com/snapetech/chaptarrng/commit/54be4d9f763bc58db7dbd8edb8ee26af01c6dbc1))
- *(security)* Cleanse untrusted values before logging - ([0765225](https://github.com/snapetech/chaptarrng/commit/07652259b0719510eca30600eb88a96dc66595a7))
- *(web)* Restore router context and modernize client types - ([69f021f](https://github.com/snapetech/chaptarrng/commit/69f021f7b254c1681452cc4b4c92a13cdba131bb))

### Documentation
- Update fork history and download platform support (release-note: none) - ([839ef20](https://github.com/snapetech/chaptarrng/commit/839ef20c0338a1c8eb9bc0dc2ddd0051da5bf986))

### Testing
- *(search)* Model monitored ebook state - ([b66cb97](https://github.com/snapetech/chaptarrng/commit/b66cb97082b84483c8d92e9a870ac4afab5f93ba))

## [0.9.942](https://github.com/snapetech/chaptarrng/compare/v0.9.942..v0.9.942) - 2026-10-05

### User-facing changes

#### Added

- **Indexers:** Configure prioritized ebook download sources with API-key grabs and browser-assisted fallback. Chaptarr tracks transfer progress and imports supported book files directly, without requiring a separate download client.

#### Changed

- **Documentation:** The README and fork guide now map ChaptarrNG's format-aware request lifecycle, Direct Download flow, backup protection, and maintained packages to their setup and API references.

#### Fixed

- **Container Image:** The container build now installs only Playwright's headless Chromium shell on AMD64 and ARM64, avoiding OS dependency detection under emulation. ARMv7 images omit Chromium, while API-resolved direct downloads remain available.

### Bug Fixes
- *(docker)* Use Noble runtime library package names - ([8568c9c](https://github.com/snapetech/chaptarrng/commit/8568c9c170341f089aa522fa60c5461691f52d12))
## [0.9.942](https://github.com/snapetech/chaptarrng/compare/v0.9.941..v0.9.942) - 2026-10-05

### Bug Fixes
- *(release)* Build headless browser images across architectures - ([298f33a](https://github.com/snapetech/chaptarrng/commit/298f33a32aaf4274a70ce2a3c54cbefcc3f08171))
- Serialize direct download state persistence - ([521627e](https://github.com/snapetech/chaptarrng/commit/521627e29271197a3bc9b8a8df6fa7385d5f8b26))
- Wait for direct download cancellation cleanup - ([0aef210](https://github.com/snapetech/chaptarrng/commit/0aef210ca377f5d105b466bd9df0da0fb1943d1e))
- Close download stream before promoting staged file - ([13c1b65](https://github.com/snapetech/chaptarrng/commit/13c1b653882ea19850234cbd85f33a35038efd6a))
- Harden browser-assisted direct downloads - ([17ef911](https://github.com/snapetech/chaptarrng/commit/17ef9119ebbb69bf7c1cdd1912a1a378b6d57903))

### Documentation
- Complete fork feature history - ([4833664](https://github.com/snapetech/chaptarrng/commit/4833664364ccdbf54dbcf5e8fe76f55d22f85e9b))
- Format fork feature guide - ([c38b23a](https://github.com/snapetech/chaptarrng/commit/c38b23ad5d2ea50c1423016822bcf5c38daa7420))

### Testing
- Await direct download redownload before cleanup - ([472cded](https://github.com/snapetech/chaptarrng/commit/472cdedbf676e619128d9eb088a57b5c638f78e5))
- Gate Playwright browser fixtures explicitly - ([4519bbf](https://github.com/snapetech/chaptarrng/commit/4519bbfd61b692cb14b2413402e7c1e70882f9aa))

### Maintenance
- Isolate test assembly compilation - ([cbee1cb](https://github.com/snapetech/chaptarrng/commit/cbee1cb10dd38b142f0f5d845f7af9ef4c2de8d6))## [0.9.942](https://github.com/snapetech/chaptarrng/compare/v0.9.941..v0.9.942) - 2026-10-05

### Bug Fixes
- Serialize direct download state persistence - ([521627e](https://github.com/snapetech/chaptarrng/commit/521627e29271197a3bc9b8a8df6fa7385d5f8b26))
- Wait for direct download cancellation cleanup - ([0aef210](https://github.com/snapetech/chaptarrng/commit/0aef210ca377f5d105b466bd9df0da0fb1943d1e))
- Close download stream before promoting staged file - ([13c1b65](https://github.com/snapetech/chaptarrng/commit/13c1b653882ea19850234cbd85f33a35038efd6a))
- Harden browser-assisted direct downloads - ([17ef911](https://github.com/snapetech/chaptarrng/commit/17ef9119ebbb69bf7c1cdd1912a1a378b6d57903))

### Documentation
- Complete fork feature history - ([4833664](https://github.com/snapetech/chaptarrng/commit/4833664364ccdbf54dbcf5e8fe76f55d22f85e9b))
- Format fork feature guide - ([c38b23a](https://github.com/snapetech/chaptarrng/commit/c38b23ad5d2ea50c1423016822bcf5c38daa7420))

### Testing
- Await direct download redownload before cleanup - ([472cded](https://github.com/snapetech/chaptarrng/commit/472cdedbf676e619128d9eb088a57b5c638f78e5))
- Gate Playwright browser fixtures explicitly - ([4519bbf](https://github.com/snapetech/chaptarrng/commit/4519bbfd61b692cb14b2413402e7c1e70882f9aa))

### Maintenance
- Isolate test assembly compilation - ([cbee1cb](https://github.com/snapetech/chaptarrng/commit/cbee1cb10dd38b142f0f5d845f7af9ef4c2de8d6))## [0.9.941](https://github.com/snapetech/chaptarrng/compare/v0.9.940..v0.9.941) - 2026-10-04

### User-facing changes

#### Changed

- **Donations:** The Donations page now directs Ko-fi contributions to the Snapetech fork maintainer, clearly separating this fork from upstream Chaptarr.

#### Security

- **Integrations:** ChaptarrNG now supports a SeerrNG-specific service key that is limited to book requests, searches, library status, and pending author imports while keeping the normal API key available for full administrative access.
  - **Action required:** Configure a separate CHAPTARR__AUTH__SEERRAPIKEY value in the ChaptarrNG container and enter that value in SeerrNG's Bookshelf service settings.

### Features
- Add restricted SeerrNG service API key - ([113c5ff](https://github.com/snapetech/chaptarrng/commit/113c5ff4e4bf02b1e4bdf9ea857b62e632af592a))

### Maintenance
- Use Snapetech Ko-fi support link - ([756e012](https://github.com/snapetech/chaptarrng/commit/756e012b264ef4faf3591f539afb1888b51e1677))

## [0.9.940](https://github.com/snapetech/chaptarrng/compare/v0.9.939..v0.9.940) - 2026-10-04

### User-facing changes

#### Added

- **Integrations:** ChaptarrNG now exposes a versioned system capabilities endpoint that reports the Bookshelf API's provider-ID dialect and supported media types to compatible request managers such as SeerrNG.

#### Changed

- **Donations:** The Donations page now directs Ko-fi contributions to the Snapetech fork maintainer, clearly separating this fork from upstream Chaptarr.

#### Security

- **Backups:** Full backups can now be stored as authenticated encrypted archives when a backup passphrase is configured, while existing ZIP backups remain restorable.
  - **Action required:** Mount a secret file containing a long random passphrase and set CHAPTARR_BACKUP_ENCRYPTION_KEY_FILE to its in-container path. Protect or remove existing plain ZIP backups separately.

### Features

- Add SeerrNG capability discovery and encrypted backups - ([865e0b1](https://github.com/snapetech/chaptarrng/commit/865e0b14b6b26a4eeda8d8ce381d94ed4dba4346))

### Bug Fixes

- *(ci)* Rely on repository default CodeQL setup - ([319bceb](https://github.com/snapetech/chaptarrng/commit/319bcebbb3ec43b4558c737a8bcb9fd77ddea31c))

### Maintenance

- Use Snapetech Ko-fi support link - ([756e012](https://github.com/snapetech/chaptarrng/commit/756e012b264ef4faf3591f539afb1888b51e1677))

## [0.9.939](https://github.com/snapetech/chaptarrng/compare/v0.9.938..v0.9.939) - 2026-10-01

### User-facing changes

#### Fixed

- **App Experience:** NG-branded icons and bundled frontend assets refresh the app's presentation and downloads. Cookie-authenticated users can open the web UI, and unusually long Transmission ETAs no longer break queue responses.

### Features
- *(release)* Add changelog and Discord announcements (#39) - ([65887a0](https://github.com/snapetech/chaptarrng/commit/65887a0963626d9bb2545a2c9919208ea0ad0a49))
- Improve package branding and downloads - ([13d5e53](https://github.com/snapetech/chaptarrng/commit/13d5e53c6089ba092a5d437f8e6b2b4747653735))
- Publish pending ChaptarrNG updates - ([1171a87](https://github.com/snapetech/chaptarrng/commit/1171a870bf1f44dac799eae53f44507562ed78f8))

### Bug Fixes
- *(security)* Enumerate only fixture test files - ([4d60eba](https://github.com/snapetech/chaptarrng/commit/4d60eba5e02c72646a0bc144d6bf4b74679e1bce))
- *(security)* Constrain test fixture file paths - ([14dfae5](https://github.com/snapetech/chaptarrng/commit/14dfae526a54fb3c742bf116927be2afda60f859))
- Align OpenAPI security scopes with new type - ([9c2d5fe](https://github.com/snapetech/chaptarrng/commit/9c2d5fe93d0157e10e45debc0809a8e4801377bb))
- Migrate built-in FluentValidation calls to v12 - ([53a09f4](https://github.com/snapetech/chaptarrng/commit/53a09f4ce70f216ccf1223efa4ac462b3e57a256))
- Migrate configuration change handler for NLog 6 - ([7f8e9f2](https://github.com/snapetech/chaptarrng/commit/7f8e9f2edabefdb5789ac6d790035255e2fde3c0))
- Resolve CI compatibility blockers - ([accc257](https://github.com/snapetech/chaptarrng/commit/accc257c8938981b20fa276340d2afc19d393b3a))

### Maintenance
- Update security dependencies and compatibility - ([63afb7f](https://github.com/snapetech/chaptarrng/commit/63afb7f6b309003fc7d53f8a3772c3a1d271968f))## [0.9.938](https://github.com/snapetech/chaptarrng/compare/v0.9.937..v0.9.938) - 2026-09-29

### Changed

- **Distribution:** ChaptarrNG's Unraid setup now points to its dedicated package repository, keeping the application release and Community Apps listing independently maintained.

## [0.9.937](https://github.com/snapetech/chaptarrng/compare/v0.9.936..v0.9.937) - 2026-09-29

### Added

- **Books:** Scoped book lookups now retain the requested ebook or audiobook format through metadata searches, so SeerrNG receives matching catalog results.
- **Distribution:** The release adds ChaptarrNG's maintained fork identity and Unraid template.

## [0.9.936](https://github.com/snapetech/chaptarrng/releases/tag/v0.9.936) - 2026-09-29

### Added

- **Books:** ChaptarrNG adds format-scoped requests and a durable pending-import lifecycle, allowing SeerrNG to resume requests after author metadata preparation and safely share queued work.
- **Identity:** ChaptarrNG is a standalone book manager that keeps the Chaptarr API identity for Readarr-compatible clients.
