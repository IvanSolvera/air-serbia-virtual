# ==============================================================================
# AirSerbiaVirtua.Api — multi-stage build
# Build context = repo root:  docker build -f AirSerbiaVirtua.Api/Dockerfile .
# ==============================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY AirSerbiaVirtua.Api/AirSerbiaVirtua.Api.csproj AirSerbiaVirtua.Api/
RUN dotnet restore AirSerbiaVirtua.Api/AirSerbiaVirtua.Api.csproj

COPY AirSerbiaVirtua.Api/ AirSerbiaVirtua.Api/
RUN dotnet publish AirSerbiaVirtua.Api/AirSerbiaVirtua.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Migrations are NOT applied here (Production skips auto-migrate by design) —
# run the EF bundle as a deploy step first; see doc/deployment.md.
EXPOSE 5000
ENTRYPOINT ["dotnet", "AirSerbiaVirtua.Api.dll"]
