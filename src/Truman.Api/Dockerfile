FROM node:26-alpine AS web-build
# Supplied by CI. The Vite plugin cannot work a release out here: it reads git HEAD, and .git is
# not in the build context. Empty locally, where the plugin falls back to auto-detection.
ARG SENTRY_RELEASE=
WORKDIR /src/web
COPY web/package.json web/package-lock.json ./
RUN npm ci
COPY web/ ./
# The Vite plugin uploads source maps during the build, so it needs the token here. Mounted as a
# BuildKit secret rather than a build arg, which would be recorded in the image history.
RUN --mount=type=secret,id=sentry_auth_token \
    export SENTRY_AUTH_TOKEN=$(cat /run/secrets/sentry_auth_token 2>/dev/null); \
    npm run build

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
# Both supplied by CI, which has the git history this build does not: MinVer needs the version,
# and SourceRevisionId puts the commit into InformationalVersion and hence the Sentry release.
ARG VERSION=
ARG COMMIT_SHA=
WORKDIR /src
COPY ["Truman.Api/Truman.Api.csproj", "Truman.Api/"]
COPY ["Truman.Data/Truman.Data.csproj", "Truman.Data/"]
COPY Directory.Packages.props ./
COPY Directory.Build.props ./
RUN dotnet restore "Truman.Api/Truman.Api.csproj"
COPY . .
# Since 6.12.1 sentry-cli skips the upload with a warning when the token is absent, as on fork PRs.
RUN --mount=type=secret,id=sentry_auth_token \
    export SENTRY_AUTH_TOKEN=$(cat /run/secrets/sentry_auth_token 2>/dev/null); \
    if [ -n "$VERSION" ]; then export MinVerVersionOverride="$VERSION"; fi; \
    dotnet publish "Truman.Api/Truman.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false \
      ${COMMIT_SHA:+/p:SourceRevisionId=$COMMIT_SHA}

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 5001
COPY --from=build /app/publish .
COPY --from=web-build /src/web/build /app/web/build
ENTRYPOINT ["dotnet", "Truman.Api.dll"]
