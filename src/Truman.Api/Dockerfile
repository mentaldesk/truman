FROM node:22-alpine AS web-build
WORKDIR /src/web
COPY web/package.json web/package-lock.json ./
RUN npm ci
COPY web/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Truman.Api/Truman.Api.csproj", "Truman.Api/"]
COPY ["Truman.Data/Truman.Data.csproj", "Truman.Data/"]
COPY Directory.Packages.props ./
COPY Directory.Build.props ./
RUN dotnet restore "Truman.Api/Truman.Api.csproj"
COPY . .
RUN --mount=type=secret,id=sentry_auth_token \
    SENTRY_AUTH_TOKEN=$(cat /run/secrets/sentry_auth_token 2>/dev/null || true) && \
    if [ -n "$SENTRY_AUTH_TOKEN" ]; then \
      export SENTRY_AUTH_TOKEN && \
      dotnet publish "Truman.Api/Truman.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false; \
    else \
      dotnet publish "Truman.Api/Truman.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false /p:UseSentryCLI=false; \
    fi

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 5001
COPY --from=build /app/publish .
COPY --from=web-build /src/web/build /app/web/build
ENTRYPOINT ["dotnet", "Truman.Api.dll"]
