# VPS Deployment Guide — Accountrack alongside an existing Docker stack

A complete, reproducible runbook for deploying Accountrack onto a VPS that **already runs a Docker
Compose stack** with its own **PostgreSQL** and an **Nginx reverse proxy** terminating TLS for other
subdomains (e.g. an app + n8n). Accountrack **reuses your existing PostgreSQL** (a dedicated role +
database inside it) and is fronted by your **existing Nginx** on its own subdomain.

This guide is written against a concrete, working example so nothing is left implicit:

| Thing | Example value (adjust to yours) |
|------|-------------------------------|
| VPS public IP | `157.230.242.234` |
| Subdomain | `accountrack.yvnalvworks.com` |
| Existing stack dir (holds `docker-compose.yml`, `.env`, `nginx/`) | `/root/app` |
| Where you clone this repo | `/root/projects/AccounTrack` |
| Existing Postgres container / superuser | container `postgres`, superuser `abc` |
| Existing TLS cert | one **SAN** cert named `yvnalvworks.com` (`certbot`, `standalone`) |

> **Do not run `docker compose down`** at any point — that stops your `app`, `n8n`, and `postgres`
> too. Everything below uses `docker compose up -d <service>` so your other services keep running.

---

## 0. Prerequisites & architecture

**You need:** SSH/root on the VPS; the existing Postgres superuser password; access to your DNS
provider (Hostinger in this example); and the Accountrack repo pushed to GitHub (deploy from `main`).

**What gets added:** two containers on your existing Compose default network —
- `accountrack-api` (.NET 8) — connects to your existing `postgres` container, migrates + seeds on
  first boot. Gets the network alias **`api`** so the SPA can reach it at `http://api:8080`.
- `accountrack-web` (Vue SPA + internal Nginx) — serves the built SPA and reverse-proxies `/api` →
  `api:8080`. Only `expose`d internally; your central Nginx proxies the subdomain to it.

```
Browser ──HTTPS──▶ your Nginx (yvnalvworks-nginx, :443)
                     │  server_name accountrack.yvnalvworks.com
                     ▼
                accountrack-web:80  ──/api──▶  accountrack-api:8080 (alias: api)
                                                    │
                                                    ▼
                                          postgres:5432  (DB: accountrack, role: accountrack)
```

There are **7 things** to do, all before the final `up`: (1) merge compose, (2) clone repo,
(3) create DB role+db, (4) create `.env`, (5) DNS record, (6) TLS cert, (7) Nginx block → then up.

---

## 1. Merge the two services into your existing `docker-compose.yml`

Add the two `accountrack-*` services under `services:`, and add `- accountrack-web` to your Nginx
`depends_on`. **Everything else stays exactly as you had it.** Full file (your services unchanged):

