#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 3 ]]; then
  echo "Usage: $0 <version> <runtime-id> <output-directory>" >&2
  exit 2
fi

version=$1
rid=$2
output=$3

if [[ ! "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
  echo "Version must be MAJOR.MINOR.PATCH." >&2
  exit 2
fi

case "$rid" in
  linux-x64|linux-arm64|win-x64) ;;
  *) echo "Unsupported release runtime identifier: $rid" >&2; exit 2 ;;
esac

if [[ -z "${SIXLABORS_LICENSE_KEY:-}" ]]; then
  echo "SIXLABORS_LICENSE_KEY must be available to the build process." >&2
  exit 2
fi
if [[ ! -s _output/UI/index.html ]]; then
  echo "Build the frontend first; _output/UI/index.html is missing." >&2
  exit 2
fi

mkdir -p -- "$output"
output=$(cd "$output" && pwd -P)
work=$(mktemp -d "${TMPDIR:-/tmp}/chaptarrng-standalone.XXXXXX")
trap 'rm -rf -- "$work"' EXIT
bundle="$work/ChaptarrNG"
mkdir -p "$bundle"

dotnet publish src/NzbDrone.Console/Chaptarr.Console.csproj \
  --configuration Release \
  --framework net10.0 \
  --runtime "$rid" \
  --self-contained true \
  -p:UseAppHost=true \
  --verbosity minimal \
  --output "$bundle"

dotnet publish src/NzbDrone.Update/Chaptarr.Update.csproj \
  --configuration Release \
  --framework net10.0 \
  --runtime "$rid" \
  --self-contained true \
  -p:UseAppHost=true \
  --verbosity minimal \
  --output "$bundle/Chaptarr.Update"

mkdir -p "$bundle/UI"
cp -a _output/UI/. "$bundle/UI/"
cp LICENSE "$bundle/LICENSE"
test -s "$bundle/UI/index.html"

asset="ChaptarrNG-${version}-${rid}"
if [[ "$rid" == win-* ]]; then
  archive="$output/${asset}.zip"
  (cd "$bundle" && zip -q -X -r "$archive" .)
else
  archive="$output/${asset}.tar.gz"
  tar --sort=name --mtime='UTC 2020-01-01' --owner=0 --group=0 --numeric-owner \
    --format=gnu -czf "$archive" -C "$work" ChaptarrNG
fi
(cd "$output" && sha256sum "$(basename "$archive")" > "$(basename "$archive").sha256")
echo "Built ${archive}"
