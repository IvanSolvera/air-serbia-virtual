# Phase W4 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Pilot prepares a booking on the web ("dispatch ready"), desktop pulls it and starts the flight in one click; all DTOs converge into `AirSerbiaVirtua.Contracts`; every segment gets automated tests (first test projects in the solution).

**Architecture:** Per approved spec `doc/w4-desktop-slimdown-design.md`. One nullable column on `Booking`, three endpoints on `BookingsController`, a web Briefing page, a desktop `DispatchService` + Pilot Centre card, and a three-way DTO consolidation (API + desktop + web → Contracts). Seams `IApiService` (desktop) and `IPortalApi` (web) make the clients testable.

**Tech Stack:** .NET 10, EF Core 10 + Npgsql, Blazor WASM, WPF (CommunityToolkit.Mvvm), NUnit 4 + Moq + FluentAssertions, bUnit, WebApplicationFactory + Testcontainers (Postgres).

**Conventions for every task:**
- Work on branch `dev`. Conventional commits (`feat`/`test`/`refactor`/`chore`).
- Solution file is `AirSerbiaVirtual.sln` at repo root. API runs dev on `:5036`.
- Package versions: run `dotnet add package <name>` **without** a version (latest stable). Expected majors: NUnit 4.x, NUnit3TestAdapter 5.x, Microsoft.NET.Test.Sdk 17/18.x, Moq 4.x, FluentAssertions 8.x, Testcontainers.PostgreSql 4.x, Microsoft.AspNetCore.Mvc.Testing 10.x, bunit 1.x.
- `dotnet test` for `Tests.Unit` always needs `-p:Platform=x64` (it references the x64-only Desktop/Core projects).
- Docker Desktop must be running for `Tests.Api`. If Testcontainers cannot start (no Docker), see the fallback note in Task 5 — do not silently skip the tests.

---

## File map (who owns what)

| File | Responsibility |
|---|---|
| `AirSerbiaVirtua.Contracts/Enums.cs` (new) | The five shared status enums |
| `AirSerbiaVirtua.Contracts/VaContracts.cs` | All wire records (single source) |
| `AirSerbiaVirtua.Contracts/FuelEstimator.cs` (new) | Shared planning-fuel math |
| `AirSerbiaVirtua.Api/Controllers/BookingsController.cs` | + dispatch endpoints, `/active` |
| `AirSerbiaVirtua.Api/Models/Booking.cs` | + `DispatchReadyAtUtc` |
| `AirSerbiaVirtua.Acars.Core/ApiContracts.cs` | Shrinks to sim-side helpers only |
| `AirSerbiaVirtua.Acars.Core/IApiService.cs` (new) | Desktop API seam |
| `AirSerbiaVirtua.Acars.Desktop/Services/DispatchService.cs` (new) | Shared start-flight sequence |
| `AirSerbiaVirtua.Web.Client/Services/IPortalApi.cs` (new) | Web API seam |
| `AirSerbiaVirtua.Web.Client/Pages/PortalBriefing.razor` (new) | Web Briefing page |
| `AirSerbiaVirtua.Tests.Unit` (new project) | Contracts + desktop unit tests |
| `AirSerbiaVirtua.Tests.Api` (new project) | API integration tests (real Postgres) |
| `AirSerbiaVirtua.Tests.Web` (new project) | bUnit component tests |

---

### Task 1: Scaffold `AirSerbiaVirtua.Tests.Unit`

**Files:**
- Create: `AirSerbiaVirtua.Tests.Unit/AirSerbiaVirtua.Tests.Unit.csproj`
- Create: `AirSerbiaVirtua.Tests.Unit/CanaryTests.cs`
- Modify: `AirSerbiaVirtual.sln` (via `dotnet sln add`)

- [ ] **Step 1: Create the project**

```powershell
dotnet new nunit -o AirSerbiaVirtua.Tests.Unit -n AirSerbiaVirtua.Tests.Unit
dotnet sln AirSerbiaVirtual.sln add AirSerbiaVirtua.Tests.Unit\AirSerbiaVirtua.Tests.Unit.csproj
```

- [ ] **Step 2: Edit the csproj** — x64 (Desktop/Core are x64-only), WPF-capable TFM, references. Replace the generated `<PropertyGroup>`/`<ItemGroup>`s so the file reads:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <Platforms>x64</Platforms>
    <PlatformTarget>x64</PlatformTarget>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="FluentAssertions" />
    <PackageReference Include="Moq" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="NUnit" />
    <PackageReference Include="NUnit3TestAdapter" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\AirSerbiaVirtua.Contracts\AirSerbiaVirtua.Contracts.csproj" />
    <ProjectReference Include="..\AirSerbiaVirtua.Acars.Core\AirSerbiaVirtua.Acars.Core.csproj" />
    <ProjectReference Include="..\AirSerbiaVirtua.Acars.Desktop\AirSerbiaVirtua.Acars.Desktop.csproj" />
  </ItemGroup>

</Project>
```

Then pin the packages to latest stable (no version attribute = run these to materialize versions):

```powershell
dotnet add AirSerbiaVirtua.Tests.Unit package FluentAssertions
dotnet add AirSerbiaVirtua.Tests.Unit package Moq
dotnet add AirSerbiaVirtua.Tests.Unit package Microsoft.NET.Test.Sdk
dotnet add AirSerbiaVirtua.Tests.Unit package NUnit
dotnet add AirSerbiaVirtua.Tests.Unit package NUnit3TestAdapter
```

- [ ] **Step 3: Replace the template test with a canary** — `AirSerbiaVirtua.Tests.Unit/CanaryTests.cs`:

```csharp
using FluentAssertions;

namespace AirSerbiaVirtua.Tests.Unit;

public class CanaryTests
{
    [Test]
    public void TestInfrastructure_Works() => true.Should().BeTrue();
}
```

Delete the template `UnitTest1.cs`. If `Test`/`[Test]` is not found, add `global using NUnit.Framework;` via a `Usings.cs` file:

```csharp
global using NUnit.Framework;
```

- [ ] **Step 4: Run it**

Run: `dotnet test AirSerbiaVirtua.Tests.Unit\AirSerbiaVirtua.Tests.Unit.csproj -p:Platform=x64`
Expected: 1 passed.

- [ ] **Step 5: Commit**

```powershell
git add -A
git commit -m "test: scaffold Tests.Unit project (NUnit/Moq/FluentAssertions, x64)"
```

---

### Task 2: `FuelEstimator` in Contracts (TDD)

The fuel math currently lives privately in `AirSerbiaVirtua.Acars.Desktop/ViewModels/BriefingViewModel.cs:110-125`. It moves to Contracts so web and desktop share identical numbers. Note: the original signature takes `plannedMin` but never uses it — drop the parameter.

**Files:**
- Create: `AirSerbiaVirtua.Contracts/FuelEstimator.cs`
- Test: `AirSerbiaVirtua.Tests.Unit/Contracts/FuelEstimatorTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using AirSerbiaVirtua.Contracts;
using FluentAssertions;

namespace AirSerbiaVirtua.Tests.Unit.Contracts;

public class FuelEstimatorTests
{
    // burn/nm: A319=7.0, A320=7.4, A321=8.2, ATR72=2.6, E195=5.6, default=6.5
    // reserve = round(burn * 360 * 0.75) + 400

    [TestCase("A319", 300, 2100, 2290)]
    [TestCase("A320", 300, 2220, 2398)]
    [TestCase("A321", 100, 820, 2614)]
    [TestCase("ATR72", 200, 520, 1102)]
    [TestCase("E195", 100, 560, 1912)]
    [TestCase("B738", 100, 650, 2155)]    // unknown type -> default burn
    public void Estimate_ComputesTripAndReserve(string type, int nm, int expTrip, int expReserve)
    {
        var (trip, reserve) = FuelEstimator.Estimate(type, nm);
        trip.Should().Be(expTrip);
        reserve.Should().Be(expReserve);
    }

    [Test]
    public void Estimate_IsCaseInsensitive()
    {
        FuelEstimator.Estimate("a319", 300).Should().Be(FuelEstimator.Estimate("A319", 300));
    }