```yaml
services:
  app:
    build:
      context: .
      dockerfile: Dockerfile
    container_name: yvnalvworks-app
    restart: unless-stopped
    mem_limit: 250m
    environment:
      - NODE_ENV=production
      - PORT=3000
    expose:
      - "3000"

  nginx:
    image: nginx:1.27-alpine
    container_name: yvnalvworks-nginx
    restart: unless-stopped
    mem_limit: 100m
    depends_on:
      - app
      - n8n
      - accountrack-web          # ← added
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./nginx/${NGINX_CONF:-default.conf}:/etc/nginx/conf.d/default.conf:ro
      - ./nginx/ssl:/etc/nginx/ssl:ro
      - /etc/letsencrypt:/etc/letsencrypt:ro

  postgres:
    image: postgres:16
    container_name: postgres
    restart: always
    mem_limit: 300m
    environment:
      POSTGRES_USER: abc
      POSTGRES_PASSWORD: Test@123
      POSTGRES_DB: postgresdb
    volumes:
      - postgres_data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U yvnalvworks"]
      interval: 10s
      timeout: 5s
      retries: 5

  n8n:
    image: n8nio/n8n:latest
    container_name: n8n
    restart: unless-stopped
    mem_limit: 400m
    expose:
      - "5678"
    environment:
      - DB_TYPE=postgresdb
      - DB_POSTGRESDB_HOST=postgres
      - DB_POSTGRESDB_PORT=5432
      - DB_POSTGRESDB_DATABASE=postgresdb
      - DB_POSTGRESDB_USER=yvnalvworks
      - DB_POSTGRESDB_PASSWORD=Test@123_
      - N8N_HOST=n8n.yvnalvworks.com
      - N8N_PORT=5678
      - N8N_PROTOCOL=https
      - WEBHOOK_URL=https://n8n.yvnalvworks.com/
      - N8N_EDITOR_BASE_URL=https://n8n.yvnalvworks.com/
      - N8N_SECURE_COOKIE=true
      - N8N_PROXY_HOPS=1
      - GENERIC_TIMEZONE=Asia/Jakarta
      - N8N_DIAGNOSTICS_ENABLED=false
    depends_on:
      postgres:
        condition: service_healthy
    volumes:
      - n8n_data:/home/node/.n8n

  # ─────────────────────────── Accountrack API (.NET 8) ───────────────────────────
  accountrack-api:
    build:
      context: ${ACCOUNTRACK_DIR:-./accountrack}
      dockerfile: Dockerfile.api
    container_name: accountrack-api
    restart: unless-stopped
    mem_limit: 512m
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      DOTNET_gcServer: "0"
      ConnectionStrings__Default: "Host=postgres;Port=5432;Database=${ACCOUNTRACK_DB:-accountrack};Username=${ACCOUNTRACK_DB_USER:-accountrack};Password=${ACCOUNTRACK_DB_PASSWORD:?set ACCOUNTRACK_DB_PASSWORD in .env}"
      Jwt__SigningKey: ${ACCOUNTRACK_JWT_SIGNING_KEY:?set ACCOUNTRACK_JWT_SIGNING_KEY in .env (>=32 chars)}
      Jwt__Issuer: Accountrack
      Jwt__Audience: Accountrack
      Database__Initialize: "true"
      Database__AutoMigrate: "true"
      Seed__Enabled: ${ACCOUNTRACK_SEED_ENABLED:-true}
      Seed__AdminEmail: ${ACCOUNTRACK_ADMIN_EMAIL:?set ACCOUNTRACK_ADMIN_EMAIL in .env}
      Seed__AdminPassword: ${ACCOUNTRACK_ADMIN_PASSWORD:?set ACCOUNTRACK_ADMIN_PASSWORD in .env}
    expose:
      - "8080"
    networks:
      default:
        aliases:
          - api                    # the SPA container proxies /api → http://api:8080
    depends_on:
      postgres:
        condition: service_healthy

  # ─────────────────────────── Accountrack SPA (Vue + nginx) ──────────────────────
  accountrack-web:
    build:
      context: ${ACCOUNTRACK_DIR:-./accountrack}/frontend
    container_name: accountrack-web
    restart: unless-stopped
    mem_limit: 64m
    expose:
      - "80"
    depends_on:
      - accountrack-api

volumes:
  postgres_data:
  n8n_data:
```

Notes:
- The **`api` network alias** is required — the SPA container's internal Nginx hard-codes
  `proxy_pass http://api:8080`.
- `DOTNET_gcServer: "0"` uses workstation GC (smaller RAM footprint on a shared VPS).
- The two required-var markers (`${…:?set … in .env}`) make Compose **fail fast** if `.env` is
  incomplete — that's intentional.

---

## 2. Clone the repo (mind the path & case)

The build `context` is resolved **relative to the directory containing `docker-compose.yml`**
(here `/root/app`), unless you set an absolute `ACCOUNTRACK_DIR`. Clone it **outside** your `app/`
folder so it isn't swept into the `app` service's `context: .` build.

```bash
cd /root/projects                                            # your code folder (create it if needed)
git clone https://github.com/yvnalv/AccounTrack.git          # → /root/projects/AccounTrack
```

