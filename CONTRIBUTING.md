# Contributing

The following dependencies will be required to work with this project locally.

## Dependencies

### Docker Desktop

Please install Docker Desktop via their instructions (Install Docker Desktop on [Mac](https://docs.docker.com/desktop/install/mac-install/), [Windows](https://docs.docker.com/desktop/install/windows-install/), or [Linux](https://docs.docker.com/desktop/install/linux-install/).)

### Devbox

All other software dependencies to work locally with the solution are defined in the `devbox.json` and `devbox.lock` files in the root directory.

Please install Devbox according to their instructions: https://www.jetify.com/devbox/docs/installing_devbox/

Once installed you can run:

```
devbox shell
```

from anywhere in the repo and devbox will use Nix package manager to install a copy of all of the required software in an isolated environment.

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

The `.env` file uses hierarchical configuration keys with double underscores (`__`) to map to ASP.NET Core's configuration sections:

- `Authentication__Google__ClientId` maps to `Authentication:Google:ClientId`
- `Email__Brevo__ApiKey` maps to `Email:Brevo:ApiKey`
- `AI__ApiKey` maps to `AI:ApiKey`

This approach ensures that sensitive configuration is kept out of source control and can be easily overridden in different environments (local development, Docker, production).
