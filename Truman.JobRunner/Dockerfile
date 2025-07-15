FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Truman.JobRunner/Truman.JobRunner.csproj", "Truman.JobRunner/"]
COPY Directory.Packages.props ./
RUN dotnet restore "Truman.JobRunner/Truman.JobRunner.csproj"
COPY . .
RUN dotnet build "Truman.JobRunner/Truman.JobRunner.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Truman.JobRunner/Truman.JobRunner.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Truman.JobRunner.dll"] 