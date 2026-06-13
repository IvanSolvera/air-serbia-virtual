# Continuation log — pick-up notes

## ✅ SESSION 2026-06-13 — PHASE W4 COMPLETE (tasks 5–13 shipped)

**Everything is committed** on `dev` (clean tree apart from
`.claude/settings.local.json` — leave it). W4 (desktop slim-down + web dispatch
handoff + Contracts convergence + first automated test suites) is **fully done**.
Tasks 1–4 landed in the prior session; this session executed **tasks 5–13** of
`doc/w4-implementation-plan.md` test-by-test.

**What shipped this session:**
- **T5** `AirSerbiaVirtua.Tests.Api` — integration host + `WireCompatTests`.
  ⚠ **Docker is not installed on this machine**, so the Testcontainers fixture
  uses the **local Postgres server against a dedicated `airserbiavirtua_test`
  database** (the plan's documented fallback). `ApiTestHost` hard-guards the DB
  name to end in `_test` and injects the conn string via in-memory config
  (overrides user-secrets) so the dev DB can never be touched.
- **T6** `Booking.DispatchReadyAtUtc` + migration `AddBookingDispatch`; endpoints
  `GET /api/v1/bookings/active`, `POST`/`DELETE /api/v1/bookings/{id}/dispatch`.
- **T7** Desktop + Acars.Core converged on Contracts: `IApiService` seam,
  `GetActiveBookingsAsync`, `ApiContracts.cs` shrunk to `PositionReports`/
  `PhaseMapping`; all the local status-label enum mirrors deleted in favour of
  the real Contracts enums.
- **T8** `DispatchService` (the one start-flight sequence) + Bookings delegation.
- **T9** Pilot Centre "Resume dispatch" card. **T10** Bookings READY chip.
- **T11** Web `IPortalApi` seam + dispatch client methods + `Tests.Web` (bUnit)
  + PortalBook READY badge + Prepare link.
- **T12** Web **Briefing page** (`/portal/briefing`): route facts, shared
  `FuelEstimator`, dep/arr METARs, Mark/Undo dispatch-ready, `?bookingId` deep
  link + Briefing nav tab.

**Verification (all green):** full `dotnet build AirSerbiaVirtual.sln -p:Platform=x64`
→ 0 errors. **Tests.Unit 38/38** (x64), **Tests.Api 14/14** (local Postgres),
**Tests.Web 7/7** (bUnit). PoC `--selftest` ALL PASS. Live curl smoke against the
dev API proved login → book → `POST dispatch` (timestamp set, ordered first) →
`DELETE` (cleared). The `AddBookingDispatch` migration applied to the dev DB on
API boot.

**Deviations from the plan (all deliberate, see commits):**
- Docker fallback for Tests.Api (above).
- **FluentAssertions pinned 8.10.0** in every test project (9.x = paid licence).
- **bUnit resolved to 2.7.2** (latest), not 1.x → API is `BunitContext` (not
  `TestContext`) and `Render<T>()` (not `RenderComponent<T>()`); assembly-level
  `[FixtureLifeCycle(InstancePerTestCase)]` so NUnit gives bUnit a fresh context
  per test. The WASM `@rendermode` rendered fine in bUnit 2.x — no workaround.
- Fixed a real bug in the plan's `Active_ReturnsOnlyOpenAndConfirmed` test: it
  reused one date for 3 bookings of the same pilot+route, violating the
  `(PilotId,RouteId,Date)` unique index → gave them distinct dates (ordering
  assertion unchanged).
- `PilotCentreDispatchTests` clears Moq invocations after the ctor's eager
  refresh so the not-authenticated test's `Times.Never` holds.

**REMAINING — manual checks only (no code left):**
1. **Browser pass of the dispatch handoff** (curl can't run WASM): start
   API+Web+Desktop, sign in ASL001, on `/portal/briefing` Mark dispatch ready →
   open desktop **Pilot Centre** → the resume-dispatch card should appear →
   **Start flight** → lands in ACARS Live. Check READY chips on both the web
   PortalBook row and the desktop Bookings row.
2. Older verification debt still open: portal click-through (login/book/logbook/
   change-password), join-form submit from a browser, Auto Login round-trip, and
   the long-standing **Phase 2b MSFS verification flight** (JU360 LYBE→LOWW +
   mid-cruise Wi-Fi pull).

**Leftover test artifact:** booking **id 7** (JU360, today, Open) for ASL001 in
the **dev** DB, created by the live smoke — cancel it or use it for the browser pass.

**Run order (dev):** Postgres → API (`dotnet run --project AirSerbiaVirtua.Api
--launch-profile http`, :5036) → Web (`dotnet run --project AirSerbiaVirtua.Web`,
:5166) → Desktop (`-p:Platform=x64`, bin\x64\Debug). Tests.Api needs Postgres up;
Tests.Unit needs `-p:Platform=x64`.

**Next after the browser pass:** W5 (admin web), W6 (ASV Dispatch AI). Hetzner
still not provisioned.

---

## ⏸ SESSION CLOSED 2026-06-06 (evening) — W4 MID-EXECUTION, resume here

**Everything is committed** (clean tree apart from `.claude/settings.local.json`,
which is harness permission noise — leave it). We are **mid-way through phase W4**,
executed task-by-task via subagents from the approved plan.

**The two documents that drive everything:**
- `doc/w4-desktop-slimdown-design.md` — approved W4 spec (decisions: desktop
  KEEPS Bookings/Briefing + gains dispatch pull; explicit `DispatchReadyAtUtc`
  flag set from web Briefing; full Desktop+API DTO convergence into Contracts;
  tests for every segment: NUnit/Moq/FluentAssertions + Testcontainers Postgres
  + bUnit).
- `doc/w4-implementation-plan.md` — 13 bite-sized TDD tasks with complete code.
  **Resume = execute Task 5 onward** (each task: implementer subagent → spec
  compliance review → code quality review, per superpowers
  subagent-driven-development).

**W4 progress: Tasks 1–4 of 13 DONE, all reviewed + approved:**
- T1 `59bd61d` — `AirSerbiaVirtua.Tests.Unit` scaffolded (NUnit 4.3.2, Moq,
  FluentAssertions 8.10.0 — pinned, do NOT bump FA to 9.x = paid license;
  net10.0-windows, x64; sln platform mappings fixed).
- T2 `316caaa` — `Contracts/FuelEstimator.cs` + 8 tests.
- T3 `22e04c3` + fix `6b67b8d` — Contracts convergence: `Contracts/Enums.cs`
  (5 enums), `VaContracts.cs` rewritten (enum-typed Status fields,
  `BookingInfo.DispatchReadyAtUtc`, all missing records added), 9 golden-JSON
  `WireShapeTests`, Web.Client int→enum fallout fixed (PortalBook/Dashboard/
  Logbook; UnderReview keeps label "REVIEW").
- T4 `3e3e356` — API serves Contracts directly: `Api/Dtos/ApiDtos.cs` +
  `Api/Models/Enums.cs` DELETED, renames across 8 controllers + MetarService,
  `public partial class Program;` appended (WebApplicationFactory-ready).
  Live smoke verified wire unchanged.
- **Tests.Unit: 18/18 green** (`dotnet test AirSerbiaVirtua.Tests.Unit\...csproj -p:Platform=x64`).

**NEXT: Task 5** — scaffold `AirSerbiaVirtua.Tests.Api` (Testcontainers
PostgreSql fixture `ApiTestHost`, `TestData` seeding, `WireCompatTests`).
⚠ Docker Desktop must be RUNNING before Task 5's test run (first run pulls
postgres:16-alpine). Then T6 (dispatch migration + endpoints), T7 (desktop
convergence + IApiService), T8 (DispatchService), T9 (Pilot Centre card),
T10 (READY chip), T11 (IPortalApi + Tests.Web), T12 (web Briefing page),
T13 (full verification + docs).

**Discoveries that correct plan assumptions (already accounted for):**
- `AppDbContext` stores enums as **strings** (`HasConversion<string>()`), not
  ints — enum move was still safe (identical names); don't add migrations for it.
- `AircraftStatus` crosses the wire as a **string** (AircraftInfo.Status) — by
  design, leave it.
- Stale comment at `Acars.Core/ApiContracts.cs:139` ("must match
  Api.Models.FlightPhase") — file gets rewritten in T7 anyway.
- A leftover smoke-test API process was killed (T4); if builds fail with file
  locks, check `Get-Process AirSerbiaVirtua.Api`.

**Pre-W4 verification debt still open** (browser pass on portal, join-form
submit, Auto Login round-trip, Phase 2b MSFS flight) — see the list below.

---

## ⏸ SESSION CLOSED 2026-06-06 — (superseded by the entry above)

**Everything is committed** (HEAD `e2069cd` on `dev`, clean tree). The giant
2026-06-06 session delivered, in order: prod items #7-10, the full "Flight
Crew Suite" desktop redesign (v1+v2 mockups), quick wins (Auto Login via DPAPI,
Aborted flights, Brand.* cleanup), Phase 5 (METARs page, Roster Admin,
Outstation flights — **desktop app is feature-complete**), and the website
W1+W2+W3 (Blazor public site + WASM pilot portal) + VATSIM ID on applications.

**First things next session (verification debt — built but not human-tested):**
1. **Browser pass on the portal** (http://localhost:5166/portal): login ASL001,
   book a leg, dashboard, logbook, change-password round-trip. curl couldn't
   execute WASM.
2. **Join-form submit from a browser** (antiforgery blocked CLI testing), then
   approve the applicant in desktop Roster Admin (ASL888 w/ VATSIM 1234567 and
   ASL999 are already waiting as test entries — approve or delete).
3. **Auto Login round-trip**: desktop login with toggle ON → restart app →
   should land in Pilot Centre.
4. **Phase 2b verification flight** (MSFS, JU360 LYBE→LOWW + mid-cruise Wi-Fi
   pull) — STILL the oldest open item; checklist further down this file.

**Then: phase W4** — desktop slim-down: pull "active dispatch" from web
bookings (`GET /api/bookings/active` concept), Briefing on the web with
dispatch-ready state, converge desktop DTOs into AirSerbiaVirtua.Contracts.
After that W5 (admin web), W6 (ASV Dispatch AI). **Hetzner not provisioned.**

**Dev run order:** Postgres → API (:5036) → Web (:5166) → Desktop
(bin\x64\Debug — build with `-p:Platform=x64`!).

## Update 2026-06-06 (W3 + VATSIM ID)

**VATSIM ID on applications:** `Pilot.VatsimId` (+ migration `AddPilotVatsimId`),
optional on register (validated 6-8 digits, 400 otherwise), shown in BOTH admin
surfaces (web AdminPilotDto + desktop Roster Admin under the email). Join form
has the optional field ("speeds up approval"). E2E: ASL888 registered w/
1234567, visible to admin, bad id rejected. **ASL888 + ASL999 are leftover test
pilots** (Pending/Inactive) — approve or delete at will.

**W3 pilot portal (WASM, all routes serve 200):**
- Infra: `PortalSession` (JWT pair + profile in localStorage, restore-once),
  `PortalApi` (bearer + refresh-on-401 retry, same pattern as desktop),
  WASM config via Web.Client/wwwroot/appsettings.json (Api:BaseUrl), CORS ok.
  All portal pages `@rendermode PortalRender.NoPrerender` (localStorage is
  browser-only — prerender would paint signed-out state).
- Pages: /portal/login (enter-to-submit), /portal dashboard (career card,
  active bookings, last-flight tiles), /portal/book (my bookings w/ cancel +
  route table w/ Book today), /portal/logbook (totals + status badges),
  /portal/profile (roster entry + change password). Shared `PortalNav`
  (identity header + tab strip + sign out).
- **Browser click-through still needed** (curl can't run WASM): login as
  ASL001, book a leg, check logbook, change-password round-trip.
- W3 deliberately deferred: Briefing page + "dispatch ready" desktop handoff
  (`GET /api/bookings/active` pull) — that's the W4 entry point.

**Next: W4** (desktop slim-down + pull web dispatch + contracts convergence),
W5 admin web, W6 ASV Dispatch AI. Hetzner still pending.

## Update 2026-06-06 (W1+W2 complete) — public website live locally

**API:** new anonymous `GET /api/v1/stats` (pilots/flights/hours/routes);
`GET /routes` and `GET /aircraft` lists now `[AllowAnonymous]` (public
schedules/fleet pages); rest of those controllers stays authorized.

**Web (SSR, all pages verified 200 against live API):**
- Home — hero + stats strip wired to /stats (graceful "—" when API down).
- Schedules — full network table (flight/leg/AC chip/distance/ETE/operating
  days), server-side filter via `?q=` (verified: q=LOWW shows JU360 only).
- Fleet — aircraft cards w/ photos by type (A32x→a319.png, A330, ATR72;
  others get a type monogram), status badges (Active/InFlight/Maintenance).
- Join us — SSR EditForm (FormName="join") → POST /auth/register → Pending +
  success state. NOT yet submitted live (antiforgery) — test from browser,
  then approve via desktop Roster Admin tab.
- /portal — W3 coming-soon. **Login/auth deliberately moved to W3** (belongs
  with the WASM portal; Join covers W1's register half).

**Docker:** real Dockerfiles for Api + Web (multi-stage, context = repo root);
docker-compose now buildable (untested — Docker not exercised this session).

**Run order (dev):** API :5036 → Web :5166 (`Api:BaseUrl` in Web appsettings).

**Next: W3 pilot portal** — WASM login (JWT in browser storage + auth state
provider), dashboard, book-a-flight, briefing w/ "dispatch ready", logbook,
profile. Then W4 desktop slim-down + contracts convergence, W5 admin web,
W6 AI dispatcher. Hetzner still not provisioned.

## Update 2026-06-06 (W1) — Website foundation scaffolded (Blazor per spec)

User chose **Blazor per doc/website-design.md** (over adopting the static SPA
directly); the delivered static design is the visual reference, copied into
the repo at `doc/design/website/` (index.html + app.js + assets + README that
maps mock arrays → API endpoints).

Done (phase W1, partial):
- New projects in sln: `AirSerbiaVirtua.Contracts` (shared DTOs, zero deps),
  `AirSerbiaVirtua.Web` (Blazor Web App host, SSR public pages),
  `AirSerbiaVirtua.Web.Client` (WASM, interactive portal later).
  Web → Web.Client + Contracts; Web.Client → Contracts.
- Contracts: auth/profile/routes/fleet/bookings/pireps/metar/admin records
  (`AirSerbiaVirtua.Contracts` ns). **Desktop still uses its own copies in
  Acars.Core/ApiContracts.cs — converge in W4** (noted in file header).
- Theme: Tailwind CDN + brand config (ink/royal/crimson/mist, Sora/Manrope/
  JetBrains Mono) in `App.razor`; `.panel/.tab-btn/.grid-overlay` etc. ported
  into `wwwroot/app.css`. Aircraft assets in `wwwroot/assets/`.
- `MainLayout` (sticky header: SVG chevron logo, tabs Home/Schedules/Fleet/
  Join us, crimson "Pilot portal" button; footer w/ disclaimer) and `Home`
  (hero + CTA + stats strip with "—" placeholders for `GET /api/v1/stats`).
- `ComingSoon.razor` serves /schedules /fleet /join /portal until W2/W3.
- CORS: web dev origins (http://localhost:5166, https://localhost:7064) added
  to API `appsettings.Development.json`.
- Root `docker-compose.yml` (postgres/api/web; Dockerfiles still TODO).
- Smoke-tested: `dotnet run` Web on :5166 → landing 200 with hero/brand/
  Tailwind, /schedules 200 coming-soon. Web runs standalone (no API needed yet).

**W1 remaining:** API+Web Dockerfiles; login/register pages (WASM) against the
API; `GET /api/v1/stats` endpoint + wire the landing stats strip.
**Then W2:** schedules table, fleet cards, route map (Leaflet), join form.
**Hosting:** Hetzner — NOT yet provisioned; everything local for now.

## Update 2026-06-06 (late night) — Outstation Flights: ACARS app FEATURE-COMPLETE

Every sidebar tab is now a real page. Outstation (Option A — ad-hoc Route rows):
- `Route.IsOutstation` + migration `AddOutstationRoutes`; schedule list
  (`GET /routes`) excludes outstation legs; `GET /routes/{id}` still serves
  them (Briefing works).
- `POST /api/v1/flights/start-outstation` { depIcao, arrIcao, aircraftId,
  flightNumber }: validates ICAOs, auto-registers unknown airports as stubs
  (lat/lon 0 → distance 0), creates one-off Route + Pending PIREP, locks
  aircraft. No booking involved; submit/abort already handle bookingless
  sessions.
- Desktop `OutstationView`: charter form (flight number default JU9xx, dep/arr
  ICAO, Active-fleet ComboBox) → starts session → jumps to ACARS Live.
- E2E verified: LYBE→LDZA (LDZA auto-stubbed), hidden from schedule, POSREP
  accepted, abort w/o booking, aircraft released, logbook row correct.

**WEBSITE (next big block):** design delivered at
`e:\Programiranje\Projects\AirSerbiaVirtual\WebSite\air-serbia-virtual\` —
static SPA (HTML + Tailwind CDN + vanilla JS), 8 tabs (Home, Live Flights,
Schedules, Profile, Fleet, Dispatch, Downloads, Heritage), mock data in
`app.js` with a README mapping each mock array to suggested API endpoints.
Architecture spec in `doc/website-design.md`. **Hosting target: Hetzner — not
yet provisioned** (build + wire locally first).

## Update 2026-06-06 (night) — Phase 5: METARs page + Roster Admin

**METARs page (placeholder retired):** `MetarsViewModel/View` — multi-ICAO
lookup (max 8, Enter or button), network-airport chips auto-loaded from the
route schedule, result cards with observation age; graceful "no METAR" state.

**Roster Admin (the Pending-login hole is CLOSED):**
- `Pilot.IsAdmin` + migration `AddPilotAdmin` (seeds ASL001 → admin + Active).
- JWT carries `role: Admin` claim when `IsAdmin`.
- `GET/POST /api/v1/admin/pilots[/{id}/activate|/{id}/deactivate]` guarded by
  `[Authorize(Roles="Admin")]`. Deactivate blocks self + admins.
- **Login now rejects Pending** ("Account awaiting approval…") in addition to
  Banned/Inactive. Registration flow: register → Pending → admin approves.
- `PilotProfileDto`/`PilotProfile` gained `IsAdmin`; new `AdminPilot(Dto)`.
- Desktop: "Roster Admin" sidebar item (ShieldAccountOutline) visible only to
  admins, below a separator; `AdminView` roster table (status badges, shield
  marker on admins, Approve for Pending/Inactive, Deactivate w/ confirm for
  Active non-admins), summary line "N pilots · M awaiting approval".
- **E2E verified (all 9 checks):** migration seed, roster list, register→Pending,
  Pending login 403, activate, activated login OK, non-admin admin-access 403,
  deactivate + inactive login 403, self-deactivate 409.
- Test artifact: pilot **ASL999** (Test Pilot) left in DB as **Inactive** —
  handy for demoing the Approve button; delete whenever.

**Phase 5 remaining:** Outstation flights (needs design decision: nullable
Pirep.RouteId vs ad-hoc Route rows; then start-outstation endpoint + UI).
Then: Website + AI Dispatcher (doc/website-design.md).

## Update 2026-06-06 (evening) — quick wins

- **Auto Login (real)** — refresh token persisted via DPAPI (CurrentUser) to
  `%LOCALAPPDATA%/AirSerbiaVirtua/session.token` (`SessionTokenStore`).
  `ApiService`: `TokensUpdated` event (fires on every rotation), `GetMeAsync`
  (new API `GET /api/v1/auth/me`), `TryRestoreSessionAsync`. `SessionService`
  persists on rotation while AutoLogin=on (deletes when off / on sign-out);
  `App.OnStartup` calls `TryAutoLoginAsync()` before showing the shell.
  LoginViewModel saves toggles BEFORE the login call (ordering matters — the
  token event fires mid-login). Server side verified (refresh→me round-trip);
  **client E2E (restart app with toggle on) still needs a manual check.**
- **Aborted flights (GVA #14, user-abort half)** — `PirepStatus.Aborted=4`,
  `POST /api/v1/flights/{id}/abort` re-opens booking + releases aircraft;
  ACARS page got a ghost Abort button (confirm dialog). Logbook/Debrief show
  Aborted (mist badge). Verified E2E incl. dup-abort 409. Crash-detection
  auto-abort still open. **Cleaned up the 2 stuck Pending flights** — fleet
  all-Active again.
- **Legacy cleanup** — old `Brand.*` palette/styles deleted from App.xaml
  (MahApps dictionaries kept — they still provide base control styles).
- Next big block (user's pick): see Mid-term roadmap — Website + AI Dispatcher
  (doc/website-design.md) and Phase 5; GVA #13 (rich telemetry) deliberately
  deferred until after the Phase 2b verification flight.

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
