# Deployment — TLS, reverse proxy, prod migrations

Production-readiness item **#10**. Target topology: one Linux VM (or container
host) running the API behind a TLS-terminating reverse proxy, plus PostgreSQL.
All ACARS clients talk HTTPS to the proxy from arbitrary networks.

```
ACARS clients ──HTTPS──> Caddy/nginx (:443, TLS) ──HTTP──> Kestrel (127.0.0.1:5000) ──> PostgreSQL (:5432)
```

---

## 1. Configuration via environment variables

The API reads standard ASP.NET Core configuration. **Never** put secrets in
`appsettings.json` — the app fail-fasts on a missing/short `Jwt:Key`.

| Variable | Example | Notes |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` | Anything but `Development` disables Swagger + auto-migrate |
| `ASPNETCORE_URLS` | `http://127.0.0.1:5000` | Bind Kestrel to loopback only — the proxy is the public face |
| `ConnectionStrings__Default` | `Host=localhost;Port=5432;Database=airserbiavirtua;Username=asv;Password=...` | Use a dedicated DB role, not `postgres` |
| `Jwt__Key` | 64+ random chars | `openssl rand -base64 48`; min 32 bytes enforced at startup |
| `Jwt__Issuer` | `AirSerbiaVirtua` | Already in appsettings; override if needed |
| `Jwt__Audience` | `AirSerbiaVirtua.Acars` | — |
| `Cors__AllowedOrigins__0` | `https://airserbiavirtual.example` | Only needed once the web UI ships; omit entirely for ACARS-only (locked down by default) |

Logs: Serilog writes compact JSON to console **and** `logs/asv-YYYYMMDD.log`
(rolling daily, 30 days kept) relative to the working directory. Make sure the
service user can write there, or ship console output to journald/docker logs.

---

## 2. Database migrations (prod)

`Program.cs` runs `Database.Migrate()` **only in Development**. In production,
apply migrations as an explicit deploy step *before* the new API build starts.

Recommended: a self-contained **migrations bundle** built in CI:

```bash
# CI build step (needs dotnet-ef: dotnet tool install -g dotnet-ef)
dotnet ef migrations bundle \
  --project AirSerbiaVirtua.Api/AirSerbiaVirtua.Api.csproj \
  --self-contained -r linux-x64 -o artifacts/efbundle

# Deploy step, on the server, BEFORE restarting the API
./efbundle --connection "$ConnectionStrings__Default"
```

Deploy order: **stop/drain API → run bundle → start new API**. The bundle is
idempotent (applies only pending migrations). Roll back by deploying the
previous API build — never `migrations remove` against a prod DB.

---

## 3. Reverse proxy + TLS

### Option A — Caddy (recommended: automatic Let's Encrypt)

`/etc/caddy/Caddyfile`:

```caddyfile
api.airserbiavirtual.example {
    reverse_proxy 127.0.0.1:5000
    encode gzip
    # Caddy obtains + renews the Let's Encrypt certificate automatically.
}
```

```bash
sudo apt install caddy        # Debian/Ubuntu, ports 80+443 open
sudo systemctl reload caddy
```

### Option B — nginx + certbot

```nginx
server {
    listen 443 ssl http2;
    server_name api.airserbiavirtual.example;

    ssl_certificate     /etc/letsencrypt/live/api.airserbiavirtual.example/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/api.airserbiavirtual.example/privkey.pem;

    location / {
        proxy_pass http://127.0.0.1:5000;
        proxy_set_header Host              $host;
        proxy_set_header X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
server {
    listen 80;
    server_name api.airserbiavirtual.example;
    return 301 https://$host$request_uri;
}
```

```bash
sudo certbot --nginx -d api.airserbiavirtual.example   # issues + auto-renews
```

> Note: the app calls `UseHttpsRedirection()`, but TLS terminates at the proxy.
> Since Kestrel only ever sees HTTP from loopback, either forward
> `X-Forwarded-Proto` (nginx config above does) or leave it — the proxy already
> 301s port 80. Rate limiting keys on `RemoteIpAddress`; behind a proxy that is
> the proxy's IP, so when moving beyond a single box add
> `ForwardedHeadersMiddleware` (`UseForwardedHeaders`) so the auth limiter sees
> real client IPs.

---

## 4. Running the API

### Option A — systemd unit

`/etc/systemd/system/asv-api.service`:

