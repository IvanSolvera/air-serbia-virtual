# ==============================================================================
# AirSerbiaVirtua.Web — Blazor Web App (SSR + WASM client)
# Build context = repo root:  docker build -f AirSerbiaVirtua.Web/Dockerfile .
# ==============================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY AirSerbiaVirtua.Contracts/AirSerbiaVirtua.Contracts.csproj AirSerbiaVirtua.Contracts/
COPY AirSerbiaVirtua.Web.Client/AirSerbiaVirtua.Web.Client.csproj AirSerbiaVirtua.Web.Client/
COPY AirSerbiaVirtua.Web/AirSerbiaVirtua.Web.csproj AirSerbiaVirtua.Web/
RUN dotnet restore AirSerbiaVirtua.Web/AirSerbiaVirtua.Web.csproj

COPY AirSerbiaVirtua.Contracts/ AirSerbiaVirtua.Contracts/
COPY AirSerbiaVirtua.Web.Client/ AirSerbiaVirtua.Web.Client/
COPY AirSerbiaVirtua.Web/ AirSerbiaVirtua.Web/
RUN dotnet publish AirSerbiaVirtua.Web/AirSerbiaVirtua.Web.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "AirSerbiaVirtua.Web.dll"]
