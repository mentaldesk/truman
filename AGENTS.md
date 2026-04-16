# AGENTS.md

## Purpose

Truman is a personalised news/relevance app with three main parts:

- `src/Truman.Api` — ASP.NET Core API for auth, profiles, tag preferences, and article retrieval
- `src/Truman.JobRunner` — background worker/CLI that fetches RSS feeds, analyses content with Gemini, and writes results into Postgres
- `src/web` — Svelte frontend

Supporting pieces:

- `src/Truman.Data` — EF Core data layer and migrations
- `k8s/charts/truman` + `Taskfile.yml` — current local/dev deployment flow via kind + Helm
- `.github/workflows` — CI and an older Cloud Run deployment workflow

## Current direction

The repo reflects a Kubernetes learning phase. Treat that stack as implementation history, not sacred architecture.

Jim's stated goal is to simplify this so it can run comfortably on another VPS, ideally with a much smaller operational footprint and possibly as a single container. When making changes, prefer moves that:

- reduce deployment complexity
- preserve clear service boundaries in code even if runtime topology gets simpler
- make local development easier without requiring a cluster
- keep secrets/config externalised

Do not expand the Kubernetes surface area unless there is a very good reason.

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
- Be careful with destructive database or cluster reset commands.

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
- The checked-in `src/web/README.md` is generic template boilerplate, so do not treat it as authoritative project documentation.
- Frontend config depends on `Frontend__BaseUrl` and the chart values for API URL wiring.

### Infra

- `Taskfile.yml` is the main operational entrypoint for current local k8s workflows.
- `k8s/charts/truman` contains the Helm chart.
- There is currently no repo-root Docker Compose setup.
- There is a Cloud Run workflow in `.github/workflows/cloudrun-docker.yml`, but treat it as historical unless verified.

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

### Local config

- `cp env.example .env`
- Fill in required OAuth, email, AI, DB, and Sentry values before expecting full app behaviour.

### Current k8s/dev flow

- `task kind-generate-config`
- `task kind-create`
- `task helm-dev-deploy`
- `task helm-port-forward`

Use `task helm-dev-reset` only with care; it tears down Postgres-related artefacts.

## Change guidance

### If asked to simplify deployment

Start by separating concerns conceptually:

1. What must always run? likely API + web + Postgres
2. What can run on a schedule? JobRunner
3. What can be collapsed into one image/process versus one container with multiple processes versus separate simple services?

Good simplification targets usually include:

- replacing kind+Helm local workflows with Docker Compose or a small set of plain container definitions
- making JobRunner runnable by cron/systemd/host scheduler instead of Kubernetes CronJob
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

If you learn the real intended production topology, deployment constraints, feed sources, or user-facing product goals, update this file. Right now the main architectural fact worth preserving is simple: the codebase has useful application pieces, but the operational model is more complicated than it needs to be.