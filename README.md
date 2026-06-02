# AirSerbiaVirtua.Api

ASP.NET Core 8 Web API backend for the Air Serbia Virtua virtual airline,
using PostgreSQL via EF Core (Npgsql).

## Prerequisites
- .NET 8 SDK
- PostgreSQL 14+

## Configuration

### Connection string
Set via user-secrets (dev) or `ConnectionStrings__Default` env var (prod):

```bash
cd AirSerbiaVirtua.Api
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=airserbiavirtua;Username=postgres;Password=postgres"
```

### JWT signing key (required)
The API refuses to start without a valid `Jwt:Key` (must be ≥ 32 bytes). The key
is **never** committed; configure it via user-secrets in dev:

```bash
cd AirSerbiaVirtua.Api
dotnet user-secrets set "Jwt:Key" "$(openssl rand -base64 48)"
```

In production set the `Jwt__Key` environment variable to a 48+ byte random value.

## First run
```bash
dotnet restore
dotnet tool install --global dotnet-ef      # if not already installed
dotnet ef database update                   # creates schema + seeds LYBE & fleet
dotnet run
```
In Development the app also calls `Database.Migrate()` on startup, so `dotnet run`
alone will apply migrations against a running database.

Swagger UI: `https://localhost:<port>/swagger` — health probe: `/health`.

## Data model
Entities live in `Models/`, the context in `Data/AppDbContext.cs`. Enums are
stored as strings; `Rank.AllowedAircraftTypes` and `Route.Days` are stored as
`jsonb`. Seed data (hub **LYBE** and the A319/A320/A320neo/A321neo/ATR72/E195/A330
fleet) is defined in `Data/SeedData.cs` and applied through the migration.

## Migrations
The initial migration `Migrations/20260602120000_InitialCreate` was authored to
match the model. To regenerate from scratch instead:
```bash
rm -rf Migrations
dotnet ef migrations add InitialCreate
```
