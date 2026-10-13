# Integration smoke tests

The SeerrNG smoke stack builds the ChaptarrNG candidate, starts both services
on an isolated Compose network, and checks capability discovery, paged book
reads, and the restricted service key.

Set the inputs in the shell; the Compose file does not need a `.env` file:
the SeerrNG checkout must have dependencies installed and `dist/` built first
(`yarn install --frozen-lockfile && yarn build` from that checkout).

```sh
export SIXLABORS_LICENSE_FILE="$HOME/Downloads/sixlabors.lic"
export SEERRNG_SOURCE="/path/to/seerrng"
export CHAPTARR_GLOBAL_API_KEY="$(openssl rand -hex 24)"
export CHAPTARR_SEERR_API_KEY="$(openssl rand -hex 24)"

docker compose -p chaptarrng-seerrng-smoke \
  -f integration/seerrng-smoke.compose.yml \
  up --build --abort-on-container-exit --exit-code-from smoke
docker compose -p chaptarrng-seerrng-smoke \
  -f integration/seerrng-smoke.compose.yml down -v --remove-orphans
```

Use distinct test API keys and a valid Six Labors build license. Keep the
license outside the checkout; `.dockerignore` excludes it from the build
context, and Compose mounts it only as a BuildKit secret.
