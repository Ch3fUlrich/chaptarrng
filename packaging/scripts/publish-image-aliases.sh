#!/usr/bin/env bash
set -euo pipefail

: "${SOURCE_IMAGE:?SOURCE_IMAGE must identify the pushed ChaptarrNG image by digest}"
: "${EXPECTED_DIGEST:?EXPECTED_DIGEST must contain the build output digest}"
: "${VERSION:?VERSION must contain the ChaptarrNG package version}"
: "${GITHUB_STEP_SUMMARY:?GITHUB_STEP_SUMMARY must point to the workflow summary file}"
: "${RUNNER_TEMP:?RUNNER_TEMP is required for alias metadata files}"

if [[ "$SOURCE_IMAGE" != *"@$EXPECTED_DIGEST" ]]; then
  echo "SOURCE_IMAGE must be pinned to EXPECTED_DIGEST." >&2
  exit 2
fi

summary="$GITHUB_STEP_SUMMARY"
metadata_index=0
failed=0
publish_ghcr_aliases="${PUBLISH_GHCR_ALIASES:-false}"
publish_legacy_aliases="${PUBLISH_LEGACY_ALIASES:-false}"

if [[ "$publish_ghcr_aliases" != "true" && "$publish_ghcr_aliases" != "false" ]]; then
  echo 'PUBLISH_GHCR_ALIASES must be true or false.' >&2
  exit 2
fi
if [[ "$publish_legacy_aliases" != "true" && "$publish_legacy_aliases" != "false" ]]; then
  echo 'PUBLISH_LEGACY_ALIASES must be true or false.' >&2
  exit 2
fi

publish_alias() {
  local target="$1"
  local source="$2"
  local metadata="$RUNNER_TEMP/chaptarrng-alias-$metadata_index.json"
  metadata_index=$((metadata_index + 1))

  if ! docker buildx imagetools create --tag "$target" --metadata-file "$metadata" "$source"; then
    echo "- $target: publish failed." >> "$summary"
    failed=1
    return
  fi

  local actual_digest
  if actual_digest="$(python3 - "$metadata" <<'PY'
import json
import sys
with open(sys.argv[1], encoding="utf-8") as stream:
    data = json.load(stream)
print(data.get("containerimage.descriptor", {}).get("digest", ""))
PY
  )"; then
    :
  else
    echo "- $target: could not read the published manifest digest." >> "$summary"
    failed=1
    return
  fi
  if [[ "$actual_digest" != "$EXPECTED_DIGEST" ]]; then
    echo "- $target: published manifest digest $actual_digest; expected $EXPECTED_DIGEST." >> "$summary"
    failed=1
    return
  fi

  echo "- $target: published with the standard image digest $EXPECTED_DIGEST." >> "$summary"
}

{
  echo "### Legacy image aliases"
  echo
  echo "The ChaptarrNG image was built once. Each alias below points to that same multi-architecture manifest digest."
} >> "$summary"

if [[ "$publish_legacy_aliases" == "true" && "$publish_ghcr_aliases" == "true" ]]; then
  publish_alias "ghcr.io/snapetech/bookshelfng:latest" "$SOURCE_IMAGE"
  publish_alias "ghcr.io/snapetech/bookshelfng:hardcover" "$SOURCE_IMAGE"
  publish_alias "ghcr.io/snapetech/bookshelfng:softcover" "$SOURCE_IMAGE"
else
  if [[ "$publish_legacy_aliases" != "true" ]]; then
    echo '- GHCR legacy aliases: skipped because the BookshelfNG migration bridge is disabled.' >> "$summary"
  else
    echo '- GHCR legacy aliases: skipped because GHCR alias publication was disabled for this invocation.' >> "$summary"
  fi
fi

if [[ "${DOCKERHUB_CONFIGURED:-false}" == "true" ]]; then
  dockerhub_source="docker.io/snapetech/chaptarrng:$VERSION"
  dockerhub_transport="docker://$dockerhub_source"
  ghcr_source="docker://$SOURCE_IMAGE"
  if skopeo --authfile "$HOME/.docker/config.json" copy --all --preserve-digests "$ghcr_source" "$dockerhub_transport"; then
    if dockerhub_digest="$(skopeo --authfile "$HOME/.docker/config.json" inspect --raw "$dockerhub_transport" | sha256sum | cut -d ' ' -f1)"; then
      if [[ "sha256:$dockerhub_digest" == "$EXPECTED_DIGEST" ]]; then
        echo "- docker.io/snapetech/chaptarrng:$VERSION: mirrored with the standard image digest $EXPECTED_DIGEST." >> "$summary"
        publish_alias "docker.io/snapetech/chaptarrng:latest" "$dockerhub_source"
        if [[ "$publish_legacy_aliases" == "true" ]]; then
          publish_alias "docker.io/snapetech/bookshelfng:latest" "$dockerhub_source"
          publish_alias "docker.io/snapetech/bookshelfng:hardcover" "$dockerhub_source"
          publish_alias "docker.io/snapetech/bookshelfng:softcover" "$dockerhub_source"
        else
          echo '- Docker Hub BookshelfNG aliases: skipped because the migration bridge is disabled.' >> "$summary"
        fi
      else
        echo "- Docker Hub version mirror digest was sha256:$dockerhub_digest; expected $EXPECTED_DIGEST." >> "$summary"
        failed=1
      fi
    else
      echo "- Docker Hub version mirror digest could not be inspected." >> "$summary"
      failed=1
    fi
  else
    echo "- Docker Hub version mirror docker.io/snapetech/chaptarrng:$VERSION: publish failed." >> "$summary"
    failed=1
  fi
else
  echo "- Docker Hub image aliases: skipped because both publisher credentials are not configured." >> "$summary"
fi

if (( failed != 0 )); then
  exit 1
fi
