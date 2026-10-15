#!/usr/bin/env bash
# These checks match literal GitHub Actions expressions in the workflow file.
# shellcheck disable=SC2016
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd -P)"
work="$(mktemp -d "${TMPDIR:-/tmp}/chaptarrng-image-aliases.XXXXXX")"
trap 'rm -rf -- "$work"' EXIT

mkdir -p "$work/bin" "$work/home/.docker" "$work/runner"
touch "$work/home/.docker/config.json"

cat > "$work/bin/docker" <<'MOCK_DOCKER'
#!/usr/bin/env bash
set -euo pipefail
[[ "$1" == buildx && "$2" == imagetools && "$3" == create ]]
shift 3
target=""
metadata=""
source=""
while (($#)); do
  case "$1" in
    --tag)
      target="$2"
      shift 2
      ;;
    --metadata-file)
      metadata="$2"
      shift 2
      ;;
    *)
      source="$1"
      shift
      ;;
  esac
done
[[ -n "$target" && -n "$metadata" && -n "$source" ]]
printf '%s\t%s\n' "$target" "$source" >> "$PUBLISH_LOG"
printf '{"containerimage.descriptor":{"digest":"%s"}}\n' "$MOCK_METADATA_DIGEST" > "$metadata"
MOCK_DOCKER

cat > "$work/bin/skopeo" <<'MOCK_SKOPEO'
#!/usr/bin/env bash
set -euo pipefail
[[ "$1" == --authfile ]]
case "$3" in
  copy)
    printf 'copy\t%s\t%s\n' "${@: -2:1}" "${@: -1}" >> "$SKOPEO_LOG"
    ;;
  inspect)
    [[ "$4" == --raw ]]
    printf 'inspect\t%s\n' "$5" >> "$SKOPEO_LOG"
    printf '%s\n' "$MOCK_MANIFEST"
    ;;
  *)
    exit 2
    ;;
esac
MOCK_SKOPEO

chmod 0700 "$work/bin/docker" "$work/bin/skopeo"

manifest='{"schemaVersion":2}'
digest="sha256:$(printf '%s\n' "$manifest" | sha256sum | cut -d ' ' -f1)"
source="ghcr.io/snapetech-org/chaptarrng@${digest}"
script="$repo_root/packaging/scripts/publish-image-aliases.sh"
docker_workflow="$repo_root/.github/workflows/docker-publish.yml"
release_workflow="$repo_root/.github/workflows/release.yml"
package_workflow="$repo_root/.github/workflows/release-distribution.yml"
chocolatey_publish_script="$repo_root/packaging/scripts/publish-chocolatey-packages.ps1"
build_workflow="$repo_root/.github/workflows/build.yml"

grep -Fq 'STANDARD_IMAGE: ghcr.io/snapetech-org/chaptarrng' "$docker_workflow"
grep -Fq 'REGISTRY: ghcr.io' "$docker_workflow"
grep -Fq 'registry: ${{ env.REGISTRY }}' "$docker_workflow"
grep -Fq 'image="${STANDARD_IMAGE}"' "$docker_workflow"
grep -Fq 'SOURCE_IMAGE: ${{ env.STANDARD_IMAGE }}@${{ steps.build.outputs.digest }}' "$docker_workflow"
grep -Fq 'subject-name: ${{ env.STANDARD_IMAGE }}' "$docker_workflow"
grep -Fq 'legacy_bridge_enabled: ${{ vars.CHAPTARRNG_MIGRATION_BRIDGE_ENABLED == ' "$release_workflow"
grep -Fq 'PUBLISH_LEGACY_ALIASES: ${{ inputs.legacy_bridge_enabled }}' "$docker_workflow"
grep -Fq 'PUBLISH_GHCR_ALIASES: false' "$docker_workflow"
grep -Fq 'name: Dispatch personal-account BookshelfNG GHCR bridge' "$release_workflow"
grep -Fq 'repositories: bookshelfng-ghcr-release-bridge' "$release_workflow"
grep -Fq 'permission-contents: write' "$release_workflow"
grep -Fq 'IMAGE_DIGEST: ${{ needs.docker.outputs.image_digest }}' "$release_workflow"
grep -Fq 'value: ${{ jobs.build-and-push.outputs.image_digest }}' "$docker_workflow"
grep -Fq 'publish_legacy_aliases="${PUBLISH_LEGACY_ALIASES:-false}"' "$script"
grep -Fq 'legacy_bridge_enabled:' "$package_workflow"
grep -Fq 'REQUESTED_LEGACY_BRIDGE: ${{ inputs.legacy_bridge_enabled }}' "$package_workflow"
grep -Fq 'MIGRATION_BRIDGE_GATE: ${{ vars.CHAPTARRNG_MIGRATION_BRIDGE_ENABLED || ' "$package_workflow"
grep -Fq 'legacy_bridge_enabled=false' "$package_workflow"
grep -Fq 'if [[ "${REQUESTED_LEGACY_BRIDGE,,}" == true && "${MIGRATION_BRIDGE_GATE,,}" == true ]]' "$package_workflow"
grep -Fq 'needs.release-target.outputs.legacy_bridge_enabled' "$package_workflow"
grep -Fq 'LEGACY_BRIDGE_ENABLED: ${{ needs.release-target.outputs.legacy_bridge_enabled }}' "$package_workflow"
grep -Fq 'pkg_bases=(chaptarrng-bin)' "$package_workflow"
grep -Fq "if: needs.credentials.outputs.bookshelfng_copr == 'true' && needs.release-target.outputs.legacy_bridge_enabled == 'true'" "$package_workflow"
grep -Fq 'run: ./packaging/scripts/publish-chocolatey-packages.ps1' "$package_workflow"
grep -Fq "LEGACY_BRIDGE_ENABLED -ne 'true'" "$chocolatey_publish_script"
grep -Fq 'run: ./packaging/scripts/test-chocolatey-publish-gate.ps1' "$build_workflow"
if grep -Fq 'ghcr.io/${{ github.repository }}' "$docker_workflow"; then
  echo 'The published GHCR identity must not be derived from the transferred repository owner.' >&2
  exit 1
