# SECURITY.md

Security posture for Accountrack. Security takes precedence over convenience (Core Principle 3).
Related: MULTI_TENANCY.md (isolation), ADR-0019/0020.

## 1. Authentication

**Phase 1 (MVP):**
- Email + password login. Passwords hashed with a memory-hard algorithm (**Argon2id**, or
  ASP.NET Core Identity's PBKDF2 if Argon2 is unavailable), never stored or logged in plaintext.
- **JWT access token**, short-lived (~15 min), signed (RS256 preferred; key from secret store).
- **Refresh token**, long-lived, **persisted, rotating, and revocable** with reuse detection:
  - Each refresh issues a new refresh token and invalidates the prior one.
  - Reuse of a consumed refresh token revokes the whole token family (theft response).
  - Tokens are revocable on logout, password change, and by admins.
- Account protections: lockout after N failed attempts (exponential backoff), password policy
  (length + breach-list check), optional email verification.

**Phase 2:** Google / Microsoft OAuth (OIDC). **Future:** MFA (TOTP), SSO.

### Token storage (frontend)
- Access token kept in memory (not localStorage).
- Refresh token in an **httpOnly, Secure, SameSite** cookie (CSRF-protected) — preferred — or a
  secure platform store. Decision recorded when the frontend auth flow is built.

## 2. Authorization (RBAC + SoD)

- **RBAC** (ADR-0019). Permissions are data, never hardcoded, named `Module.Action`, e.g.
  `Sales.View`, `Sales.Create`, `Sales.Approve`, `Accounting.Post`, `Inventory.Adjust`,
  `Accounting.PeriodClose`.
- Roles aggregate permissions; users get roles (and optionally direct permissions). Permission
  checks happen in the **authorization pipeline behavior**, not scattered in handlers.
- **Standard roles** are seeded per tenant (CHG-0076): **Administrator** (full access),
  **Accountant**, **Sales**, **Purchasing**, **Warehouse**, **Viewer** — see
  `StandardRoleDefinitions` for the default permission matrix. Roles are **dynamic and editable** in
  Settings → Roles & access (`Admin.Roles`): an admin can edit any role's permissions and
  create/rename/delete custom roles. Two guards prevent lock-out and tampering: the **Administrator**
  role is always full-access and immutable, and **built-in roles** cannot be renamed or deleted (only
  their permissions are editable); a role still assigned to users cannot be deleted.
- **Company scope**: every authorized action is checked against the active `CompanyId` ∈ the
  user's granted companies (see MULTI_TENANCY.md).
- **Frontend enforcement is UX only, never the security boundary (CHG-0127).** The SPA mirrors the
  same permissions: routes declare a required permission (`meta.permission`, inherited by children) and
  a global navigation guard redirects a user who lacks it to a **403 page**; the sidebar, ⌘K command
  palette, and write-action buttons are hidden by the same permission (one source of truth). This is
  defense-in-depth for usability — the **authoritative gate is the backend** `RequireAuthorization` on
  every endpoint, so a hand-crafted request from a user missing the permission is still rejected (403).

### Segregation of Duties (SoD)
- Sensitive verbs are **distinct permissions**: Create vs **Edit** vs **Cancel**/**Delete** vs Approve
  vs Post vs Pay. For transactional documents, editing a draft requires `Sales.Edit` / `Purchasing.Edit`
  and cancelling requires `Sales.Cancel` / `Purchasing.Cancel` — separate from `*.Create` (CHG-0075).
  **Master data** (and the Chart of Accounts, which is master data) likewise splits into
  `MasterData.Create` / `MasterData.Edit` / `MasterData.Delete` (deactivate) — replacing the former
  coarse `MasterData.Manage` (CHG-0081) — so the creator of a record need not be the one allowed to
  amend or deactivate it (BR-X-7/8).
- Business rule (BUSINESS_RULES.md): the user who **created/submitted** a financial document
  may not be the **sole approver**; approval/posting by the same user is blocked unless an admin
  explicitly grants a "self-approve" exception (audited).
- SoD violations are detectable in reports for compliance review.

## 3. Tenant & Data Isolation

See MULTI_TENANCY.md. Summary of the non-negotiables:
- Tenant from validated JWT only; never client-supplied.
- Global query filters on all tenant-owned entities.
- `IgnoreQueryFilters` banned outside reviewed admin paths (build-enforced).
- Per-company uniqueness; TenantId-leading indexes.
- Permanent cross-tenant isolation test suite.

## 4. Auditing & Non-Repudiation

- Immutable **Audit Log** (ADR-0006): user, timestamp, entity, action, before/after — captured
  automatically via EF SaveChanges interceptor; append-only (no update/delete path).
- Security events logged: login success/failure, lockout, token refresh/revocation, permission
  changes, period reopen, self-approve exceptions, `IgnoreQueryFilters` admin usage.
- Posted journals are immutable (ADR-0009) — financial non-repudiation by design.

## 5. Input, Transport & API Security

- **Validation** on every command (FluentValidation); reject unknown/oversized payloads.
- **Parameterized queries / EF** only; no string-concatenated SQL. Any raw SQL is reviewed and
  must include tenant predicates.
- **HTTPS everywhere**; HSTS in production; TLS termination at the edge Nginx.
- **Security headers — implemented** (CHG-0128) at the SPA's Nginx (`frontend/nginx.conf`), the
  internet-facing surface: `Content-Security-Policy` (`default-src 'self'`; `script-src 'self'` — the
  Vite build ships **no inline bootstrap script**, the module-preload polyfill is disabled for this
  reason; `style-src 'self' 'unsafe-inline'` for Vue/ECharts inline styles; `img-src 'self' data:`;
  self-hosted `font-src 'self'`; `frame-ancestors 'none'`; `object-src 'none'`; `form-action 'self'`),
  `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy:
  strict-origin-when-cross-origin`. Repeated in the `/assets/` location (Nginx does not inherit
  `add_header` into a location that sets its own).
- **CORS**: the SPA and API are **same-origin** (Nginx reverse-proxies `/api/` to the API container),
  so no cross-origin grant is configured — nothing to restrict.
- **Rate limiting — implemented** (CHG-0128): a per-client fixed window (`AuthRateLimiting`, .NET 8
  `AddRateLimiter`) on the anonymous auth endpoints `/auth/login`, `/auth/register`, `/auth/refresh`.
  Over-limit requests get `429` in the standard failure envelope (`RATE_LIMITED`) with `Retry-After`.
  Configurable under `RateLimiting:Auth` (default 20 / 60 s). The client key is the leftmost
  `X-Forwarded-For` hop (then `X-Real-IP`, then the socket) — **spoofable**, so fully robust per-IP
  limiting additionally requires the edge proxy to overwrite (not append) the client-facing hop.
- **CSRF** protection if cookies are used for auth.
- File uploads (if any): type/size validation, stored outside web root, scanned.

## 6. Secrets & Configuration

- No secrets, connection strings, or API keys in source or `appsettings.json`. Use environment
  variables / a secret store (e.g. user-secrets in dev, Key Vault / Docker secrets in prod).
- Separate config per environment (Local/Development/UAT/Production).
- JWT signing keys rotated; private keys never committed.

## 7. Concurrency & Integrity

- Optimistic concurrency (`RowVersion`) prevents lost updates (ADR-0021).
- Idempotent posting prevents duplicate financial effects on retries (ADR-0021).
- Money is decimal with defined scale and rounding policy (ACCOUNTING_DESIGN.md) — no floats.

## 8. Privacy

- PII (names, emails, contacts) is access-controlled and never logged in plaintext beyond what
  is necessary. Audit "before/after" for PII fields is access-restricted.
- Soft delete retains data for audit; a documented, permissioned hard-purge process exists for
  legal data-removal requests (operates above the tenant filter, fully audited).

## 9. Dependency & Supply-Chain Hygiene

- Pinned dependencies; automated vulnerability scanning (e.g. `dotnet list package
  --vulnerable`, `npm audit`) in CI.
- Container base images patched; minimal images for production.

## 10. Security Testing

- Isolation suite (MULTI_TENANCY.md §9).
- AuthZ tests: each protected endpoint denies without the required permission and across company
  scope.
- Auth tests: lockout, refresh rotation, reuse-detection revocation.
- Periodic `/security-review` on changed code before merge to main.

## 11. Threat Checklist (high-severity, ranked)

1. Cross-tenant data leak via query-filter bypass → §3, MULTI_TENANCY.md (Critical).
2. Privilege escalation / missing company-scope check → §2 (High).
3. Refresh-token theft/replay → §1 rotation + reuse detection (High).
4. Broken SoD enabling fraud (create+approve+pay by one user) → §2 (High).
5. Injection via raw SQL → §5 (Medium).
6. Secret leakage in config/logs → §6 (Medium).
