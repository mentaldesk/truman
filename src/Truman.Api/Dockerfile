FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5001

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
ARG SENTRY_AUTH_TOKEN
ENV SENTRY_AUTH_TOKEN=$SENTRY_AUTH_TOKEN
WORKDIR /src
COPY ["Truman.Api/Truman.Api.csproj", "Truman.Api/"]
COPY Directory.Packages.props ./
COPY Directory.Build.props ./
RUN dotnet restore "Truman.Api/Truman.Api.csproj"
COPY . .
RUN dotnet build "Truman.Api/Truman.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Truman.Api/Truman.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Truman.Api.dll"]
