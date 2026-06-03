# Continuation log — pick-up notes

## Update 2026-06-03 (mid-session)

Investigated the user's Delta Virtual ACARS client — it's the **Global Virtual
Airlines Group (GVA) ACARS** framework (proprietary, .NET Framework 4.8 /
WinForms, talks a raw TCP+XML socket protocol to a GVA server we don't have).
**Decision: keep our REST/JSON/.NET 10 architecture, mine GVA for ideas.** Full
writeup in `doc/acars-comparison.md`; borrowed-ideas roadmap added to
`doc/production-readiness.md` section D (items 11–14).

First GVA idea landed in code: **`ISimBridge` abstraction** (item 11, done).
- New `AirSerbiaVirtua.Acars.Core/ISimBridge.cs` interface.
- `FsuipcService` now implements `ISimBridge` (added `Name => "FSUIPC"`).
- `SimulatorService` depends on `ISimBridge` via constructor injection
  (defaults to `FsuipcService`; can take a fake bridge in tests).
- Full solution builds green (0 errors). No behavior change — pure refactor.

**Prod items #5 + #6 done (2026-06-03):**
- **#5 POSREP idempotency** — `PositionLog.ClientReportId` (Guid) + unique index
  `(PirepId, ClientReportId)`; migration `AddPosrepIdempotency` applied to dev DB;
  `FlightsController.Position` returns 200 on duplicate instead of 500.
- **#6 Offline buffer** — new `PosrepQueue` (Acars.Core): durable directory-of-files
  queue under `%LOCALAPPDATA%/AirSerbiaVirtua/posrep/`, ordered delivery,
  exponential backoff+jitter, 5s send timeout, drain-before-PIREP, survives
  crashes/restarts. `AcarsViewModel` now enqueues instead of sending directly;
  UI shows queue depth (`QueueStatusText`).
- Verified by `--selftest` (PoC): flight-sim test + **11/11 PosrepQueue asserts PASS**.
- **Still needs a live Wi-Fi-drop test during an actual flight** (folds into Phase 2b).

**Phase 2b verification (below) is STILL the next real task** — unchanged, needs
a live MSFS flight which only the user can fly. While flying it, also exercise
#6: pull Wi-Fi for ~1 min mid-cruise and confirm POSREPs catch up (queue depth
ticks up then drains, no rows lost, no duplicates).

---

Last session ended **2026-06-02**, late evening. Phase 2b implementation
finished but the live POSREP → PIREP round-trip was not yet verified against a
real MSFS flight. Everything else worked end-to-end.

## Where we left off

**Working / tested:**
- API on `http://localhost:5036` connects to Postgres `airserbiavirtua` on
  localhost:5432, all 4 migrations applied (InitialCreate, AddPilotPasswordHash,
  AddRefreshTokens, SeedRouteNetwork).
- Desktop client signs in as `ASL001 / test1234`.
- Bookings page lists 6 LYBE routes, "Book today" creates an Open booking,
  Cancel removes the row from the active list, Cancelled/Flown/Expired are
  filtered out of MY BOOKINGS (BookingsViewModel.IsActive).
- ACARS Live page: FSUIPC7 + MSFS 2024 telemetry works (verified earlier),
  10-phase tracker, milestones, metrics — all live.
- Phase 2a is solid.

**Implemented but NOT yet tested:**
- **Phase 2b** — Start flight from Bookings → navigates to ACARS Live, opens a
  Pending PIREP server-side (`FlightsController.Start`), `FlightSessionState`
  shared singleton holds session id, `AcarsViewModel` runs a 30s
  `PeriodicTimer` calling `ApiService.PushPositionAsync` until session clears,
  Submit PIREP button shows when phase reaches Arrived.

## First thing next session

**Verify Phase 2b end-to-end against MSFS 2024:**

1. Start Postgres → API → Desktop.
2. Login `ASL001 / test1234` (or whatever the user reset to).
3. Bookings → Book a short route (JU360 LYBE→LOWW, 50min planned).
4. Click **Start flight** → check that:
   - Server returns `FlightStartResponse { FlightSessionId, AircraftRegistration }`
   - Auto-navigation to ACARS Live happens
   - Session strip shows `Active session: JU360 on YU-API — started HH:mm UTC`
   - DB check:
     ```sql
     SELECT * FROM "Pireps" WHERE "PilotId" = 1 ORDER BY "Id" DESC LIMIT 1;
     -- expect Status='Pending', Source='ACARS'
     SELECT "Status" FROM "Aircraft" WHERE "Registration" = 'YU-API';
     -- expect 'InFlight'
     ```
5. Connect to sim, fly the short route in MSFS, watch:
   - `LAST POSREP` timestamp updates ~every 30s
   - DB check: `SELECT COUNT(*) FROM "PositionLogs" WHERE "PirepId" = X;`
     should grow.
6. After landing + on-block + parking brake set → state machine reaches
   `Arrived` → **Submit PIREP** button enables.
