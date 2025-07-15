FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Truman.Api/Truman.Api.csproj", "Truman.Api/"]
COPY Directory.Packages.props ./
RUN dotnet restore "Truman.Api/Truman.Api.csproj"
COPY . .
RUN dotnet build "Truman.Api/Truman.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
# Install Entity Framework tools
RUN dotnet tool install --global dotnet-ef
ENV PATH="${PATH}:/root/.dotnet/tools"
RUN dotnet publish "Truman.Api/Truman.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false
# Create EF migrations bundle
RUN dotnet ef migrations bundle --startup-project "Truman.Api/Truman.Api.csproj" --project "Truman.Data/Truman.Data.csproj" -o /app/publish/efbundle

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Truman.Api.dll"]
