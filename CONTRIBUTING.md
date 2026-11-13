# Contributing

## Prerequisites

- [Docker Engine](https://docs.docker.com/engine/install/) with the Docker Compose plugin
- That's it for running the app. For running tests locally (outside Docker), you'll also need:
  - [.NET SDK](https://dotnet.microsoft.com/download) (version matching `global.json`)
  - [Node.js](https://nodejs.org/) (LTS)

## Setup

1. Copy the example env file and fill in values:
   ```bash
   cp env.example .env
   ```
   Update `.env` with your credentials — OAuth providers, Brevo API key, Google AI API key, and database connection string.

2. Start the app:
   ```bash
   docker compose up --build
   ```

## Trying a branch end to end

`scripts/try.sh` builds this checkout, brings it up on a free port against a throwaway Postgres,
fills it with made-up sample stories, signs you in as `try@localhost` and opens it, then removes the
stack and its database when you quit. It needs no `.env`, so it works in a bare checkout or a scratch worktree:

```bash
TRUMAN_TRY_ENV=path/to/keys.env scripts/try.sh
```

That file supplies `AI__ApiKey`, `AI__Model` and `Jev__ApiKey` — the JobRunner reads all three while
building its host, so even `--run-migrations` needs them. Everything else is generated per run: the
database credentials, the JWT key, and an empty `Sentry__Dsn` and `Email__Brevo__ApiKey`, so a try
reports nothing and emails nobody.

The sample stories are in `src/Truman.Samples/SampleStories.json`. No feeds or AI are
used, so a try is up in seconds and shows the same stories every time: every tone from 1 to 10,
stories matching several of the reader's values, one or none, and every presenter. The try reader
starts with a profile, so the feed, badges and Why it's here box show on first load.

`--live` fetches the feeds and analyses up to 10 real stories instead, which takes minutes and spends
real AI quota. A task that changes fetching or analysis needs a live try, and its PR should say so.

`--no-seed` adds no stories at all; `--no-login` leaves you at the sign-in screen.

## Configuration structure

`.env` uses ASP.NET Core's double-underscore convention to represent nested config keys:

- `Authentication__Google__ClientId` → `Authentication:Google:ClientId`
- `Email__Brevo__ApiKey` → `Email:Brevo:ApiKey`
- `AI__ApiKey` → `AI:ApiKey`

This keeps secrets out of source control and works consistently across local Docker and production environments.

## Running tests

```bash
dotnet restore truman.sln
dotnet build truman.sln
dotnet test truman.sln
```

Frontend:

```bash
cd src/web
npm install
npm run test
npm run build
```
