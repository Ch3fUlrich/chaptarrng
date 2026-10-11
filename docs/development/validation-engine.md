# Validation engine

ChaptarrNG keeps `dotnet test` and its disposable PostgreSQL migration smoke as
the native test runners. The engine records each runner's source snapshot,
discovered test inventory, native case totals, platform identity, workflow
attempt, and private task logs. A final GitHub job reconciles the Linux,
Windows, macOS, and PostgreSQL smoke receipts before the workflow is complete.

## Local use

Use Node.js `26.10.0` and .NET SDK `10.0.401`, matching the CI toolchain. Build
the existing CI outputs first:

```sh
yarn install --frozen-lockfile
yarn typecheck
yarn build
dotnet restore src/Chaptarr.NoTests.sln
dotnet restore src/Chaptarr.Core.Test/Chaptarr.Core.Test.csproj
dotnet build src/Chaptarr.NoTests.sln --configuration Release --no-restore
dotnet build src/Chaptarr.Core.Test/Chaptarr.Core.Test.csproj --configuration Release --no-restore -p:UseSharedCompilation=false
node scripts/validation-engine/engine.mjs --plan --tests-only --json
node scripts/validation-engine/engine.mjs --tests-only
```

The Core task runs the existing NUnit project with `--no-build` and
`--no-restore`, so its receipt refers to the test assembly built from that same
checkout. Linux also requires the Playwright Chromium setup from CI when direct
download tests run. The PostgreSQL task is GitHub-only: CI supplies three
throwaway databases from its PostgreSQL service, and the engine refuses to run
that task outside GitHub Actions.

## Receipt contract

Each task requires a nonzero native case count. NUnit XML provides total,
passed, failed, skipped, and inconclusive counts; the Node test contracts use
the standard TAP summary with the same zero-case and accounting checks. The
migration smoke emits one case only after the app starts against the workflow's ephemeral PostgreSQL service and `/ping` responds.

The source fingerprint binds the commit, tree, working diff, configured build
inputs, and test-source inventory. Each platform receipt separately hashes its
compiled test assembly; platform binaries are allowed to differ. Before and
after a task the engine checks that the source fingerprint has not moved. Logs
and receipts are private under the system temporary directory, overlapping
runs in one checkout are rejected, and child processes do not inherit
credential-shaped environment variables.

The reconciliation step requires one passing receipt for each expected node,
from the same workflow run, attempt, commit, source fingerprint, and inventory.
It rejects duplicate or missing platform nodes, test tasks with zero cases,
runner identity mismatches, and stale artifacts. The CI build, frontend build,
artifact publication, and release-note checks remain separate workflow gates.
