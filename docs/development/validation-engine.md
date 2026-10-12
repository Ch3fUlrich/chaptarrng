# Validation engine

ChaptarrNG keeps `dotnet test` and its disposable PostgreSQL migration smoke as
the native test runners. The engine records each runner's source snapshot,
discovered test inventory, native case totals, platform identity, workflow
attempt, and private task logs. A final GitHub job reconciles the Linux,
Windows, macOS, and PostgreSQL smoke receipts before the workflow is complete.

## Local use

Use Node.js `26.10.0` and .NET SDK `10.0.401`, matching the CI toolchain. Build
the existing CI outputs first:

ImageSharp 4.1.3 requires the complete Six Labors license at build time. Store
it as a single shell-quoted `SIXLABORS_LICENSE_KEY` assignment in the ignored
`.env.sixlabors` file; use the full contents of `sixlabors.lic`, not only its
`Key` field. Do not commit either file. Source it before restore/build commands
and unset the variable before starting the validation engine, whose child
processes intentionally do not inherit credential-shaped environment values.
GitHub Actions reads the same value from the private `SIXLABORS_LICENSE_KEY`
repository secret. Fork pull request workflows cannot read repository secrets;
those builds need a locally supplied license or maintainer validation from a
reviewed in-repository branch. See the [ImageSharp build-license guide](imagesharp-license.md)
for the licensing rationale, application steps, and full local, CI, and
container setup instructions.

```sh
yarn install --frozen-lockfile
yarn typecheck
yarn build
set -a
. ./.env.sixlabors
set +a
dotnet restore src/Chaptarr.NoTests.sln
dotnet restore src/Chaptarr.Core.Test/Chaptarr.Core.Test.csproj
dotnet build src/Chaptarr.NoTests.sln --configuration Release --no-restore
dotnet build src/Chaptarr.Core.Test/Chaptarr.Core.Test.csproj --configuration Release --no-restore -p:UseSharedCompilation=false
unset SIXLABORS_LICENSE_KEY
node scripts/validation-engine/engine.mjs --plan --tests-only --json
node scripts/validation-engine/engine.mjs --tests-only
```

For BuildKit container builds, source the same file and pass the value as a
BuildKit secret, for example:

```sh
docker buildx build --secret id=SIXLABORS_LICENSE_KEY,env=SIXLABORS_LICENSE_KEY -f Dockerfile.cross .
```

The Dockerfiles consume the secret only in the restore/publish instruction; it
does not enter the build context, image layers, or image metadata.

The Core task runs the existing NUnit project with `--no-build` and
`--no-restore`, so its receipt refers to the test assembly built from that same
checkout. Linux also requires the Playwright Chromium setup from CI when direct
download tests run. The PostgreSQL task is GitHub-only and assigned to the
dedicated PostgreSQL node: CI supplies three throwaway databases from its
PostgreSQL service, while ordinary platform nodes select only the NUnit task.

## Receipt contract

Each task requires a nonzero native case count. NUnit XML provides total,
passed, failed, skipped, and inconclusive counts; the Node test contracts use
the standard TAP summary with the same zero-case and accounting checks. The
migration smoke emits one case only after the app starts against the workflow's ephemeral PostgreSQL service and `/ping` responds.

The source fingerprint binds the commit, tree, working diff, configured build
inputs, and test-source inventory. Text inputs and inventory files use Git's
CRLF-to-LF normalization so Windows checkouts produce the same fingerprint as
Linux and macOS. Each platform receipt separately hashes its compiled test
assembly byte-for-byte; platform binaries are allowed to differ. Before and
after a task the engine checks that the source fingerprint has not moved. Logs
and receipts are private under the system temporary directory, overlapping
runs in one checkout are rejected, and child processes do not inherit
credential-shaped environment variables.

The artifact downloader preserves each runner receipt in its own subdirectory,
and the reconciler finds `receipt.json` recursively so same-named artifacts do
not overwrite one another. It requires one passing receipt for each expected
node, from the same workflow run, attempt, commit, source fingerprint, and
inventory.
It rejects duplicate or missing platform nodes, test tasks with zero cases,
runner identity mismatches, and stale artifacts. The CI build, frontend build,
artifact publication, and release-note checks remain separate workflow gates.
