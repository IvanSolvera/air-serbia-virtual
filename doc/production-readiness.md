# Production Readiness Checklist

Kontekst: jedan ASP.NET Core API + PostgreSQL na centralnom serveru (24/7). Više ACARS klijenata se nezavisno autentifikuje i šalje POSREP/PIREP preko HTTPS-a iz različitih mreža (kućni Wi-Fi, hoteli, mobilni hotspot). API piše/čita Postgres.

Lista je poređana po redu izvršenja — svaka stavka je samostalna, ima jasnu definiciju "gotovo".

---

## A — Bezbednost (mora pre izlaska iz localhosta)

### 1. JWT signing key van repozitorijuma
**Trenutno:** `appsettings.json:15` ima literal `"CHANGE_ME_dev_only_super_secret_signing_key_min_32_chars"`.
**Uradi:**
- Premestiti `Jwt:Key` u user-secrets za dev i u env var (`Jwt__Key`) za prod.
- U `Program.cs` dodati fail-fast guard: ako `Jwt:Key` nije postavljen ili je < 32 bajta → throw na startu.
- U `appsettings.json` ostaviti samo `Issuer`, `Audience`, `ExpiresMinutes` (bez `Key`).
- Dodati instrukcije u `README.md`.
**Gotovo kad:** `git grep CHANGE_ME` ništa ne nalazi i API odbija da starta bez konfigurisanog ključa.

### 2. Per-pilot hešovane lozinke
**Trenutno:** `AuthController.cs:36-38` koristi jedan globalni `Auth:DevPassword="letmein"` za sve.
**Uradi:**
- Dodati `PasswordHash` (string, max 255) i `PasswordUpdatedAtUtc` (DateTimeOffset?) na `Pilot`.
- EF Core migracija `AddPilotPasswordHash`.
- Dodati `BCrypt.Net-Next` NuGet paket (ili `Microsoft.AspNetCore.Identity` PasswordHasher).
- Servis `IPasswordHasher` sa `Hash(plain)` i `Verify(plain, hash)`.
- `AuthController.Login` koristi `Verify(req.Password, pilot.PasswordHash)`; ukloniti `Auth:DevPassword` granu.
- Privremeno: skripta/seed za rebackfill testnih naloga sa generisanim lozinkama (logovati na konzolu pri seed-u u dev-u).
- Dodati `POST /api/auth/change-password` (Authorize) za autentifikovanog pilota.
**Gotovo kad:** login radi sa per-pilot lozinkom, `Auth:DevPassword` ne postoji nigde u kodu, change-password endpoint pokriven smoke testom.

### 3. Rate limiting na auth endpoint-ima
**Trenutno:** nema — login je otvoren za brute force preko interneta.
**Uradi:**
- Registrovati `AddRateLimiter` u `Program.cs`.
- Fixed-window policy za `/api/auth/*`: 10 req/min po IP (uz `KeyedByIPAddress`).
- Globalni fallback policy: 100 req/min po IP za sve ostalo (ili izostaviti ako se pokaže preusko).
- Vratiti 429 sa `Retry-After`.
**Gotovo kad:** loop sa 50 brzih login pokušaja vraća 429 nakon ~10.

---

## B — Pouzdanost pred dugotrajnim letovima

### 4. Refresh token rotacija
**Trenutno:** JWT istekne posle 720 min. Pilot na 11h letu može da izgubi token usred POSREP-a → POSREP/PIREP padaju do kraja leta.
**Uradi:**
- Novi entitet `RefreshToken { Id, PilotId, TokenHash, ExpiresAtUtc, RevokedAtUtc?, ReplacedById? }` + migracija.
- `POST /api/auth/login` vraća `(accessToken, refreshToken)`; access TTL = 60 min, refresh TTL = 30 dana.
- `POST /api/auth/refresh` rotira: stari refresh se obeleži kao Revoked, izdaje se nov par. Ako se već iskorišćen refresh ponovo upotrebi → revoke svih aktivnih za tog pilota (theft detection).
- `ApiService` u klijentu: 401 sa `WWW-Authenticate: Bearer error="invalid_token"` → automatski refresh i retry zahteva jednom.
**Gotovo kad:** simulacija 2h leta sa veštački kratkim access TTL-om (5 min) završi PIREP bez 401-ice.

