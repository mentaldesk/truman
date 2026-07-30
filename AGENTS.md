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

The repo has moved from Kubernetes/Helm/kind to a simple Docker Compose-based deployment. When making changes, prefer moves that:

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
- Work on branches in this repo rather than a personal fork. Same-repo branch PRs get
  secrets under `pull_request`, so image publishing, preview deployments, and Sentry symbol
  upload all work normally. Fork PRs do not — see below.

## CI, secrets, and fork PRs

CI is split by trust level. That split is deliberate; do not collapse it.

- `build.yml` (build + test) runs on every PR, forks included. It uses no secrets, which is
  what makes it safe to run against code nobody has reviewed yet.
- `ghcr.yml` / `ghcr-jobrunner.yml` build the container images. They publish to GHCR and
  receive the Sentry build secret only when the PR branch lives in this repo; for fork PRs
  they build without pushing, purely to prove the image still builds.

GitHub withholds secrets from `pull_request` runs whose head branch is in a fork. That
withholding is the mechanism protecting this repo's credentials.

**Never switch these workflows to `pull_request_target` to "fix" missing secrets on a fork
PR.** `pull_request_target` runs with the base repo's secrets, so checking out and building
PR code under it grants arbitrary code execution — and every secret in scope — to anyone who
opens a PR. A `docker build` executes attacker-controlled code through `npm` lifecycle
scripts, MSBuild targets, and `RUN` lines, and any BuildKit secret mounted into that build is
readable by it. This repo shipped exactly that mistake in #56; don't reintroduce it. The
"approve and run workflows" button is not a meaningful guard, because it looks identical for
benign and hostile PRs.

### Giving a fork PR a real preview deployment

Read the diff first. You are vouching for code that is about to run with our credentials.
Then push the contributor's branch to a repo-owned branch:

```bash
git fetch origin pull/<PR-NUMBER>/head
git push origin FETCH_HEAD:preview/pr-<PR-NUMBER>
```

That branch is in this repo, so the ordinary same-repo path applies and the images publish.
Re-read the diff before re-pushing whenever the contributor updates the PR — a stale
`preview/*` branch is harmless, a blindly refreshed one is not.

### Testing Sentry instrumentation

Two credentials, two different jobs — don't confuse one for the other:

- **Runtime events** ("does my new instrumentation actually fire?") need `Sentry__Dsn`,
  injected as an environment variable at run time. `compose.preview.yaml` already passes it.
  No build-time secret is involved.
- **Symbolicated stack traces, source context, and releases** need `SENTRY_AUTH_TOKEN` at
  *build* time, consumed as a BuildKit secret by the Dockerfiles.

When that token is absent the Dockerfiles fall back to `/p:UseSentryCLI=false` and the build
succeeds silently. So if source context is missing in Sentry, suspect a missing or misnamed
secret before suspecting the SDK.

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

If you learn more about the feed sources, user-facing product goals, or deployment constraints, update this file.
