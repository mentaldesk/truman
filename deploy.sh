#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
docker compose -f compose.prod.yaml pull
docker compose -f compose.prod.yaml up -d --remove-orphans
docker compose -f compose.prod.yaml ps
