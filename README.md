# Truman

## Local Docker Compose

This repo now has a simple Docker Compose path for the merged app + Postgres setup.

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
- If `SENTRY_AUTH_TOKEN_TRUMAN` is present in your shell or `.env`, the image build can use it for Sentry artifact upload; otherwise the build skips that step.
- This Compose path is intended to replace kind/Helm for simple local/VPS deployment work.
