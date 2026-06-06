# Phase W4 — Web dispatch handoff + contracts convergence

Date: 2026-06-06
Status: Approved design, pre-implementation
Parent: `doc/website-design.md` (phase W4)

## Goal

The pilot books and prepares a flight on the web; the desktop ACARS client
pulls that prepared ("dispatch ready") booking and starts the flight in one
click. Simultaneously, end the three-way DTO duplication by making
`AirSerbiaVirtua.Contracts` the single source for wire types used by the API,
the desktop client, and the web.

## Decisions (made 2026-06-06)

| Decision | Choice |
|---|---|
| Desktop slim-down extent | **Keep** desktop Bookings + Briefing pages as-is; **add** the dispatch pull. The original "remove Bookings/Briefing UI" wording in website-design.md is superseded — those pages were since built, restyled, and work. Web becomes the preferred path; desktop stays self-sufficient. |
| "Dispatch ready" semantics | **Explicit flag**: `Booking.DispatchReadyAtUtc` (`DateTimeOffset?`), set by the pilot from the web Briefing page. No separate Dispatch table, no persisted fuel plan (the estimate is recomputed identically on both clients). |
| Contracts convergence scope | **Desktop + API** — both reference Contracts; duplicated records in `Api.Dtos/ApiDtos.cs` and `Acars.Core/ApiContracts.cs` are deleted. Wire shapes unchanged. |
| Testing | **Automated tests for every W4 segment** (first test projects in the solution): NUnit 4 + Moq + FluentAssertions; API integration tests via WebApplicationFactory + Testcontainers Postgres (fallback: dedicated `airserbiavirtua_test` DB on local Postgres if Docker proves unusable); bUnit for web components. Scope = W4 code + convergence guards; pre-W4 features keep manual coverage. |

## 1. Data model

One migration, `AddBookingDispatch`:

- `Booking.DispatchReadyAtUtc` — `DateTimeOffset?`, null = not prepared.

No expiry logic changes. A booking that becomes Flown/Cancelled/Expired simply
drops out of the active list; a stale flag on it is harmless history.

## 2. API — `BookingsController`

| Endpoint | Behavior |
|---|---|
| `POST /api/v1/bookings/{id}/dispatch` | Sets `DispatchReadyAtUtc = now` on a booking owned by the caller with status Open or Confirmed. Idempotent — re-POST refreshes the timestamp. 404 if missing or not owned; 409 if Flown/Cancelled/Expired. Returns the updated `BookingInfo`. |
| `DELETE /api/v1/bookings/{id}/dispatch` | Clears the flag ("unprepare"). Same guards. 204 on success. |
| `GET /api/v1/bookings/active` | The caller's Open/Confirmed bookings, any date. Order: dispatch-ready first (newest `DispatchReadyAtUtc` first), then unprepared by `Date` ascending, then `Id`. Item shape = `BookingInfo`. |

`BookingInfo` gains `DateTimeOffset? DispatchReadyAtUtc = null` — additive and
camelCase; pre-W4 clients ignore it.

## 3. Web portal — Briefing page

New `/portal/briefing` (WASM, `@rendermode PortalRender.NoPrerender`, new tab
in `PortalNav` between Book and Logbook):

- Left column: the pilot's active bookings from `GET /bookings/active`;
  click to select.
- Right column, for the selected booking: route facts from
  `GET /routes/{id}` (flight number, leg, aircraft type, distance, planned
  time), fuel estimate (trip / reserve / total via shared `FuelEstimator`),
  dep + arr METARs from `GET /metar?icaos=`.
- **Mark dispatch ready** button → `POST /bookings/{id}/dispatch` → green
  "DISPATCH READY · HH:mmZ" badge + **Undo** (DELETE). Conflict (409) renders
  the API message inline and refreshes the list.
- Deep link: `/portal/briefing?bookingId={id}` preselects that booking.
- Empty state when no active bookings ("Book a flight first") linking to
  /portal/book.

Testability seam: `PortalApi` gets an `IPortalApi` interface (members the
portal pages use) so bUnit tests can substitute a fake; pages inject the
interface.

`/portal/book` changes: each MY BOOKINGS row gets a "Prepare →" link to the
briefing deep link, and a "READY" badge when `DispatchReadyAtUtc` is set.

### Shared fuel estimator

`EstimateFuel` (currently private in desktop `BriefingViewModel`) moves to a
pure static `FuelEstimator` class in **Contracts** — zero dependencies, both
clients call it so web and desktop always show identical numbers. This is a
deliberate, narrow exception to "Contracts = DTOs only". Desktop
`BriefingViewModel` switches to it.

## 4. Desktop — dispatch pull

