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

### 6. Klijent: offline buffer + retry/backoff
**Trenutno:** `ApiService.PushPositionAsync` baci exception na bilo koju mrežnu grešku — POSREP je izgubljen.
**Uradi:**
- Lokalni queue (SQLite ili append-only JSONL fajl u `%LOCALAPPDATA%/AirSerbiaVirtua/`).
- Background worker fluša queue u redosledu po Timestamp-u, sa exponential backoff (1s → 2s → 4s … cap 60s) i jitter.
- POSREP HttpClient timeout: 5s (umesto 30s) — ne sme da blokira sledeći tick.
- PIREP submit: čeka da queue bude prazan (sve POSREP-e prosleđene), pa šalje sinhrono uz retry.
- UI/console indikator: broj POSREP-a u queue-u + status veze.
**Gotovo kad:** isključen Wi-Fi tokom 5-minutnog testa, pa uključen — svi POSREP-i stignu na server u tačnom redosledu.

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

### 8. Strukturisani logovi (Serilog)
**Trenutno:** default `ILogger` u Console, nečitljivo preko više pilota.
**Uradi:**
- `Serilog.AspNetCore` + `Serilog.Sinks.Console` (JSON formatter) + `Serilog.Sinks.File` (rolling daily).
- Properti `PilotId` na request scope (iz JWT claims) → svaki log line ima pilot-context.
- Health, /api/auth/login i POSREP endpointi logovani na Information; greške na Warning/Error sa stack-om.
**Gotovo kad:** `logs/asv-YYYYMMDD.log` sadrži JSON linije sa `pilotId`, `routeId`, `flightSessionId`.

### 9. CORS politika (eksplicitno)
**Trenutno:** nije postavljena — defaultno same-origin, ali nedeklarisano.
**Uradi:**
- Ako ostane samo native ACARS klijent → eksplicitno `app.UseCors(p => p.DisallowCredentials())` ili izostaviti (native HTTP klijent ne radi CORS check).
- Ako planiraš web UI → konfigurisati allowed origin iz `appsettings:Cors:AllowedOrigins`.
**Gotovo kad:** politika je svesno doneta i komentarisana u `Program.cs`.

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

---

## Status

- [x] 1. JWT signing key van repozitorijuma
- [x] 2. Per-pilot hešovane lozinke
- [x] 3. Rate limiting na auth endpoint-ima
- [x] 4. Refresh token rotacija
- [ ] 5. Idempotencija POSREP-a
- [ ] 6. Klijent: offline buffer + retry/backoff
- [ ] 7. API versioning
- [ ] 8. Strukturisani logovi (Serilog)
- [ ] 9. CORS politika
- [ ] 10. Deployment dokument
