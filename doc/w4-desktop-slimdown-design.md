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

## 7. Verification plan

1. **Wire-compat regression** (the convergence risk): capture JSON from
   `POST /auth/login`, `GET /routes`, `GET /bookings/mine`,
   `GET /pireps/mine` before and after the refactor; diff must be empty.
2. **Dispatch E2E via curl**: book → `POST .../dispatch` → booking appears
   first in `/active` with timestamp → `DELETE` clears → 409 when marking a
   Flown booking.
3. **Build**: full solution 0 errors; desktop built with `-p:Platform=x64`.
4. **Click-through**: web `/portal/briefing` (select booking → METARs/fuel
   render → Mark ready → badge) → desktop Pilot Centre shows the card →
   Start flight → ACARS Live with session strip; Bookings shows READY chip.
5. **Existing-flow regression**: desktop login → bookings list → start →
   POSREP accepted (proves renamed contracts still parse end-to-end).

## Out of scope

- Removing desktop Bookings/Briefing pages (decided against).
- Persisted dispatch/fuel-plan records, briefing snapshots.
- Booking expiry job, W5 admin web, W6 AI dispatcher, Hetzner provisioning.
