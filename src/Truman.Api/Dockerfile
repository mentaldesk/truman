FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5001

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
ARG APP_VERSION=dev
ARG SENTRY_AUTH_TOKEN
ENV SENTRY_AUTH_TOKEN=$SENTRY_AUTH_TOKEN
WORKDIR /src
COPY ["Truman.Api/Truman.Api.csproj", "Truman.Api/"]
COPY ["Truman.Data/Truman.Data.csproj", "Truman.Data/"]
COPY Directory.Packages.props ./
COPY Directory.Build.props ./
RUN dotnet restore "Truman.Api/Truman.Api.csproj"
COPY . .
RUN dotnet build "Truman.Api/Truman.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
ARG APP_VERSION=dev
ARG SENTRY_AUTH_TOKEN
ENV SENTRY_AUTH_TOKEN=$SENTRY_AUTH_TOKEN
RUN dotnet publish "Truman.Api/Truman.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false /p:Version=$APP_VERSION /p:InformationalVersion=$APP_VERSION

FROM base AS final
ARG APP_VERSION=dev
ARG GIT_SHA=unknown
ARG REPO_URL="https://github.com/mentaldesk/truman"
LABEL org.opencontainers.image.title="truman-api" \
      org.opencontainers.image.version="$APP_VERSION" \
      org.opencontainers.image.revision="$GIT_SHA" \
      org.opencontainers.image.source="$REPO_URL" \
      org.opencontainers.image.vendor="truman"
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Truman.Api.dll"]
