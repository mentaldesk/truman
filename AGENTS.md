# AGENTS.md

## Purpose

Truman is a personalised news/relevance app with three main parts:

- `src/Truman.Api` — ASP.NET Core API for auth, profiles, tag preferences, and article retrieval
- `src/Truman.JobRunner` — background worker/CLI that fetches RSS feeds, analyses content with Gemini, and writes results into Postgres
- `src/web` — Svelte frontend

Supporting pieces:

- `src/Truman.Data` — EF Core data layer and migrations
- `compose.yaml` — local dev setup (API, JobRunner, web, Postgres)
- `.github/workflows` — CI (build + test) and GHCR image publishing workflows

## Current direction

The repo has moved from Kubernetes/Helm/kind to a simple Docker Compose + Dokploy deployment on a VPS. When making changes, prefer moves that:

- reduce deployment complexity
- preserve clear service boundaries in code even if runtime topology gets simpler
- make local development easier
- keep secrets/config externalised

## Working rules

- Work on feature branches, not `main`.
- Keep PRs focused and reviewable.
- Avoid mixing architecture cleanup, feature work, and infra churn in one PR unless they are tightly linked.
- Before changing deployment shape, understand whether the change affects:
  - API startup/config
  - JobRunner scheduling/execution
  - frontend API base URL handling
  - database migrations/persistence
- Never commit real secrets. Use `.env` / secret injection patterns only.

## Repo map

### Backend

- `src/Truman.Api/Program.cs` wires up auth, OpenAPI, Sentry, CORS, and endpoint registration.
- Feature folders under `src/Truman.Api/Features/*` hold endpoint and service logic.
- Auth currently includes Google, Facebook, JWT, and magic-link flows.

### Job processing

- `src/Truman.JobRunner/Program.cs` can:
  - run DB migrations (`--run-migrations`)
  - fetch RSS content (`--fetch`)
  - analyse content (`--analyse`)
  - run the default fetch+analyse path
- The job runner uses Gemini via Semantic Kernel.
- If changing prompts/schemas, inspect the instruction/schema files alongside the code.

### Data

- `src/Truman.Data` contains entities, `TrumanDbContext`, connection-string helpers, and EF migrations.
- Postgres is the source of truth.
- Schema changes should come with a migration and a sanity check of affected API/job paths.

### Frontend

- `src/web` is a Svelte app built with Vite.
- Frontend config depends on `Frontend__BaseUrl` and the API URL wiring.

### Infra

- `compose.yaml` at the repo root is the local dev entrypoint — runs API, JobRunner, web, and Postgres.
- `.github/workflows` contains a build/test workflow and two GHCR image publishing workflows.
- Production runs on a VPS via Dokploy, exposed via Cloudflare Tunnel at https://truman.news.

## Useful commands

Run these from the repo root unless there is a better documented path.

### General

- `dotnet restore truman.sln`
- `dotnet build truman.sln`
- `dotnet test truman.sln`

### Frontend

- `cd src/web && npm install`
- `cd src/web && npm run dev`
- `cd src/web && npm run build`
- `cd src/web && npm run test`

### Local dev

- `cp env.example .env`
- Fill in required OAuth, email, AI, DB, and Sentry values.
- `docker compose up --build`

## Change guidance

### If asked to simplify deployment

Start by separating concerns conceptually:

1. What must always run? likely API + web + Postgres
2. What can run on a schedule? JobRunner
3. What can be collapsed into one image/process versus one container with multiple processes versus separate simple services?

Good simplification targets usually include:

- making JobRunner runnable by cron/systemd/host scheduler
- serving the built frontend from the API container or a simple web container
- documenting the minimum environment needed for a single-VPS deployment

### If changing config

- Keep environment-variable names stable where practical.
- If renaming config keys, update all affected layers: `.env` example, app binding, deployment manifests, and docs.

### If changing data model or relevance logic

- Review both API and JobRunner code paths.
- Check whether tag preferences, user profile fields, presenter content, or RSS/article ingestion assumptions are affected.

### If touching auth/email

- Treat externally facing auth and magic-link flows as sensitive.
- Prefer small, testable changes.
- Flag any uncertainty around redirect URLs, token handling, or provider config.

## What to verify before opening a PR

At minimum, run whatever is relevant to the change:

- `dotnet build truman.sln`
- `dotnet test truman.sln`
- `cd src/web && npm run build`
- `cd src/web && npm run test`

If infra/deployment was touched, also verify the new path actually starts cleanly rather than assuming it works.

## Notes for future agents

Production runs on a VPS via Dokploy with a Cloudflare Tunnel on https://truman.news. If you learn more about the real feed sources, user-facing product goals, or deployment constraints, update this file.
