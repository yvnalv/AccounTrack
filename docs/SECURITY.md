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
- **Company scope**: every authorized action is checked against the active `CompanyId` ∈ the
  user's granted companies (see MULTI_TENANCY.md).

### Segregation of Duties (SoD)
- Sensitive verbs are **distinct permissions**: Create vs Approve vs Post vs Pay.
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
- **HTTPS everywhere**; HSTS in production; TLS termination at Nginx.
- Security headers: CSP, X-Content-Type-Options, X-Frame-Options/frame-ancestors, Referrer-
  Policy.
- **CORS** restricted to known frontend origins.
- **Rate limiting** on auth and sensitive endpoints (brute-force protection).
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
