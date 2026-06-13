# Air Serbia Virtual — Website + AI Dispatcher design

Date: 2026-06-04
Status: Approved design, pre-implementation

## Goal

A full VA management website where pilots gather info, book flights, and prepare
flights; the desktop ACARS client pulls the prepared dispatch at flight start.
Plus an in-browser AI chatbot ("ASV Dispatch") that helps visitors and pilots,
grounded exclusively on the VA's own API data. Reference look-and-feel:
BAVirtual VMS (dark theme, icon sidebar, filterable data tables).

## Decisions

| Decision | Choice |
|---|---|
| v1 scope | Full VA management system (public + pilot portal + admin) |
| Frontend | Blazor Web App (.NET 10): static SSR for public pages, Interactive WASM behind login |
| AI dispatcher | In-browser WebLLM (Llama 3.2 1B), data-grounded, template fallback without WebGPU |
| Desktop client | Slims down to core ACARS (track flight + submit PIREP); booking/briefing move to web |
| Hosting | Undecided → Docker (compose: web + api + postgres) for portability |
| Structure | Approach A: new projects in the existing solution; website is a client of the existing API |

## 1. Architecture & solution layout

New projects added to `AirSerbiaVirtual.sln`:

```
AirSerbiaVirtua.Contracts        NEW — shared DTOs only (zero dependencies)
AirSerbiaVirtua.Web              NEW — Blazor Web App server host (SSR public pages)
AirSerbiaVirtua.Web.Client       NEW — WASM project (interactive pages + chatbot)
```

Existing projects:

```
AirSerbiaVirtua.Api              gains new endpoints (schedules, fleet, roster, admin, airports, stats)
AirSerbiaVirtua.Acars.Core       keeps sim/queue logic; DTOs move OUT to Contracts
AirSerbiaVirtua.Acars.Desktop    slims down in phase W4
AirSerbiaVirtua.Acars.PoC        untouched
```

Dependency flow:

```
Contracts  (PirepListItem, MetarInfo, ScheduleDto, BookingDto, ...)
   ^
   ├── Api          (serves them)
   ├── Acars.Core   (desktop client uses them)
   └── Web.Client   (website uses them)
```

Render strategy: static SSR for landing/fleet/schedules/join (fast first paint,
SEO, no WASM download for casual visitors); Interactive WebAssembly for
everything behind login plus the chatbot widget.

Data flow — one API for all clients:

```
Browser (Blazor WASM) ──JWT──> AirSerbiaVirtua.Api ──EF──> Postgres
Desktop ACARS client  ──JWT──> AirSerbiaVirtua.Api            │
Web SSR pages         ──HTTP─> Api (public endpoints, no auth)┘
```

