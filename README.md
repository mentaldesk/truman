# Truman

Your reality is defined by the sum of all the little things you choose to pay attention to.

Consciously choosing the media you consume and the sentiment it conveys allows you to take control of your reality and focus on the things that are important to you.

Truman lets you create your own personalised news feed consisting of only the things you care about, presented the way you want.

I'm running my own personal instance at [https://truman.news](https://truman.news) but ultimately that feeds up stories that are likely to be of interest to me, but not to you. If you want your own personalised feed, you can run your own instance of Truman and configure it to your liking.

### Prerequisites

- Docker Engine
- Docker Compose plugin
- a local `.env` file in the repo root (copy from `env.example`)

### Start the app

```bash
cp env.example .env
# fill in real values as needed

docker compose up --build
```

The app should then be available at:

- `http://localhost:5001/`
- `http://localhost:5001/openapi/v1.json`

### Notes

- The app container builds the frontend and serves it from the API.
- Postgres runs as a separate container with a named volume.
- Compose injects `POSTGRES_HOST=postgres` for the app container.
- Sentry artifact upload is handled at build time via a BuildKit secret. In GitHub Actions, the `SENTRY_AUTH_TOKEN` repository secret is passed automatically. Local `docker compose` builds skip Sentry upload by default (the build falls back to `/p:UseSentryCLI=false` when the secret is absent).
- This Compose path is intended for simple local/VPS deployment work or staging environments for branches/PRs.