> **Gotcha (we hit both):**
> - `git clone` names the folder after the repo → **`AccounTrack`** (capital A, capital T), *not*
>   `accountrack`. Linux paths are **case-sensitive**.
> - Folder is `projects` (plural) in this example — use your real path.
>
> Find the true path any time with:
> ```bash
> find /root -maxdepth 4 -name Dockerfile.api 2>/dev/null
> # → /root/projects/AccounTrack/Dockerfile.api  ⇒  ACCOUNTRACK_DIR=/root/projects/AccounTrack
> ```

You'll set `ACCOUNTRACK_DIR` to that repo-root directory in step 4.

---

## 3. Create Accountrack's DB role + database (one-time)

Your `postgres` volume is already initialized, so the image's init scripts won't run — create the
role + database manually, as the superuser (`abc`). Choose a **strong, unique** password (not
`Test@123`); it must match `ACCOUNTRACK_DB_PASSWORD` in step 4.

```bash
docker exec -it postgres psql -U abc -d postgresdb \
  -c "CREATE ROLE accountrack LOGIN PASSWORD 'STRONG_UNIQUE_DB_PASSWORD';" \
  -c "CREATE DATABASE accountrack OWNER accountrack;"
```

The app role only needs to own its own database (it creates its per-module schemas itself) — no
superuser rights at runtime.

---

## 4. Create the `.env` (next to `docker-compose.yml`)

Compose auto-loads `.env` from the compose directory. Create it:

```bash
cd /root/app
nano .env
```

Paste and fill in the four `CHANGE_ME` values:

```dotenv
# ── Accountrack — VPS secrets. DO NOT commit. ──────────────────────────────
ACCOUNTRACK_DIR=/root/projects/AccounTrack        # repo root from step 2 (exact case!)
ACCOUNTRACK_DB=accountrack
ACCOUNTRACK_DB_USER=accountrack
ACCOUNTRACK_DB_PASSWORD=CHANGE_ME_strong_db_password   # == step 3
ACCOUNTRACK_JWT_SIGNING_KEY=CHANGE_ME_random_48        # openssl rand -base64 48   (>=32 chars)
ACCOUNTRACK_ADMIN_EMAIL=admin@yvnalvworks.com
ACCOUNTRACK_ADMIN_PASSWORD=CHANGE_ME_login_password    # your web-app login
ACCOUNTRACK_SEED_ENABLED=true                          # first boot; set false afterwards
```

Save/exit nano: **Ctrl+O**, **Enter**, **Ctrl+X**. Generate the JWT key with `openssl rand -base64 48`.

Lock permissions and verify Compose can read every required var:

```bash
chmod 600 .env                                    # owner-only read/write (secrets)
docker compose config >/dev/null && echo "OK: .env + paths good"
```

> `chmod 600` = only your user can read the file (it holds DB password, JWT key, admin password). A
> default `644` would let other accounts on the box read your secrets. Purely a security step; not
> required for Compose to work.
>
> If `docker compose config` errors with `set ACCOUNTRACK_… in .env`, a required var is still blank.

---

## 5. DNS — point the subdomain at the VPS

In your DNS provider (Hostinger example), add an **A record** mirroring your `n8n` one:

| Type (Jenis) | Name (Nama) | Points to (Konten) | TTL |
|---|---|---|---|
| `A` | `accountrack` | `157.230.242.234` | `300` |

Wait a few minutes, then confirm from the VPS (must return your IP before the cert step):

```bash
dig +short accountrack.yvnalvworks.com        # → 157.230.242.234
```

---

## 6. TLS — expand your existing SAN certificate

Check what you already have:

```bash
sudo certbot certificates
```

In this setup there is **one multi-domain (SAN) certificate** named `yvnalvworks.com` covering
`yvnalvworks.com`, `www.yvnalvworks.com`, `n8n.yvnalvworks.com`, issued with the **`standalone`**
authenticator (confirm via `sudo cat /etc/letsencrypt/renewal/yvnalvworks.com.conf` →
`authenticator = standalone`). **Add the new subdomain to that same cert** (one cert, one renewal).

Standalone binds port 80, so free it briefly by stopping Nginx, then restart:

