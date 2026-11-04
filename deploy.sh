#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
# The jobs profile excludes jobrunner from pull too, so without it migrations run on a stale image.
docker compose -f compose.prod.yaml --profile jobs pull
docker compose -f compose.prod.yaml up -d --remove-orphans
docker compose -f compose.prod.yaml ps