    [Test]
    public void Estimate_ZeroDistance_HasZeroTripButFullReserve()
    {
        var (trip, reserve) = FuelEstimator.Estimate("A319", 0);
        trip.Should().Be(0);
        reserve.Should().Be(2290);
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test AirSerbiaVirtua.Tests.Unit\AirSerbiaVirtua.Tests.Unit.csproj -p:Platform=x64`
Expected: compile error `'FuelEstimator' does not exist` — that's the red state.

- [ ] **Step 3: Implement** — `AirSerbiaVirtua.Contracts/FuelEstimator.cs`:

```csharp
namespace AirSerbiaVirtua.Contracts;

/// <summary>
/// Rough planning fuel shared by the web Briefing page and the desktop client:
/// cruise burn per nm by type + a fixed reserve block (taxi + contingency +
/// 45 min final reserve). Indicative only — not a real OFP.
/// </summary>
public static class FuelEstimator
{
    /// <returns>(trip kg, reserve kg)</returns>
    public static (int TripKg, int ReserveKg) Estimate(string aircraftType, int distanceNm)
    {
        double burnPerNm = aircraftType.ToUpperInvariant() switch
        {
            "A319" => 7.0,
            "A320" => 7.4,
            "A321" => 8.2,
            "ATR72" or "AT72" or "ATR" => 2.6,
            "E195" or "E190" => 5.6,
            _ => 6.5
        };
        int trip = (int)Math.Round(distanceNm * burnPerNm);
        // Reserve: 45 min at ~ (burnPerNm * 360 nm/h) plus a fixed taxi/contingency pad.
        int reserve = (int)Math.Round(burnPerNm * 360 * 0.75) + 400;
        return (trip, reserve);
    }
}
```

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test AirSerbiaVirtua.Tests.Unit\AirSerbiaVirtua.Tests.Unit.csproj -p:Platform=x64`
Expected: all green (9 tests + canary).

- [ ] **Step 5: Commit**

```powershell
git add -A
git commit -m "feat(contracts): shared FuelEstimator with unit tests"
```

---

### Task 3: Contracts convergence — enums + canonical records + wire-shape guards

This task makes `Contracts` the single source of types. The API and desktop still compile against their own copies (removed in Tasks 4 and 7); only the **web** consumes Contracts today, so web fallout is fixed here.

**Files:**
- Create: `AirSerbiaVirtua.Contracts/Enums.cs`
- Rewrite: `AirSerbiaVirtua.Contracts/VaContracts.cs`
- Test: `AirSerbiaVirtua.Tests.Unit/Contracts/WireShapeTests.cs`
- Modify (compile fallout): `AirSerbiaVirtua.Web.Client/Pages/PortalBook.razor`, plus any other Web/Web.Client file comparing `Status` as `int` (find with the grep in Step 4)

- [ ] **Step 1: Write the failing wire-shape tests** — `AirSerbiaVirtua.Tests.Unit/Contracts/WireShapeTests.cs`. These lock the JSON the API has been emitting (camelCase, enums as numbers) so the convergence in Tasks 4/7 cannot change the protocol:

```csharp
using System.Text.Json;
using AirSerbiaVirtua.Contracts;
using FluentAssertions;

namespace AirSerbiaVirtua.Tests.Unit.Contracts;

/// <summary>
/// Golden-JSON guards: serializing the canonical records with web defaults must
/// produce exactly what the API emitted before convergence (camelCase keys,
/// enums as numbers). If one of these fails, a client protocol just broke.
/// </summary>
public class WireShapeTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static readonly DateTimeOffset T = new(2026, 6, 6, 14, 32, 0, TimeSpan.Zero);

    [Test]
    public void BookingInfo_WithDispatchFlag_SerializesToExpectedJson()
    {
        var b = new BookingInfo(7, 3, "JU360", "LYBE", "LOWW", "A319", 55,
            new DateOnly(2026, 6, 6), BookingStatus.Open, T);
        JsonSerializer.Serialize(b, Web).Should().Be(
            "{\"id\":7,\"routeId\":3,\"flightNumber\":\"JU360\",\"depIcao\":\"LYBE\"," +
            "\"arrIcao\":\"LOWW\",\"aircraftType\":\"A319\",\"plannedMinutes\":55," +
            "\"date\":\"2026-06-06\",\"status\":0,\"dispatchReadyAtUtc\":\"2026-06-06T14:32:00+00:00\"}");
    }

    [Test]
    public void BookingInfo_WithoutDispatchFlag_SerializesNull()
    {
        var b = new BookingInfo(7, 3, "JU360", "LYBE", "LOWW", "A319", 55,
            new DateOnly(2026, 6, 6), BookingStatus.Confirmed);
        JsonSerializer.Serialize(b, Web).Should().Contain("\"status\":1")
            .And.Contain("\"dispatchReadyAtUtc\":null");
    }

    [Test]
    public void PilotProfile_SerializesStatusAsNumber()
    {
        var p = new PilotProfile(1, "ASL001", "Test Pilot", "t@asv.test",
            2, "First Officer", 12.5m, PilotStatus.Active, "LYBE", T, false);
        var json = JsonSerializer.Serialize(p, Web);
        json.Should().Contain("\"status\":1").And.Contain("\"callsign\":\"ASL001\"")
            .And.Contain("\"isAdmin\":false").And.Contain("\"totalHours\":12.5");
    }

    [Test]
    public void RouteInfo_SerializesToExpectedJson()
    {
        var r = new RouteInfo(3, "JU360", "LYBE", "LOWW", "A319", 300, 55, [1, 2, 3]);
        JsonSerializer.Serialize(r, Web).Should().Be(
            "{\"id\":3,\"flightNumber\":\"JU360\",\"depIcao\":\"LYBE\",\"arrIcao\":\"LOWW\"," +
            "\"aircraftType\":\"A319\",\"distanceNm\":300,\"plannedMinutes\":55,\"days\":[1,2,3]}");
    }

    [Test]
    public void PirepListItem_SerializesStatusAsNumber()
    {
        var p = new PirepListItem(9, "JU360", "LYBE", "LOWW", "A319", "YU-API",
            T, T.AddMinutes(55), 55, 48, 2100, -180, 92, PirepStatus.Accepted);
        JsonSerializer.Serialize(p, Web).Should()
            .Contain("\"status\":1").And.Contain("\"landingRateFpm\":-180");
    }

    [Test]
    public void PositionReport_SerializesPhaseAsNumber()
    {
        var id = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var pr = new PositionReport(id, T, 44.8, 20.3, 35000, 447, FlightPhase.Cruise);
        JsonSerializer.Serialize(pr, Web).Should()
            .Contain("\"clientReportId\":\"11111111-2222-3333-4444-555555555555\"")
            .And.Contain("\"phase\":5");
    }

    [Test]
    public void FlightStartResponse_RoundTrips()
    {
        var json = "{\"flightSessionId\":12,\"routeId\":3,\"aircraftId\":4," +
                   "\"aircraftRegistration\":\"YU-API\",\"startedAtUtc\":\"2026-06-06T14:32:00+00:00\"}";
        var r = JsonSerializer.Deserialize<FlightStartResponse>(json, Web)!;
        r.Should().Be(new FlightStartResponse(12, 3, 4, "YU-API", T));
    }

    [Test]
    public void LoginResponse_RoundTrips()
    {
        var tokens = new AuthTokens("at", T, "rt", T.AddDays(7));
        var pilot = new PilotProfile(1, "ASL001", "Test Pilot", "t@asv.test",
            2, "First Officer", 12.5m, PilotStatus.Active, "LYBE", T, true);
        var login = new LoginResponse(tokens, pilot);
        var roundTripped = JsonSerializer.Deserialize<LoginResponse>(
            JsonSerializer.Serialize(login, Web), Web);
        roundTripped.Should().Be(login);
    }

    [Test]
    public void EnumOrdinals_MatchTheDatabaseAndClients()
    {
        ((int)BookingStatus.Open).Should().Be(0);
        ((int)BookingStatus.Confirmed).Should().Be(1);
        ((int)BookingStatus.Flown).Should().Be(2);
        ((int)BookingStatus.Cancelled).Should().Be(3);
        ((int)BookingStatus.Expired).Should().Be(4);
        ((int)PirepStatus.Aborted).Should().Be(4);
        ((int)PilotStatus.Banned).Should().Be(4);
        ((int)FlightPhase.Shutdown).Should().Be(10);
        ((int)AircraftStatus.Retired).Should().Be(4);
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test AirSerbiaVirtua.Tests.Unit\AirSerbiaVirtua.Tests.Unit.csproj -p:Platform=x64`
Expected: compile errors (`BookingStatus` not found, `BookingInfo` ctor arity, missing `PositionReport`/`FlightStartResponse`). Red.

- [ ] **Step 3a: Create `AirSerbiaVirtua.Contracts/Enums.cs`** — verbatim move of the five enums from `AirSerbiaVirtua.Api/Models/Enums.cs` (which is deleted in Task 4), namespace changed:

```csharp
namespace AirSerbiaVirtua.Contracts;

/// <summary>Lifecycle state of a virtual pilot.</summary>
public enum PilotStatus
{
    Pending = 0,
    Active = 1,
    OnLeave = 2,
    Inactive = 3,
    Banned = 4
}

/// <summary>Operational state of an airframe in the fleet.</summary>
public enum AircraftStatus
{
    Active = 0,
    InFlight = 1,
    Maintenance = 2,
    Stored = 3,
    Retired = 4
}

/// <summary>State of a route booking made by a pilot.</summary>
public enum BookingStatus
{
    Open = 0,
    Confirmed = 1,
    Flown = 2,
    Cancelled = 3,
    Expired = 4
}

/// <summary>State of a submitted pilot report (PIREP).</summary>
public enum PirepStatus
{
    Pending = 0,
    Accepted = 1,
    Rejected = 2,
    UnderReview = 3,
    /// <summary>Flight was abandoned (sim crash, user abort) — first-class outcome, not a stuck Pending.</summary>
    Aborted = 4
}

/// <summary>Phase of flight for a recorded position sample.</summary>
public enum FlightPhase
{
    Preflight = 0,
    Pushback = 1,
    Taxi = 2,
    Takeoff = 3,
    Climb = 4,
    Cruise = 5,
    Descent = 6,
    Approach = 7,
    Landing = 8,
    TaxiIn = 9,
    Shutdown = 10
}
```

- [ ] **Step 3b: Rewrite `AirSerbiaVirtua.Contracts/VaContracts.cs`** with the full converged set (this is the complete file):

```csharp
namespace AirSerbiaVirtua.Contracts;

// =============================================================================
// Shared DTO contracts for ALL AirSerbiaVirtua parties: the API serves these,
// the desktop ACARS client and the web portal consume them. Property names
// serialize to camelCase (System.Text.Json web defaults); enums serialize as
// numbers (no JsonStringEnumConverter anywhere — keep it that way or every
// client breaks). Wire shapes are locked by Tests.Unit/Contracts/WireShapeTests.
// =============================================================================

// ---- Auth -------------------------------------------------------------------
public record LoginRequest(string Callsign, string Password);

public record RefreshRequest(string RefreshToken);

public record RegisterRequest(
    string Callsign, string Name, string Email, string Password, string HubIcao,
    string? VatsimId = null);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record PilotProfile(
    int Id, string Callsign, string Name, string Email,
    int RankId, string RankName, decimal TotalHours,
    PilotStatus Status, string HubId, DateTimeOffset DateJoined,
    bool IsAdmin = false);

public record AuthTokens(
    string AccessToken,
    DateTimeOffset AccessExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshExpiresAtUtc);

public record LoginResponse(AuthTokens Tokens, PilotProfile Pilot);

public record RefreshResponse(AuthTokens Tokens);

// ---- Schedules / routes -------------------------------------------------------
public record RouteInfo(
    int Id,
    string FlightNumber,
    string DepIcao,
    string ArrIcao,
    string AircraftType,
    int DistanceNm,
    int PlannedMinutes,
    List<int> Days);

// ---- Fleet ---------------------------------------------------------------------
public record AircraftInfo(
    int Id,
    string Type,
    string Registration,
    string Status,
    string HubId);

// ---- Bookings -------------------------------------------------------------------
public record BookingInfo(
    int Id,
    int RouteId,
    string FlightNumber,
    string DepIcao,
    string ArrIcao,
    string AircraftType,
    int PlannedMinutes,
    DateOnly Date,
    BookingStatus Status,
    DateTimeOffset? DispatchReadyAtUtc = null);

public record CreateBookingRequest(int RouteId, DateOnly Date);

// ---- Flight session ---------------------------------------------------------
public record FlightStartRequest(int RouteId, int AircraftId, DateOnly Date);

public record FlightStartResponse(
    int FlightSessionId, int RouteId, int AircraftId,
    string AircraftRegistration, DateTimeOffset StartedAtUtc);

// ---- Outstation (ad-hoc charter) -----------------------------------------------
public record OutstationStartRequest(
    string DepIcao, string ArrIcao, int AircraftId, string FlightNumber);

// ---- Position report (POSREP) ----------------------------------------------
/// <summary>
/// POSREP wire payload. <see cref="ClientReportId"/> is generated once per
/// sample and stays stable across retries so the server can dedupe
/// (production-readiness item #5). The telemetry-to-report factory lives in
/// Acars.Core (PositionReports.FromTelemetry) — it needs sim-side types.
/// </summary>
public record PositionReport(
    Guid ClientReportId,
    DateTimeOffset Timestamp, double Lat, double Lon,
    int AltFt, int GsKts, FlightPhase Phase);

// ---- PIREP submission -------------------------------------------------------
public record PirepSubmitRequest(
    int FlightSessionId, DateTimeOffset DepActual, DateTimeOffset ArrActual,
    int BlockMin, int AirMin, int FuelUsedKg, int LandingRateFpm,
    string Source, string? RawJson);

public record PirepResult(
    int PirepId, PirepStatus Status, int Score, int LandingRateFpm,
    int BlockMin, decimal PilotTotalHours, int RankId, string RankName, bool Promoted);

// ---- Logbook ----------------------------------------------------------------
public record PirepListItem(
    int Id,
    string FlightNumber,
    string DepIcao,
    string ArrIcao,
    string AircraftType,
    string AircraftRegistration,
    DateTimeOffset DepActual,
    DateTimeOffset ArrActual,
    int BlockMin,
    int AirMin,
    int FuelUsedKg,
    int LandingRateFpm,
    int Score,
    PirepStatus Status);

// ---- Airport sync -----------------------------------------------------------
public record AirportDto(
    string Icao, string? Iata, string Name, string Country,
    double Lat, double Lon, int Elevation, int Revision);

public record AirportSyncResponse(int Since, int LatestRevision, int Count, List<AirportDto> Airports);

// ---- Weather ----------------------------------------------------------------------
public record MetarInfo(string Icao, string? Raw, DateTimeOffset? ObservedAtUtc);

// ---- Public stats (website landing) -------------------------------------------------
public record VaStats(int Pilots, int FlightsFlown, decimal HoursLogged, int Routes);

// ---- Admin -----------------------------------------------------------------------
public record AdminPilot(
    int Id, string Callsign, string Name, string Email,
    string RankName, decimal TotalHours,
    PilotStatus Status, string HubId, DateTimeOffset DateJoined,
    bool IsAdmin, string? VatsimId = null);
```

- [ ] **Step 4: Fix web compile fallout** — `Status` fields changed `int` → enum. Find every site:

```powershell
# from repo root
Get-ChildItem AirSerbiaVirtua.Web, AirSerbiaVirtua.Web.Client -Recurse -Include *.razor,*.cs |
  Select-String -Pattern "\.Status" | Select-Object Path, LineNumber, Line
```

Known fixes (apply the same pattern to anything else the grep finds):

In `AirSerbiaVirtua.Web.Client/Pages/PortalBook.razor` replace:

```csharp
private IEnumerable<BookingInfo> ActiveBookings =>
    _bookings?.Where(b => b.Status is 0 or 1) ?? [];
```

with:

```csharp
private IEnumerable<BookingInfo> ActiveBookings =>
    _bookings?.Where(b => b.Status is BookingStatus.Open or BookingStatus.Confirmed) ?? [];
```

and replace:

```csharp
private static string StatusLabel(int status) => status switch
{
    0 => "OPEN", 1 => "CONFIRMED", 2 => "FLOWN", 3 => "CANCELLED", _ => "EXPIRED"
};
```

with:

```csharp
private static string StatusLabel(BookingStatus status) =>
    status.ToString().ToUpperInvariant();
```

Apply equivalents in `PortalLogbook.razor` (PirepStatus), `PortalDashboard.razor`, `PortalProfile.razor`, and the Web (SSR) admin/fleet pages — wherever the grep shows int comparisons or int-typed helper params for `Status`. `AircraftInfo.Status` stays a `string` — leave fleet-status code alone.

- [ ] **Step 5: Build the affected projects + run unit tests**

Run: `dotnet build AirSerbiaVirtua.Web\AirSerbiaVirtua.Web.csproj`
Expected: 0 errors (building Web also builds Web.Client + Contracts).
Run: `dotnet test AirSerbiaVirtua.Tests.Unit\AirSerbiaVirtua.Tests.Unit.csproj -p:Platform=x64`
Expected: all green including the 9 wire-shape tests.

- [ ] **Step 6: Commit**

```powershell
git add -A
git commit -m "feat(contracts): converge enums + records into Contracts with wire-shape guards"
```

---

### Task 4: API switches to Contracts

**Files:**
- Modify: `AirSerbiaVirtua.Api/AirSerbiaVirtua.Api.csproj` (add project reference)
- Delete: `AirSerbiaVirtua.Api/Dtos/ApiDtos.cs`, `AirSerbiaVirtua.Api/Models/Enums.cs`
- Modify: every file under `AirSerbiaVirtua.Api` that used `Api.Dtos` types or `Api.Models` enums (controllers, `Services/MetarService.cs`, `Auth/*`, `Models/*`, `Program.cs`)

- [ ] **Step 1: Add the reference**

```powershell
dotnet add AirSerbiaVirtua.Api reference AirSerbiaVirtua.Contracts\AirSerbiaVirtua.Contracts.csproj
```

- [ ] **Step 2: Delete the duplicated definitions**

```powershell
Remove-Item AirSerbiaVirtua.Api\Dtos\ApiDtos.cs
Remove-Item AirSerbiaVirtua.Api\Models\Enums.cs
```

- [ ] **Step 3: Mechanical rename across the API.** Replace `using AirSerbiaVirtua.Api.Dtos;` with `using AirSerbiaVirtua.Contracts;` everywhere, add `using AirSerbiaVirtua.Contracts;` to the `Models/*.cs` entity files that use the enums (`Pilot.cs`, `Aircraft.cs`, `Booking.cs`, `Pirep.cs`, …), and apply this rename table:

| Old (Api.Dtos) | New (Contracts) |
|---|---|
| `RouteDto` | `RouteInfo` |
| `BookingDto` | `BookingInfo` |
| `AircraftDto` | `AircraftInfo` |
| `PilotProfileDto` | `PilotProfile` |
| `AuthTokensDto` | `AuthTokens` |
| `AdminPilotDto` | `AdminPilot` |
| `PirepListItemDto` | `PirepListItem` |
| `PirepResultDto` | `PirepResult` |
| `PositionReportDto` | `PositionReport` |
| `MetarDto` | `MetarInfo` |
| (same name) | `LoginRequest`, `RefreshRequest`, `RegisterRequest`, `ChangePasswordRequest`, `CreateBookingRequest`, `LoginResponse`, `RefreshResponse`, `FlightStartRequest`, `FlightStartResponse`, `PirepSubmitRequest`, `AirportDto`, `AirportSyncResponse`, `OutstationStartRequest` |

Find all usage sites:

```powershell
Get-ChildItem AirSerbiaVirtua.Api -Recurse -Include *.cs |
  Select-String -Pattern "Dto|Api\.Dtos" | Select-Object Path, LineNumber, Line
```

`BookingInfo`'s new `DispatchReadyAtUtc` parameter defaults to `null`, so existing `BookingDto(...)`→`BookingInfo(...)` construction sites compile unchanged (the column itself arrives in Task 6).

- [ ] **Step 4: Make `Program` visible to WebApplicationFactory.** Append to the very end of `AirSerbiaVirtua.Api/Program.cs`:

```csharp
/// <summary>Marker for WebApplicationFactory (integration tests).</summary>
public partial class Program;
```

- [ ] **Step 5: Build everything**

Run: `dotnet build AirSerbiaVirtual.sln -p:Platform=x64`
Expected: 0 errors. (If the sln platform mapping complains, build `AirSerbiaVirtua.Api` and `AirSerbiaVirtua.Web` without the flag and `Tests.Unit` with it.)

- [ ] **Step 6: Live smoke (wire unchanged).** With local Postgres up:

```powershell
dotnet run --project AirSerbiaVirtua.Api --launch-profile http
# in a second shell:
curl.exe -s http://localhost:5036/api/v1/routes | Select-Object -First 1
```

Expected: same JSON shape as before (camelCase keys: `id`, `flightNumber`, `depIcao`, …). Stop the API afterwards.

- [ ] **Step 7: Commit**

```powershell
git add -A
git commit -m "refactor(api): serve Contracts types directly; delete Api.Dtos and Models enums"
```

---

### Task 5: Scaffold `AirSerbiaVirtua.Tests.Api` — Testcontainers fixture + convergence regression

**Files:**
- Create: `AirSerbiaVirtua.Tests.Api/AirSerbiaVirtua.Tests.Api.csproj`
- Create: `AirSerbiaVirtua.Tests.Api/ApiTestHost.cs`
- Create: `AirSerbiaVirtua.Tests.Api/TestData.cs`
- Create: `AirSerbiaVirtua.Tests.Api/WireCompatTests.cs`

- [ ] **Step 1: Create the project**

```powershell
dotnet new nunit -o AirSerbiaVirtua.Tests.Api -n AirSerbiaVirtua.Tests.Api
dotnet sln AirSerbiaVirtual.sln add AirSerbiaVirtua.Tests.Api\AirSerbiaVirtua.Tests.Api.csproj
dotnet add AirSerbiaVirtua.Tests.Api reference AirSerbiaVirtua.Api\AirSerbiaVirtua.Api.csproj
dotnet add AirSerbiaVirtua.Tests.Api package FluentAssertions
dotnet add AirSerbiaVirtua.Tests.Api package Microsoft.AspNetCore.Mvc.Testing
dotnet add AirSerbiaVirtua.Tests.Api package Testcontainers.PostgreSql
```

(The nunit template already includes NUnit + adapter + test SDK.) Delete `UnitTest1.cs`. Add a `Usings.cs`:

```csharp
global using NUnit.Framework;
```

- [ ] **Step 2: Write the assembly-level fixture** — `AirSerbiaVirtua.Tests.Api/ApiTestHost.cs`:

```csharp
using System.Net.Http.Headers;
using AirSerbiaVirtua.Api.Auth;
using AirSerbiaVirtua.Api.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace AirSerbiaVirtua.Tests.Api;

/// <summary>
/// One Postgres container + one API host for the whole test assembly.
/// Program.cs auto-migrates in Development, so fixture startup also proves the
/// migration chain applies cleanly on a blank database.
/// </summary>
[SetUpFixture]
public sealed class ApiTestHost
{
    public static WebApplicationFactory<Program> Factory { get; private set; } = null!;
    private static PostgreSqlContainer _pg = null!;

    [OneTimeSetUp]
    public async Task StartAsync()
    {
        _pg = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
        await _pg.StartAsync();

        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:Default", _pg.GetConnectionString());
            builder.UseSetting("Jwt:Key", "w4-integration-test-signing-key-0123456789");
            builder.UseSetting("Jwt:Issuer", "asv-tests");
            builder.UseSetting("Jwt:Audience", "asv-tests");
        });

        // Boot the host now (runs EF migrations) so the first test isn't slow/flaky.
        _ = Factory.CreateClient();
    }

    [OneTimeTearDown]
    public async Task StopAsync()
    {
        await Factory.DisposeAsync();
        await _pg.DisposeAsync();
    }

    /// <summary>
    /// Authenticated client WITHOUT going through /auth/login — mints an access
    /// token directly via the host's JwtTokenService. Keeps us clear of the
    /// 10/min auth rate limit (all test traffic shares one IP partition).
    /// </summary>
    public static HttpClient CreateClientFor(Pilot pilot)
    {
        var client = Factory.CreateClient();
        var jwt = Factory.Services.GetRequiredService<JwtTokenService>();
        var (token, _) = jwt.CreateAccessToken(pilot);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
```

- [ ] **Step 3: Write the seeding helper** — `AirSerbiaVirtua.Tests.Api/TestData.cs`:

```csharp
using AirSerbiaVirtua.Api.Data;
using AirSerbiaVirtua.Api.Models;
using AirSerbiaVirtua.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace AirSerbiaVirtua.Tests.Api;

/// <summary>
/// Inserts an isolated pilot + route + aircraft graph per call (unique ICAOs,
/// callsigns, tails via a counter) so tests never share or reset state.
/// </summary>
public static class TestData
{
    private static int _seq;

    public static async Task<(Pilot Pilot, Route Route, Aircraft Aircraft)> SeedAsync(
        bool admin = false)
    {
        var n = Interlocked.Increment(ref _seq);
        using var scope = ApiTestHost.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var dep = new Airport { Icao = $"ZA{n:00}", Name = "Test Dep", Country = "RS" };
        var arr = new Airport { Icao = $"ZB{n:00}", Name = "Test Arr", Country = "AT" };
        var rank = new Rank { Name = $"Rank {n}", MinHours = 0, AllowedAircraftTypes = ["A319"] };
        db.AddRange(dep, arr, rank);
        await db.SaveChangesAsync();

        var pilot = new Pilot
        {
            Callsign = $"TST{n:000}", Name = "Test Pilot", Email = $"t{n}@asv.test",
            PasswordHash = "not-a-real-hash", RankId = rank.Id,
            Status = PilotStatus.Active, IsAdmin = admin,
            HubId = dep.Icao, DateJoined = DateTimeOffset.UtcNow
        };
        var aircraft = new Aircraft
        {
            Type = "A319", Registration = $"YU-T{n:00}",
            HubId = dep.Icao, Status = AircraftStatus.Active
        };
        var route = new Route
        {
            FlightNumber = $"JU9{n:00}", DepIcao = dep.Icao, ArrIcao = arr.Icao,
            AircraftType = "A319", Distance = 300, PlannedTime = 55,
            Days = [1, 2, 3, 4, 5, 6, 7]
        };
        db.AddRange(pilot, aircraft, route);
        await db.SaveChangesAsync();
        return (pilot, route, aircraft);
    }

    public static async Task<Booking> SeedBookingAsync(
        int pilotId, int routeId, BookingStatus status = BookingStatus.Open,
        DateOnly? date = null, DateTimeOffset? dispatchReadyAtUtc = null)
    {
        using var scope = ApiTestHost.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var booking = new Booking
        {
            PilotId = pilotId, RouteId = routeId, Status = status,
            Date = date ?? DateOnly.FromDateTime(DateTime.UtcNow),
            DispatchReadyAtUtc = dispatchReadyAtUtc
        };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();
        return booking;
    }
}
```

Note: `SeedBookingAsync` references `Booking.DispatchReadyAtUtc`, which arrives in Task 6. **For this task, comment out the `DispatchReadyAtUtc = dispatchReadyAtUtc` line and the parameter** (leave a `// W4 Task 6:` marker); restore them in Task 6 Step 3.

- [ ] **Step 4: Write the convergence regression tests** — `AirSerbiaVirtua.Tests.Api/WireCompatTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AirSerbiaVirtua.Contracts;
using FluentAssertions;

namespace AirSerbiaVirtua.Tests.Api;

/// <summary>
/// Locks the pre-W4 wire format of the endpoints every client already uses.
/// Asserts on raw JSON keys + numeric enum values, not on deserialized types
/// (deserializing through Contracts would mask a drift in both directions).
/// </summary>
public class WireCompatTests
{
    [Test]
    public async Task Routes_List_EmitsCamelCaseKeys()
    {
        var (pilot, route, _) = await TestData.SeedAsync();
        var client = ApiTestHost.CreateClientFor(pilot);

        var json = await client.GetStringAsync($"api/v1/routes?hub={route.DepIcao}");

        json.Should().Contain("\"flightNumber\"").And.Contain("\"depIcao\"")
            .And.Contain("\"distanceNm\"").And.Contain("\"plannedMinutes\"")
            .And.Contain("\"days\"");
        json.Should().NotContain("\"FlightNumber\"");   // PascalCase would mean options drift
    }

    [Test]
    public async Task Bookings_Mine_EmitsNumericStatus()
    {
        var (pilot, route, _) = await TestData.SeedAsync();
        await TestData.SeedBookingAsync(pilot.Id, route.Id);
        var client = ApiTestHost.CreateClientFor(pilot);

        var json = await client.GetStringAsync("api/v1/bookings/mine");

        using var doc = JsonDocument.Parse(json);
        var first = doc.RootElement.EnumerateArray().First();
        first.GetProperty("status").ValueKind.Should().Be(JsonValueKind.Number);
        first.GetProperty("status").GetInt32().Should().Be(0);   // Open
        first.GetProperty("date").GetString().Should().MatchRegex(@"^\d{4}-\d{2}-\d{2}$");
    }

    [Test]
    public async Task Pireps_Mine_EmptyForFreshPilot_Returns200List()
    {
        var (pilot, _, _) = await TestData.SeedAsync();
        var client = ApiTestHost.CreateClientFor(pilot);

        var resp = await client.GetAsync("api/v1/pireps/mine");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        (await resp.Content.ReadFromJsonAsync<List<PirepListItem>>()).Should().BeEmpty();
    }

    [Test]
    public async Task Auth_Login_WrongPassword_Returns401_NotException()
    {
        var (pilot, _, _) = await TestData.SeedAsync();
        var client = ApiTestHost.Factory.CreateClient();

        var resp = await client.PostAsJsonAsync("api/v1/auth/login",
            new LoginRequest(pilot.Callsign, "wrong-password"));

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Anonymous_Routes_IsAllowed_ButBookingsIsNot()
    {
        var client = ApiTestHost.Factory.CreateClient();

        (await client.GetAsync("api/v1/routes")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("api/v1/bookings/mine")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
```

- [ ] **Step 5: Run (Docker must be up)**

Run: `dotnet test AirSerbiaVirtua.Tests.Api\AirSerbiaVirtua.Tests.Api.csproj`
Expected: 5 passed. First run pulls `postgres:16-alpine` (slow once).

**Docker fallback (only if Testcontainers cannot start):** replace the container in `ApiTestHost` with the local server — `UseSetting("ConnectionStrings:Default", "Host=localhost;Database=airserbiavirtua_test;Username=postgres;Password=<dev password from user-secrets>")` and add a `[OneTimeSetUp]` step that runs `db.Database.EnsureDeleted()` before `Migrate()`. Tell the user this happened.

- [ ] **Step 6: Commit**

```powershell
git add -A
git commit -m "test(api): Testcontainers integration fixture + wire-format regression suite"
```

---

### Task 6: `DispatchReadyAtUtc` migration + dispatch endpoints (TDD)

**Files:**
- Modify: `AirSerbiaVirtua.Api/Models/Booking.cs`
- Create: migration `AddBookingDispatch` (generated)
- Modify: `AirSerbiaVirtua.Api/Controllers/BookingsController.cs`
- Modify: `AirSerbiaVirtua.Tests.Api/TestData.cs` (restore the commented parameter)
- Test: `AirSerbiaVirtua.Tests.Api/DispatchEndpointTests.cs`

- [ ] **Step 1: Add the column to the entity** — in `AirSerbiaVirtua.Api/Models/Booking.cs` add below `Status`:

```csharp
    /// <summary>
    /// Set when the pilot marks this booking "dispatch ready" on the web
    /// Briefing page; null = not prepared. The desktop client offers
    /// dispatch-ready bookings for today as one-click "Resume dispatch".
    /// </summary>
    public DateTimeOffset? DispatchReadyAtUtc { get; set; }
```

- [ ] **Step 2: Generate the migration**

```powershell
dotnet ef migrations add AddBookingDispatch --project AirSerbiaVirtua.Api
```

Expected: new files under `AirSerbiaVirtua.Api/Migrations/` adding a nullable `timestamp with time zone` column. (If `dotnet ef` is missing: `dotnet tool install -g dotnet-ef`.)

- [ ] **Step 3: Restore `TestData.SeedBookingAsync`** — un-comment the `dispatchReadyAtUtc` parameter and the `DispatchReadyAtUtc = dispatchReadyAtUtc` assignment from Task 5 Step 3.

- [ ] **Step 4: Write the failing endpoint tests** — `AirSerbiaVirtua.Tests.Api/DispatchEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using AirSerbiaVirtua.Contracts;
using FluentAssertions;

namespace AirSerbiaVirtua.Tests.Api;

public class DispatchEndpointTests
{
    // ---- POST /bookings/{id}/dispatch ---------------------------------------

    [Test]
    public async Task MarkReady_OnOpenBooking_SetsTimestampAndReturnsBooking()
    {
        var (pilot, route, _) = await TestData.SeedAsync();
        var booking = await TestData.SeedBookingAsync(pilot.Id, route.Id);
        var client = ApiTestHost.CreateClientFor(pilot);

        var resp = await client.PostAsync($"api/v1/bookings/{booking.Id}/dispatch", null);

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = (await resp.Content.ReadFromJsonAsync<BookingInfo>())!;
        dto.Id.Should().Be(booking.Id);
        dto.DispatchReadyAtUtc.Should().NotBeNull()
            .And.BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(10));
    }

    [Test]
    public async Task MarkReady_Twice_IsIdempotentAndRefreshesTimestamp()
    {
        var (pilot, route, _) = await TestData.SeedAsync();
        var booking = await TestData.SeedBookingAsync(pilot.Id, route.Id);
        var client = ApiTestHost.CreateClientFor(pilot);

        var first = await (await client.PostAsync($"api/v1/bookings/{booking.Id}/dispatch", null))
            .Content.ReadFromJsonAsync<BookingInfo>();
        await Task.Delay(50);
        var secondResp = await client.PostAsync($"api/v1/bookings/{booking.Id}/dispatch", null);

        secondResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var second = await secondResp.Content.ReadFromJsonAsync<BookingInfo>();
        second!.DispatchReadyAtUtc.Should().BeOnOrAfter(first!.DispatchReadyAtUtc!.Value);
    }

    [Test]
    public async Task MarkReady_OnFlownBooking_Returns409()
    {
        var (pilot, route, _) = await TestData.SeedAsync();
        var booking = await TestData.SeedBookingAsync(pilot.Id, route.Id, BookingStatus.Flown);
        var client = ApiTestHost.CreateClientFor(pilot);

        var resp = await client.PostAsync($"api/v1/bookings/{booking.Id}/dispatch", null);

        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task MarkReady_OnAnotherPilotsBooking_Returns404()
    {
        var (owner, route, _) = await TestData.SeedAsync();
        var (other, _, _) = await TestData.SeedAsync();
        var booking = await TestData.SeedBookingAsync(owner.Id, route.Id);
        var client = ApiTestHost.CreateClientFor(other);

        var resp = await client.PostAsync($"api/v1/bookings/{booking.Id}/dispatch", null);

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task MarkReady_Anonymous_Returns401()
    {
        var client = ApiTestHost.Factory.CreateClient();
        var resp = await client.PostAsync("api/v1/bookings/1/dispatch", null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---- DELETE /bookings/{id}/dispatch -------------------------------------

    [Test]
    public async Task ClearReady_RemovesFlag_Returns204()
    {
        var (pilot, route, _) = await TestData.SeedAsync();
        var booking = await TestData.SeedBookingAsync(
            pilot.Id, route.Id, dispatchReadyAtUtc: DateTimeOffset.UtcNow);
        var client = ApiTestHost.CreateClientFor(pilot);

        var resp = await client.DeleteAsync($"api/v1/bookings/{booking.Id}/dispatch");

        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var mine = await client.GetFromJsonAsync<List<BookingInfo>>("api/v1/bookings/mine");
        mine!.Single(b => b.Id == booking.Id).DispatchReadyAtUtc.Should().BeNull();
    }

    [Test]
    public async Task ClearReady_OnCancelledBooking_Returns409()
    {
        var (pilot, route, _) = await TestData.SeedAsync();
        var booking = await TestData.SeedBookingAsync(pilot.Id, route.Id, BookingStatus.Cancelled);
        var client = ApiTestHost.CreateClientFor(pilot);

        var resp = await client.DeleteAsync($"api/v1/bookings/{booking.Id}/dispatch");

        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ---- GET /bookings/active -------------------------------------------------

    [Test]
    public async Task Active_ReturnsOnlyOpenAndConfirmed_OrderedReadyFirst()
    {
        var (pilot, route, _) = await TestData.SeedAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var open       = await TestData.SeedBookingAsync(pilot.Id, route.Id, BookingStatus.Open,      today.AddDays(2));
        var flown      = await TestData.SeedBookingAsync(pilot.Id, route.Id, BookingStatus.Flown,     today);
        var cancelled  = await TestData.SeedBookingAsync(pilot.Id, route.Id, BookingStatus.Cancelled, today);
        var readyOld   = await TestData.SeedBookingAsync(pilot.Id, route.Id, BookingStatus.Open,      today,
            dispatchReadyAtUtc: DateTimeOffset.UtcNow.AddMinutes(-30));
        var readyNew   = await TestData.SeedBookingAsync(pilot.Id, route.Id, BookingStatus.Confirmed, today.AddDays(1),
            dispatchReadyAtUtc: DateTimeOffset.UtcNow);
        var client = ApiTestHost.CreateClientFor(pilot);

        var active = await client.GetFromJsonAsync<List<BookingInfo>>("api/v1/bookings/active");

        active!.Select(b => b.Id).Should().Equal(readyNew.Id, readyOld.Id, open.Id);
        active.Should().NotContain(b => b.Id == flown.Id || b.Id == cancelled.Id);
    }

    [Test]
    public async Task Active_DoesNotLeakOtherPilotsBookings()
    {
        var (pilot, route, _) = await TestData.SeedAsync();
        var (other, otherRoute, _) = await TestData.SeedAsync();
        await TestData.SeedBookingAsync(other.Id, otherRoute.Id);
        var client = ApiTestHost.CreateClientFor(pilot);

        var active = await client.GetFromJsonAsync<List<BookingInfo>>("api/v1/bookings/active");

        active.Should().BeEmpty();
    }
}
```

- [ ] **Step 5: Run to verify failure**

Run: `dotnet test AirSerbiaVirtua.Tests.Api\AirSerbiaVirtua.Tests.Api.csproj`
Expected: the new tests fail with 404/405 (endpoints don't exist). `WireCompatTests` still pass.

- [ ] **Step 6: Implement in `BookingsController`.** Add a mapper, switch the two existing `BookingInfo` construction sites to it, and add the three actions:

```csharp
    private static BookingInfo ToDto(Booking b) => new(
        b.Id, b.RouteId,
        b.Route!.FlightNumber, b.Route.DepIcao, b.Route.ArrIcao,
        b.Route.AircraftType, b.Route.PlannedTime,
        b.Date, b.Status, b.DispatchReadyAtUtc);

    /// <summary>
    /// The calling pilot's Open/Confirmed bookings — dispatch-ready first
    /// (newest preparation wins), then by service date. The desktop client
    /// pulls this to offer "Resume dispatch".
    /// </summary>
    [HttpGet("active")]
    public async Task<ActionResult<List<BookingInfo>>> Active()
    {
        var pilotId = User.PilotId();
        if (pilotId is null) return Unauthorized();

        var bookings = await _db.Bookings
            .AsNoTracking()
            .Where(b => b.PilotId == pilotId &&
                        (b.Status == BookingStatus.Open || b.Status == BookingStatus.Confirmed))
            .Include(b => b.Route)
            .ToListAsync();

        var ordered = bookings
            .OrderByDescending(b => b.DispatchReadyAtUtc.HasValue)
            .ThenByDescending(b => b.DispatchReadyAtUtc)
            .ThenBy(b => b.Date)
            .ThenBy(b => b.Id)
            .Select(ToDto)
            .ToList();

        return Ok(ordered);
    }

    /// <summary>
    /// Marks an Open/Confirmed booking dispatch-ready (web Briefing page).
    /// Idempotent — repeating refreshes the timestamp.
    /// </summary>
    [HttpPost("{id:int}/dispatch")]
    public async Task<ActionResult<BookingInfo>> MarkDispatchReady(int id)
    {
        var pilotId = User.PilotId();
        if (pilotId is null) return Unauthorized();

        var booking = await _db.Bookings
            .Include(b => b.Route)
            .FirstOrDefaultAsync(b => b.Id == id && b.PilotId == pilotId);
        if (booking is null) return NotFound();

        if (booking.Status is not (BookingStatus.Open or BookingStatus.Confirmed))
            return Conflict(new { message = $"Booking is {booking.Status} — only Open or Confirmed bookings can be dispatched." });

        booking.DispatchReadyAtUtc = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(ToDto(booking));
    }

    /// <summary>Clears the dispatch-ready flag ("unprepare").</summary>
    [HttpDelete("{id:int}/dispatch")]
    public async Task<IActionResult> ClearDispatchReady(int id)
    {
        var pilotId = User.PilotId();
        if (pilotId is null) return Unauthorized();

        var booking = await _db.Bookings.FirstOrDefaultAsync(b => b.Id == id && b.PilotId == pilotId);
        if (booking is null) return NotFound();

        if (booking.Status is not (BookingStatus.Open or BookingStatus.Confirmed))
            return Conflict(new { message = $"Booking is {booking.Status}." });

        booking.DispatchReadyAtUtc = null;
        await _db.SaveChangesAsync();
        return NoContent();
    }
```

Also update `Mine()` and `Create()` to use `ToDto` (Mine's projection moves to `Include(b => b.Route)` + in-memory `Select(ToDto)`, or simply add `b.DispatchReadyAtUtc` as the tenth argument of the existing projections — either is fine, `ToDto` is less duplication; in `Create()` set `booking.Route = route;` before calling `ToDto(booking)`).

- [ ] **Step 7: Run to verify pass**

Run: `dotnet test AirSerbiaVirtua.Tests.Api\AirSerbiaVirtua.Tests.Api.csproj`
Expected: all green (14 tests).

- [ ] **Step 8: Commit**

```powershell
git add -A
git commit -m "feat(api): booking dispatch-ready flag — migration, mark/clear/active endpoints"
```

---

### Task 7: Desktop + Acars.Core converge on Contracts (`IApiService`, `GetActiveBookingsAsync`)

**Files:**
- Modify: `AirSerbiaVirtua.Acars.Core/AirSerbiaVirtua.Acars.Core.csproj` (reference Contracts)
- Rewrite: `AirSerbiaVirtua.Acars.Core/ApiContracts.cs` (only sim-side helpers remain)
- Create: `AirSerbiaVirtua.Acars.Core/IApiService.cs`
- Modify: `AirSerbiaVirtua.Acars.Core/ApiService.cs` (implements `IApiService`, renames, + `GetActiveBookingsAsync`)
- Modify: `AirSerbiaVirtua.Acars.Desktop/Services/ISessionService.cs` + `SessionService.cs` (`Api` becomes `IApiService`)
- Modify: desktop ViewModels using renamed types (`BookingsViewModel`, `OutstationViewModel`, `AcarsViewModel`, `PilotCentreViewModel`, `BriefingViewModel`, `LogbookViewModel`, `DebriefingViewModel`, `AdminViewModel`, `MetarsViewModel` — compiler-driven)
- Test: `AirSerbiaVirtua.Tests.Unit/Core/PositionReportsTests.cs`

- [ ] **Step 1: Reference Contracts from Core**

```powershell
dotnet add AirSerbiaVirtua.Acars.Core reference AirSerbiaVirtua.Contracts\AirSerbiaVirtua.Contracts.csproj
```

- [ ] **Step 2: Write the failing factory test** — `AirSerbiaVirtua.Tests.Unit/Core/PositionReportsTests.cs`. (`PositionReport.FromTelemetry` becomes `PositionReports.FromTelemetry` because the wire record moved to Contracts and must not depend on sim types.)

```csharp
using AirSerbiaVirtua.Acars.Core;
using AirSerbiaVirtua.Contracts;
using FluentAssertions;

namespace AirSerbiaVirtua.Tests.Unit.Core;

public class PositionReportsTests
{
    [Test]
    public void FromTelemetry_MapsFieldsAndAssignsFreshReportId()
    {
        var sample = new FlightData
        {
            SampleTimeUtc = new DateTimeOffset(2026, 6, 6, 12, 0, 0, TimeSpan.Zero),
            LatitudeDeg = 44.818,
            LongitudeDeg = 20.309,
            AltitudeFt = 35012.4,
            GroundSpeedKts = 446.7
        };

        var report = PositionReports.FromTelemetry(sample, FlightState.Cruise);

        report.ClientReportId.Should().NotBeEmpty();
        report.Timestamp.Should().Be(sample.SampleTimeUtc);
        report.Lat.Should().Be(44.818);
        report.Lon.Should().Be(20.309);
        report.AltFt.Should().Be(35012);
        report.GsKts.Should().Be(447);
        report.Phase.Should().Be(FlightPhase.Cruise);
    }

    [Test]
    public void FromTelemetry_TwoCalls_GetDistinctReportIds()
    {
        var sample = new FlightData { SampleTimeUtc = DateTimeOffset.UtcNow };
        PositionReports.FromTelemetry(sample, FlightState.Taxi).ClientReportId
            .Should().NotBe(PositionReports.FromTelemetry(sample, FlightState.Taxi).ClientReportId);
    }

    [TestCase(FlightState.Boarding, FlightPhase.Preflight)]
    [TestCase(FlightState.Arrived, FlightPhase.Shutdown)]
    [TestCase(FlightState.Landing, FlightPhase.Landing)]
    public void PhaseMapping_CoversClientOnlyStates(FlightState state, FlightPhase expected)
    {
        state.ToApiPhase().Should().Be(expected);
    }
}
```

(If `FlightData` is not constructible with an object initializer — check `AirSerbiaVirtua.Acars.Core/FlightData.cs` — adapt the construction to its actual shape; the assertions stay.)

- [ ] **Step 3: Run to verify failure** — compile error (`PositionReports` missing). Red.

- [ ] **Step 4: Shrink `AirSerbiaVirtua.Acars.Core/ApiContracts.cs`** to exactly this (everything else now comes from Contracts):

```csharp
using AirSerbiaVirtua.Contracts;

namespace AirSerbiaVirtua.Acars.Core;

// =============================================================================
// W4: all wire DTOs moved to AirSerbiaVirtua.Contracts (single source for API,
// desktop and web). Only sim-side helpers remain here — they depend on
// FlightData / FlightState, which the Contracts project must never see.
// =============================================================================

/// <summary>Builds POSREP wire records from live telemetry.</summary>
public static class PositionReports
{
    /// <summary>Builds a POSREP from a telemetry sample, assigning a fresh report id.</summary>
    public static PositionReport FromTelemetry(FlightData data, FlightState phase) =>
        new(
            ClientReportId: Guid.NewGuid(),
            Timestamp: data.SampleTimeUtc,
            Lat: data.LatitudeDeg,
            Lon: data.LongitudeDeg,
            AltFt: (int)Math.Round(data.AltitudeFt),
            GsKts: (int)Math.Round(data.GroundSpeedKts),
            Phase: phase.ToApiPhase());
}

public static class PhaseMapping
{
    /// <summary>Maps the client state machine's phase to the API FlightPhase.</summary>
    public static FlightPhase ToApiPhase(this FlightState state) => state switch
    {
        FlightState.Preflight => FlightPhase.Preflight,
        FlightState.Boarding => FlightPhase.Preflight,
        FlightState.Taxi => FlightPhase.Taxi,
        FlightState.Takeoff => FlightPhase.Takeoff,
        FlightState.Climb => FlightPhase.Climb,
        FlightState.Cruise => FlightPhase.Cruise,
        FlightState.Descent => FlightPhase.Descent,
        FlightState.Landing => FlightPhase.Landing,
        FlightState.TaxiIn => FlightPhase.TaxiIn,
        FlightState.Arrived => FlightPhase.Shutdown,
        _ => FlightPhase.Preflight
    };
}
```

- [ ] **Step 5: Create `AirSerbiaVirtua.Acars.Core/IApiService.cs`** — the full public surface of `ApiService`:

```csharp
using AirSerbiaVirtua.Contracts;

namespace AirSerbiaVirtua.Acars.Core;

/// <summary>
/// Seam over <see cref="ApiService"/> so ViewModels and services are unit-testable
/// with a mock. One member per API endpoint the desktop client uses.
/// </summary>
public interface IApiService : IDisposable
{
    PilotProfile? Pilot { get; }
    bool IsAuthenticated { get; }
    string BaseHost { get; }
    DateTimeOffset? AccessTokenExpiresAtUtc { get; }
    DateTimeOffset? RefreshTokenExpiresAtUtc { get; }
    event Action<AuthTokens>? TokensUpdated;

    Task<LoginResponse> LoginAsync(string callsign, string password, CancellationToken ct = default);
    Task<bool> RefreshAsync(CancellationToken ct = default);
    Task<PilotProfile> GetMeAsync(CancellationToken ct = default);
    Task<bool> TryRestoreSessionAsync(string refreshToken, CancellationToken ct = default);
    Task LogoutAsync(CancellationToken ct = default);
    Task<long?> PingAsync(CancellationToken ct = default);
    Task ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken ct = default);

    Task<AirportSyncResponse> SyncAirportsAsync(int sinceRevision, CancellationToken ct = default);

    Task<FlightStartResponse> StartFlightAsync(int routeId, int aircraftId, DateOnly date, CancellationToken ct = default);
    Task<FlightStartResponse> StartOutstationFlightAsync(OutstationStartRequest request, CancellationToken ct = default);
    Task PushPositionAsync(int flightSessionId, PositionReport report, CancellationToken ct = default);
    Task PushPositionAsync(int flightSessionId, FlightData data, FlightState phase, CancellationToken ct = default);
    Task AbortFlightAsync(int flightSessionId, CancellationToken ct = default);
    Task<PirepResult> SubmitPirepAsync(PirepSubmitRequest request, CancellationToken ct = default);

    Task<List<RouteInfo>> GetRoutesAsync(string? hubIcao = null, CancellationToken ct = default);
    Task<RouteInfo> GetRouteAsync(int id, CancellationToken ct = default);

    Task<List<BookingInfo>> GetMyBookingsAsync(CancellationToken ct = default);
    Task<List<BookingInfo>> GetActiveBookingsAsync(CancellationToken ct = default);
    Task<BookingInfo> CreateBookingAsync(int routeId, DateOnly date, CancellationToken ct = default);
    Task CancelBookingAsync(int bookingId, CancellationToken ct = default);

    Task<List<AircraftInfo>> GetAircraftAsync(string? type = null, string? status = null, CancellationToken ct = default);
    Task<List<PirepListItem>> GetMyPirepsAsync(string? status = null, CancellationToken ct = default);
    Task<List<MetarInfo>> GetMetarAsync(IEnumerable<string> icaos, CancellationToken ct = default);

    Task<List<AdminPilot>> GetAdminPilotsAsync(CancellationToken ct = default);
    Task ActivatePilotAsync(int pilotId, CancellationToken ct = default);
    Task DeactivatePilotAsync(int pilotId, CancellationToken ct = default);
}
```

- [ ] **Step 6: Update `ApiService.cs`:**
  - Declaration: `public sealed class ApiService : IApiService`.
  - Add `using AirSerbiaVirtua.Contracts;`.
  - Renames: `ApiRoute` → `RouteInfo`, `ApiBooking` → `BookingInfo`, `ApiAircraft` → `AircraftInfo` (return types + deserialization generics).
  - `PushPositionAsync` overload: `PositionReport.FromTelemetry(data, phase)` → `PositionReports.FromTelemetry(data, phase)`.
  - Add after `GetMyBookingsAsync`:

```csharp
    /// <summary>Open/Confirmed bookings, dispatch-ready first (W4 dispatch pull).</summary>
    public async Task<List<BookingInfo>> GetActiveBookingsAsync(CancellationToken ct = default)
    {
        using var resp = await SendWithAuthRetryAsync(() => _http.GetAsync("api/v1/bookings/active", ct), ct);
        await EnsureSuccess(resp, "Bookings (active)");
        return (await resp.Content.ReadFromJsonAsync<List<BookingInfo>>(Json, ct)) ?? new();
    }
```

- [ ] **Step 7: Switch the session seam.** In `ISessionService.cs`: `ApiService Api { get; }` → `IApiService Api { get; }`, add `using AirSerbiaVirtua.Contracts;` (for `PilotProfile`). In `SessionService.cs`: `public IApiService Api { get; }` (still constructs `new ApiService(options.Value.BaseUrl)`).

- [ ] **Step 8: Compiler-driven cleanup of Desktop + PoC.** Build and fix every error — they are all one of:
  - missing `using AirSerbiaVirtua.Contracts;` (add it),
  - `ApiBookingStatus` → `BookingStatus`, `ApiFlightPhase` → `FlightPhase`, `ApiRoute` → `RouteInfo`, `ApiBooking` → `BookingInfo`, `ApiAircraft` → `AircraftInfo` (rename),
  - `PilotCentreViewModel`'s private `PilotStatusLabel` enum: delete it and use `((PilotStatus)p.Status)` — wait, `p.Status` IS `PilotStatus` now, so `StatusText = p.Status.ToString();`,
  - `BriefingViewModel`: delete the private `EstimateFuel` method, replace the call with `var (trip, reserve) = FuelEstimator.Estimate(route.AircraftType, route.DistanceNm);`.

Run: `dotnet build AirSerbiaVirtual.sln -p:Platform=x64` until 0 errors.

- [ ] **Step 9: Run unit tests + PoC selftest**

Run: `dotnet test AirSerbiaVirtua.Tests.Unit\AirSerbiaVirtua.Tests.Unit.csproj -p:Platform=x64`
Expected: all green including the new `PositionReportsTests`.
Run: `dotnet run --project AirSerbiaVirtua.Acars.PoC -- --selftest`
Expected: same pass count as before the refactor (flight-sim test + 11/11 PosrepQueue asserts).

- [ ] **Step 10: Commit**

```powershell
git add -A
git commit -m "refactor(desktop): converge Core/Desktop on Contracts; add IApiService seam + GetActiveBookingsAsync"
```

---

### Task 8: `DispatchService` (TDD) + `BookingsViewModel` delegation

**Files:**
- Create: `AirSerbiaVirtua.Acars.Desktop/Services/DispatchService.cs`
- Modify: `AirSerbiaVirtua.Acars.Desktop/ViewModels/BookingsViewModel.cs`
- Modify: `AirSerbiaVirtua.Acars.Desktop/App.xaml.cs` (register singleton)
- Test: `AirSerbiaVirtua.Tests.Unit/Desktop/DispatchServiceTests.cs`
- Create: `AirSerbiaVirtua.Tests.Unit/Desktop/FakeSimBridge.cs`

- [ ] **Step 1: Write the fake bridge** — `AirSerbiaVirtua.Tests.Unit/Desktop/FakeSimBridge.cs`:

```csharp
using AirSerbiaVirtua.Acars.Core;

namespace AirSerbiaVirtua.Tests.Unit.Desktop;

/// <summary>No-op sim bridge so SimulatorService can be constructed in tests.</summary>
public sealed class FakeSimBridge : ISimBridge
{
    public string Name => "Fake";
    public bool IsConnected => false;
    public event Action<string>? Log { add { } remove { } }
    public bool EnsureConnected() => false;
    public FlightData? ReadCurrent() => null;
    public void Close() { }
    public void Dispose() { }
}
```

- [ ] **Step 2: Write the failing tests** — `AirSerbiaVirtua.Tests.Unit/Desktop/DispatchServiceTests.cs`:

```csharp
using AirSerbiaVirtua.Acars.Core;
using AirSerbiaVirtua.Acars.Desktop.Services;
using AirSerbiaVirtua.Contracts;
using FluentAssertions;
using Moq;

namespace AirSerbiaVirtua.Tests.Unit.Desktop;

public class DispatchServiceTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);
    private static readonly BookingInfo Booking = new(
        7, 3, "JU360", "LYBE", "LOWW", "A319", 55, Today, BookingStatus.Open, DateTimeOffset.UtcNow);
    private static readonly AircraftInfo Airframe = new(4, "A319", "YU-API", "Active", "LYBE");
    private static readonly FlightStartResponse Start = new(12, 3, 4, "YU-API", DateTimeOffset.UtcNow);

    private Mock<IApiService> _api = null!;
    private Mock<ISessionService> _session = null!;
    private Mock<INavigationService> _navigation = null!;
    private FlightSessionState _flightState = null!;
    private SimulatorService _sim = null!;
    private DispatchService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _api = new Mock<IApiService>();
        _session = new Mock<ISessionService>();
        _session.SetupGet(s => s.Api).Returns(_api.Object);
        _session.SetupGet(s => s.IsAuthenticated).Returns(true);
        _navigation = new Mock<INavigationService>();
        _flightState = new FlightSessionState();
        _sim = new SimulatorService(new FakeSimBridge());
        _sut = new DispatchService(_session.Object, _flightState, _navigation.Object, _sim);
    }

    [TearDown]
    public void TearDown() => _sim.Dispose();

    [Test]
    public async Task Start_HappyPath_StartsFlightSetsStateAndNavigates()
    {
        _api.Setup(a => a.GetAircraftAsync("A319", "Active", It.IsAny<CancellationToken>()))
            .ReturnsAsync([Airframe]);
        _api.Setup(a => a.StartFlightAsync(3, 4, Today, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Start);

        var result = await _sut.StartAsync(Booking);

        result.Success.Should().BeTrue();
        result.Registration.Should().Be("YU-API");
        _flightState.HasActiveSession.Should().BeTrue();
        _flightState.FlightSessionId.Should().Be(12);
        _flightState.FlightNumber.Should().Be("JU360");
        _navigation.Verify(n => n.NavigateTo(NavTarget.Acars), Times.Once);
    }

    [Test]
    public async Task Start_NoAirframeAvailable_FailsWithoutTouchingState()
    {
        _api.Setup(a => a.GetAircraftAsync("A319", "Active", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _sut.StartAsync(Booking);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("A319");
        _flightState.HasActiveSession.Should().BeFalse();
        _api.Verify(a => a.StartFlightAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateOnly>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _navigation.Verify(n => n.NavigateTo(It.IsAny<NavTarget>()), Times.Never);
    }

    [Test]
    public async Task Start_ApiConflict_SurfacesMessageAndLeavesStateClean()
    {
        _api.Setup(a => a.GetAircraftAsync("A319", "Active", It.IsAny<CancellationToken>()))
            .ReturnsAsync([Airframe]);
        _api.Setup(a => a.StartFlightAsync(3, 4, Today, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiException("Flight start failed: 409 Conflict. aircraft in flight",
                System.Net.HttpStatusCode.Conflict));

        var result = await _sut.StartAsync(Booking);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("409");
        _flightState.HasActiveSession.Should().BeFalse();
        _navigation.Verify(n => n.NavigateTo(It.IsAny<NavTarget>()), Times.Never);
    }

    [Test]
    public async Task Start_SessionAlreadyActive_RefusesImmediately()
    {
        _flightState.Set(Start, "JU999");

        var result = await _sut.StartAsync(Booking);

        result.Success.Should().BeFalse();
        _api.Verify(a => a.GetAircraftAsync(It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Start_NotAuthenticated_Refuses()
    {
        _session.SetupGet(s => s.IsAuthenticated).Returns(false);

        var result = await _sut.StartAsync(Booking);

        result.Success.Should().BeFalse();
    }
}
```

- [ ] **Step 3: Run to verify failure** — compile error (`DispatchService` missing). Red.

- [ ] **Step 4: Implement** — `AirSerbiaVirtua.Acars.Desktop/Services/DispatchService.cs`:

```csharp
using AirSerbiaVirtua.Acars.Core;
using AirSerbiaVirtua.Contracts;

namespace AirSerbiaVirtua.Acars.Desktop.Services;

public sealed record DispatchStartResult(bool Success, string? Message, string? Registration = null);

/// <summary>
/// The one start-flight sequence (W4): pick the first Active airframe of the
/// booking's type, open the server session, reset the sim state machine, set
/// the shared FlightSessionState and jump to ACARS Live. Used by the Bookings
/// page Start button and the Pilot Centre "Resume dispatch" card.
/// </summary>
public sealed class DispatchService
{
    private readonly ISessionService _session;
    private readonly FlightSessionState _flightState;
    private readonly INavigationService _navigation;
    private readonly SimulatorService _sim;

    public DispatchService(
        ISessionService session,
        FlightSessionState flightState,
        INavigationService navigation,
        SimulatorService sim)
    {
        _session = session;
        _flightState = flightState;
        _navigation = navigation;
        _sim = sim;
    }

    public async Task<DispatchStartResult> StartAsync(BookingInfo booking, CancellationToken ct = default)
    {
        if (!_session.IsAuthenticated)
            return new(false, "Sign in to start a flight.");
        if (_flightState.HasActiveSession)
            return new(false, "A flight session is already active.");

        try
        {
            // First Active airframe of the required type; the API returns
            // Conflict if it gets snatched in between — surfaced via Message.
            var fleet = await _session.Api.GetAircraftAsync(booking.AircraftType, "Active", ct);
            if (fleet.Count == 0)
                return new(false, $"No {booking.AircraftType} airframes are available right now.");

            var aircraft = fleet[0];
            var start = await _session.Api.StartFlightAsync(booking.RouteId, aircraft.Id, booking.Date, ct);

            _sim.ResetSession();
            _flightState.Set(start, booking.FlightNumber);
            _navigation.NavigateTo(NavTarget.Acars);
            return new(true, null, aircraft.Registration);
        }
        catch (Exception ex)
        {
            return new(false, ex.Message);
        }
    }
}
```

- [ ] **Step 5: Run to verify pass**

Run: `dotnet test AirSerbiaVirtua.Tests.Unit\AirSerbiaVirtua.Tests.Unit.csproj -p:Platform=x64`
Expected: all green.

- [ ] **Step 6: Delegate from `BookingsViewModel`.** Inject `DispatchService` (replace the direct `SimulatorService`/start logic):
  - Constructor: replace the `SimulatorService sim` parameter with `DispatchService dispatch`; store `_dispatch`. Remove the now-unused `_sim` field.
  - Add to `BookingRow`: `public BookingInfo Source { get; }` — assign `Source = b;` in the constructor.
  - Replace the body of `StartFlightAsync(BookingRow? row)` with:

```csharp
        if (row is null || !_session.IsAuthenticated) return;

        IsBusy = true;
        StatusMessage = null;
        HasError = false;
        try
        {
            var result = await _dispatch.StartAsync(row.Source);
            if (!result.Success)
            {
                HasError = true;
                StatusMessage = result.Message;
                return;
            }

            row.Status = BookingStatus.Confirmed;
            StatusMessage = $"Flight {row.FlightNumber} started on {result.Registration}. Switching to ACARS Live.";
        }
        finally
        {
            IsBusy = false;
        }
```

  (`BookingRow.Status` is `int` today — Task 7's compiler-driven cleanup may already have made it `BookingStatus`; either way make `row.Status` assignment compile against the row's actual type, preferring the enum.)
  - In `App.xaml.cs` `ConfigureServices`, add below `services.AddSingleton<FlightSessionState>();`:

```csharp
                services.AddSingleton<DispatchService>();
```

- [ ] **Step 7: Build + run all unit tests**

Run: `dotnet build AirSerbiaVirtual.sln -p:Platform=x64` then `dotnet test AirSerbiaVirtua.Tests.Unit\AirSerbiaVirtua.Tests.Unit.csproj -p:Platform=x64`
Expected: 0 errors, all green.

- [ ] **Step 8: Commit**

```powershell
git add -A
git commit -m "feat(desktop): DispatchService extracted from Bookings start-flight, fully unit-tested"
```

---

### Task 9: Pilot Centre "Resume dispatch" card (TDD)

**Files:**
- Modify: `AirSerbiaVirtua.Acars.Desktop/ViewModels/PilotCentreViewModel.cs`
- Modify: `AirSerbiaVirtua.Acars.Desktop/Views/PilotCentreView.xaml`
- Test: `AirSerbiaVirtua.Tests.Unit/Desktop/PilotCentreDispatchTests.cs`

- [ ] **Step 1: Write the failing tests**:

```csharp
using AirSerbiaVirtua.Acars.Core;
using AirSerbiaVirtua.Acars.Desktop.Services;
using AirSerbiaVirtua.Acars.Desktop.ViewModels;
using AirSerbiaVirtua.Contracts;
using FluentAssertions;
using Moq;

namespace AirSerbiaVirtua.Tests.Unit.Desktop;

public class PilotCentreDispatchTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static BookingInfo Booking(int id, DateOnly date, DateTimeOffset? ready) => new(
        id, 3, $"JU{id:000}", "LYBE", "LOWW", "A319", 55, date, BookingStatus.Open, ready);

    private Mock<IApiService> _api = null!;
    private Mock<ISessionService> _session = null!;
    private FlightSessionState _flightState = null!;
    private PilotCentreViewModel _vm = null!;

    [SetUp]
    public void SetUp()
    {
        _api = new Mock<IApiService>();
        _session = new Mock<ISessionService>();
        _session.SetupGet(s => s.Api).Returns(_api.Object);
        _session.SetupGet(s => s.IsAuthenticated).Returns(true);
        _session.SetupGet(s => s.Pilot).Returns((PilotProfile?)null);
        _flightState = new FlightSessionState();

        var sim = new SimulatorService(new FakeSimBridge());
        var dispatch = new DispatchService(
            _session.Object, _flightState, new Mock<INavigationService>().Object, sim);
        _vm = new PilotCentreViewModel(_session.Object, _flightState, dispatch);
    }

    [Test]
    public async Task Refresh_ReadyBookingForToday_ShowsCard()
    {
        _api.Setup(a => a.GetActiveBookingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([Booking(7, Today, Now)]);

        await _vm.RefreshDispatchAsync();

        _vm.HasDispatch.Should().BeTrue();
        _vm.DispatchTitle.Should().Contain("JU007").And.Contain("LYBE").And.Contain("LOWW");
        _vm.DispatchSub.Should().Contain("A319");
    }

    [Test]
    public async Task Refresh_NewestPreparedWins_WhenSeveralReadyToday()
    {
        _api.Setup(a => a.GetActiveBookingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                Booking(1, Today, Now.AddMinutes(-30)),
                Booking(2, Today, Now)
            ]);

        await _vm.RefreshDispatchAsync();

        _vm.DispatchTitle.Should().Contain("JU002");
    }

    [Test]
    public async Task Refresh_ReadyBookingForTomorrow_NoCard()
    {
        _api.Setup(a => a.GetActiveBookingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([Booking(7, Today.AddDays(1), Now)]);

        await _vm.RefreshDispatchAsync();

        _vm.HasDispatch.Should().BeFalse();
    }

    [Test]
    public async Task Refresh_UnpreparedBookingToday_NoCard()
    {
        _api.Setup(a => a.GetActiveBookingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([Booking(7, Today, null)]);

        await _vm.RefreshDispatchAsync();

        _vm.HasDispatch.Should().BeFalse();
    }

    [Test]
    public async Task Refresh_ActiveFlightSession_NoCard()
    {
        _flightState.Set(new FlightStartResponse(12, 3, 4, "YU-API", Now), "JU360");
        _api.Setup(a => a.GetActiveBookingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([Booking(7, Today, Now)]);

        await _vm.RefreshDispatchAsync();

        _vm.HasDispatch.Should().BeFalse();
    }

    [Test]
    public async Task Refresh_ApiFailure_NoCardAndNoThrow()
    {
        _api.Setup(a => a.GetActiveBookingsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("offline"));

        await _vm.RefreshDispatchAsync();

        _vm.HasDispatch.Should().BeFalse();
    }

    [Test]
    public async Task Refresh_NotAuthenticated_NoCardAndNoApiCall()
    {
        _session.SetupGet(s => s.IsAuthenticated).Returns(false);

        await _vm.RefreshDispatchAsync();

        _vm.HasDispatch.Should().BeFalse();
        _api.Verify(a => a.GetActiveBookingsAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
```

- [ ] **Step 2: Run to verify failure** — compile error (ctor arity, `RefreshDispatchAsync` missing). Red.

- [ ] **Step 3: Implement in `PilotCentreViewModel`:**
  - New fields + ctor parameters (`FlightSessionState flightState, DispatchService dispatch`); DI resolves them (registration is already transient, no `App.xaml.cs` change needed).
  - Add members:

```csharp
    private readonly FlightSessionState _flightState;
    private readonly DispatchService _dispatch;
    private BookingInfo? _dispatchBooking;

    [ObservableProperty] private bool _hasDispatch;
    [ObservableProperty] private string _dispatchTitle = string.Empty;
    [ObservableProperty] private string _dispatchSub = string.Empty;
    [ObservableProperty] private string? _dispatchMessage;
```

  - Constructor becomes:

```csharp
    public PilotCentreViewModel(ISessionService session, FlightSessionState flightState, DispatchService dispatch)
    {
        _session = session;
        _flightState = flightState;
        _dispatch = dispatch;
        _session.StateChanged += (_, _) =>
        {
            OnUi(Load);
            _ = RefreshDispatchAsync();
        };
        Load();
        _ = RefreshDispatchAsync();
    }
```

  - The refresh + start command:

```csharp
    /// <summary>
    /// W4 dispatch pull: shows the resume-dispatch card iff a web-prepared
    /// (dispatch-ready) booking dated today exists and no session is active.
    /// Failures are silent — the card is simply absent when offline.
    /// </summary>
    public async Task RefreshDispatchAsync()
    {
        try
        {
            if (!_session.IsAuthenticated || _flightState.HasActiveSession)
            {
                ClearDispatchCard();
                return;
            }

            var bookings = await _session.Api.GetActiveBookingsAsync();
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var pick = bookings
                .Where(b => b.DispatchReadyAtUtc is not null && b.Date == today)
                .OrderByDescending(b => b.DispatchReadyAtUtc)
                .FirstOrDefault();

            if (pick is null)
            {
                ClearDispatchCard();
                return;
            }

            _dispatchBooking = pick;
            DispatchTitle = $"Dispatch ready: {pick.FlightNumber} {pick.DepIcao} → {pick.ArrIcao}";
            DispatchSub = $"{pick.AircraftType} · prepared {pick.DispatchReadyAtUtc:HH:mm}Z";
            HasDispatch = true;
        }
        catch
        {
            ClearDispatchCard();
        }
    }

    private void ClearDispatchCard()
    {
        _dispatchBooking = null;
        HasDispatch = false;
    }

    [RelayCommand]
    private async Task StartDispatchAsync()
    {
        if (_dispatchBooking is null) return;
        DispatchMessage = null;
        var result = await _dispatch.StartAsync(_dispatchBooking);
        if (result.Success)
            ClearDispatchCard();          // navigation to ACARS already happened
        else
            DispatchMessage = result.Message;
    }
```

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test AirSerbiaVirtua.Tests.Unit\AirSerbiaVirtua.Tests.Unit.csproj -p:Platform=x64`
Expected: all green (7 new tests).

- [ ] **Step 5: Add the card to `PilotCentreView.xaml`.** Read the file first; insert this card as the FIRST element inside the page's main content stack (above the profile header), adjusting only the converter resource if the view already declares one for bool→Visibility (`App.xaml`/view resources — reuse the existing key if present, otherwise add `<BooleanToVisibilityConverter x:Key="BoolToVis"/>` to the view's resources):

```xml
        <!-- W4: resume-dispatch card — visible only when a web-prepared booking for today exists -->
        <Border Style="{StaticResource Suite.Tile}" Margin="0,0,0,18"
                Visibility="{Binding HasDispatch, Converter={StaticResource BoolToVis}}">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>
                <StackPanel VerticalAlignment="Center">
                    <TextBlock Text="{Binding DispatchTitle}"
                               FontFamily="{StaticResource Suite.Display}"
                               FontWeight="Bold" FontSize="17" Foreground="White"/>
                    <TextBlock Text="{Binding DispatchSub}"
                               Style="{StaticResource Suite.PageSub}" Margin="0,4,0,0"/>
                    <TextBlock Text="{Binding DispatchMessage}"
                               Foreground="#F2546A" FontSize="12.5" Margin="0,6,0,0"
                               TextWrapping="Wrap"/>
                </StackPanel>
                <Button Grid.Column="1" Content="Start flight"
                        Style="{StaticResource Suite.BtnCrimson}"
                        Command="{Binding StartDispatchCommand}"
                        VerticalAlignment="Center" Margin="16,0,0,0"/>
            </Grid>
        </Border>
```

(Style keys `Suite.Tile`, `Suite.PageSub`, `Suite.BtnCrimson`, `Suite.Display` exist in `App.xaml` — verify spelling against the file while editing.)

- [ ] **Step 6: Build the desktop x64 + launch check**

Run: `dotnet build AirSerbiaVirtua.Acars.Desktop -p:Platform=x64`
Expected: 0 errors. (Visual check happens in Task 13's click-through.)

- [ ] **Step 7: Commit**

```powershell
git add -A
git commit -m "feat(desktop): Pilot Centre resume-dispatch card pulling web-prepared bookings"
```

---

### Task 10: Bookings page READY chip

**Files:**
- Modify: `AirSerbiaVirtua.Acars.Desktop/ViewModels/BookingsViewModel.cs` (`BookingRow`)
- Modify: `AirSerbiaVirtua.Acars.Desktop/Views/BookingsView.xaml`
- Test: `AirSerbiaVirtua.Tests.Unit/Desktop/BookingRowTests.cs`

- [ ] **Step 1: Write the failing test**:

```csharp
using AirSerbiaVirtua.Acars.Desktop.ViewModels;
using AirSerbiaVirtua.Contracts;
using FluentAssertions;

namespace AirSerbiaVirtua.Tests.Unit.Desktop;

public class BookingRowTests
{
    private static BookingInfo Booking(DateTimeOffset? ready) => new(
        7, 3, "JU360", "LYBE", "LOWW", "A319", 55,
        new DateOnly(2026, 6, 6), BookingStatus.Open, ready);

    [Test]
    public void IsDispatchReady_TrueWhenFlagSet()
    {
        new BookingRow(Booking(DateTimeOffset.UtcNow)).IsDispatchReady.Should().BeTrue();
    }

    [Test]
    public void IsDispatchReady_FalseWhenNull()
    {
        new BookingRow(Booking(null)).IsDispatchReady.Should().BeFalse();
    }

    [Test]
    public void Source_RoundTripsTheDto()
    {
        var dto = Booking(null);
        new BookingRow(dto).Source.Should().BeSameAs(dto);
    }
}
```

- [ ] **Step 2: Run to verify failure** — `IsDispatchReady` missing. Red.

- [ ] **Step 3: Implement.** In `BookingRow` add:

```csharp
    public bool IsDispatchReady => Source.DispatchReadyAtUtc is not null;
```

(`Source` exists since Task 8.) In `BookingsView.xaml`, inside the MY BOOKINGS row template next to the status badge, add a green chip (match the surrounding badge markup — `Suite.BadgeGreen`/`Suite.BadgeGreenText` style keys per `App.xaml`):

```xml
        <Border Style="{StaticResource Suite.BadgeGreen}" Margin="6,0,0,0"
                Visibility="{Binding IsDispatchReady, Converter={StaticResource BoolToVis}}">
            <TextBlock Text="READY" Style="{StaticResource Suite.BadgeGreenText}"/>
        </Border>
```

Reuse the view's existing bool→Visibility converter key, adding one if absent (same note as Task 9 Step 5).

- [ ] **Step 4: Run tests + build**

Run: `dotnet test AirSerbiaVirtua.Tests.Unit\AirSerbiaVirtua.Tests.Unit.csproj -p:Platform=x64` then `dotnet build AirSerbiaVirtua.Acars.Desktop -p:Platform=x64`
Expected: green, 0 errors.

- [ ] **Step 5: Commit**

```powershell
git add -A
git commit -m "feat(desktop): READY chip on dispatch-prepared bookings"
```

---

### Task 11: Web seam (`IPortalApi`) + dispatch client methods + `Tests.Web` + PortalBook changes (TDD)

**Files:**
- Create: `AirSerbiaVirtua.Web.Client/Services/IPortalApi.cs`
- Modify: `AirSerbiaVirtua.Web.Client/Services/PortalApi.cs`
- Modify: `AirSerbiaVirtua.Web.Client/Program.cs`
- Modify: all `@inject PortalApi Api` sites → `@inject IPortalApi Api` (`PortalNav.razor`, `PortalLogin.razor`, `PortalDashboard.razor`, `PortalBook.razor`, `PortalLogbook.razor`, `PortalProfile.razor`)
- Modify: `AirSerbiaVirtua.Web.Client/Pages/PortalBook.razor` (Prepare link + READY badge)
- Create: `AirSerbiaVirtua.Tests.Web` project + `PortalTestContext.cs` + `PortalBookTests.cs`

- [ ] **Step 1: Create the seam.** `AirSerbiaVirtua.Web.Client/Services/IPortalApi.cs`:

```csharp
using AirSerbiaVirtua.Contracts;

namespace AirSerbiaVirtua.Web.Client.Services;

/// <summary>Seam over <see cref="PortalApi"/> so bUnit tests can fake the API.</summary>
public interface IPortalApi
{
    Task<(bool Ok, string? Error)> LoginAsync(string callsign, string password);
    Task LogoutAsync();

    Task<List<RouteInfo>> GetRoutesAsync();
    Task<RouteInfo?> GetRouteAsync(int id);
    Task<List<BookingInfo>> GetMyBookingsAsync();
    Task<List<BookingInfo>> GetActiveBookingsAsync();
    Task<(bool Ok, string? Error)> CreateBookingAsync(int routeId, DateOnly date);
    Task<(bool Ok, string? Error)> CancelBookingAsync(int bookingId);
    Task<(bool Ok, string? Error)> MarkDispatchReadyAsync(int bookingId);
    Task<(bool Ok, string? Error)> ClearDispatchReadyAsync(int bookingId);

    Task<List<MetarInfo>> GetMetarAsync(IEnumerable<string> icaos);
    Task<List<PirepListItem>> GetMyPirepsAsync();
    Task<(bool Ok, string? Error)> ChangePasswordAsync(string current, string next);
}
```

- [ ] **Step 2: Implement in `PortalApi`.** Declare `public sealed class PortalApi(HttpClient http, PortalSession session) : IPortalApi` and add the new methods next to the existing booking methods:

```csharp
    public Task<List<BookingInfo>> GetActiveBookingsAsync() =>
        GetAsync<List<BookingInfo>>("api/v1/bookings/active");

    public async Task<RouteInfo?> GetRouteAsync(int id)
    {
        var resp = await SendWithRetryAsync(() => NewRequest(HttpMethod.Get, $"api/v1/routes/{id}"));
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<RouteInfo>();
    }

    public Task<List<MetarInfo>> GetMetarAsync(IEnumerable<string> icaos) =>
        GetAsync<List<MetarInfo>>($"api/v1/metar?icaos={Uri.EscapeDataString(string.Join(",", icaos))}");

    public async Task<(bool Ok, string? Error)> MarkDispatchReadyAsync(int bookingId)
    {
        var resp = await SendWithRetryAsync(() => NewRequest(HttpMethod.Post, $"api/v1/bookings/{bookingId}/dispatch"));
        return resp.IsSuccessStatusCode ? (true, null) : (false, await ReadErrorAsync(resp, "Dispatch failed"));
    }

    public async Task<(bool Ok, string? Error)> ClearDispatchReadyAsync(int bookingId)
    {
        var resp = await SendWithRetryAsync(() => NewRequest(HttpMethod.Delete, $"api/v1/bookings/{bookingId}/dispatch"));
        return resp.IsSuccessStatusCode ? (true, null) : (false, await ReadErrorAsync(resp, "Undo failed"));
    }
```

In `Program.cs` replace `builder.Services.AddScoped<PortalApi>();` with:

```csharp
builder.Services.AddScoped<IPortalApi, PortalApi>();
```

Switch every page/component `@inject PortalApi Api` → `@inject IPortalApi Api` (grep: `Select-String -Path AirSerbiaVirtua.Web.Client\**\*.razor -Pattern "inject PortalApi"`).

- [ ] **Step 3: Scaffold `Tests.Web`**

```powershell
dotnet new nunit -o AirSerbiaVirtua.Tests.Web -n AirSerbiaVirtua.Tests.Web
dotnet sln AirSerbiaVirtual.sln add AirSerbiaVirtua.Tests.Web\AirSerbiaVirtua.Tests.Web.csproj
dotnet add AirSerbiaVirtua.Tests.Web reference AirSerbiaVirtua.Web.Client\AirSerbiaVirtua.Web.Client.csproj
dotnet add AirSerbiaVirtua.Tests.Web package bunit
dotnet add AirSerbiaVirtua.Tests.Web package Moq
dotnet add AirSerbiaVirtua.Tests.Web package FluentAssertions
```

Change the csproj's first line to use the Razor SDK: `<Project Sdk="Microsoft.NET.Sdk.Razor">` and ensure `<TargetFramework>net10.0</TargetFramework>`. Delete `UnitTest1.cs`; add `Usings.cs`:

```csharp
global using NUnit.Framework;
```

Shared test context — `AirSerbiaVirtua.Tests.Web/PortalTestContext.cs`:

```csharp
using AirSerbiaVirtua.Contracts;
using AirSerbiaVirtua.Web.Client.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace AirSerbiaVirtua.Tests.Web;

/// <summary>
/// bUnit TestContext pre-wired with a signed-in PortalSession (loose JSInterop
/// stands in for localStorage) and a Moq IPortalApi.
/// </summary>
public class PortalTestContext : Bunit.TestContext
{
    public Mock<IPortalApi> Api { get; } = new();
    public PortalSession Session { get; }

    protected static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    public PortalTestContext()
    {
        // Fuel figures render with N0 ("2,100") — pin the culture so assertions
        // don't depend on the OS locale (Serbian would format "2.100").
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;

        JSInterop.Mode = JSRuntimeMode.Loose;
        Session = new PortalSession(JSInterop.JSRuntime);
        Services.AddSingleton(Session);
        Services.AddSingleton(Api.Object);
    }

    /// <summary>Signs the fake session in (must run before rendering a portal page).</summary>
    public async Task SignInAsync()
    {
        var tokens = new AuthTokens("at", DateTimeOffset.UtcNow.AddHours(1), "rt", DateTimeOffset.UtcNow.AddDays(7));
        var pilot = new PilotProfile(1, "ASL001", "Test Pilot", "t@asv.test",
            2, "First Officer", 12.5m, PilotStatus.Active, "LYBE", DateTimeOffset.UtcNow);
        await Session.SetAsync(tokens, pilot);
    }

    public static BookingInfo Booking(int id, DateTimeOffset? ready = null,
        BookingStatus status = BookingStatus.Open) => new(
        id, 3, $"JU{id:000}", "LYBE", "LOWW", "A319", 55, Today, status, ready);
}
```

(If rendering pages that declare `@rendermode PortalRender.NoPrerender` throws in bUnit, call `SetAssignedRenderMode(null)` on the render — or, per bUnit docs for the installed version, render mode metadata is ignored; adjust per the actual error.)

- [ ] **Step 4: Write the failing PortalBook tests** — `AirSerbiaVirtua.Tests.Web/PortalBookTests.cs`:

```csharp
using AirSerbiaVirtua.Web.Client.Pages;
using Bunit;
using FluentAssertions;
using Moq;

namespace AirSerbiaVirtua.Tests.Web;

public class PortalBookTests : PortalTestContext
{
    [Test]
    public async Task ReadyBooking_ShowsReadyBadge_AndPrepareLink()
    {
        await SignInAsync();
        Api.Setup(a => a.GetRoutesAsync()).ReturnsAsync([]);
        Api.Setup(a => a.GetMyBookingsAsync())
            .ReturnsAsync([Booking(7, ready: DateTimeOffset.UtcNow)]);

        var cut = RenderComponent<PortalBook>();

        cut.Markup.Should().Contain("READY");
        cut.Find($"a[href='portal/briefing?bookingId=7']").TextContent.Should().Contain("Prepare");
    }

    [Test]
    public async Task UnpreparedBooking_HasPrepareLink_ButNoReadyBadge()
    {
        await SignInAsync();
        Api.Setup(a => a.GetRoutesAsync()).ReturnsAsync([]);
        Api.Setup(a => a.GetMyBookingsAsync()).ReturnsAsync([Booking(7)]);

        var cut = RenderComponent<PortalBook>();

        cut.Markup.Should().NotContain("READY");
        cut.FindAll("a[href='portal/briefing?bookingId=7']").Should().HaveCount(1);
    }
}
```

- [ ] **Step 5: Run to verify failure**

Run: `dotnet test AirSerbiaVirtua.Tests.Web\AirSerbiaVirtua.Tests.Web.csproj`
Expected: both tests fail (no badge/link in markup). Red. (If they fail on session redirect instead, fix the context wiring first — the failure must be the missing UI.)

- [ ] **Step 6: Implement in `PortalBook.razor`.** In the MY BOOKINGS row, after the status-badge `<td>`, add a READY badge inside that same cell and a Prepare link in the actions cell before Cancel:

```razor
                                <td class="px-5 py-4">
                                    <span class="rounded-md bg-emerald-400/10 border border-emerald-400/30 px-2 py-0.5 font-mono text-[10px] text-emerald-200">
                                        @StatusLabel(b.Status)
                                    </span>
                                    @if (b.DispatchReadyAtUtc is not null)
                                    {
                                        <span class="ml-2 rounded-md bg-emerald-400/20 border border-emerald-400/50 px-2 py-0.5 font-mono text-[10px] text-emerald-100">READY</span>
                                    }
                                </td>
                                <td class="px-5 py-4 text-right whitespace-nowrap">
                                    <a href="portal/briefing?bookingId=@b.Id"
                                       class="rounded-lg panel px-3.5 py-1.5 text-[12px] font-bold text-royal-bright hover:text-white transition mr-2">
                                        Prepare →
                                    </a>
                                    <button @onclick="() => CancelAsync(b)" disabled="@_busy"
                                            class="rounded-lg panel px-3.5 py-1.5 text-[12px] font-bold text-mist hover:text-white transition disabled:opacity-40">
                                        Cancel
                                    </button>
                                </td>
```

(The Cancel `<td>` replaces the existing actions cell.)

- [ ] **Step 7: Run to verify pass**

Run: `dotnet test AirSerbiaVirtua.Tests.Web\AirSerbiaVirtua.Tests.Web.csproj`
Expected: green.

- [ ] **Step 8: Commit**

```powershell
git add -A
git commit -m "feat(web): IPortalApi seam, dispatch client methods, PortalBook READY badge + Prepare link with bUnit tests"
```

---

### Task 12: Web Briefing page (TDD)

**Files:**
- Create: `AirSerbiaVirtua.Web.Client/Pages/PortalBriefing.razor`
- Modify: `AirSerbiaVirtua.Web.Client/Shared/PortalNav.razor` (new tab)
- Test: `AirSerbiaVirtua.Tests.Web/PortalBriefingTests.cs`

- [ ] **Step 1: Write the failing tests**:

```csharp
using AirSerbiaVirtua.Contracts;
using AirSerbiaVirtua.Web.Client.Pages;
using Bunit;
using FluentAssertions;
using Moq;

namespace AirSerbiaVirtua.Tests.Web;

public class PortalBriefingTests : PortalTestContext
{
    private static readonly RouteInfo Route = new(3, "JU360", "LYBE", "LOWW", "A319", 300, 55, [1, 2, 3]);

    private void SetupHappyApi(params BookingInfo[] bookings)
    {
        Api.Setup(a => a.GetActiveBookingsAsync()).ReturnsAsync([.. bookings]);
        Api.Setup(a => a.GetRouteAsync(3)).ReturnsAsync(Route);
        Api.Setup(a => a.GetMetarAsync(It.IsAny<IEnumerable<string>>())).ReturnsAsync(
        [
            new MetarInfo("LYBE", "LYBE 061330Z 12008KT CAVOK 24/12 Q1018", DateTimeOffset.UtcNow),
            new MetarInfo("LOWW", "LOWW 061320Z 30010KT 9999 FEW040 21/10 Q1015", DateTimeOffset.UtcNow)
        ]);
    }

    [Test]
    public async Task NoBookings_ShowsEmptyStateLinkingToBook()
    {
        await SignInAsync();
        Api.Setup(a => a.GetActiveBookingsAsync()).ReturnsAsync([]);

        var cut = RenderComponent<PortalBriefing>();

        cut.Markup.Should().Contain("No active bookings");
        cut.Find("a[href='portal/book']").Should().NotBeNull();
    }

    [Test]
    public async Task SelectedBooking_RendersRouteFactsFuelAndMetars()
    {
        await SignInAsync();
        SetupHappyApi(Booking(7));

        var cut = RenderComponent<PortalBriefing>();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("JU360").And.Contain("300 nm");
            // A319 over 300 nm: trip 2100, reserve 2290, total 4390
            cut.Markup.Should().Contain("2,100").And.Contain("2,290").And.Contain("4,390");
            cut.Markup.Should().Contain("LYBE 061330Z").And.Contain("LOWW 061320Z");
        });
    }

    [Test]
    public async Task MarkReady_CallsApi_AndShowsReadyBadgeWithUndo()
    {
        await SignInAsync();
        SetupHappyApi(Booking(7));
        Api.Setup(a => a.MarkDispatchReadyAsync(7)).ReturnsAsync((true, null));
        // after marking, reload returns the flagged booking
        Api.SetupSequence(a => a.GetActiveBookingsAsync())
            .ReturnsAsync([Booking(7)])
            .ReturnsAsync([Booking(7, ready: DateTimeOffset.UtcNow)]);

        var cut = RenderComponent<PortalBriefing>();
        cut.WaitForElement("button#mark-ready").Click();

        cut.WaitForAssertion(() =>
        {
            Api.Verify(a => a.MarkDispatchReadyAsync(7), Times.Once);
            cut.Markup.Should().Contain("DISPATCH READY");
            cut.Find("button#undo-ready").Should().NotBeNull();
        });
    }

    [Test]
    public async Task MarkReady_Conflict_ShowsApiMessageInline()
    {
        await SignInAsync();
        SetupHappyApi(Booking(7));
        Api.Setup(a => a.MarkDispatchReadyAsync(7))
            .ReturnsAsync((false, "Booking is Flown — only Open or Confirmed bookings can be dispatched."));

        var cut = RenderComponent<PortalBriefing>();
        cut.WaitForElement("button#mark-ready").Click();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Booking is Flown"));
    }

    [Test]
    public async Task DeepLink_BookingIdQuery_PreselectsThatBooking()
    {
        await SignInAsync();
        var other = new BookingInfo(8, 3, "JU361", "LOWW", "LYBE", "A319", 55, Today, BookingStatus.Open, null);
        SetupHappyApi(Booking(7), other);

        var nav = Services.GetRequiredService<Bunit.TestDoubles.FakeNavigationManager>();
        nav.NavigateTo("portal/briefing?bookingId=8");

        var cut = RenderComponent<PortalBriefing>();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("JU361"));
    }
}
```

Add `using Microsoft.Extensions.DependencyInjection;` if `GetRequiredService` is unresolved. (bUnit version note: the fake navigation manager type may be `BunitNavigationManager` in newer bUnit — use whichever the installed version exposes.)

- [ ] **Step 2: Run to verify failure** — compile error (`PortalBriefing` missing). Red.

- [ ] **Step 3: Implement** — `AirSerbiaVirtua.Web.Client/Pages/PortalBriefing.razor` (complete file):

```razor
@page "/portal/briefing"
@rendermode PortalRender.NoPrerender
@inject PortalSession Session
@inject IPortalApi Api
@inject NavigationManager Nav

<PageTitle>Briefing — Air Serbia Virtual</PageTitle>

<section class="mx-auto max-w-7xl px-4 sm:px-6 py-10">
    @if (!_ready)
    {
        <p class="text-mist text-[13.5px]">Loading briefing…</p>
    }
    else
    {
        <PortalNav />

        @if (_message is not null)
        {
            <div class="mb-6 rounded-xl border border-crimson/50 bg-crimson/10 px-5 py-3.5 text-[13.5px] text-rose-100">
                @_message
            </div>
        }

        @if (_bookings.Count == 0)
        {
            <div class="panel-solid rounded-2xl px-5 py-10 text-center">
                <p class="text-mist/70 text-[13.5px]">
                    No active bookings to brief.
                    <a class="text-royal-bright hover:underline" href="portal/book">Book a flight</a> first.
                </p>
            </div>
        }
        else
        {
            <div class="grid gap-6 lg:grid-cols-[320px_1fr]">
                <!-- Booking list -->
                <div>
                    <p class="font-mono text-[10px] tracking-[0.2em] text-mist/70 mb-4">ACTIVE BOOKINGS</p>
                    <div class="panel-solid rounded-2xl overflow-hidden">
                        @foreach (var b in _bookings)
                        {
                            <button @onclick="() => SelectAsync(b)"
                                    class="w-full text-left px-5 py-4 border-b hairline last:border-0 transition-colors @(b.Id == _selected?.Id ? "bg-royal/10" : "hover:bg-royal/5")">
                                <span class="font-display font-bold text-white">@b.FlightNumber</span>
                                <span class="ml-2 font-mono text-[12px] text-slate-100">@b.DepIcao → @b.ArrIcao</span>
                                @if (b.DispatchReadyAtUtc is not null)
                                {
                                    <span class="ml-2 rounded-md bg-emerald-400/20 border border-emerald-400/50 px-2 py-0.5 font-mono text-[10px] text-emerald-100">READY</span>
                                }
                                <p class="font-mono text-[11px] text-mist/70 mt-1">@b.Date.ToString("dd MMM yyyy")</p>
                            </button>
                        }
                    </div>
                </div>

                <!-- Briefing panel -->
                <div>
                    @if (_selected is null || _route is null)
                    {
                        <p class="text-mist/60 text-[13px]">Select a booking to brief.</p>
                    }
                    else
                    {
                        <div class="flex items-center justify-between mb-4">
                            <p class="font-mono text-[10px] tracking-[0.2em] text-mist/70">
                                BRIEFING — @_route.FlightNumber @_route.DepIcao → @_route.ArrIcao
                            </p>
                            @if (_selected.DispatchReadyAtUtc is { } readyAt)
                            {
                                <div class="flex items-center gap-2">
                                    <span class="rounded-md bg-emerald-400/20 border border-emerald-400/50 px-2.5 py-1 font-mono text-[10.5px] text-emerald-100">
                                        DISPATCH READY · @readyAt.ToString("HH:mm")Z
                                    </span>
                                    <button id="undo-ready" @onclick="UndoReadyAsync" disabled="@_busy"
                                            class="rounded-lg panel px-3 py-1 text-[11.5px] font-bold text-mist hover:text-white transition disabled:opacity-40">
                                        Undo
                                    </button>
                                </div>
                            }
                            else
                            {
                                <button id="mark-ready" @onclick="MarkReadyAsync" disabled="@_busy"
                                        class="rounded-lg bg-gradient-to-b from-crimson-bright to-crimson px-4 py-1.5 text-[12px] font-bold text-white shadow shadow-crimson/30 hover:brightness-110 transition disabled:opacity-40">
                                    Mark dispatch ready
                                </button>
                            }
                        </div>

                        <!-- Route facts -->
                        <div class="grid grid-cols-2 sm:grid-cols-4 gap-3 mb-6">
                            <div class="panel-solid rounded-xl px-4 py-3">
                                <p class="font-mono text-[9.5px] tracking-[0.16em] text-mist/70">AIRCRAFT</p>
                                <p class="font-display font-bold text-white mt-1">@_route.AircraftType</p>
                            </div>
                            <div class="panel-solid rounded-xl px-4 py-3">
                                <p class="font-mono text-[9.5px] tracking-[0.16em] text-mist/70">DISTANCE</p>
                                <p class="font-display font-bold text-white mt-1">@_route.DistanceNm nm</p>
                            </div>
                            <div class="panel-solid rounded-xl px-4 py-3">
                                <p class="font-mono text-[9.5px] tracking-[0.16em] text-mist/70">PLANNED</p>
                                <p class="font-display font-bold text-white mt-1">@($"{_route.PlannedMinutes / 60}h {_route.PlannedMinutes % 60:00}m")</p>
                            </div>
                            <div class="panel-solid rounded-xl px-4 py-3">
                                <p class="font-mono text-[9.5px] tracking-[0.16em] text-mist/70">SERVICE DATE</p>
                                <p class="font-display font-bold text-white mt-1">@_selected.Date.ToString("dd MMM")</p>
                            </div>
                        </div>

                        <!-- Fuel estimate -->
                        <p class="font-mono text-[10px] tracking-[0.2em] text-mist/70 mb-3">PLANNING FUEL (INDICATIVE)</p>
                        <div class="grid grid-cols-3 gap-3 mb-6">
                            <div class="panel-solid rounded-xl px-4 py-3">
                                <p class="font-mono text-[9.5px] tracking-[0.16em] text-mist/70">TRIP</p>
                                <p class="font-mono font-bold text-white mt-1">@($"{_fuelTrip:N0}") kg</p>
                            </div>
                            <div class="panel-solid rounded-xl px-4 py-3">
                                <p class="font-mono text-[9.5px] tracking-[0.16em] text-mist/70">RESERVE</p>
                                <p class="font-mono font-bold text-white mt-1">@($"{_fuelReserve:N0}") kg</p>
                            </div>
                            <div class="panel-solid rounded-xl px-4 py-3 border border-royal/40">
                                <p class="font-mono text-[9.5px] tracking-[0.16em] text-mist/70">BLOCK TOTAL</p>
                                <p class="font-mono font-bold text-royal-bright mt-1">@($"{_fuelTrip + _fuelReserve:N0}") kg</p>
                            </div>
                        </div>

                        <!-- Weather -->
                        <p class="font-mono text-[10px] tracking-[0.2em] text-mist/70 mb-3">WEATHER</p>
                        <div class="space-y-3">
                            <div class="panel-solid rounded-xl px-4 py-3">
                                <p class="font-mono text-[9.5px] tracking-[0.16em] text-mist/70">DEP · @_route.DepIcao</p>
                                <p class="font-mono text-[12px] text-slate-100 mt-1">@(_depMetar?.Raw ?? "No report available.")</p>
                            </div>
                            <div class="panel-solid rounded-xl px-4 py-3">
                                <p class="font-mono text-[9.5px] tracking-[0.16em] text-mist/70">ARR · @_route.ArrIcao</p>
                                <p class="font-mono text-[12px] text-slate-100 mt-1">@(_arrMetar?.Raw ?? "No report available.")</p>
                            </div>
                        </div>
                    }
                </div>
            </div>
        }
    }
</section>

@code {
    [SupplyParameterFromQuery(Name = "bookingId")]
    public int? BookingId { get; set; }

    private bool _ready;
    private bool _busy;
    private string? _message;
    private List<BookingInfo> _bookings = [];
    private BookingInfo? _selected;
    private RouteInfo? _route;
    private MetarInfo? _depMetar;
    private MetarInfo? _arrMetar;
    private int _fuelTrip;
    private int _fuelReserve;

    protected override async Task OnInitializedAsync()
    {
        await Session.EnsureRestoredAsync();
        if (!Session.IsAuthenticated)
        {
            Nav.NavigateTo("portal/login");
            return;
        }

        await LoadAsync(preferId: BookingId);
        _ready = true;
    }

    private async Task LoadAsync(int? preferId)
    {
        try
        {
            _bookings = await Api.GetActiveBookingsAsync();
        }
        catch
        {
            _message = "The crew desk is unreachable — try again in a minute.";
            _bookings = [];
            return;
        }

        BookingInfo? pick = null;
        if (preferId is { } id) pick = _bookings.FirstOrDefault(b => b.Id == id);
        pick ??= _bookings.FirstOrDefault();
        await SelectAsync(pick);
    }

    private async Task SelectAsync(BookingInfo? b)
    {
        _selected = b;
        _route = null;
        _depMetar = _arrMetar = null;
        if (b is null) return;

        _route = await Api.GetRouteAsync(b.RouteId);
        if (_route is null) return;

        (_fuelTrip, _fuelReserve) = FuelEstimator.Estimate(_route.AircraftType, _route.DistanceNm);

        try
        {
            var metars = await Api.GetMetarAsync([_route.DepIcao, _route.ArrIcao]);
            _depMetar = metars.FirstOrDefault(m => m.Icao.Equals(_route.DepIcao, StringComparison.OrdinalIgnoreCase));
            _arrMetar = metars.FirstOrDefault(m => m.Icao.Equals(_route.ArrIcao, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            // METAR proxy down — the panel shows "No report available."
        }
    }

    private async Task MarkReadyAsync()
    {
        if (_selected is null) return;
        _busy = true;
        _message = null;
        var (ok, error) = await Api.MarkDispatchReadyAsync(_selected.Id);
        if (!ok) _message = error;
        await ReloadKeepingSelectionAsync();
        _busy = false;
    }

    private async Task UndoReadyAsync()
    {
        if (_selected is null) return;
        _busy = true;
        _message = null;
        var (ok, error) = await Api.ClearDispatchReadyAsync(_selected.Id);
        if (!ok) _message = error;
        await ReloadKeepingSelectionAsync();
        _busy = false;
    }

    private async Task ReloadKeepingSelectionAsync()
    {
        var keep = _selected?.Id;
        _bookings = await Api.GetActiveBookingsAsync();
        var again = keep is { } id ? _bookings.FirstOrDefault(b => b.Id == id) : null;
        // Selection object is stale after reload — rebind without refetching route/metar
        // unless the booking vanished (e.g. flown elsewhere).
        if (again is not null && _selected is not null && again.RouteId == _selected.RouteId)
            _selected = again;
        else
            await SelectAsync(again ?? _bookings.FirstOrDefault());
    }
}
```

- [ ] **Step 4: Add the nav tab.** In `PortalNav.razor` after the "Book a flight" NavLink:

```razor
        <NavLink href="portal/briefing" class="tab-btn px-3.5 py-3 hover:text-white transition-colors" ActiveClass="active">Briefing</NavLink>
```

- [ ] **Step 5: Run to verify pass**

Run: `dotnet test AirSerbiaVirtua.Tests.Web\AirSerbiaVirtua.Tests.Web.csproj`
Expected: all green (7 tests).

- [ ] **Step 6: Commit**

```powershell
git add -A
git commit -m "feat(web): Briefing page with fuel/METARs and dispatch-ready toggle, bUnit-tested"
```

---

### Task 13: Full verification + docs

- [ ] **Step 1: Full build + every test suite**

```powershell
dotnet build AirSerbiaVirtual.sln -p:Platform=x64
dotnet test AirSerbiaVirtua.Tests.Unit\AirSerbiaVirtua.Tests.Unit.csproj -p:Platform=x64
dotnet test AirSerbiaVirtua.Tests.Api\AirSerbiaVirtua.Tests.Api.csproj
dotnet test AirSerbiaVirtua.Tests.Web\AirSerbiaVirtua.Tests.Web.csproj
```

Expected: 0 build errors; every suite green. Report exact counts.

- [ ] **Step 2: Live E2E smoke (Postgres up).** Start API (`dotnet run --project AirSerbiaVirtua.Api --launch-profile http` — applies the new migration to the dev DB) and Web (`dotnet run --project AirSerbiaVirtua.Web`). Then with a real pilot login via curl: book a route, `POST /api/v1/bookings/{id}/dispatch`, confirm `GET /api/v1/bookings/active` orders it first, `DELETE` clears. Leave both running for the user's browser pass.

- [ ] **Step 3: Update docs.**
  - `doc/next-session.md`: prepend a W4 entry — what shipped, test counts, and the remaining **manual checklist**: browser pass on `/portal/briefing` (mark ready → desktop Pilot Centre card → Start flight → ACARS Live), READY chips on both clients, plus the still-pending Phase 2b MSFS flight.
  - `doc/website-design.md` line `4. **W4 Desktop slim-down** — desktop pulls active dispatch; remove its Bookings/Briefing UI`: append ` *(amended 2026-06-06: desktop keeps Bookings/Briefing — see doc/w4-desktop-slimdown-design.md)*`.

- [ ] **Step 4: Final commit**

```powershell
git add -A
git commit -m "docs: W4 close-out — test counts, manual checklist, design amendment note"
```

---

## Spec coverage map (self-check)

| Spec section | Tasks |
|---|---|
| §1 Data model (`DispatchReadyAtUtc`) | 6 |
| §2 API endpoints (mark/clear/active) | 6 |
| §3 Web Briefing + PortalBook changes + `IPortalApi` | 11, 12 |
| §3 Shared `FuelEstimator` | 2 |
| §4 `DispatchService` + `IApiService` | 7, 8 |
| §4 Pilot Centre card | 9 |
| §4 Bookings READY chip | 10 |
| §5 Contracts convergence (enums, records, deletions, refs) | 3, 4, 7 |
| §6 Error handling | built into 6, 8, 9, 12 (tested) |
| §7 Testing (3 projects, per-segment coverage, golden JSON) | 1, 2, 3, 5, 6, 8–12 |
| §7 Manual checklist | 13 |
