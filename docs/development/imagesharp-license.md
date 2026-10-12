# ImageSharp build license

ChaptarrNG uses `SixLabors.ImageSharp` to resize book, author, and narrator
cover images in `src/NzbDrone.Core/MediaCover/ImageResizer.cs`. The package is a
direct dependency of `src/NzbDrone.Core/Chaptarr.Core.csproj`.

## Why a license is required

Starting with ImageSharp 4.0, Six Labors validates licenses at build time for
projects that directly reference ImageSharp. ChaptarrNG uses ImageSharp 4.1.3,
so source builds must provide a valid Six Labors license. This is a build-time
dependency requirement; it is not a ChaptarrNG runtime setting. People running
the published ChaptarrNG images do not need to obtain or configure a license.

The Six Labors license is separate from ChaptarrNG's GPL license. Community
licenses may be scoped to the exact assembly names listed in the application;
check that your license covers the ChaptarrNG assemblies produced by your build.

## Obtain a license

Apply through the [Six Labors licensing portal](https://licensing.sixlabors.com/).
Review the current [ImageSharp license terms and options](https://docs.sixlabors.com/articles/imagesharp/index.html#license)
and request a license that covers your use and the required assembly names.
Six Labors sends the issued license as `sixlabors.lic`. Use the complete file
contents when configuring builds; do not extract only its `Key` field.

ChaptarrNG does not commit the license file or key. Keep the file outside the
repository, and keep `.env.sixlabors` private. The repository ignores that env
file and the Docker build context excludes license files.

## Local source builds

Create the ignored repository-root `.env.sixlabors` file with a private editor
or secret manager. It should contain one shell-quoted assignment with the full
contents of `sixlabors.lic`; do not put the actual value in a shell command:

```dotenv
SIXLABORS_LICENSE_KEY='<complete contents of sixlabors.lic>'
```

```sh
chmod 600 .env.sixlabors
set -a
. ./.env.sixlabors
set +a
dotnet restore src/Chaptarr.NoTests.sln
dotnet build src/Chaptarr.NoTests.sln --configuration Release
unset SIXLABORS_LICENSE_KEY
```

`src/Directory.Build.props` passes `SIXLABORS_LICENSE_KEY` to the ImageSharp
build target. If you are running the validation engine, follow its instructions
to build with the license and clear the variable before starting the engine.

## GitHub Actions builds

In the repository's **Settings → Secrets and variables → Actions**, add a
repository secret named `SIXLABORS_LICENSE_KEY` containing the complete
`sixlabors.lic` file contents. The build and image-publish workflows pass it to
the build only. GitHub does not provide repository secrets to workflows started
from fork pull requests, so those workflows cannot perform the licensed build.

## Local Docker builds

Source `.env.sixlabors` in the shell, then pass the environment variable as a
BuildKit secret:

```sh
set -a
. ./.env.sixlabors
set +a
docker buildx build --secret id=SIXLABORS_LICENSE_KEY,env=SIXLABORS_LICENSE_KEY -f Dockerfile.cross .
unset SIXLABORS_LICENSE_KEY
```

The Dockerfile mounts the secret only for the restore/publish build step. It is
not copied into the build context, image layers, runtime environment, or image
metadata. Do not add the key to a Docker `ARG`, `ENV`, or runtime configuration.

## References

- [Six Labors ImageSharp license and build setup](https://docs.sixlabors.com/articles/imagesharp/index.html#license)
- [Six Labors licensing portal](https://licensing.sixlabors.com/)
- [ChaptarrNG validation-engine build instructions](validation-engine.md#local-use)