### `DispatchService` (new, `Acars.Desktop/Services`)

Extracts the start-flight sequence currently inlined in
`BookingsViewModel.StartFlightAsync`: pick first Active airframe of the
required type (`GetAircraftAsync(type, "Active")`) → `StartFlightAsync(routeId,
aircraftId, date)` → `SimulatorService.ResetSession()` → `FlightSessionState.Set`
→ navigate to ACARS Live. Conflict/no-airframe errors are returned to the
caller for display. Callers: `BookingsViewModel` (unchanged behavior) and the
Pilot Centre dispatch card.

Testability seam: `ApiService` gets an `IApiService` interface and
`ISessionService.Api` exposes it (mechanical property-type change; ViewModels
already consume it through the session). `DispatchService` and the ViewModels
become unit-testable with a mocked `IApiService`; the sim side already has its
`ISimBridge` fake seam.

### Pilot Centre — "Resume dispatch" card

Pilot Centre is the post-login landing page (`MainViewModel.cs:176`), so the
card lives there:

- On load and on Refresh, `PilotCentreViewModel` calls new
  `ApiService.GetActiveBookingsAsync()` (`GET /bookings/active`).
- Card shows iff: at least one booking is dispatch-ready **and dated today**,
  and no flight session is active. Multiple matches → newest
  `DispatchReadyAtUtc` wins (the rest remain visible on Bookings).
- Content: "Dispatch ready: JU360 LYBE→LOWW · A319 · prepared 14:32Z" +
  crimson **Start flight** button (Suite.BtnCrimson) → `DispatchService` →
  ACARS Live.
- Any failure of the pull (offline, 401 mid-refresh, API down) → card simply
  absent; log only, never an error surface.

### Bookings page

MY BOOKINGS rows get a green "READY" chip (Suite.BadgeGreen) when
`DispatchReadyAtUtc` is set. Booking/cancel/start behavior unchanged.

## 5. Contracts convergence

### Enums

All five enums move from `Api.Models/Enums.cs` (file deleted) to
`AirSerbiaVirtua.Contracts/Enums.cs`: `PilotStatus`, `AircraftStatus`,
`BookingStatus`, `PirepStatus`, `FlightPhase`. API entities and all clients
use them. Wire format unchanged: no `JsonStringEnumConverter` is registered,
so enums already serialize as numbers; EF stores ints. Desktop deletes its
`ApiBookingStatus` and `ApiFlightPhase` mirrors.

### Records

`Contracts` becomes the only definition; names converge on the existing
Contracts style:

| API (`Api.Dtos`, deleted) | Desktop (`ApiContracts.cs`, deleted) | Contracts (canonical) |
|---|---|---|
| `RouteDto` | `ApiRoute` | `RouteInfo` |
| `BookingDto` | `ApiBooking` | `BookingInfo` (+ `DispatchReadyAtUtc`) |
| `AircraftDto` | `ApiAircraft` | `AircraftInfo` |
| `PilotProfileDto` | `PilotProfile` | `PilotProfile` (Status becomes `PilotStatus`) |
| `AuthTokensDto` | `AuthTokens` | `AuthTokens` |
| `AdminPilotDto` | `AdminPilot` | `AdminPilot` (Status becomes `PilotStatus`) |
| `PirepListItemDto` | `PirepListItem` | `PirepListItem` (Status becomes `PirepStatus`) |
| `PirepResultDto` | `PirepResult` | `PirepResult` (Status becomes `PirepStatus`) |
| `MetarDto` | `MetarInfo` | `MetarInfo` |
| `PositionReportDto` | `PositionReport` | `PositionReport` (Phase becomes `FlightPhase`) |
| same-named | same-named | `LoginRequest`, `RefreshRequest`, `RegisterRequest`, `ChangePasswordRequest`, `CreateBookingRequest`, `LoginResponse`, `RefreshResponse`, `FlightStartRequest`, `FlightStartResponse`, `PirepSubmitRequest`, `AirportDto`, `AirportSyncResponse`, `OutstationStartRequest` — move as-is |

Existing Contracts records whose `Status` fields are `int` (`PilotProfile`,
`BookingInfo`, `PirepListItem`, `AdminPilot`) become enum-typed; Web.Client
portal pages that compare status ints switch to enum comparisons (compile-time
mechanical change, same wire values).

### What stays in `Acars.Core`

- `PhaseMapping` (`FlightState` → `FlightPhase`) — depends on the sim-side
  state machine.
- The POSREP factory: `PositionReport.FromTelemetry` becomes a static helper
  `PositionReports.FromTelemetry(FlightData, FlightState)` in Acars.Core,
  building the Contracts record (the record itself carries no sim
  dependencies).
