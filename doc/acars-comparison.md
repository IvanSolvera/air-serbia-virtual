# ACARS architecture comparison — GVA/Delta vs. AirSerbiaVirtual

Date: 2026-06-03. Investigated the Delta Virtual ACARS client the user runs
(`J:\Program Files\Delta Virtual\ACARSv3\`) to decide whether to adopt it,
switch architectures, or mine it for ideas.

## What it actually is

The "Delta VA ACARS" is **not Delta-specific code**. It is the
**Global Virtual Airlines Group (GVA) ACARS** framework — namespaces
`Gvagroup.Acars.*`, assembly title *"GVA ACARS"*, *"Copyright © 2004–2024
Global Virtual Airlines Group"*. Delta Virtual is one airline (one tenant)
running on the shared GVA platform.

It is a **20-year-old, very mature** client:

- **.NET Framework 4.8**, **WinForms** (we are .NET 10 / WPF).
- Ships PDBs, so it decompiles cleanly with `ilspycmd` — but it is
  **proprietary, copyrighted code**; we study the design, we do not lift it.

Assemblies:

| DLL | Role |
|-----|------|
| `ACARS.dll` (10.9k lines decompiled) | Core domain: nav DB, telemetry, flight model, charts, maps, online networks |
| `ACARSConnection.dll` | Network transport — raw TCP socket + XML protocol + compression |
| `ACARS_UI.dll` | WinForms UI |
| `FSUIPCBridge.dll` (15.4k lines) | FSUIPC bridge (MSFS / P3D / FSX) |
| `XPBridge.dll` | X-Plane bridge |
| `LDS32.exe`, `LVLDSDK.dll` | Level-D 767 SDK integration |
| `GMap.NET.*` | Moving map |
| `PdfiumViewer`, `pdfium.dll` | PDF approach charts |
| `System.Data.SQLite` | Local cache DB |

## Architecture: fundamentally opposite to ours

| | **GVA / Delta** | **Ours (AirSerbiaVirtual)** |
|---|---|---|
| Transport | Persistent **raw TCP socket** (port 15527) | **HTTP REST + JSON** |
| Protocol | Custom **XML** (`<ACARSResponse>…`), binary framing w/ magic+length prefix, GZIP/Deflate compression, server `HELLO` handshake, **server-push capable** | Stateless request/response, 30 s POSREP poll |
| Reliability | **ACK buffer** (`ACKBuffer`, `RequestStack`, `ACKContext`) for guaranteed delivery; AES message encryption | JWT bearer; no delivery guarantee yet |
| Backend | **Proprietary GVA server we do not have and cannot see** | **Our own** ASP.NET Core + PostgreSQL + EF Core, fully owned |
| Runtime | .NET Framework 4.8, WinForms | .NET 10, WPF/MVVM |
| Sims | FSUIPC **and** X-Plane **and** Level-D, behind a clean `Bridge` interface | FSUIPC only |
| Telemetry | `PositionData` — **~80 fields** | `FlightData` — ~10 fields |
| Phases | 14 (`FlightPhase`: incl. Pushback, Aborted, Error, PirepFile) | 10 (`FlightState`) |
| Extras | Full nav DB (airways, runways, gates, SIDs/STARs, oceanic tracks, ETOPS, ILS cats), VATSIM/IVAO controllers, PDF charts, moving map, elite/loyalty levels, multiplayer position sharing | none yet |

### Telemetry gap (the most striking difference)

GVA's `PositionData` carries roughly 80 fields per sample, including:

- Per-engine **N1 / N2 / throttle / fuel-flow** (arrays of 6 engines)
- Every **autopilot / autothrottle mode flag** (AP_GPS/NAV/HDG/APR/ALT/LNAV,
  AT_IAS/MACH/VNAV/FLCH, managed V/S)
- **Lights** (nav/beacon/landing/taxi/strobe/…), **ground ops**
  (catering/fueling/de-ice/boarding/cargo), **boarding progress**
- **Weather** (wind, visibility, ceiling, temp, pressure), **ATC** tuned
  (COM1/COM2 + resolved controllers), NAV1/2/ADF freqs, transponder
- **G-force, angle of attack, CG, frame rate, free VAS, touchdown G**

Ours: position, IAS/GS/VS, altitude, on-ground, parking brake, fuel, flight
number, tail number. Adequate for the current 10-phase tracker and PIREP
scoring, but thin if we want richer scoring/debrief.

### The `Bridge` abstraction (cleanest idea worth copying)

GVA isolates the simulator behind interfaces:

```
Bridge            : connection lifecycle, CurrentPosition, aircraft info,
                    set radios/transponder, gauges, fuel/payload, …
WriteableBridge   : Bridge + Write(MPUpdate)        // multiplayer
FlightPlanBridge  : Bridge + LoadPlan(...)          // FMC/route injection
```

`FSUIPCBridge` and `XPBridge` are two implementations of the same contract.
The flight logic never references FSUIPC directly. **This is exactly how we
should structure `FsuipcService` so X-Plane support later is a new class, not
a rewrite.**

## Decision: keep our architecture, mine GVA for ideas

We are **not switching**. Three hard reasons:

1. **We don't have their server.** The client speaks a proprietary socket/XML
   protocol to GVA's backend. Adopting it means reverse-engineering and
   reimplementing their entire server, or locking ourselves to infrastructure
   we don't control. We have already built and proven our own API + Postgres
   backend (Phase 2 works end-to-end).
2. **It's decompiled, copyrighted, proprietary, and on a legacy stack**
   (.NET Framework 4.8 / WinForms). We can read it for inspiration; we cannot
   reuse it.
3. **Our stack is more modern and testable** — REST/JSON/JWT, a deterministic
   unit-tested state machine, EF Core. That is worth keeping.

GVA's real value to us is as a **20-year feature roadmap**. We borrow ideas,
implemented natively, one at a time.

## What we are borrowing (mapped to our work)

| GVA idea | Our action | Status / where |
|----------|-----------|----------------|
| `Bridge` interface abstraction | `ISimBridge` in `Acars.Core`; `FsuipcService` implements it; `SimulatorService` depends on the interface | **Done** 2026-06-03 (this change) |
| ACK / reliable delivery buffer | Solves prod item **#5 (POSREP idempotency)** and **#6 (offline buffer + backoff)** — client-side queue with ack/retry | roadmap `production-readiness.md` #5, #6 |
| Per-packet compression | Optional GZIP for POSREP batches once offline buffer lands | roadmap (B11) |
| Richer telemetry (`PositionData`) | Selectively expand `FlightData` (per-engine, AP modes, G-force, AoA, touchdown G) for better scoring/debrief | roadmap (B12); Phase 4 |
| More flight phases (Pushback/Aborted/Error) | Add to `FlightState` so failed/aborted flights are first-class | roadmap (C13) |
| Nav DB + PDF charts + moving map | Feeds **Phase 4 Briefing/Debriefing** | roadmap Phase 4 |
| X-Plane bridge | Second `ISimBridge` implementation when there's demand | roadmap Phase 5+ |

## How to revisit the source

Decompiled with the global tool `ilspycmd` (installed 2026-06-03):

```bash
ilspycmd "J:/Program Files/Delta Virtual/ACARSv3/ACARS.dll" -o <out> -usepdb
# same for ACARSConnection.dll, FSUIPCBridge.dll, XPBridge.dll
```

Key types to study: `Gvagroup.Acars.PositionData`, `FlightPhase`,
`FlightStatus`, `Bridge`/`WriteableBridge`/`FlightPlanBridge`,
`Gvagroup.Acars.Net.ACARSConnection`, `DataCompressor`, `ACKBuffer`.
Do **not** commit decompiled output to the repo (copyright).
