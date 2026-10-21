FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
# Both supplied by CI, which has the git history this build does not: MinVer needs the version,
# and SourceRevisionId puts the commit into InformationalVersion and hence the Sentry release.
ARG VERSION=
ARG COMMIT_SHA=
WORKDIR /src
COPY ["Truman.JobRunner/Truman.JobRunner.csproj", "Truman.JobRunner/"]
COPY ["Truman.Data/Truman.Data.csproj", "Truman.Data/"]
COPY Directory.Packages.props ./
COPY Directory.Build.props ./
RUN dotnet restore "Truman.JobRunner/Truman.JobRunner.csproj"
COPY . .
# Since 6.12.1 sentry-cli skips the upload with a warning when the token is absent, as on fork PRs.
RUN --mount=type=secret,id=sentry_auth_token \
    export SENTRY_AUTH_TOKEN=$(cat /run/secrets/sentry_auth_token 2>/dev/null); \
    if [ -n "$VERSION" ]; then export MinVerVersionOverride="$VERSION"; fi; \
    dotnet publish "Truman.JobRunner/Truman.JobRunner.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false \
      ${COMMIT_SHA:+/p:SourceRevisionId=$COMMIT_SHA}

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Truman.JobRunner.dll"]
