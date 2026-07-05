# DEPLOYMENT.md

Build, configuration, environments, and deployment for Accountrack. Stack: Docker, Docker
Compose, Nginx, GitHub, GitHub Actions (`CLAUDE.md`).

## 0. Quick start — self-hosted on a single VPS (CHG-0091)

The repo ships a ready stack: `Dockerfile.api`, `frontend/Dockerfile` + `frontend/nginx.conf`,
`docker-compose.yml`, and `.env.example`. It runs **PostgreSQL + API + SPA/Nginx**. TLS and
subdomain routing are **not** in this stack — your existing reverse proxy (the one already serving
your other subdomains) terminates TLS and forwards a subdomain to the one published `web` port.

```bash
git clone <repo> && cd Accountrack
cp .env.example .env
# Edit .env: POSTGRES_USER + strong POSTGRES_PASSWORD, a >=32-char JWT_SIGNING_KEY (openssl rand -base64 48),
# your ADMIN_EMAIL / ADMIN_PASSWORD, and WEB_PORT (default 8090).
docker compose up -d --build
```

First boot migrates every module schema and (with `SEED_ENABLED=true`) seeds the permission
catalog, standard roles, a working company (chart of accounts, posting rules, PPN 11%, system
accounts) and your administrator. Then point your reverse proxy at the stack, e.g.:

- **Nginx Proxy Manager / Traefik / Caddy** → proxy `accountrack.yourdomain.com` → `http://<host>:${WEB_PORT}`.
- The `web` container serves the SPA and reverse-proxies `/api` to the API (same origin, no CORS).
- Log in at your subdomain with `ADMIN_EMAIL` / `ADMIN_PASSWORD`. After first login, set
  `SEED_ENABLED=false` in `.env` and `docker compose up -d` to avoid re-seeding.

**Notes & limits**
- The app **refuses to start** outside Development if `Jwt:SigningKey` is missing/`<32` chars.
- Migrations auto-apply on boot here (single-VPS convenience). For a formal Prod pipeline, run
  migrations as a discrete, backed-up step instead (§4) and set `Database__AutoMigrate=false`.
- The public `/register` sign-up creates a tenant + company but does **not yet** provision that
  company's accounting — so use the seeded company above. Per-registration accounting provisioning
  is a known follow-up before multi-tenant SaaS use.
- If your reverse proxy runs in Docker, either publish the port (default) and target `host:WEB_PORT`,
  or attach the `web` service to your proxy's external network and route by container name.

### Local development in Docker

To run the whole app in Docker on your own machine (self-contained PostgreSQL + API + SPA, isolated
from any host PostgreSQL and the `dotnet run` / `npm run dev` servers), use `docker-compose.dev.yml`:

```bash
cp .env.example .env      # set POSTGRES_USER/POSTGRES_PASSWORD (dev defaults for the rest are fine)
docker compose -f docker-compose.dev.yml up -d --build
```

SPA → http://localhost:8090 · API/Swagger → http://localhost:8081/swagger · PostgreSQL →
`localhost:5433`. Runs in `Development` (Swagger on) and auto-migrates + seeds a working company +
admin on first boot. `down -v` wipes its isolated DB volume.

## 0.1 Integrating into an existing reverse-proxy compose (e.g. alongside n8n)

If your VPS already runs a compose stack with an Nginx that terminates TLS and routes subdomains
(serving other apps like n8n), add Accountrack **into that same compose** instead of the standalone
`docker-compose.yml`. Clone this repo into a subfolder (e.g. `./accountrack`) next to your compose,
then add these services under `services:` (they join the default network, so your Nginx reaches them
by name — just like `app`/`n8n`):