### 5. Idempotencija POSREP-a
**Trenutno:** ako klijent pošalje POSREP, dobije timeout i retry-uje, server upiše duplikat.
**Uradi:**
- `PositionReport` DTO dobija `ClientReportId` (Guid).
- `PositionLog` dobija `ClientReportId` (Guid) + jedinstveni indeks `(PirepId, ClientReportId)`.
- Migracija + `FlightsController.Position`: pri pokušaju duplikat-insert-a vrati 200 (već primljeno) umesto 500.
- Klijent generiše Guid jednom po POSREP-u, isti se koristi za sve retry-jeve.
**Gotovo kad:** trostruki push istog POSREP-a rezultuje jednim redom u `PositionLog`.
**✅ Urađeno 2026-06-03:** `PositionLog.ClientReportId` + unique index `(PirepId, ClientReportId)`, migracija `AddPosrepIdempotency` (nullable→backfill `gen_random_uuid()`→NOT NULL, primenjena na dev DB). `FlightsController.Position` hvata `DbUpdateException` i vraća 200 ako red već postoji. `PositionReport.FromTelemetry` generiše Guid jednom; `PosrepQueue` ga čuva u fajlu pa svi retry-jevi nose isti id. Pokriveno `--selftest`-om (PosrepQueueTest C).

### 6. Klijent: offline buffer + retry/backoff
**Trenutno:** `ApiService.PushPositionAsync` baci exception na bilo koju mrežnu grešku — POSREP je izgubljen.
**Uradi:**
- Lokalni queue (SQLite ili append-only JSONL fajl u `%LOCALAPPDATA%/AirSerbiaVirtua/`).
- Background worker fluša queue u redosledu po Timestamp-u, sa exponential backoff (1s → 2s → 4s … cap 60s) i jitter.
- POSREP HttpClient timeout: 5s (umesto 30s) — ne sme da blokira sledeći tick.
- PIREP submit: čeka da queue bude prazan (sve POSREP-e prosleđene), pa šalje sinhrono uz retry.
- UI/console indikator: broj POSREP-a u queue-u + status veze.
- **GVA referenca:** njihov `ACKBuffer` / `RequestStack` rešava tačno ovaj problem (guaranteed delivery sa ack-om). Vidi `doc/acars-comparison.md`. #5 i #6 zajedno = naša verzija tog pattern-a (Guid idempotency ključ + lokalni queue sa ack/retry).
**Gotovo kad:** isključen Wi-Fi tokom 5-minutnog testa, pa uključen — svi POSREP-i stignu na server u tačnom redosledu.
**✅ Urađeno 2026-06-03:** `PosrepQueue` u `Acars.Core` — durable directory-of-files queue (`%LOCALAPPDATA%/AirSerbiaVirtua/posrep/`), atomic write (temp+rename), filename `{unixMillis}_{guid}.json` = hronološki sort. Background worker šalje u redosledu, exponential backoff 1s→2s→…→cap 60s + jitter, briše fajl tek nakon ack-a. Send timeout 5s po POSREP-u (linked CTS) — ne blokira tick. `WaitForDrainAsync` pre PIREP submit-a. UI: `QueueStatusText` (broj u redu) + `LastPosRepText` (zadnji ack). `AcarsViewModel.PosRepLoopAsync` sada samo `Enqueue` (nikad ne baca). Preživljava restart (novi queue pokupi zaostale fajlove). Pokriveno `--selftest`-om: 11/11 PASS (durability, crash-reload, ordered delivery, idempotent retry). **Preostaje:** end-to-end Wi-Fi test tokom živog leta (Phase 2b).

---

## C — Operacija i evolucija

