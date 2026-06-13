# Production deploy / update runbook — asv.solvera.one

> **This is how production actually runs.** The repo also contains a
> `docker-compose.yml` + Dockerfiles, but **the server does NOT use Docker** — it
> runs the apps as **systemd services** behind **Caddy** with a **native
> PostgreSQL**. Treat the compose files as an unused alternative; follow this doc.

## Production topology (Contabo VPS, host `vmi3356921`, 13.140.150.148)

| Thing | Detail |
|---|---|
| OS / access | Ubuntu; SSH as `root`. App/build user is **`ivan`**; services run as **`asv`**. |
| .NET | .NET 10 SDK at `/usr/bin/dotnet` (build/publish happen on the box). |
| Source repo | `/home/ivan/air-serbia-virtual`, branch **`dev`** (owned by `ivan`). |
| Deployed binaries | `/opt/asv/api` and `/opt/asv/web` (owned by `asv`). |
| API service | `asv-api.service` → `/opt/asv/api/AirSerbiaVirtua.Api.dll`, `http://127.0.0.1:5000`, `EnvironmentFile=/etc/asv/api.env`. |
| Web service | `asv-web.service` → `/opt/asv/web/AirSerbiaVirtua.Web.dll`, `http://127.0.0.1:5001`, `Api__BaseUrl=https://api-asv.solvera.one/`. |
| Database | PostgreSQL 17 native, `127.0.0.1:5432`, DB `airserbiavirtua`, user `asv`. |
| Reverse proxy | Caddy: `api-asv.solvera.one`→:5000, `asv.solvera.one`→:5001 (+ `/downloads/*` from `/var/www/asv-downloads`). TLS is automatic. |
| Secrets | `/etc/asv/api.env` (root, 600): `ConnectionStrings__Default`, `Jwt__Key`, `Cors__AllowedOrigins__0=https://asv.solvera.one`. |
| Public URLs | Site `https://asv.solvera.one` · API `https://api-asv.solvera.one` · installer `https://asv.solvera.one/downloads/AirSerbiaVirtua-ACARS-win-x64.exe`. |

**WASM API URL:** `AirSerbiaVirtua.Web.Client/wwwroot/appsettings.json` is a *tracked
file with a local edit on the server* setting `Api:BaseUrl=https://api-asv.solvera.one/`
(committed value is localhost). Keep that edit — the browser portal calls the API
cross-origin at `api-asv.solvera.one` (CORS allows `asv.solvera.one`).

---

## Update procedure (ship new commits)

Run as `root` (SSH in). Adjust which services you republish to what changed.

```bash
# 1. BACK UP THE DB FIRST
sudo -u postgres pg_dump -Fc airserbiavirtua > /root/asv-backup-$(date +%Y%m%d-%H%M%S).dump
ls -lh /root/asv-backup-*.dump | tail -1

# 2. Pull latest (as ivan; fast-forward keeps the local WASM appsettings edit)
sudo -u ivan git -C /home/ivan/air-serbia-virtual pull --ff-only origin dev
sudo -u ivan grep -o api-asv.solvera.one /home/ivan/air-serbia-virtual/AirSerbiaVirtua.Web.Client/wwwroot/appsettings.json   # confirm preserved
```

### 3. Apply DB migrations — ONLY if new migrations were added

> **Gotcha:** `/etc/asv/api.env` is a *systemd* EnvironmentFile. Do **not** `source`
> it in bash — the semicolons in the connection string terminate the shell
> assignment (you'd get `Host=localhost` only). Extract it line-literal:

```bash
export PATH="$PATH:/root/.dotnet/tools"
dotnet tool install --global dotnet-ef --version 10.0.7 2>/dev/null || true
CONN=$(grep '^ConnectionStrings__Default=' /etc/asv/api.env | sed 's/^ConnectionStrings__Default=//')
echo "DB: $(echo "$CONN" | grep -oiE 'Database=[^;]+')"   # sanity: Database=airserbiavirtua
cd /home/ivan/air-serbia-virtual
dotnet ef database update --project AirSerbiaVirtua.Api/AirSerbiaVirtua.Api.csproj --connection "$CONN"
```
Migrations are idempotent (applies only pending). This is required because the API
runs in `Production`, which does **not** auto-migrate.

### 4. Publish (build as `ivan` to avoid root-owning the repo)

```bash
sudo -u ivan bash -lc '
  cd /home/ivan/air-serbia-virtual
  rm -rf /tmp/asv-api-pub /tmp/asv-web-pub
  dotnet publish AirSerbiaVirtua.Api/AirSerbiaVirtua.Api.csproj -c Release -o /tmp/asv-api-pub --nologo
  dotnet publish AirSerbiaVirtua.Web/AirSerbiaVirtua.Web.csproj -c Release -o /tmp/asv-web-pub --nologo
'
test -f /tmp/asv-api-pub/AirSerbiaVirtua.Api.dll && test -f /tmp/asv-web-pub/AirSerbiaVirtua.Web.dll || { echo "PUBLISH FAILED — stop"; exit 1; }
```

### 5. Swap into /opt/asv + restart (brief downtime; keeps a rollback copy)

```bash
TS=$(date +%Y%m%d-%H%M%S)
systemctl stop asv-web asv-api
mv /opt/asv/api /opt/asv/api.bak-$TS && mv /opt/asv/web /opt/asv/web.bak-$TS
cp -a /tmp/asv-api-pub /opt/asv/api && cp -a /tmp/asv-web-pub /opt/asv/web
chown -R asv:asv /opt/asv/api /opt/asv/web
systemctl start asv-api; sleep 4; systemctl start asv-web; sleep 3
```

### 6. Verify

```bash
systemctl is-active asv-api asv-web caddy postgresql@17-main
curl -fsS http://127.0.0.1:5000/health                       # {"status":"ok"}
curl -fsS http://127.0.0.1:5000/api/v1/routes | head -c 80   # DB + new code
curl -s -o /dev/null -w "%{http_code}\n" http://127.0.0.1:5001/   # 200
```
From anywhere: `https://asv.solvera.one`, `https://asv.solvera.one/download`,
`https://api-asv.solvera.one/health`.

### Rollback

```bash
systemctl stop asv-web asv-api
rm -rf /opt/asv/api /opt/asv/web
mv /opt/asv/api.bak-<TS> /opt/asv/api && mv /opt/asv/web.bak-<TS> /opt/asv/web
systemctl start asv-api; sleep 3; systemctl start asv-web
# DB (only if a migration must be undone): restore the step-1 dump:
#   sudo -u postgres psql -c "DROP DATABASE airserbiavirtua;" -c "CREATE DATABASE airserbiavirtua OWNER asv;"
#   sudo -u postgres pg_restore -d airserbiavirtua /root/asv-backup-<TS>.dump
```

---

## Notes & gotchas

- **Build as `ivan`.** If you build/publish as `root` in the repo, `obj/bin` become
  root-owned and break `ivan`'s next build — `chown -R ivan:ivan /home/ivan/air-serbia-virtual` to fix.
- **`sudo` resets the environment**, so you can't reliably pass `ConnectionStrings__Default`
  to a `sudo -u ivan` shell — run the migration as `root` (as above).
- **Caddy** config is `/etc/caddy/Caddyfile`; reload with `systemctl reload caddy`
  (zero-downtime). Always `caddy validate --adapter caddyfile --config /etc/caddy/Caddyfile` first.
- The ACARS installer is published + uploaded separately — see `doc/release-acars.md`.
