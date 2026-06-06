# Continuation log — pick-up notes

## Update 2026-06-06 (later) — per-page Suite redesign v2

User supplied a second mockup (`Air Serbia Virtual - ACARS App (standalone) 2.html`,
same bundler format; page markup lives in the gzip-compressed JS resource in the
manifest). All inner pages reworked to match it:
- **Page framework**: Sora 30px titles (no kicker), 13.5 mist subs, actions
  top-right; content area gradient (#061328→#040D1C + royal radial glow) behind
  every page (`MainWindow`).
- **New shared styles** (App.xaml): `Suite.PageTitle/PageSub`, `Suite.Tile`,
  `Suite.TblWrap/TblRow` (hover royal tint), `Suite.Chip(+Text)`,
  `Suite.Badge{Crimson,Amber,Green,Mist}(+Text)`, `Suite.Banner`,
  buttons reworked: `Suite.BtnCrimson` (gradient, main actions: Refresh, Book,
  Start flight, Connect), `Suite.BtnPrimary` (royal: Update password, Submit
  PIREP), `Suite.BtnGhost` (dark .btn base: Cancel).
- **Pages**: PilotCentre (74px avatar, crimson callsign + amber/green status
  badges, hairline-topped stats row, ACCOUNT SECURITY card), Bookings (chips,
  badges, hover rows, MY BOOKINGS empty-state text), Briefing (banner + ghost
  DEP/ROUTE/ARR cards when idle), ACARS Live (phase dots: royal=done,
  crimson glow=current — `PhaseItem.IsDone` added; tele tiles, state strip,
  kv milestones/metrics), Debriefing (gradient hero w/ crimson radial glow,
  Sora 54 score, landing strip, status-colored tiles), Logbook (mono summary
  sub, dark select + crimson refresh, badge statuses, empty state), Placeholder
  (84px radial ring + PHASE 5 badge).
- Sidebar pilot card: shows initials when signed in, click → Pilot Centre.
- Status bar: FSUIPC red LED + "Disconnected" when sim not connected (per mockup).
- `ZeroToVisConverter` added for table empty states.
- Build green, app launches. **User to click through pages for visual sign-off.**

## Update 2026-06-06

**Prod items #7–#10 done — production-readiness checklist 10/10 (core), only GVA
extras 12–14 remain.**
- **#7 API versioning** — `Asp.Versioning.Mvc(.ApiExplorer)` 10.0.0; URL segment
  `/api/v1/...`; all 8 controllers `[ApiVersion("1.0")]`; `ApiService` now calls
  `api/v1/...`. Live-verified: old `/api/auth/login` → 404, new → 401/200,
  Swagger paths show `/api/v1/...`. `/health` deliberately unversioned.
- **#8 Serilog** — bootstrap logger + `UseSerilog`; console readable in dev /
  CompactJson in prod; file CompactJson `logs/asv-YYYYMMDD.log` rolling daily,
  30 kept; `UseSerilogRequestLogging` + post-auth middleware pushes `PilotId`
  onto every authenticated request's log context; Login/FlightStart/POSREP log
  Information with `RouteId`/`FlightSessionId`. Verified `"PilotId":1` in file.
- **#9 CORS** — config-driven (`Cors:AllowedOrigins`, default empty = deny all
  cross-origin); `DisallowCredentials` (web UI will use JWT header, not cookies);
  decision commented in `Program.cs`.
- **#10 Deployment doc** — `doc/deployment.md`: Caddy/nginx + Let's Encrypt,
  systemd + docker-compose, `dotnet ef migrations bundle` step, env vars,
  pg_dump cron + restore drill, deploy checklist.
- Build green (0 errors). Smoke-tested live against dev Postgres: health, 404 on
  unversioned, login ASL001, `GET /api/v1/routes` → 6 routes.
- **Breaking note:** any older client build talks `/api/...` and will 404 — use
  current `ApiService` (now on `api/v1/`).

