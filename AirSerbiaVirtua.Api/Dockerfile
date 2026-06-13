# ==============================================================================
# AirSerbiaVirtua.Api — multi-stage build
# Build context = repo root:  docker build -f AirSerbiaVirtua.Api/Dockerfile .
# The API references AirSerbiaVirtua.Contracts (since W4), so both projects are
# restored/published here. A self-contained EF migrations bundle is also baked
# in at /app/efbundle — the compose 'migrator' service runs it before the API
# starts (Production deliberately does NOT auto-migrate).
# ==============================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore (csproj layer cached until a .csproj changes)
COPY AirSerbiaVirtua.Contracts/AirSerbiaVirtua.Contracts.csproj AirSerbiaVirtua.Contracts/
COPY AirSerbiaVirtua.Api/AirSerbiaVirtua.Api.csproj AirSerbiaVirtua.Api/
RUN dotnet restore AirSerbiaVirtua.Api/AirSerbiaVirtua.Api.csproj

# Publish
COPY AirSerbiaVirtua.Contracts/ AirSerbiaVirtua.Contracts/
COPY AirSerbiaVirtua.Api/ AirSerbiaVirtua.Api/
RUN dotnet publish AirSerbiaVirtua.Api/AirSerbiaVirtua.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# Self-contained migrations bundle (linux-x64) for the migrator service.
RUN dotnet tool install --global dotnet-ef --version 10.* \
 && /root/.dotnet/tools/dotnet-ef migrations bundle \
        --project AirSerbiaVirtua.Api/AirSerbiaVirtua.Api.csproj \
        --configuration Release --self-contained -r linux-x64 \
        -o /app/publish/efbundle

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
RUN chmod +x ./efbundle

# Migrations are NOT applied by the API at startup (Production skips auto-migrate);
# the compose 'migrator' service runs ./efbundle first. See doc/deployment.md.
EXPOSE 5000
ENTRYPOINT ["dotnet", "AirSerbiaVirtua.Api.dll"]