### 7. API versioning
**Trenutno:** rute su `/api/auth`, `/api/flights`, … bez verzije. Jednom kad pilot instalira klijent, server-side incompatibilna promena lomi sve instalirane klijente.
**Uradi:**
- `Asp.Versioning.Mvc` paket.
- Default verzija `1.0`, čitati iz URL segmenta (`/api/v1/auth/login`).
- Kontroleri označeni `[ApiVersion("1.0")]`.
- Klijent u `ApiService` koristi `api/v1/...` prefix.
**Gotovo kad:** `/api/v1/auth/login` radi, `/api/auth/login` vraća 404, Swagger prikazuje verziju.
**✅ Urađeno 2026-06-06:** `Asp.Versioning.Mvc` + `Asp.Versioning.Mvc.ApiExplorer` 10.0.0. URL-segment reader, svi (8) kontroleri `[ApiVersion("1.0")]` + `Route("api/v{version:apiVersion}/...")`. `ApiService` prebačen na `api/v1/`. Verifikovano uživo: `/api/auth/login` → 404, `/api/v1/auth/login` → 401/200, Swagger paths pokazuju `/api/v1/...`. `/health` namerno ostaje bez verzije.

### 8. Strukturisani logovi (Serilog)
**Trenutno:** default `ILogger` u Console, nečitljivo preko više pilota.
**Uradi:**
- `Serilog.AspNetCore` + `Serilog.Sinks.Console` (JSON formatter) + `Serilog.Sinks.File` (rolling daily).
- Properti `PilotId` na request scope (iz JWT claims) → svaki log line ima pilot-context.
- Health, /api/auth/login i POSREP endpointi logovani na Information; greške na Warning/Error sa stack-om.
**Gotovo kad:** `logs/asv-YYYYMMDD.log` sadrži JSON linije sa `pilotId`, `routeId`, `flightSessionId`.
**✅ Urađeno 2026-06-06:** `Serilog.AspNetCore` 10.0.0, bootstrap logger + `UseSerilog`. Console: čitljiv u dev-u, CompactJson u prod-u; File: CompactJson `logs/asv-.log` rolling daily (30 dana). `UseSerilogRequestLogging` sa `PilotId` u diagnostic context-u + middleware posle `UseAuthentication` koji `LogContext.PushProperty("PilotId", ...)` — svaka linija autentifikovanog zahteva nosi pilota. `AuthController.Login` (Information/Warning) i `FlightsController.Start`/`Position` loguju `RouteId`/`FlightSessionId`. Verifikovano: `logs/asv-20260606.log` sadrži JSON sa `"PilotId":1`.

### 9. CORS politika (eksplicitno)
**Trenutno:** nije postavljena — defaultno same-origin, ali nedeklarisano.
**Uradi:**
- Ako ostane samo native ACARS klijent → eksplicitno `app.UseCors(p => p.DisallowCredentials())` ili izostaviti (native HTTP klijent ne radi CORS check).
- Ako planiraš web UI → konfigurisati allowed origin iz `appsettings:Cors:AllowedOrigins`.
**Gotovo kad:** politika je svesno doneta i komentarisana u `Program.cs`.
**✅ Urađeno 2026-06-06:** default policy čita `Cors:AllowedOrigins` iz konfiguracije (`appsettings.json` ima prazan niz = default-deny, nijedan `Access-Control-Allow-Origin` se ne emituje). Kad web UI (doc/website-design.md) krene, origin se dodaje per-environment (`Cors__AllowedOrigins__0`). `DisallowCredentials` jer web UI šalje JWT u header-u, ne cookies. Odluka komentarisana u `Program.cs`.

### 10. Deployment — TLS, reverse proxy, prod migracije
**Trenutno:** `Program.cs:64` radi `Database.Migrate()` samo u Developmentu (dobro). Nema dokumentacije za prod.
**Uradi:**
- Dodati `doc/deployment.md` sa:
  - Reverse proxy (nginx ili Caddy) ispred Kestrel-a sa Let's Encrypt sertifikatom.
  - Systemd unit fajl ili docker-compose.yml za API.
  - `dotnet ef migrations bundle` ili zaseban migrator korak u CI pre nego što se API restartuje.
  - `Jwt__Key`, `ConnectionStrings__Default`, `Auth__*` env varijable.
  - Backup strategija Postgres-a (pg_dump u cron-u + retention).