```yaml
  accountrack-db:
    image: postgres:16-alpine
    container_name: accountrack-db
    restart: unless-stopped
    mem_limit: 512m                     # PostgreSQL is light; ~256–512 MB is plenty
    environment:
      POSTGRES_DB: Accountrack
      POSTGRES_USER: ${ACCOUNTRACK_DB_USER}
      POSTGRES_PASSWORD: ${ACCOUNTRACK_DB_PASSWORD}
    volumes:
      - accountrack_pg:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U \"$$POSTGRES_USER\" -d \"$$POSTGRES_DB\""]
      interval: 10s
      timeout: 5s
      retries: 12
      start_period: 20s

  accountrack-api:
    build:
      context: ./accountrack
      dockerfile: Dockerfile.api
    container_name: accountrack-api
    restart: unless-stopped
    mem_limit: 400m
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ConnectionStrings__Default: "Host=accountrack-db;Port=5432;Database=Accountrack;Username=${ACCOUNTRACK_DB_USER};Password=${ACCOUNTRACK_DB_PASSWORD}"
      Jwt__SigningKey: ${ACCOUNTRACK_JWT_KEY}     # >= 32 chars; app refuses to start without it
      Jwt__Issuer: Accountrack
      Jwt__Audience: Accountrack
      Database__Initialize: "true"
      Database__AutoMigrate: "true"
      Seed__Enabled: "true"                        # set false after first boot
      Seed__AdminEmail: ${ACCOUNTRACK_ADMIN_EMAIL}
      Seed__AdminPassword: ${ACCOUNTRACK_ADMIN_PASSWORD}
    depends_on:
      accountrack-db:
        condition: service_healthy

  accountrack-web:
    build:
      context: ./accountrack/frontend
    container_name: accountrack-web
    restart: unless-stopped
    mem_limit: 100m
    expose:
      - "80"                            # internal only; your Nginx proxies to it
    depends_on:
      - accountrack-api
```

Add the volume under the top-level `volumes:` key:

```yaml
volumes:
  # ... your existing volumes ...
  accountrack_pg:
```

Add these to the `.env` next to your compose (never commit real secrets):

```
ACCOUNTRACK_DB_USER=accountrack
ACCOUNTRACK_DB_PASSWORD=<strong database password>
ACCOUNTRACK_JWT_KEY=<random, >=32 chars: openssl rand -base64 48>
ACCOUNTRACK_ADMIN_EMAIL=you@yourcompany.com
ACCOUNTRACK_ADMIN_PASSWORD=<strong admin password>
```

Finally add an Nginx server block (in your mounted conf) for the subdomain — mirror your n8n block,
proxying to the `accountrack-web` container:

```nginx
server {
    listen 443 ssl;
    http2 on;
    server_name accountrack.yvnalvworks.com;

    ssl_certificate     /etc/letsencrypt/live/accountrack.yvnalvworks.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/accountrack.yvnalvworks.com/privkey.pem;

    client_max_body_size 25m;           # CSV/Excel imports

    location / {
        proxy_pass http://accountrack-web:80;
        proxy_http_version 1.1;
        proxy_set_header Host              $host;
        proxy_set_header X-Real-IP         $remote_addr;
        proxy_set_header X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

Then: DNS `accountrack.yvnalvworks.com` → VPS, issue the cert (certbot), and
`docker compose up -d --build accountrack-db accountrack-api accountrack-web` + reload Nginx.

> **RAM:** PostgreSQL is light (~256–512 MB). With your existing services the VPS should be fine on
> **≥ 2 GB** total — much lower than the previous SQL Server requirement.

## 1. Environments

| Env | Purpose | Config source |
|---|---|---|
| **Local** | developer machine | `appsettings.Development.json` + user-secrets |
| **Development** | shared dev/integration | env vars + secret store |
| **UAT** | acceptance testing | env vars + secret store |
| **Production** | live | env vars + secret store |

Configuration files: `appsettings.json` (base, non-secret), `appsettings.Development.json`,
`appsettings.Production.json`. **Never** hardcode secrets, connection strings, or API keys —
inject via environment variables / Docker secrets / a vault (SECURITY.md §6).

## 2. Topology

```
            ┌─────────────────────────────┐
  client ──►│ Nginx (TLS, reverse proxy)  │
            └──────┬───────────────┬──────┘
                   │ /api          │ /
            ┌──────▼──────┐  ┌─────▼───────────┐
            │ Accountrack │  │ Vue SPA (static)│
            │   API (.NET)│  └─────────────────┘
            └──────┬──────┘
            ┌──────▼──────┐
            │ PostgreSQL  │
            └─────────────┘
  (future: Redis, RabbitMQ, Hangfire dashboard, OTEL collector)
