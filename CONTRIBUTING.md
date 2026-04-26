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
