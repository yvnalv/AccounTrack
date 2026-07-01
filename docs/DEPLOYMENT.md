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
