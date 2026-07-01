# DEPLOYMENT.md

Build, configuration, environments, and deployment for Accountrack. Stack: Docker, Docker
Compose, Nginx, GitHub, GitHub Actions (`CLAUDE.md`).

## 0. Quick start — self-hosted on a single VPS (CHG-0091)

The repo ships a ready stack: `Dockerfile.api`, `frontend/Dockerfile` + `frontend/nginx.conf`,
`docker-compose.yml`, and `.env.example`. It runs **SQL Server + API + SPA/Nginx**. TLS and
subdomain routing are **not** in this stack — your existing reverse proxy (the one already serving
your other subdomains) terminates TLS and forwards a subdomain to the one published `web` port.

```bash
git clone <repo> && cd Accountrack
cp .env.example .env
# Edit .env: strong MSSQL_SA_PASSWORD, a >=32-char JWT_SIGNING_KEY (openssl rand -base64 48),
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

## 0.1 Integrating into an existing reverse-proxy compose (e.g. alongside n8n)

If your VPS already runs a compose stack with an Nginx that terminates TLS and routes subdomains
(serving other apps like n8n), add Accountrack **into that same compose** instead of the standalone
`docker-compose.yml`. Clone this repo into a subfolder (e.g. `./accountrack`) next to your compose,
then add these services under `services:` (they join the default network, so your Nginx reaches them
by name — just like `app`/`n8n`):

```yaml
  accountrack-db:
    image: mcr.microsoft.com/mssql/server:2022-latest
    container_name: accountrack-db
    restart: unless-stopped
    mem_limit: 2g                       # SQL Server needs ~2 GB; ensure the VPS has room
    environment:
      ACCEPT_EULA: "Y"
      MSSQL_SA_PASSWORD: ${ACCOUNTRACK_SA_PASSWORD}
      MSSQL_PID: Developer
      MSSQL_MEMORY_LIMIT_MB: "1536"     # cap SQL's own usage under the container limit
    volumes:
      - accountrack_mssql:/var/opt/mssql
    healthcheck:
      test: ["CMD-SHELL", "/opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P \"$$ACCOUNTRACK_SA_PASSWORD\" -Q 'SELECT 1' -b -o /dev/null"]
      interval: 10s
      timeout: 5s
      retries: 18
      start_period: 40s

  accountrack-api:
    build:
      context: ./accountrack
      dockerfile: Dockerfile.api
    container_name: accountrack-api
    restart: unless-stopped
    mem_limit: 400m
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ConnectionStrings__Default: "Server=accountrack-db;Database=Accountrack;User Id=sa;Password=${ACCOUNTRACK_SA_PASSWORD};TrustServerCertificate=True;Encrypt=False"
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
  accountrack_mssql:
```

Add these to the `.env` next to your compose (never commit real secrets):

```
ACCOUNTRACK_SA_PASSWORD=<strong SA password>
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

> **RAM:** SQL Server wants ~2 GB. With your existing services (~1 GB of limits) the VPS should have
> **≥ 4 GB** total. If it's a small box, this is the main thing to check before deploying.

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
            │ SQL Server  │
            └─────────────┘
  (future: Redis, RabbitMQ, Hangfire dashboard, OTEL collector)
```

- **Nginx** terminates TLS, serves the built SPA, and reverse-proxies `/api` to the API.
- **API** is the modular-monolith host (`Accountrack.Api`).
- **SQL Server** is the single database (shared schema, ADR-0004).

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

## 5. CI/CD (GitHub Actions)

**CI (on PR):**
1. Restore + build (warnings-as-errors).
2. Lint/format check (`dotnet format --verify-no-changes`, ESLint/Prettier).
3. Unit + architecture + contract tests.
4. Integration tests (SQL Server via Testcontainers/service container).
5. Frontend build + Vitest.
6. Dependency vulnerability scan (`dotnet list package --vulnerable`, `npm audit`).
7. Security review of flagged changes (raw SQL / `IgnoreQueryFilters`).

**CD:**
- Build + push images on merge to `main` (Dev), tag/release for UAT and Production.
- Promote the same artifact across environments (build once, deploy many).
- Production deploy is gated/approved; runs migrations as a discrete step; supports rollback to
  the previous image.

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
- Regular automated SQL Server backups (full + differential/log) with tested restore.
- Migrations preceded by a backup in UAT/Prod.
- Documented restore procedure and RPO/RTO targets (to be set with the business).

## 9. Secrets
- Dev: `dotnet user-secrets`. Dev/UAT/Prod: environment variables / Docker secrets / vault.
- JWT signing keys rotated; private keys never committed; `.gitignore` excludes secret files.
