# DATABASE_ACCESS.md — Connecting to the PostgreSQL database

How to reach the Accountrack database safely — from a terminal (`psql`) or a GUI (pgAdmin) — on the
VPS and locally. Written for someone who has **not used SSH tunnelling before**.

Related: [VPS_DEPLOYMENT_GUIDE.md](VPS_DEPLOYMENT_GUIDE.md) (how the stack is deployed),
[DEPLOYMENT.md](DEPLOYMENT.md), [SECURITY.md](SECURITY.md), [DATABASE.md](DATABASE.md) (schema design).

---

## 1. Why the database is not exposed to the internet

The production Postgres port is **deliberately not published publicly**. A database on a public IP is
scanned and credential-stuffed within hours of going live; a strong password becomes the only thing
between an attacker and every tenant's financial records.

So the rule is: **never bind Postgres to `0.0.0.0`.** All remote access goes through SSH, which is
already hardened and authenticated. This costs one extra command per session and removes an entire
class of attack.

| Approach | Verdict |
|---|---|
| Publish `5432:5432` (public) | ❌ **Never.** Exposes the DB to the whole internet. |
| **SSH tunnel** (loopback publish + `ssh -L`) | ✅ **Recommended.** No new public ports; reuses SSH auth. |
| `docker exec … psql` | ✅ Fine for quick queries; no setup at all. |
| Firewall-pinned public port | 🟡 Better than open, but your IP changes and the service is still exposed. |
| pgAdmin web app on the VPS | 🟡 Works, but adds another internet-facing app to patch. |
| Tailscale / WireGuard VPN | ✅ Good alternative if you want several devices connected. |

---

## 2. What is actually running (reference)

| Thing | Value |
|---|---|
| Stack directory (real `docker-compose.yml` + `.env`) | `/root/app` |
| Repo clone (source only, build context) | `/root/projects/AccounTrack` |
| Postgres container | `postgres` (shared with n8n) |
| Accountrack database | `accountrack` |
| Accountrack DB role (app login) | `accountrack` — password in `/root/app/.env` as `ACCOUNTRACK_DB_PASSWORD` |
| Instance superuser | `abc` (see `POSTGRES_USER` in the stack compose) |
| Other database on the instance | `postgresdb` (n8n) |
| API / SPA services | `accountrack-api`, `accountrack-web` |

> **Run Compose from `/root/app`, not from the repo clone.** The repo also contains a standalone
> `docker-compose.yml` for local use; running Compose from `/root/projects/AccounTrack` picks up that
> file and fails with `required variable POSTGRES_USER is missing a value`.

> ⚠️ **Never run `docker compose down` in `/root/app`** — that stack also runs **n8n** and
> **Postgres**. Only ever `stop` / `up -d` a named service.

Accountrack's data spans 14 schemas: `accounting`, `approval`, `audit`, `billing`, `company`,
`expenses`, `identity`, `inventory`, `masterdata`, `notification`, `process`, `purchasing`, `sales`,
`platform`.

---

## 3. Option A — `psql` in the container (no setup)

Quickest path for a one-off query. Run on the VPS:

```bash
docker exec -it postgres psql -U accountrack -d accountrack
```

Useful once inside:

```
\dn                 -- list schemas
\dt identity.*      -- tables in a schema
\d  sales."SalesOrders"   -- describe a table
\q                  -- quit
```

```sql
SELECT "Id","Email","FullName" FROM identity."Users";
SELECT "Id","Name" FROM company."Tenants";
```

For instance-wide work (both databases, roles), connect as the superuser instead:
`docker exec -it postgres psql -U abc -d postgres`.

> Identifiers are **PascalCase**, so they must be double-quoted in SQL: `identity."Users"`, not
> `identity.users`.

---

## 4. Option B — pgAdmin over an SSH tunnel (recommended GUI)

### What a tunnel actually is

SSH can carry other traffic inside the encrypted connection you already use to log in. You tell your
PC: *"anything arriving at `localhost:5544`, send it through SSH and hand it to port 5432 at the far
end."* pgAdmin then talks to `localhost:5544` believing it is a local database. **No port is opened on
the internet** — that is the entire security benefit.

```
pgAdmin ──► localhost:5544 ══[ encrypted SSH ]══► VPS ──► 127.0.0.1:5432 (postgres)
   your PC                                                    the VPS
```

### Step 1 (one time, on the VPS) — publish Postgres to loopback only

Edit the stack compose file:

```bash
nano /root/app/docker-compose.yml
```

Find the service block that begins `postgres:` (the one with `container_name: postgres`) and add a
`ports:` key **inside that block**. Indentation is what matters: `ports:` sits **4 spaces** in, level
with `image:` and `environment:`; the value sits **6 spaces** in.

```yaml
  postgres:
    image: postgres:16
    container_name: postgres
    restart: always
    ports:                                 # ← add (4 spaces)
      - "127.0.0.1:5432:5432"              # ← add (6 spaces, keep the quotes)
    environment:
      POSTGRES_USER: abc
      ...
```

The `127.0.0.1:` prefix is the whole point — it binds the VPS's loopback interface only, unreachable
from outside. Plain `"5432:5432"` would bind `0.0.0.0` and expose the database publicly.

Check the syntax (this only parses; it changes nothing), then apply:

```bash
cd /root/app
docker compose config >/dev/null && echo "YAML OK"
docker compose up -d postgres
docker compose ps postgres
```