7. Click Submit → check that:
   - PIREP row updates: `Status='Accepted'`, `Score`, `BlockMin`, `LandingRateFpm` populated
   - Pilot's `TotalHours` increased
   - Aircraft back to `Active`
   - Booking → `Flown`
   - FlightSessionState cleared, session strip back to "No active flight session..."

If something fails along the way, that's where to start debugging.

## Known issues / loose ends to address next

- **Air Serbia roster is fake.** Pilot ASL001 is in `Pending` status but login
  accepts Pending (only Banned/Inactive are rejected). Admin/promote flow is
  not implemented. Fine for dev; tighten later.
- **Routes table & MY BOOKINGS height splitting** — when status banner shows
  up, the 6th route in AVAILABLE ROUTES gets clipped. Three options noted in
  the chat (bias 1.6:1, overlay banner, single scroll). Cosmetic, defer.
- ~~**POSREP idempotency (doc item #5)**~~ — DONE 2026-06-03. `ClientReportId`
  Guid + unique `(PirepId, ClientReportId)` index; server returns 200 on dup.
- ~~**Client offline buffer (doc item #6)**~~ — DONE 2026-06-03. `PosrepQueue`
  persists every sample to disk and delivers with retry/backoff. Still wants a
  live Wi-Fi-drop test during a real flight.
- **`AcarsView.xaml`** — when SubmitPirepCommand fires, the success message
  ("PIREP submitted. Block Xm, landing Y fpm.") goes into `SessionHeaderText`
  but there's no separate success banner. Looks OK, but consider a brief
  toast/banner.

## Production-readiness checklist progress

See `doc/production-readiness.md` — 4 of 10 done:
- [x] 1. JWT signing key out of repo
- [x] 2. Per-pilot hashed passwords
- [x] 3. Rate limiting on auth endpoints
- [x] 4. Refresh token rotation
- [ ] 5. POSREP idempotency
- [ ] 6. Client offline buffer + retry/backoff
- [ ] 7. API versioning
- [ ] 8. Structured logs (Serilog)
- [ ] 9. CORS policy
- [ ] 10. Deployment doc (TLS, reverse proxy, prod migrations)

## Mid-term roadmap (Phase 3+)

Once Phase 2b is verified:

**Phase 3 — Pilot Centre + Logbook**
- `PilotCentreView`: bind `_session.Pilot` (Callsign, Name, Email, Rank,
  TotalHours, HubId, DateJoined), add Change password form, show a
  "ranks/progression" widget.
- `LogbookView`: list all submitted PIREPs from `GET /api/pireps/mine`
  (endpoint doesn't exist yet — add it). Filterable by status. Show
  Cancelled / Flown / Expired bookings too (Bookings page filters them
  out by design).

**Phase 4 — Briefing & Debriefing**
- `BriefingView`: pre-flight summary — route, fuel estimate, weather
  (METAR fetch via `SyncController` or new endpoint), NOTAMs.
- `DebriefingView`: post-flight analysis — score breakdown, landing
  rate graph, fuel actual vs plan, time vs schedule.

**Phase 5 — Admin / METARs / Outstation**
- Admin tab to promote Pending pilots to Active, manage roster.
- METARs lookup (third-party API integration).
- Outstation flights (charter / one-off routes outside scheduled network).

## Important state to remember

- **Postgres password:** `112358.Zivanovic` (in API user-secrets, not in repo).
- **JWT key:** stored in user-secrets, regenerated each install.
- **API port (dev):** 5036 HTTP (`launchSettings.json` http profile).
- **Desktop API base URL:** `appsettings.json` → `Api:BaseUrl =
  http://localhost:5036/`.
- **Brand palette + style:** locked in `App.xaml` (chocolate dark + Air Serbia
  red + Cambria serif). Don't redesign without asking.
- **Project structure:** `Acars.Core` is the shared library, `Acars.Desktop`
  is WPF, `Acars.PoC` is `--selftest` harness only, `Api` is the ASP.NET host.
- **Single-singleton ViewModels:** `AcarsViewModel` and `BookingsViewModel`
  are singletons so background subscriptions and refresh state persist across
  navigation. Don't switch to transient without unsubscribing.

## How to resume cleanly

```bash
# 1. Postgres must be running on localhost:5432
# 2. Start API
dotnet run --project AirSerbiaVirtua.Api/AirSerbiaVirtua.Api.csproj --launch-profile http
# 3. Start Desktop
dotnet run --project AirSerbiaVirtua.Acars.Desktop/AirSerbiaVirtua.Acars.Desktop.csproj
# 4. Login ASL001 / <password>
```

If the password is forgotten, reset via:
```sql
-- Generate a fresh BCrypt hash for 'test1234' first (any C# REPL), then:
UPDATE "Pilots" SET "PasswordHash" = '<hash>', "PasswordUpdatedAtUtc" = NOW()
WHERE "Callsign" = 'ASL001';
```

Or delete the pilot and re-register via Swagger.
