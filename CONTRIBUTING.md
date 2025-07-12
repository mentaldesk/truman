# Contributing

The following dependencies will be required to work with this project locally.

## Dependencies

### .NET

- Download and install [the latest version of the .NET SDK](https://dotnet.microsoft.com/en-us/download) from Microsoft.
- Run `dotnet workload restore` to install the required workloads.
- Run `dotnet restore` do download the required NuGet packages.

### PostgreSQL

You can either install PostgreSQL locally or use Docker to run the PostgreSQL container in this repo.

To run it as a container:
```
docker compose up -d postgres
```

## Configuration

Most application configuration is stored and read from `.env` files.

### Setting up your environment

1. Copy `env.example` to `.env` in the root directory:
   ```bash
   cp env.example .env
   ```

2. Update the `.env` file with your actual credentials:
   - **OAuth Providers**: Get Google and Facebook OAuth credentials from their respective developer consoles
   - **Email**: Get your Brevo API key from the Brevo dashboard
   - **AI**: Get your Google AI API key from Google AI Studio
   - **Database**: Update the connection string if using different database credentials

### Configuration Structure

The `.env` file uses hierarchical configuration keys with double underscores (`__`) to map to ASP.NET Core's 
configuration sections:

- `Authentication__Google__ClientId` maps to `Authentication:Google:ClientId`
- `Email__Brevo__ApiKey` maps to `Email:Brevo:ApiKey`
- `AI__ApiKey` maps to `AI:ApiKey`

This approach ensures that sensitive configuration is kept out of source control and can be easily overridden in 
different environments (local development, Docker, production).
