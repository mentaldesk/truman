FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
ARG SENTRY_AUTH_TOKEN
ARG APP_VERSION=dev
ENV SENTRY_AUTH_TOKEN=$SENTRY_AUTH_TOKEN
WORKDIR /src
COPY ["Truman.JobRunner/Truman.JobRunner.csproj", "Truman.JobRunner/"]
COPY Directory.Packages.props ./
COPY Directory.Build.props ./
RUN dotnet restore "Truman.JobRunner/Truman.JobRunner.csproj"
COPY . .
RUN dotnet build "Truman.JobRunner/Truman.JobRunner.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
ARG APP_VERSION=dev
RUN dotnet publish "Truman.JobRunner/Truman.JobRunner.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false /p:Version=$APP_VERSION /p:InformationalVersion=$APP_VERSION

FROM base AS final
ARG APP_VERSION=dev
ARG GIT_SHA=unknown
LABEL org.opencontainers.image.title="truman-jobrunner" \
      org.opencontainers.image.version="$APP_VERSION" \
      org.opencontainers.image.revision="$GIT_SHA" \
      org.opencontainers.image.source="https://github.com/${GITHUB_REPOSITORY:-your/repo}" \
      org.opencontainers.image.vendor="truman"
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Truman.JobRunner.dll"]