```bash
sudo docker stop yvnalvworks-nginx

sudo certbot certonly --cert-name yvnalvworks.com --standalone --key-type ecdsa --expand \
  -d yvnalvworks.com -d www.yvnalvworks.com -d n8n.yvnalvworks.com -d accountrack.yvnalvworks.com

sudo docker start yvnalvworks-nginx
```

- Re-list **all** existing domains **plus** the new one — certbot replaces the cert's domain set, so
  omitting any would drop it.
- `--key-type ecdsa` matches the existing cert; `--expand` updates it in place.
- The cert path stays `/etc/letsencrypt/live/yvnalvworks.com/…` and now covers accountrack too.

**Persist the renewal hooks** (standalone renewal needs port 80 free; `--dry-run` proves hooks but
doesn't save them). Append them to the renewal config once:

```bash
sudo tee -a /etc/letsencrypt/renewal/yvnalvworks.com.conf >/dev/null <<'EOF'
pre_hook = docker stop yvnalvworks-nginx
post_hook = docker start yvnalvworks-nginx
EOF

# verify renewal works end-to-end (stops nginx, renews, starts nginx):
sudo certbot renew --cert-name yvnalvworks.com --dry-run
```

> **Why not `--webroot`?** With Nginx running *inside a container*, the host has no served webroot
> dir (`/var/www/certbot does not exist`) unless you create + mount it and add an
> `location /.well-known/acme-challenge/` block. Standalone is simpler for this setup and matches how
> the cert is already issued/renewed.

---

## 7. Nginx — add the subdomain server block

Edit your mounted conf (`/root/app/nginx/default.conf`) and add this **alongside** your existing
blocks (keep everything else). It uses the **shared** cert path (same as n8n) and matches your style:

```nginx
server {
    listen 443 ssl http2;
    server_name accountrack.yvnalvworks.com;

    ssl_certificate /etc/letsencrypt/live/yvnalvworks.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/yvnalvworks.com/privkey.pem;

    client_max_body_size 25m;               # allow CSV/XLSX imports

    location / {
        proxy_pass http://accountrack-web:80;

        proxy_http_version 1.1;

        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto https;

        proxy_read_timeout 120s;
    }
}

server {
    listen 80;
    server_name accountrack.yvnalvworks.com;
    return 301 https://$host$request_uri;
}
```

> **Do not reload Nginx yet** — a static `proxy_pass http://accountrack-web:80` is resolved at
> reload time, so if the `accountrack-web` container isn't running yet you'll get
> *"host not found in upstream accountrack-web"*. Bring the containers up first (step 8), **then**
> reload.

---

## 8. Build, start, and wire up Nginx

Build + start **only** the two new services (leaves app/n8n/postgres running). First build pulls the
.NET SDK + Node images and compiles both apps — a few minutes.

```bash
cd /root/app
docker compose up -d --build accountrack-api accountrack-web

docker compose ps accountrack-api accountrack-web          # both "Up"
docker compose logs -f accountrack-api                     # watch migrate + seed; Ctrl-C at "Now listening on: http://+:8080"
```

Now that `accountrack-web` exists, apply the Nginx block:

```bash
docker exec yvnalvworks-nginx nginx -t                     # "syntax is ok" (http2 warnings are harmless)
docker exec yvnalvworks-nginx nginx -s reload
```

> The `the "listen ... http2" directive is deprecated` lines are **warnings, not errors** — they also
> apply to your existing n8n/app blocks. Ignore for now (optional cleanup: split into
> `listen 443 ssl;` + `http2 on;`).

---

## 9. Verify

```bash
curl -sI https://accountrack.yvnalvworks.com | head -1     # HTTP/2 200
```

Open **https://accountrack.yvnalvworks.com** → log in with `ACCOUNTRACK_ADMIN_EMAIL` /
`ACCOUNTRACK_ADMIN_PASSWORD`. On first boot the API created all module schemas in the `accountrack`
database, seeded the chart of accounts / posting rules / PPN 11% / a demo company, and your admin.

To exercise a full business cycle and read the resulting reports, follow
[END_TO_END_GUIDE.md](END_TO_END_GUIDE.md).

---

## 10. Turn off re-seeding (after first successful login)

So it doesn't attempt to re-seed on every restart:

```bash
# set ACCOUNTRACK_SEED_ENABLED=false in /root/app/.env, then:
cd /root/app
docker compose up -d accountrack-api
```

Migrations still apply automatically on future boots (`Database__AutoMigrate=true`); only the initial
catalog/admin seeding is skipped.

---

## 11. Updating to a new version

```bash
cd /root/projects/AccounTrack
git pull                                                   # get the latest main
cd /root/app
docker compose up -d --build accountrack-api accountrack-web
```

New EF migrations apply on boot. No `down` needed; your other services are untouched.

---

## 12. Inspecting the VPS database from local pgAdmin (SSH tunnel)

Never expose PostgreSQL publicly. Reach it over SSH.

**a) Publish Postgres on the VPS loopback only** — add to the `postgres` service and re-create it:
```yaml
  postgres:
    # ...
    ports:
      - "127.0.0.1:5432:5432"        # loopback only — NEVER 0.0.0.0 / "5432:5432"
```
```bash
cd /root/app && docker compose up -d postgres
```

