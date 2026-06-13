# Production UPDATE runbook — server 13.140.150.148

Ubuntu + Docker Compose, **full stack** (postgres + api + web) built on the box
from this repo. Use this to ship new work (e.g. the W4 dispatch handoff) to the
running deployment. Run as the user that owns the repo checkout and can run
`docker`/`docker compose`.

> **Why this isn't just "rebuild the API":** the W4 release adds an EF migration
> (`AddBookingDispatch`). Production does **not** auto-migrate. The compose now has
> a one-shot **`migrator`** service that runs the bundled `efbundle` against the DB
> before the API starts, so `docker compose up` applies pending migrations safely
> and idempotently. The API image build was also fixed to include the `Contracts`
> project (it didn't before W4 and would now fail to build).

---

## Update steps

```bash
# 0. SSH in and go to the repo (where docker-compose.yml + .env live)
ssh <user>@13.140.150.148
cd /path/to/air-serbia-virtual

# 1. BACK UP THE DATABASE FIRST (the migration is additive, but always back up)
docker compose exec -T postgres pg_dump -Fc -U asv airserbiavirtua \
  > ~/asv-backup-$(date +%Y%m%d-%H%M).dump
ls -lh ~/asv-backup-*.dump            # confirm it exists and is non-empty

# 2. Record the current commit (for rollback) and pull the latest
CURRENT=$(git rev-parse HEAD); echo "rolling-back point = $CURRENT"
git fetch origin
git log --oneline -1 origin/dev       # newest commit should be the deploy/W4 work
git pull --ff-only origin dev         # use whatever branch this box deploys

# 3. Rebuild images (api now bakes in the efbundle; web gets the Briefing page)
docker compose build

# 4. Bring up — 'migrator' applies AddBookingDispatch, THEN api + web (re)start
docker compose up -d

# 5. Confirm the migration ran cleanly
docker compose logs migrator | tail -30   # expect "Applying migration '...AddBookingDispatch'" or "No migrations were applied"
docker compose ps                          # migrator -> Exit 0 ; postgres/api/web -> Up (healthy)
```

If `migrator` exits non-zero the API will NOT start (by design). Read its log,
fix the cause, re-run `docker compose up -d`. Your data is safe — see Rollback.

---

## Verify

```bash
# API (loopback; the host proxy fronts these publicly)
curl -fsS http://127.0.0.1:5000/health                       # {"status":"ok"}
curl -fsS http://127.0.0.1:5000/api/v1/routes | head -c 200  # routes JSON

# New W4 dispatch endpoints (replace <password> with ASL001's)
TOKEN=$(curl -s -X POST http://127.0.0.1:5000/api/v1/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"callsign":"ASL001","password":"<password>"}' \
  | python3 -c "import sys,json;print(json.load(sys.stdin)['tokens']['accessToken'])")
curl -fsS http://127.0.0.1:5000/api/v1/bookings/active -H "Authorization: Bearer $TOKEN"   # [] or bookings
```

Then a browser pass through your public domain:
- Website loads; pilot portal `/portal/briefing` renders.
- Mark a booking **dispatch ready** → READY badge appears (web) and the desktop
  **Pilot Centre** shows the resume-dispatch card → **Start flight** → ACARS Live.

---

## Rollback

The `AddBookingDispatch` migration only **adds a nullable column** — it is safe to
leave in place even if you roll the code back. To revert the *code*:

```bash
git checkout $CURRENT          # the SHA you printed in step 2
docker compose build && docker compose up -d
```

Only if you must also restore the database (rare):

```bash
docker compose exec -T postgres psql -U asv -d postgres -c "DROP DATABASE airserbiavirtua;"
docker compose exec -T postgres psql -U asv -d postgres -c "CREATE DATABASE airserbiavirtua OWNER asv;"
cat ~/asv-backup-YYYYMMDD-HHMM.dump | docker compose exec -T postgres pg_restore -U asv -d airserbiavirtua
```

---

## Notes

- Secrets live in `.env` next to `docker-compose.yml` (`DB_PASSWORD`, `JWT_KEY`,
  `WEB_ORIGIN`) — see `.env.example`. Never commit `.env`.
- `migrator` and `api` share the locally-built `asv-api:latest` image; always run
  `docker compose build` before `up` on an update so both get the new code.
- Desktop ACARS clients are installed per-pilot and are **not** part of this
  deploy — they just need to point at the public API URL.
- First-time/full bring-up (not an update) additionally needs the host reverse
  proxy + TLS — see `doc/deployment.md`.
