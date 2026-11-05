#!/usr/bin/env bash
#
# Builds this checkout, brings it up on a free port against a throwaway database, seeds a few
# articles, signs you in, opens it, and takes the whole stack away again when you quit. It's the
# truman team's `try` in a-team.
#
# AI__ApiKey, AI__Model and Jev__ApiKey have to come from somewhere: point TRUMAN_TRY_ENV at an
# env file holding them. The JobRunner's host build demands all three even to run migrations.
#
set -euo pipefail
cd "$(dirname "$0")/.."

die() { echo "try: $*" >&2; exit 1; }

SEED=${TRUMAN_TRY_SEED:-10}
LOGIN=true
for arg in "$@"; do
  case "$arg" in
    --no-seed) SEED=0 ;;
    --no-login) LOGIN=false ;;
    *) echo "usage: scripts/try.sh [--no-seed] [--no-login]" >&2; exit 2 ;;
  esac
done

command -v docker >/dev/null || die "docker is not on PATH"
docker info >/dev/null 2>&1 || die "docker is not running"

free_port() {
  local p i
  for i in $(seq 1 50); do
    p=$((RANDOM % 10000 + 20000))
    (exec 3<>"/dev/tcp/127.0.0.1/$p") 2>/dev/null || { echo "$p"; return 0; }
  done
  return 1
}
PORT=$(free_port) || die "could not find a free port"
URL="http://localhost:$PORT"

DB_NAME=truman
DB_USER=truman

# Generated here, never read from TRUMAN_TRY_ENV: a try must not report to production Sentry, email
# anybody, or share credentials with a database that outlives it.
ENV_FILE=$(mktemp -t truman-try)
chmod 600 "$ENV_FILE"
if [ -n "${TRUMAN_TRY_ENV:-}" ]; then
  ENV_PATH=${TRUMAN_TRY_ENV/#\~/$HOME}
  [ -f "$ENV_PATH" ] || die "no env file at $ENV_PATH"
  grep -vE '^(POSTGRES_(DB|USER|PASSWORD)|Authentication__Jwt__Key|Email__Brevo__ApiKey|Sentry__Dsn|Frontend__BaseUrl|TRUMAN_TRY_PORT)=' "$ENV_PATH" >>"$ENV_FILE" || true
fi
{
  echo "POSTGRES_DB=$DB_NAME"
  echo "POSTGRES_USER=$DB_USER"
  echo "POSTGRES_PASSWORD=$(openssl rand -hex 16)"
  echo "Authentication__Jwt__Key=$(openssl rand -hex 32)"
  echo "Email__Brevo__ApiKey="
  echo "Sentry__Dsn="
  echo "Frontend__BaseUrl=$URL"
  echo "TRUMAN_TRY_PORT=$PORT"
} >>"$ENV_FILE"

missing=
for key in AI__ApiKey AI__Model Jev__ApiKey; do
  grep -qE "^$key=." "$ENV_FILE" || missing="$missing $key"
done
if [ -n "$missing" ]; then
  rm -f "$ENV_FILE"
  die "no${missing}. Point TRUMAN_TRY_ENV at an env file holding them: the JobRunner needs all three to start, even for --run-migrations."
fi

SLOT=$(basename "$PWD" | tr '[:upper:]' '[:lower:]')
PROJECT="truman-try-${SLOT//[^a-z0-9_-]/-}"
COMPOSE=(docker compose -p "$PROJECT" -f compose.try.yaml --profile jobs --env-file "$ENV_FILE")

LOGS=
cleanup() {
  echo
  echo "Taking the stack down…"
  [ -z "$LOGS" ] || kill "$LOGS" 2>/dev/null || true
  "${COMPOSE[@]}" down -v --remove-orphans >/dev/null 2>&1 || true
  rm -f "$ENV_FILE"
}
trap cleanup EXIT
trap 'exit 130' INT TERM

echo "Building this checkout…"
"${COMPOSE[@]}" build
"${COMPOSE[@]}" up -d --wait postgres

echo "Running migrations…"
"${COMPOSE[@]}" run --rm jobrunner --run-migrations

if [ "$SEED" -gt 0 ]; then
  echo "Fetching and analysing up to $SEED articles (this spends real AI quota)…"
  "${COMPOSE[@]}" run --rm jobrunner --fetch --analyse --limit "$SEED"
fi

"${COMPOSE[@]}" up -d --wait app

for i in $(seq 1 60); do
  curl -sf -o /dev/null "$URL" && break
  "${COMPOSE[@]}" ps --services --status running | grep -qx app || die "the app exited before serving $URL"
  [ "$i" -lt 60 ] || die "the app did not serve $URL within a minute"
  sleep 1
done

AT="$URL"
WHO="not signed in"
if $LOGIN; then
  EMAIL=try@localhost
  # The Brevo send fails by design; the code is already in the database by then.
  curl -s -o /dev/null "$URL/auth/start/magic?email=$EMAIL" || true
  CODE=$("${COMPOSE[@]}" exec -T postgres psql -U "$DB_USER" -d "$DB_NAME" -t -A -c \
    'select "Code" from "MagicLinks" order by "ExpiresAt" desc limit 1' 2>/dev/null | tr -d '[:space:]')
  if [ -n "$CODE" ]; then
    AT="$URL/login/verify?code=$CODE"
    WHO="signed in as $EMAIL"
  else
    WHO="no magic link could be minted, so you are not signed in"
  fi
fi

if command -v open >/dev/null; then open "$AT"
elif command -v xdg-open >/dev/null; then xdg-open "$AT"
fi

echo
echo "Truman is at $URL — $WHO."
if [ -t 0 ]; then
  echo "Press Enter when you're done. The stack and its database go with it."
  read -r _ || true
else
  echo "Following the app's log; Ctrl+C when you're done."
  # Waited on in the background: bash holds a trap until the foreground child exits, and `logs -f`
  # never does, so a signal would otherwise leave the stack up.
  "${COMPOSE[@]}" logs -f app &
  LOGS=$!
  wait "$LOGS" || true
fi