- Everything sim/queue related (`FlightData`, `FlightState`, `PosrepQueue`,
  `ISimBridge`, …) — untouched.

### Project references (new)

`Acars.Core → Contracts` and `Api → Contracts`. Web/Web.Client already
reference Contracts. Final dependency picture matches website-design.md §1.

## 6. Error handling

- Mark-ready race (booking flown/cancelled in the meantime): API 409 with
  message; web shows it inline and refreshes; desktop start-from-dispatch
  conflicts reuse the existing Start-flight error paths (same code via
  `DispatchService`).
- Desktop offline at startup: dispatch pull failure is swallowed (log only).
- `GET /bookings/active` with zero rows: empty list, both clients render
  their normal empty states.

## 7. Testing

First automated test projects in the solution. Stack: **NUnit 4 + Moq +
FluentAssertions**; **bUnit** for Blazor; **WebApplicationFactory +
Testcontainers (Postgres)** for API integration. Run with `dotnet test` on the
solution. If Docker turns out unusable on the dev machine, the integration
fixture falls back to a dedicated `airserbiavirtua_test` database on the local
Postgres server (connection string via user-secrets), reset between runs.

| Project | Targets | Covers |
|---|---|---|
| `AirSerbiaVirtua.Tests.Unit` | net10.0-windows, **x64** (references Desktop, which is x64-only) + Core + Contracts | FuelEstimator, wire-shape guards, DispatchService, ViewModel logic |
| `AirSerbiaVirtua.Tests.Api` | net10.0 | Dispatch endpoints + `/active` + convergence regression against real Postgres |
| `AirSerbiaVirtua.Tests.Web` | net10.0 (bUnit) | Briefing page, PortalBook changes |

### Per segment

**Contracts — `FuelEstimator`** (unit): burn rates per known type, unknown
type falls back to default, trip = distance × burn rounded, reserve formula,
zero-distance edge.

**Contracts — wire-shape guards** (unit): serialize each canonical record
with web defaults (camelCase) and assert golden JSON — property names, enums
as numbers, `dispatchReadyAtUtc` null-when-absent. Covers `LoginResponse`,
`PilotProfile`, `RouteInfo`, `BookingInfo` (with and without dispatch flag),
`PirepListItem`, `PositionReport`, `FlightStartResponse`. These tests are the
convergence safety net for *every* client.

**API — dispatch endpoints** (integration, real Postgres):
- `POST /dispatch`: sets timestamp; idempotent re-POST refreshes it; 404 on
  missing/foreign booking; 409 on Flown/Cancelled/Expired; 401 anonymous.
- `DELETE /dispatch`: clears flag, 204; same guard matrix.
- `GET /active`: only Open/Confirmed of the caller; ordering exactly as §2
  (ready newest-first → date → id); other pilots' bookings excluded;
  empty list when none.
- Convergence regression: `POST /auth/login`, `GET /routes`,
  `GET /bookings/mine`, `GET /pireps/mine` — response JSON contains the
  expected camelCase keys with numeric enum values (locks the pre-W4 wire
  format in place).
- Migrations apply on a blank Postgres container (fixture startup proves it).

**Web — bUnit** (fake `IPortalApi`):
- Briefing: renders active bookings; selection loads route facts + fuel +
  METARs; Mark-ready calls POST and renders READY badge + Undo; Undo calls
  DELETE; 409 message rendered inline; `?bookingId=` preselects; empty state.
- PortalBook: READY badge and "Prepare →" link render iff flag set.

**Desktop — unit** (mock `IApiService`, fake `ISimBridge`):
- `DispatchService`: happy path (first Active airframe picked → start called
  with right args → `FlightSessionState` set → sim reset → navigate to
  ACARS); no-airframe error; API conflict propagates message; state untouched
  on failure.
- `PilotCentreViewModel` card: visible iff ready-today booking and no active
  session; newest `DispatchReadyAtUtc` wins among several; hidden on API
  failure (and no error surfaced); hidden while session active.
- `BookingsViewModel`: READY chip mapping and unchanged Start-flight
  delegation to `DispatchService`.

### Manual (what automation can't reach)

1. Full solution build 0 errors; desktop with `-p:Platform=x64`.
2. Browser click-through: `/portal/briefing` mark ready → desktop Pilot
   Centre card → Start flight → ACARS Live session strip; Bookings READY chip.
3. Live-flight regression (POSREP→PIREP) stays with the Phase 2b MSFS
   verification flight.

## Out of scope

- Removing desktop Bookings/Briefing pages (decided against).
- Persisted dispatch/fuel-plan records, briefing snapshots.
- Booking expiry job, W5 admin web, W6 AI dispatcher, Hetzner provisioning.