You want to see **`127.0.0.1:5432->5432/tcp`**. If it ever reads `0.0.0.0:5432->5432/tcp`, the prefix
is missing — fix it immediately.

Data lives in the `postgres_data` named volume, so recreating the container loses nothing. Postgres
restarts for a few seconds; the API and n8n reconnect on their own.

### Step 2 (one time, on your PC) — SSH keys instead of a password

Not strictly required, but it is the single biggest hardening win for a public-facing VPS. In
**PowerShell on your own machine**:

```powershell
ssh-keygen -t ed25519                      # accept the defaults; a passphrase is optional
type $env:USERPROFILE\.ssh\id_ed25519.pub | ssh root@157.230.242.234 "mkdir -p ~/.ssh && cat >> ~/.ssh/authorized_keys"
```

Open a **new** window and confirm `ssh root@157.230.242.234` logs in without asking for a password.
**Only after that works**, disable password logins on the VPS (`PasswordAuthentication no` in
`/etc/ssh/sshd_config`, then `systemctl restart ssh`). Verifying first is what stops you locking
yourself out.

### Step 3 (each session, on your PC) — open the tunnel

Open **PowerShell on your own computer** — not your VPS terminal. This is the most common mistake.

```powershell
ssh -L 5544:127.0.0.1:5432 root@157.230.242.234
```

Read it as: *local port 5544* → *(through the VPS)* → *127.0.0.1:5432 as seen from the VPS*.

You land on a normal VPS prompt. It looks like nothing happened, but the tunnel is live. **Leave the
window open** — closing it or typing `exit` closes the tunnel and disconnects pgAdmin. Minimise it.

> Add `-N` (`ssh -N -L 5544:…`) for a tunnel with no shell. The window then looks frozen with no
> prompt — that is correct. `Ctrl+C` closes it.

Port `5544` is arbitrary; it just has to be free on your PC. If you already run a local Postgres on
5432, avoid that one.

### Step 4 — register the server in pgAdmin

Install pgAdmin if needed (<https://www.pgadmin.org/download/>). Then:

1. Right-click **Servers** → **Register** → **Server…**
2. **General** tab → **Name:** `Accountrack VPS` (just a label).
3. **Connection** tab:

   | Field | Value |
   |---|---|
   | Host name/address | `127.0.0.1` |
   | Port | `5544` ← the **tunnel** port, not 5432 |
   | Maintenance database | `accountrack` |
   | Username | `accountrack` |
   | Password | `ACCOUNTRACK_DB_PASSWORD` from `/root/app/.env` |
   | Save password | ✔ |

4. **Save.**

Get the password on the VPS with:

```bash
grep ACCOUNTRACK_DB_PASSWORD /root/app/.env
```

Browse via `Servers → Accountrack VPS → Databases → accountrack → Schemas`. To view rows:
`Schemas → identity → Tables` → right-click `Users` → **View/Edit Data → All Rows**.

*(Alternative: pgAdmin's built-in **SSH Tunnel** tab does steps 3–4 in one place — Connection host
`127.0.0.1:5432`, Tunnel host `157.230.242.234`, user `root`, your key. It still needs Step 1.)*

### Daily use

1. PowerShell → run the `ssh -L 5544:…` command → leave it open.
2. pgAdmin → double-click `Accountrack VPS`.

---

## 5. Local development

No tunnel needed — Postgres runs on your machine:

```
Host=localhost;Port=5432;Database=Accountrack_Dev;Username=postgres;Password=postgres
```

In pgAdmin: host `localhost`, port `5432`, database `Accountrack_Dev`, user `postgres`.
(`docker-compose.dev.yml` can supply this if you prefer a container.)

---

## 6. Troubleshooting

| Symptom | Cause & fix |
|---|---|
| `required variable POSTGRES_USER is missing a value` | You ran Compose from the repo clone. Use `cd /root/app`. |
| pgAdmin: `Connection refused` | Tunnel window closed, or Step 1 not applied. Check `docker compose ps postgres` shows `127.0.0.1:5432->5432/tcp`. |
| `bind: Address already in use` | Local port 5544 is taken. Use another (`5545`) and match it in pgAdmin. |
| `password authentication failed for user "accountrack"` | Password mismatch — re-copy from `/root/app/.env`, no quotes or trailing spaces. |
| `database "accountrack" does not exist` | Dropped and not yet recreated. Connect with Maintenance database `postgres` to get in. |
| `ssh: connect to host … Connection timed out` | Offline, or you ran `ssh -L` **on the VPS** instead of your PC. |
| Terminal frozen, no prompt | Expected with `-N`. The tunnel is working. |
| `psql: relation "users" does not exist` | Identifiers are PascalCase and need quotes: `identity."Users"`. |

---

## 7. Rules

**Do**
- Keep Postgres bound to `127.0.0.1` and reach it through SSH.
- Use SSH keys; disable password authentication once keys are verified.
- Consider a **read-only role** for routine browsing, so day-to-day poking cannot mutate production.
- Take a backup before any destructive work (`scripts/backup-postgres.sh`) and check the file is
  non-zero before proceeding.

**Don't**
- Publish Postgres on `0.0.0.0` / plain `"5432:5432"`.
- Run `docker compose down` in `/root/app` (it would stop n8n and Postgres too).
- Commit `.env` or paste credentials into docs, tickets or chat.
- Run ad-hoc `UPDATE`/`DELETE` against production without a backup and a `SELECT` first —
  posted accounting records are meant to be immutable and corrected by reversal (BR-ACC-3).