```

- **Nginx** terminates TLS, serves the built SPA, and reverse-proxies `/api` to the API.
- **API** is the modular-monolith host (`Accountrack.Api`).
- **PostgreSQL** is the single database (shared schema, ADR-0004; provider per ADR-0032).

## 3. Containers
- `Dockerfile` (API): multi-stage build (`dotnet publish` → minimal runtime image).
- `Dockerfile` (frontend): build SPA (`npm run build`) → static assets served by Nginx.
- `docker-compose.yml`: api + db + nginx (+ frontend build) for local/dev.
- `docker-compose.prod.yml`: production overrides (secrets, replicas, resource limits).
- Use small, patched base images; run as non-root; no secrets baked into images.

## 4. Database Migrations (DATABASE.md §5)
- Per-module EF migrations, applied in dependency order.
- **Local/Dev:** auto-apply at startup behind a flag.
- **UAT/Prod:** applied by the pipeline as an explicit, gated step (e.g. a migration job) —
  **never** auto-apply silently in production. Back up before migrating.
- Idempotent seeders run after migration (permissions catalog, default CoA, `PPN11`, system
  accounts, default posting rules).

## 5. CI/CD (GitHub Actions) — implemented (CHG-0098, auto-deploy CHG-0100)

Three workflows in [`.github/workflows/`](../.github/workflows/):

### `test.yml` — reusable build + test suite
Called by both `ci.yml` and `deploy.yml` (`workflow_call`), so the gate is defined once:
- **Backend:** `dotnet restore` → `dotnet build -c Release` (warnings-as-errors) → `dotnet test`
  against a **PostgreSQL 16 service container** (env `ACCOUNTRACK_TEST_PG` so the cross-tenant
  isolation integration tests actually run instead of skipping).
- **Frontend:** `npm ci` → `npm run build` (`vue-tsc --noEmit && vite build`).

### CI — `ci.yml` (on pull requests)
Runs the reusable `test.yml` on every **pull request** — the quality gate for proposed changes.

### CD — `deploy.yml` (auto on push to `main`, GHCR images)
**Model:** build once in Actions, the VPS only **pulls** — nothing is compiled on the RAM-tight VPS.
Triggers **automatically on every push to `main`**, and is also runnable manually
(**Actions → Deploy → Run workflow**) with an image `tag` (e.g. to roll back). Jobs run in order:
1. **test:** the reusable `test.yml`. **Hard gate** — if it fails, nothing is built or shipped.
2. **build-push:** builds the API (`Dockerfile.api`, context `.`) and SPA (`./frontend`) images and
   pushes them to **GHCR** as `ghcr.io/<owner>/accountrack-api` and `…/accountrack-web`, tagged with
   both the run tag (`latest` on a push; the chosen tag on a manual run) and the commit SHA (Buildx
   layer cache via GitHub Actions cache).
3. **deploy:** SSHes into the VPS and runs `docker compose pull` + `up -d` for the two services.
   EF migrations apply automatically as the API container restarts (`Database__AutoMigrate=true`);
   idempotent seeders run only while `ACCOUNTRACK_SEED_ENABLED=true`.

The `deploy` job targets the **`production`** GitHub Environment — add *required reviewers* to it in
repo settings to turn auto-deploy into **approve-then-deploy** (a click gates the VPS step).

### One-time setup

**a) Repository secrets** (Settings → Secrets and variables → Actions):

| Secret | Value |
|--------|-------|
| `VPS_HOST` | VPS IP/hostname (e.g. `157.230.242.234`) |
| `VPS_USER` | SSH user (e.g. `root`) |
| `VPS_SSH_KEY` | **private** key whose public half is in the VPS's `~/.ssh/authorized_keys` |
| `VPS_APP_DIR` | dir holding the compose file (e.g. `/root/app`) |

Create a dedicated deploy key: `ssh-keygen -t ed25519 -f deploy_key -N ""` → put `deploy_key.pub`
into the VPS `~/.ssh/authorized_keys`, paste `deploy_key` (private) into `VPS_SSH_KEY`.

**b) Switch the VPS compose from `build:` to `image:`** so the VPS pulls instead of builds — replace
the two Accountrack services' `build:` blocks with image refs (keep everything else):
```yaml
  accountrack-api:
    image: ghcr.io/yvnalv/accountrack-api:${ACCOUNTRACK_TAG:-latest}
    # (remove the build: block; keep environment/expose/networks/depends_on)
  accountrack-web:
    image: ghcr.io/yvnalv/accountrack-web:${ACCOUNTRACK_TAG:-latest}
    # (remove the build: block; keep expose/depends_on)