**b) Open an SSH tunnel from your machine** (map to a free local port; `5432` is often taken by a
local Postgres, so use `5544`):
```bash
ssh -L 5544:127.0.0.1:5432 root@157.230.242.234
```
Leave that terminal open.

**c) Connect in pgAdmin** → Register → Server:
- Connection: Host `127.0.0.1`, Port `5544`, Maintenance DB `accountrack`, Username `accountrack`,
  Password = your `ACCOUNTRACK_DB_PASSWORD`.
- For the whole instance (incl. n8n's `postgresdb`), connect as superuser `abc` / `Test@123`.

*(Alternative: pgAdmin's built-in **SSH Tunnel** tab — Connection host `127.0.0.1:5432`, Tunnel host
`157.230.242.234`, user `root`, your SSH key — still needs step (a)'s loopback publish.)*

---

## 13. Troubleshooting (everything we actually hit)

| Symptom | Cause & fix |
|--------|-------------|
| `unable to prepare context: path "…/accountrack" not found` | `ACCOUNTRACK_DIR` is wrong. Paths are **case-sensitive** and the repo folder is `AccounTrack`. Run `find /root -maxdepth 4 -name Dockerfile.api` and set `ACCOUNTRACK_DIR` to that file's directory. |
| `docker compose up` fails with `set ACCOUNTRACK_DB_PASSWORD in .env` (etc.) | `.env` missing/incomplete. The `${VAR:?…}` markers are hard-required. Fill them; `docker compose config >/dev/null`. |
| `certbot: /var/www/certbot does not exist` | `--webroot` needs a served dir; with dockerized Nginx use `--standalone` (stop/start nginx) instead. |
| `nginx: [emerg] host not found in upstream accountrack-web` | You reloaded Nginx before the container existed. Run `docker compose up -d … accountrack-web` first, then reload. |
| Renewal will fail in ~90 days | `standalone` needs port 80 free. Add `pre_hook`/`post_hook` (stop/start nginx) to the renewal conf (step 6). |
| `the "listen ... http2" directive is deprecated` | Warning only — safe to ignore. Optional: `listen 443 ssl;` + `http2 on;`. |
| API can't connect to DB | Role/DB not created (step 3), or password mismatch between step 3 and `.env`. |
| Don't do this | **Never** `docker compose down` (stops app/n8n/postgres). **Never** publish Postgres on `0.0.0.0`. **Never** reuse `Test@123` or commit `.env`. |

---

## Related docs
[DEPLOYMENT.md](DEPLOYMENT.md) (environments & the standalone stack) ·
[END_TO_END_GUIDE.md](END_TO_END_GUIDE.md) (using the app) ·
[SECURITY.md](SECURITY.md) · [DATABASE.md](DATABASE.md)