- Health probe konfiguracija (`/health` već postoji).
**Gotovo kad:** dokument postoji i prati real deployment.
**✅ Urađeno 2026-06-06:** `doc/deployment.md` — Caddy (auto Let's Encrypt) i nginx+certbot varijante, systemd unit sa `EnvironmentFile` i docker-compose (db + migrator + api), `dotnet ef migrations bundle` korak pre restarta API-ja, env var tabela (`Jwt__Key`, `ConnectionStrings__Default`, `Cors__AllowedOrigins__0`), pg_dump cron sa 14-dnevnom retencijom + restore drill, deploy checklist. Napomena o `ForwardedHeaders` za rate limiter iza proxy-ja.

---

---

## D — GVA-inspirisane nadogradnje (vidi `doc/acars-comparison.md`)

Ideje pozajmljene iz Global Virtual Airlines Group (GVA) ACARS klijenta —
implementirane natively u našem stack-u, ne kopiran kod. Nisu blokatori za
produkciju; redosled po vrednosti.

### 11. `ISimBridge` apstrakcija simulatora
**Zašto:** GVA izoluje simulator iza `Bridge` interfejsa (FSUIPC i X-Plane su dve implementacije). Mi smo vezani direktno za `FsuipcService`.
**Uradi:**
- `ISimBridge` interfejs u `Acars.Core` (Name, IsConnected, Log, EnsureConnected, ReadCurrent, Close, Dispose).
- `FsuipcService` implementira `ISimBridge`.
- `SimulatorService` zavisi od `ISimBridge` (constructor injection, default `FsuipcService`).
**Gotovo kad:** `SimulatorService` ne referencira `FsuipcService` direktno; može da primi fake bridge u testu. **(Urađeno 2026-06-03)**

### 12. Per-packet kompresija POSREP-a
**Zašto:** GVA GZIP/Deflate-uje svaki paket. Kad offline buffer (#6) batch-uje POSREP-e, kompresija je trivijalan dobitak na bandwidth-u (bitno za mobilni hotspot).
**Uradi:** opcioni GZIP nad batch JSON-om pre slanja; server dekompresuje po `Content-Encoding`. Tek nakon #6.
**Gotovo kad:** batch od N POSREP-a ide kao gzip telo; server ga transparentno prima.

### 13. Bogatija telemetrija (`FlightData`)
**Zašto:** GVA `PositionData` ima ~80 polja (per-engine N1/N2/throttle/flow, svi AP/AT modovi, G-force, AoA, touchdown G). Naš `FlightData` ima ~10. Ograničava kvalitet PIREP scoring-a i debrief-a.
**Uradi:** selektivno dodati polja koja nose vrednost za scoring (touchdown G, AoA, bank/pitch na touchdown-u, AP mode na approach-u). Ne svih 80. Vezano za Phase 4 Debriefing.
**Gotovo kad:** PIREP score koristi bar touchdown G + landing pitch/bank; debrief ih prikazuje.

### 14. Više faza leta (Pushback / Aborted / Error)
**Zašto:** GVA `FlightPhase` ima 14 faza uključujući Aborted i Error — neuspeli letovi su first-class. Naš `FlightState` nema način da obeleži prekinut let.
**Uradi:** dodati `Aborted` i opciono `Pushback` u `FlightState`; state machine prelazi u `Aborted` na crash/prekid; PIREP to beleži umesto da visi u Pending.
**Gotovo kad:** crash u simu ili user-abort rezultuje `Aborted` PIREP-om, ne zaglavljenim Pending-om.

---

## Status

- [x] 1. JWT signing key van repozitorijuma
- [x] 2. Per-pilot hešovane lozinke
- [x] 3. Rate limiting na auth endpoint-ima
- [x] 4. Refresh token rotacija
- [x] 5. Idempotencija POSREP-a
- [x] 6. Klijent: offline buffer + retry/backoff
- [x] 7. API versioning
- [x] 8. Strukturisani logovi (Serilog)
- [x] 9. CORS politika
- [x] 10. Deployment dokument
- [x] 11. `ISimBridge` apstrakcija simulatora (GVA)
- [ ] 12. Per-packet kompresija POSREP-a (GVA)
- [ ] 13. Bogatija telemetrija `FlightData` (GVA)
- [ ] 14. Više faza leta — Aborted/Pushback (GVA)
