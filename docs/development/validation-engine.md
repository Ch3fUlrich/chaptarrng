# Validation engine

ChaptarrNG keeps `dotnet test` and its disposable PostgreSQL migration smoke as
the native test runners. The engine records each runner's source snapshot,
discovered test inventory, native case totals, platform identity, workflow
attempt, and private task logs. A final GitHub job reconciles the Linux,
Windows, macOS, and PostgreSQL smoke receipts before the workflow is complete.

## GitHub runner routing

The Ubuntu Linux build-matrix entry may use the shared organization runner pool
only when `TRUSTED_MAIN_RUNNERS_ENABLED` is set to `true`; while unset, it
stays hosted. Pull requests and Windows/macOS matrix entries remain hosted. The
organization group is restricted to this workflow on `refs/heads/main`.

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
if [ "$(uname -s)" = Linux ]; then
  export PLAYWRIGHT_BROWSERS_PATH="$(mktemp -d "${TMPDIR:-/tmp}/chaptarr-playwright.XXXXXX")"
  pwsh ./_tests/net10.0/playwright.ps1 install --with-deps chromium
else
  export PLAYWRIGHT_BROWSERS_PATH="$HOME/.cache/ms-playwright"
  pwsh ./_tests/net10.0/playwright.ps1 install chromium
fi
TRUSTED_LAUNCHER=/path/to/trusted-test-launcher.mjs
node "$TRUSTED_LAUNCHER" "$PWD" --playwright-cache "$PLAYWRIGHT_BROWSERS_PATH" -- node scripts/validation-engine/engine.mjs --plan --tests-only --json
node "$TRUSTED_LAUNCHER" "$PWD" --playwright-cache "$PLAYWRIGHT_BROWSERS_PATH" -- node scripts/validation-engine/engine.mjs --tests-only
```

Local execution of test tasks requires the trusted Linux bubblewrap launcher.
For a branch you have not reviewed, copy `trusted-test-launcher.mjs` and
`trusted-test-sandbox-policy.mjs` from the same reviewed revision to a location
outside the checkout, keeping them together. The launcher snapshots only paths
in the Git index, hides the original checkout and common host configuration and
data roots, and runs candidate code offline. Dotted and extensionless credential
names, private/API keys, access/refresh-token names, client-secret names, and
credential material with source-like extensions are excluded. Source modules
under credential folders remain available unless their own names identify
credential material. Stage new candidate files first.
Suffixed dotenv credentials such as tokens.env.local are excluded, while
example templates such as tokens.env.example remain available.
It opens each workspace, run-data, dependency, cache, and marker source before
the sandbox masks host paths, then binds those pinned sources by inherited file
descriptor. Each persisted task log is capped at 16 MiB and ends with a marker
when output was truncated.

```sh
node /path/to/trusted-test-launcher.mjs "$PWD" --playwright-cache "$PLAYWRIGHT_BROWSERS_PATH" -- node scripts/validation-engine/engine.mjs --tests-only
```

The optional Playwright cache is mounted read-only. Run planning for an
unreviewed candidate through the same launcher because planning imports its
validation configuration. GitHub Actions remains responsible for hosted
validation.

Each runner has a limit of two concurrent validation tasks. The engine-contract
command also caps Node's test-file workers at two, so it cannot fan out across
every CPU reported by a large self-hosted host. On the platform nodes, the
engine-contract suite can run beside the Core NUnit suite; their logs, NUnit
results, and task environments are separate. The PostgreSQL smoke remains gated
by the engine contracts on its dedicated node and still uses
only the workflow's disposable databases. Tasks that share writable output
must use the same concurrency group so the scheduler runs them in separate
batches.

During a run, the engine writes a compact task summary to stderr when each task
starts and finishes, then once per minute while a task is still running. The
counts cover completed tasks; every active task and its elapsed time are shown
separately. GitHub renders `GITHUB_STEP_SUMMARY` only after its workflow step
finishes, so these updates appear in the live step log instead.

For BuildKit container builds, source the same file and pass the value as a
BuildKit secret, for example:

```sh
docker buildx build --secret id=SIXLABORS_LICENSE_KEY,env=SIXLABORS_LICENSE_KEY -f Dockerfile.cross .
```

The Dockerfiles consume the secret only in the restore/publish instruction; it
does not enter the build context, image layers, or image metadata.

The Core task runs the existing NUnit project with `--no-build` and
`--no-restore`, so its receipt refers to the test assembly built from that same
checkout. Linux direct-download tests need the Playwright Chromium cache. CI
installs it in a run-specific cache and passes only that cache path to the NUnit
task. The self-hosted Linux runner invokes PowerShell through Aqua with
`AQUA_ROOT_DIR` inside the private job directory, so package downloads do not
write to the shared runner installation. Local Linux runs should set
`PLAYWRIGHT_BROWSERS_PATH` for both browser installation and the trusted
launcher; the launcher mounts the cache read-only.
The engine passes the cache path only to the Core task and keeps its per-task
home, temporary, XDG, and environment settings private. The PostgreSQL task is
GitHub-only and assigned to the dedicated PostgreSQL node: CI supplies three
throwaway databases from its
PostgreSQL service, while ordinary platform nodes select only the NUnit task.

## Receipt contract

Each task requires a nonzero native case count. NUnit XML provides total,
passed, failed, skipped, and inconclusive counts; the Node test contracts use
the standard TAP summary with the same zero-case and accounting checks. The
migration smoke emits one case only after the app starts against the workflow's ephemeral PostgreSQL service and `/ping` responds.
NUnit receipts also include at most 100 failed test-case names, without test
stdout or assertion messages, so platform failures can be identified from the
short-lived CI artifact.

The source fingerprint binds the commit, tree, working diff, configured build
inputs, and test-source inventory. Text inputs and inventory files use Git's
CRLF-to-LF normalization so Windows checkouts produce the same fingerprint as
Linux and macOS. Each platform receipt separately hashes its compiled test
assembly byte-for-byte; platform binaries are allowed to differ. Before and
after each concurrent task batch the engine checks that the source fingerprint
has not moved and blocks successful receipts if it changes. Logs stay private
under the system temporary directory; the exported receipt carries counts and
bounded failing test names only. Overlapping runs in one checkout are rejected.
Runtime probes and task child processes
receive an explicit environment allowlist and each task's declared settings.
Every task gets run-owned home, temporary, XDG, and .NET CLI directories. NuGet
packages use a private cache shared only within the validation run. Test tasks
run with `CI=true` and `NODE_ENV=test` regardless of the invoking shell's values.
For Linux tests that use Chromium or start a nested trusted sandbox, the engine
creates a short private symlink under `/dev/shm` that points to the isolated
task temp directory. This keeps UNIX-domain socket paths within the kernel
limit without depending on shared `/tmp`; temporary data still lands in the
task directory.

The artifact downloader preserves each runner receipt in its own subdirectory,
and the reconciler finds `receipt.json` recursively so same-named artifacts do
not overwrite one another. It requires one passing receipt for each expected
node, from the same workflow run, attempt, commit, source fingerprint, and
inventory.
It rejects duplicate or missing platform nodes, test tasks with zero cases,
runner identity mismatches, and stale artifacts. The CI build, frontend build,
artifact publication, and release-note checks remain separate workflow gates.