fi

run_aliases() {
  local configured="$1"
  local expected_metadata_digest="$2"
  local inspected_manifest="$3"
  local publish_ghcr="${4:-false}"
  local publish_legacy="${5:-false}"
  : > "$work/publish.log"
  : > "$work/skopeo.log"
  : > "$work/summary.md"
  PATH="$work/bin:$PATH" \
    HOME="$work/home" \
    RUNNER_TEMP="$work/runner" \
    GITHUB_STEP_SUMMARY="$work/summary.md" \
    PUBLISH_LOG="$work/publish.log" \
    SKOPEO_LOG="$work/skopeo.log" \
    SOURCE_IMAGE="$source" \
    EXPECTED_DIGEST="$digest" \
    MOCK_METADATA_DIGEST="$expected_metadata_digest" \
    MOCK_MANIFEST="$inspected_manifest" \
    VERSION=1.2.3 \
    DOCKERHUB_CONFIGURED="$configured" \
    PUBLISH_GHCR_ALIASES="$publish_ghcr" \
    PUBLISH_LEGACY_ALIASES="$publish_legacy" \
    bash "$script"
}

run_aliases false "$digest" "$manifest" false false
[[ ! -s "$work/publish.log" ]]
grep -Fq 'GHCR legacy aliases: skipped because the BookshelfNG migration bridge is disabled.' "$work/summary.md"
grep -Fq 'Docker Hub image aliases: skipped' "$work/summary.md"

run_aliases false "$digest" "$manifest" true true
printf '%s\t%s\n' \
  ghcr.io/snapetech/bookshelfng:latest "$source" \
  ghcr.io/snapetech/bookshelfng:hardcover "$source" \
  ghcr.io/snapetech/bookshelfng:softcover "$source" > "$work/expected-ghcr.log"
cmp "$work/expected-ghcr.log" "$work/publish.log"
grep -Fq 'standard image digest' "$work/summary.md"
grep -Fq 'Docker Hub image aliases: skipped' "$work/summary.md"

run_aliases true "$digest" "$manifest" true true
dockerhub_source="docker.io/snapetech/chaptarrng:1.2.3"
printf '%s\t%s\n' \
  ghcr.io/snapetech/bookshelfng:latest "$source" \
  ghcr.io/snapetech/bookshelfng:hardcover "$source" \
  ghcr.io/snapetech/bookshelfng:softcover "$source" \
  docker.io/snapetech/chaptarrng:latest "$dockerhub_source" \
  docker.io/snapetech/bookshelfng:latest "$dockerhub_source" \
  docker.io/snapetech/bookshelfng:hardcover "$dockerhub_source" \
  docker.io/snapetech/bookshelfng:softcover "$dockerhub_source" > "$work/expected-both.log"
cmp "$work/expected-both.log" "$work/publish.log"
grep -Fq "docker.io/snapetech/chaptarrng:1.2.3: mirrored with the standard image digest $digest" "$work/summary.md"
[[ "$(wc -l < "$work/skopeo.log")" -eq 2 ]]

run_aliases true "$digest" "$manifest" false true
dockerhub_source="docker.io/snapetech/chaptarrng:1.2.3"
printf '%s\t%s\n' \
  docker.io/snapetech/chaptarrng:latest "$dockerhub_source" \
  docker.io/snapetech/bookshelfng:latest "$dockerhub_source" \
  docker.io/snapetech/bookshelfng:hardcover "$dockerhub_source" \
  docker.io/snapetech/bookshelfng:softcover "$dockerhub_source" > "$work/expected-dockerhub-only.log"
cmp "$work/expected-dockerhub-only.log" "$work/publish.log"
grep -Fq 'GHCR legacy aliases: skipped because GHCR alias publication was disabled' "$work/summary.md"
[[ "$(wc -l < "$work/skopeo.log")" -eq 2 ]]

run_aliases true "$digest" "$manifest" false false
dockerhub_source="docker.io/snapetech/chaptarrng:1.2.3"
printf '%s\t%s\n' \
  docker.io/snapetech/chaptarrng:latest "$dockerhub_source" > "$work/expected-standard-dockerhub.log"
cmp "$work/expected-standard-dockerhub.log" "$work/publish.log"
grep -Fq 'Docker Hub BookshelfNG aliases: skipped because the migration bridge is disabled.' "$work/summary.md"
[[ "$(wc -l < "$work/skopeo.log")" -eq 2 ]]

if run_aliases false 'sha256:deadbeef' "$manifest" true true > /dev/null 2>&1; then
  echo 'A legacy tag with a different published digest was accepted.' >&2
  exit 1
fi
[[ "$(wc -l < "$work/publish.log")" -eq 3 ]]
grep -Fq "expected $digest" "$work/summary.md"

if run_aliases true "$digest" '{"schemaVersion":3}' true true > /dev/null 2>&1; then
  echo 'A Docker Hub manifest with a different digest was accepted.' >&2
  exit 1
fi
[[ "$(wc -l < "$work/publish.log")" -eq 3 ]]
grep -Fq 'Docker Hub version mirror digest was' "$work/summary.md"

echo 'Legacy image alias tests passed.'
