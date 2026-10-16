# Dependency security

ChaptarrNG tracks frontend and .NET dependencies in their lock and central
version files. Run the audits after dependency restore so results cover the
versions that CI and release builds will consume:

```sh
yarn audit
dotnet list src/Chaptarr.sln package --vulnerable --include-transitive
```

CI also runs CodeQL and Trivy dependency and secret scans. `yarn audit` checks
the frontend build and lint dependency tree; the .NET command checks resolved
packages across the solution's target frameworks.

## Frontend parser depth limit

The `braces` package currently has no published release with a fix for
[GHSA-vfj7-8cjw-p6xm](https://github.com/advisories/GHSA-vfj7-8cjw-p6xm).
ChaptarrNG keeps a local MIT-licensed copy of `braces` 3.0.3 at
`patches/braces`, resolved through `package.json`. Its parser rejects nested
brace and parenthesis input beyond 256 levels before building a deeply nested
AST. This limits the recursive parser path that can exhaust the Node.js call
stack. The package is used by frontend build tooling; the change does not alter
the Chaptarr application runtime.

`patches/braces/PATCH.md` records the exact local modification. Remove the
copy and the `braces` resolution after an upstream release contains an
equivalent bound, then regenerate `yarn.lock` and rerun the full audit and
frontend build.

## .NET cryptography package

The solution centrally pins `System.Security.Cryptography.Pkcs` to `10.0.13`
in `src/Directory.Packages.props`. That is the patched .NET 10 version for
[GHSA-846f-f764-76mx](https://github.com/advisories/GHSA-846f-f764-76mx) and
[GHSA-2fmv-rv2c-59x7](https://github.com/advisories/GHSA-2fmv-rv2c-59x7).
Keep the central pin current when updating packages that bring Pkcs
transitively, and verify all target frameworks after restoring the solution.
