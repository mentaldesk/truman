FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Truman.JobRunner/Truman.JobRunner.csproj", "Truman.JobRunner/"]
COPY ["Truman.Data/Truman.Data.csproj", "Truman.Data/"]
COPY Directory.Packages.props ./
COPY Directory.Build.props ./
RUN dotnet restore "Truman.JobRunner/Truman.JobRunner.csproj"
COPY . .
ARG SENTRY_AUTH_TOKEN
ENV SENTRY_AUTH_TOKEN=$SENTRY_AUTH_TOKEN
RUN if [ -n "$SENTRY_AUTH_TOKEN" ]; then \
      dotnet publish "Truman.JobRunner/Truman.JobRunner.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false; \
    else \
      dotnet publish "Truman.JobRunner/Truman.JobRunner.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false /p:UseSentryCLI=false; \
    fi

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Truman.JobRunner.dll"]