```
`ACCOUNTRACK_TAG` (optional, in `.env`) pins which tag runs; the deploy job exports it per run.

**c) Let the VPS pull the images.** Either make the two GHCR packages **public** (GitHub → your
profile → Packages → each package → visibility → Public — then no VPS login needed), **or** keep them
private and log in once on the VPS with a PAT that has `read:packages`:
```bash
echo "<GHCR_PAT>" | docker login ghcr.io -u yvnalv --password-stdin
```

### Deploying & rolling back
- **Deploy:** Actions → **Deploy** → *Run workflow* → set `tag` (`latest`, or a version like `v1.3.0`)
  → Run. (Approve the `production` environment if you enabled reviewers.)
- **Roll back:** re-run **Deploy** with a previous tag/SHA, or on the VPS set
  `ACCOUNTRACK_TAG=<old-sha>` in `.env` and `docker compose up -d accountrack-api accountrack-web`.
  Because posted data is immutable and migrations are additive, prefer rolling **forward** with a fix
  unless a migration must be reverted (restore from backup — §8 — before down-migrating).

## 6. Configuration Keys (representative, all non-secret values in appsettings; secrets via env)
```
ConnectionStrings:Default        (env/secret)
Jwt:Issuer / Audience / KeyId    ; signing key via secret store
Jwt:AccessTokenMinutes / RefreshTokenDays
Cors:AllowedOrigins
Database:AutoMigrate             (true only in Local/Dev)
Seed:Enabled
Email:* (provider settings)      ; credentials via secret store
```

## 7. Observability
- Structured logging (JSON) with correlation/tenant/user context.
- Health checks (`/health`, `/health/ready`) for DB and dependencies.
- Future: OpenTelemetry traces/metrics, centralized logs (ElasticSearch).

## 8. Backups & DR

**Automated nightly backups** (implemented, CHG-0100) — [`scripts/backup-postgres.sh`](../scripts/backup-postgres.sh)
`pg_dump`s each database from the dockerized `postgres` container to a gzipped, timestamped file and
prunes anything older than the retention window.

**One-time setup on the VPS:**
```bash
chmod +x /root/projects/AccounTrack/scripts/backup-postgres.sh
mkdir -p /root/backups

# Install the nightly cron (02:00 Asia/Jakarta). `crontab -e` and add:
CRON_TZ=Asia/Jakarta
0 2 * * * /root/projects/AccounTrack/scripts/backup-postgres.sh >> /var/log/accountrack-backup.log 2>&1
```
Defaults (override via env in the cron line): `PG_CONTAINER=postgres`, `PG_SUPERUSER=abc`,
`BACKUP_DIR=/root/backups`, `RETENTION_DAYS=14`, `DATABASES="accountrack postgresdb"`.

**Verify / run once now:**
```bash
/root/projects/AccounTrack/scripts/backup-postgres.sh
ls -lh /root/backups
```

**Restore a database** (destructive — it overwrites the target; back up first if unsure):
```bash
gunzip -c /root/backups/accountrack_YYYYmmdd-HHMMSS.sql.gz \
  | docker exec -i postgres psql -U abc -d accountrack
```
The dumps use `--clean --if-exists`, so they drop-and-recreate objects and restore cleanly over an
existing database. **Take a fresh backup before any production migration.**

**Recommended hardening (follow-ups):**
- **Offsite copy** — after the dump, `rclone copy /root/backups <remote>:accountrack-backups` (or
  `scp`) so a lost VPS doesn't lose the backups. Add it as a line in the script or a second cron.
- **Test restores periodically** — restore the latest dump into a scratch database and check the
  trial balance balances.
- **PITR (future)** — WAL archiving / `pg_basebackup` for point-in-time recovery once RPO/RTO targets
  are set with the business.

## 9. Secrets
- Dev: `dotnet user-secrets`. Dev/UAT/Prod: environment variables / Docker secrets / vault.
- JWT signing keys rotated; private keys never committed; `.gitignore` excludes secret files.