No second database, no duplicated business logic. The API gains a CORS policy
(production checklist item #9).

Booking → ACARS handoff (core scenario): pilot books + prepares on the web; the
desktop client on startup calls `GET /api/bookings/active` and offers
"Resume dispatch: JU360 LYBE→LOWW" with route, fuel plan, and METAR.

Docker: `docker-compose.yml` with `web`, `api`, `postgres` services.

## 2. Pages & features by area

### Public area (static SSR, no login)

| Page | Content |
|---|---|
| Landing | Hero (chocolate dark + Air Serbia red), live stats strip (`GET /api/stats`), latest flown flights ticker |
| Fleet | Aircraft cards (type, registration, status) from `GET /api/fleet` |
| Schedules | BAV-style filterable table (flight no, dep, arr, times, duration, op days) + right-hand filter panel + pagination; public |
| Route map | Leaflet map (free, no API key) with route arcs from the route network |
| Join us | Application form → creates a `Pending` pilot (existing register flow) |
| About / Rules | Static content pages |

### Pilot portal (WASM, login required)

| Page | Content |
|---|---|
| Dashboard | Rank/hours progress, active booking card, last PIREP score, recent VA activity |
| Book a flight | Schedules filtered to rank → Book (reuses existing booking logic) |
| Briefing | Route facts, fuel estimate, dep/arr METAR (`GET /api/metar`), airport info; produces a "Dispatch ready" state the desktop pulls |
| Logbook | All my PIREPs (`GET /api/pireps/mine`), filters, totals |
| Profile | Avatar, rank, hub, change password |

### Admin area (WASM, role-gated)

| Page | Content |
|---|---|
| Roster | List pilots, approve Pending → Active (closes the "fake roster" loose end), set rank/hub, ban/deactivate |
| PIREP review | Pending PIREPs queue → view track/score → accept/reject with comment |
| Schedules & fleet mgmt | CRUD over routes and aircraft (currently seed-only) |
| VA stats | Flights per route, active pilots, hours per month |

### Build order (each phase shippable)

1. **W1 Foundation** — Contracts extraction, Blazor scaffold, brand theme, login/register against existing API, CORS, docker-compose
2. **W2 Public site** — landing, fleet, schedules, route map, join us
3. **W3 Pilot portal** — dashboard, booking, briefing, logbook, profile
4. **W4 Desktop slim-down** — desktop pulls active dispatch; remove its Bookings/Briefing UI *(amended 2026-06-06: desktop keeps Bookings/Briefing — see doc/w4-desktop-slimdown-design.md; shipped 2026-06-13)*
5. **W5 Admin area** — roster, PIREP review, CRUD
6. **W6 AI dispatcher** — intent router first, LLM phrasing layer second

Admin comes after the pilot portal (single admin; Swagger/SQL covers the gap).
Chatbot last because it grounds on endpoints earlier phases create.

## 3. AI Dispatcher ("ASV Dispatch")

UX: floating chat button on every page; dispatcher-styled chat panel. Works for
visitors ("how do I join?") and pilots ("METAR LYBE?", "what's my next booking?").

Key decision — the LLM never decides anything, C# does:

```
User question
   ▼
Intent router (plain C# in WASM — regex/keyword matching)
   │  "metar LYBE"        → GET /api/metar?icaos=LYBE
   │  "flights to Vienna" → GET /api/schedules?arr=LOWW
   │  "how do I join"     → FAQ corpus (static markdown)
   │  "my hours"          → GET /api/pireps/mine (if logged in)
   ▼
Facts (JSON/text)
   ▼
WebLLM (Llama 3.2 1B) — "You are ASV Dispatch. Answer ONLY from these facts: ..."
   ▼
Friendly dispatcher answer
```

A deterministic C# router picks the data source; the model only rephrases real
API data, so it cannot invent schedules. No intent matched → FAQ corpus or
"ask staff on Discord."

WebLLM integration:
- `@mlc-ai/web-llm` JS module + thin Blazor JS-interop wrapper (`DispatcherLlmService`)
- Model: Llama 3.2 1B Instruct (q4f16, ~700 MB); 3B configurable later
- Downloads only when the user first opens the chat (progress bar), then
  browser-cached; never on page load
- Model files come from MLC's CDN (HuggingFace) — not in our images, hosting
  bill unaffected

Graceful degradation: the intent router + facts work without the LLM. No
WebGPU / user declines download → same answers, template-phrased. Build order
inside W6: router first (works for 100% of users), LLM phrasing second.

New API endpoints needed: `GET /api/schedules` with filters (created in W2) and
`GET /api/airports/{icao}` (name, runways, elevation — seeded from OurAirports
open data).

## 4. Auth, roles, security & error handling

Auth on the web (reuses existing API auth):
- Login page calls existing `POST /api/auth/login` → access JWT + refresh token
- Tokens in memory in WASM; refresh token in `localStorage` for silent
  re-auth on reload via the existing rotation endpoint (theft detection already
  implemented)
- `DelegatingHandler` on the WASM `HttpClient` attaches JWT, auto-refreshes on
  401 (single-flight refresh — parallel calls must not double-rotate)
- Blazor `AuthenticationStateProvider` reads JWT claims → `<AuthorizeView>`
  gates pages and nav

Roles:
- New `Role` column on `Pilots`: `Pilot` (default) | `Admin`. One migration,
  one claim in token generation
- Admin API endpoints: `[Authorize(Roles = "Admin")]` — the real gate; WASM-side
  gating is UX only, never trusted
- Pilot `Status` (Pending/Active/Banned) keeps its meaning; Role is orthogonal

API hardening (knocks items off `doc/production-readiness.md`):
- CORS (#9): allow-list of web origin(s) only
- Public endpoints (`/api/schedules`, `/api/fleet`, `/api/stats`, `/api/metar`,
  `/api/airports`) rate-limited (~60 req/min/IP)
- METAR proxy cached server-side ~5 min per ICAO

Error handling:
- API: status codes + ProblemDetails (current style); new endpoints follow it
- WASM: one `ApiClient` wrapper returning Result-style outcomes; pages render
  loading skeleton / friendly error panel with Retry / data
- SSR public pages: API down → render page shell with "live data unavailable"
  placeholder, never a 500
- Chatbot: API failure → "having trouble reaching ops, try again in a minute";
  never a stack trace, never invented data

## 5. Testing & deployment

Testing (proportionate to a one-person project):

| What | How |
|---|---|
| Intent router | Unit tests: "metar lybe pls" → `MetarIntent("LYBE")`, "flights to vienna?" → `ScheduleIntent(arr: LOWW)`, no-match fallback |
| New API endpoints | Schedules filtering + paging, role gate (Pilot → Admin endpoint → 403), public endpoints anonymous |
| Auth flow in WASM | Unit test the `DelegatingHandler` (attach token, refresh on 401, single-flight refresh) |
| UI components | Manual click-through per phase; bUnit only if a component grows real logic |

Deployment:
- `docker-compose.yml`: `web` + `api` + `postgres` (+ named volume); standard
  multi-stage .NET 10 Dockerfiles
- Config via environment variables (connection string, JWT key, CORS origins)
- Optional `caddy` container later for automatic TLS once a host is chosen
- Dev workflow unchanged (`dotnet run` + local Postgres); compose is prod parity

## Out of scope for v1

VATSIM/IVAO network integration, Discord bot, events/tours system,
multi-airline support, email notifications (SMTP), password reset.