**"Flight Crew Suite" redesign implemented (2026-06-06).** Source: standalone
HTML mockup at `e:\Programiranje\Projects\AirSerbiaVirtual\Acars\` (bundler
format — real HTML is JSON-encoded in the `__bundler/template` script block).
User explicitly requested it, superseding the old "chocolate dark + Cambria"
brand lock.
- **Design system in `App.xaml`**: `Suite.*` tokens — ink-navy palette
  (#04101F…#16365F), royal (#2563D6/#3F86F4), crimson (#D8203F), mist text,
  hairlines, surface gradients; styles `Suite.NavItem`, `Suite.Input`,
  `Suite.Password`, `Suite.Switch`, `Suite.SignIn`, `Suite.IconBtn`,
  `Suite.WinBtn(Close)`. Old `Brand.*` keys kept untouched — inner pages
  (Bookings/ACARS/etc.) still render their old light style inside the new
  dark shell; restyling them is the next UI task.
- **Fonts embedded** (`Fonts/*.ttf`, OFL): Sora 600/700/800, Manrope 400–800,
  JetBrains Mono 400/500/600. Use via `Suite.Display/Sans/Mono`.
- **Hero image** extracted from the mockup manifest → `Resources/login-hero.png`.
- **MainWindow**: borderless `Window` + `WindowChrome` (CaptionHeight 46),
  1320×840. Titlebar: Serbian-flag stripe, chevron logo (vector Paths), brand,
  "ACARS TERMINAL", real version, custom min/max/close. Sidebar 236px:
  OPERATIONS + LOCKED chip, 8 nav items (METARs, Pilot Centre, Bookings,
  Briefing | ACARS, Debriefing, Outstation Flights, Logbook), pilot card with
  sign-out icon. Statusbar 34px: API/FSUIPC/ACARS LEDs, HUB, live Zulu clock,
  real PING (new `ApiService.PingAsync()` → /health every 15 s).
- **Nav is now LOCKED until login** (CanExecute = IsAuthenticated) — Phase 2a
  open-nav behavior intentionally removed.
- **LoginView**: hero + 372px auth panel per mockup — ASV ID/password fields
  with icons, password reveal (eye), Remember me (persists callsign via new
  `UiSettings` → `%LOCALAPPDATA%/AirSerbiaVirtua/ui-settings.json`), Auto Login
  flag (stored; takes effect once refresh-token persistence lands), gradient
  sign-in button with busy spinner, crimson error banner, secure chip showing
  real API host. Hero stats strip (17 flights / 24/28 / 92% / JU380) is static
  decoration for now.
- **New nav targets** `Metars` + `Outstation` → PlaceholderView (Phase 5).
- Verified: solution builds 0 errors, app launched, screenshot confirmed —
  matches mockup (UTC ticking, PING real, LEDs correct).
- **TODO next (UI)**: restyle inner pages (Bookings, ACARS Live, Pilot Centre,
  Briefing, Debriefing, Logbook, Placeholder) to the Suite design system;
  consider wiring hero METAR chips + stats to real data post-Phase 5.

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

**Phase 3 + 4 built (2026-06-03):**
- **Phase 3 — Pilot Centre**: real profile page (avatar/initials, rank, hours, hub,
  status, joined) from `_session.Pilot` + working Change-password form
  (`ApiService.ChangePasswordAsync` → existing `POST /api/auth/change-password`).
- **Phase 3 — Logbook**: lists PIREPs from new `GET /api/pireps/mine`
  (`PirepsController.Mine`, status filter), totals (count, hours, avg score).
- **Phase 4 — Briefing**: route facts + rough fuel estimate + live dep/arr METAR
  for the active session. New `GET /api/routes/{id}` and `GET /api/metar?icaos=`.
- **Phase 4 — Debriefing**: last PIREP analysis — score hero, landing verdict,
  block/air/fuel tiles.
- **METAR**: `MetarService` proxies aviationweather.gov (registered HttpClient),
  degrades gracefully when offline.
- Nav order now: Pilot Centre → Bookings → Briefing → ACARS Live → Debriefing → Logbook.
  Placeholder PilotCentreView/LogbookView classes deleted; replaced with real XAML.
- New client contracts: `PirepListItem`, `MetarInfo`, `ChangePasswordRequest`;
  `ApiService`: `GetMyPirepsAsync`, `GetRouteAsync`, `GetMetarAsync`, `ChangePasswordAsync`.
- Full solution builds 0 errors. **Not yet runtime-smoke-tested by me** — launch and
  click through the new tabs.

**Phase 2b verification is STILL the next real flight task** — unchanged, needs
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
- **Brand palette + style:** "Flight Crew Suite" design system (`Suite.*` keys
  in `App.xaml`) — ink-navy + royal blue + crimson, Sora/Manrope/JetBrains Mono
  (embedded TTFs). Replaced the old chocolate/Cambria look on 2026-06-06 at the
  user's request. Old `Brand.*` keys remain only for not-yet-restyled inner
  pages. Don't redesign without asking.
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