```ini
[Unit]
Description=Air Serbia Virtua API
After=network.target postgresql.service

[Service]
Type=notify
User=asv
WorkingDirectory=/opt/asv/api
ExecStart=/usr/bin/dotnet /opt/asv/api/AirSerbiaVirtua.Api.dll
Restart=always
RestartSec=5
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://127.0.0.1:5000
# Secrets: keep them out of the unit file readable by everyone —
# use a root-owned EnvironmentFile instead.
EnvironmentFile=/etc/asv/api.env

[Install]
WantedBy=multi-user.target
```

`/etc/asv/api.env` (chmod 600, owner root):

```
ConnectionStrings__Default=Host=localhost;Port=5432;Database=airserbiavirtua;Username=asv;Password=CHANGE
Jwt__Key=CHANGE_64_random_chars
```

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now asv-api
journalctl -u asv-api -f          # tail logs
```

### Option B — docker-compose

`docker-compose.yml`:

```yaml
services:
  db:
    image: postgres:17
    restart: unless-stopped
    environment:
      POSTGRES_DB: airserbiavirtua
      POSTGRES_USER: asv
      POSTGRES_PASSWORD: ${DB_PASSWORD:?set in .env}
    volumes:
      - pgdata:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U asv -d airserbiavirtua"]
      interval: 10s
      retries: 5

  migrator:
    image: asv-api:latest            # same image; runs the EF bundle then exits
    entrypoint: ["/app/efbundle", "--connection",
                 "Host=db;Port=5432;Database=airserbiavirtua;Username=asv;Password=${DB_PASSWORD}"]
    depends_on:
      db: { condition: service_healthy }

  api:
    image: asv-api:latest
    restart: unless-stopped
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_URLS: http://0.0.0.0:5000
      ConnectionStrings__Default: Host=db;Port=5432;Database=airserbiavirtua;Username=asv;Password=${DB_PASSWORD}
      Jwt__Key: ${JWT_KEY:?set in .env}
    ports:
      - "127.0.0.1:5000:5000"        # loopback only; Caddy/nginx on the host fronts it
    depends_on:
      migrator: { condition: service_completed_successfully }

volumes:
  pgdata:
```

Secrets live in a chmod-600 `.env` next to the compose file (`DB_PASSWORD=`,
`JWT_KEY=`), never in the YAML.

---

## 5. Health probes

| Endpoint | Auth | Use |
|---|---|---|
| `GET /health` | Anonymous | Liveness/readiness — returns `{"status":"ok"}` |

- systemd: pair with a simple `curl -fsS http://127.0.0.1:5000/health` cron/watchdog, or rely on `Restart=always`.
- docker-compose: add to the `api` service:
  ```yaml
  healthcheck:
    test: ["CMD", "curl", "-fsS", "http://localhost:5000/health"]
    interval: 30s
    timeout: 5s
    retries: 3
  ```
- Uptime monitoring: point an external checker (UptimeRobot etc.) at
  `https://api.../health` so pilots' POSREP failures aren't your first signal.

Note: `/health` is **unversioned** by design; all business endpoints live under
`/api/v1/...` (prod item #7).

---

## 6. PostgreSQL backups

Nightly `pg_dump` + retention, as the `postgres` (or `asv`) user:

`/etc/cron.d/asv-backup`:

```cron
# Nightly 03:15 dump, compressed custom format (pg_restore-able), 14 days kept
15 3 * * * asv pg_dump -Fc -d airserbiavirtua -f /var/backups/asv/asv-$(date +\%Y\%m\%d).dump && find /var/backups/asv -name 'asv-*.dump' -mtime +14 -delete
```

- `mkdir -p /var/backups/asv && chown asv /var/backups/asv`
- Restore drill (do this once, before you need it):
  ```bash
  createdb asv_restore_test
  pg_restore -d asv_restore_test /var/backups/asv/asv-YYYYMMDD.dump
  psql -d asv_restore_test -c 'SELECT COUNT(*) FROM "Pireps";'
  dropdb asv_restore_test
  ```
- Copy dumps off-box (rclone to object storage, or at minimum a second disk).
  A backup that lives only on the DB host is not a backup.

---

## 7. Deploy checklist (every release)

1. CI: `dotnet publish -c Release` + `dotnet ef migrations bundle` → artifacts.
2. Upload to server / push image.
3. Stop or drain the API (`systemctl stop asv-api`).
4. Run the EF bundle against prod DB.
5. Start the new API (`systemctl start asv-api`).
6. `curl -fsS https://api.../health` → `{"status":"ok"}`.
7. Smoke: login from the ACARS client, `GET /api/v1/routes` returns data.
8. Watch `logs/asv-*.log` / journald for `Warning`+ for a few minutes.
