# SUBSCRIPTION_BILLING.md — Monetization & subscription billing

> **Status:** Design ratified (2026-07-11) — gateway, tax posture, and grace behavior decided (see §3,
> §10, §6); **ADR-0039** records the gateway pick. Not yet implemented (no code/schema). This documents
> *how Accountrack charges its own tenants* — a commercial/platform concern, distinct from the ERP's
> Sales/AR features that tenants use to bill *their* customers. Figures marked *(illustrative)* are
> placeholders; figures marked *(verify)* must be re-checked against the provider's current
> pricing/features at implementation time (research current as of the 2026-01 knowledge cutoff;
> onboarding facts in §3.4 re-verified 2026-07-11).

Related: [ARCHITECTURE.md](ARCHITECTURE.md), [MULTI_TENANCY.md](MULTI_TENANCY.md),
[SECURITY.md](SECURITY.md), [ACCOUNTING_DESIGN.md](ACCOUNTING_DESIGN.md) (PPN/e-Faktur),
[INTEGRATION_EVENTS.md](INTEGRATION_EVENTS.md) (inbox/outbox), [DECISIONS.md](DECISIONS.md).

---

## 1. Purpose & scope

Let a prospective customer **sign up, pick a plan, and pay Accountrack directly from the website**, then
keep access as long as the subscription is paid — self-serve, recurring, Indonesia-first (IDR).

In scope: plans/pricing, payment providers, the subscription lifecycle, entitlement enforcement, dunning,
tax on the subscription, and the module/data design.
Out of scope: the ERP's own invoicing (that already exists), and payroll/tax filing for tenants.

**Key framing — two different "invoices":**
| | Who bills whom | Where it lives |
|---|---|---|
| **Sales Invoice** (existing) | a tenant → *their* customer | Sales module, tenant's GL |
| **Billing Invoice** (new) | Accountrack → *a tenant* | new Billing module, our books |

Never conflate them. The Billing module is our commercial ledger; it must not post into any tenant's GL.

---

## 2. Business model (decided 2026-07-10)

- **Dimension: hybrid — tier + included seats + per-seat add-on.** Each plan tier bundles a base price, an
  included user-seat count and feature/usage limits; extra active users bill as a per-seat add-on. This is
  the mainstream mature-SaaS model and scales revenue with account size without punishing small teams.
- **Cadence: monthly + annual, with an annual discount** (annual ≈ "2 months free", i.e. ~17% off), plus a
  **free trial** (default **14 days**, no charge; card optional at trial start — see §6).
- **Currency: IDR** (single functional currency, matching ADR-0013). IDR is effectively zero-decimal —
  store minor units carefully (see §9).
- **Example tiers *(illustrative — finalize before launch)*:**

  | Plan | Base / month | Included seats | Companies | Extra seat/mo | Notable limits |
  |---|---|---|---|---|---|
  | **Starter** | Rp 149k | 2 | 1 | Rp 59k | core ERP, 1 company |
  | **Business** | Rp 499k | 8 | 3 | Rp 49k | + approvals, multi-warehouse, exports |
  | **Enterprise** | Rp 1.49jt | 25 | unlimited | Rp 39k | + priority support, SSO (future) |

  Annual = monthly × 10 (2 months free). Numbers are placeholders for a pricing exercise, not a decision.

- **Entitlements** each plan encodes (enforced in-app, §7): max active users (included + purchased),
  max companies, and feature flags (approvals, import/export, advanced reports, API access…).

---

## 3. Payment provider — comparison & recommendation

Requirement: collect **IDR** from Indonesian SMBs using the methods they actually use (QRIS, Virtual
Account, e-wallets, cards), support **recurring**, pay out to a **local bank**, and expose **webhooks**.

### 3.1 Why not Stripe / global-only processors
Stripe has the best DX but **does not onboard Indonesia-domiciled businesses** for payouts *(verify)* and
is card-centric (no QRIS/VA/e-wallet for local buyers). Viable only if the company incorporates abroad —
out of scope for an ID entity. Same limitation for most US/EU-only processors.

### 3.2 Merchant-of-Record (Paddle / Lemon Squeezy / FastSpring)
They resell the product and **handle global sales tax/VAT + chargebacks + invoicing**; you receive a net
payout. Pros: zero tax/e-Faktur burden, strong international-card support, fast to integrate. Cons:
**no Indonesian local rails** (no QRIS/VA/e-wallet), **higher effective fees** (~5%+ vs ~1–3%), foreign
payout/FX, and less control over the checkout/dunning UX. **Poor fit for an IDR-first SMB product**;
reconsider only if the primary market becomes international.

### 3.3 Local gateways — Xendit vs Midtrans (the realistic choice)

Both are reputable, PCI-DSS Level 1, sandbox-friendly, and cover the local methods. Differences that
matter for a **subscription** product:

| Dimension | **Xendit** | **Midtrans** (GoTo/Gojek) |
|---|---|---|
| Local methods | QRIS, VA (major banks), e-wallets, cards, retail, PayLater | QRIS, VA, e-wallets (strong GoPay), cards, retail, PayLater |
| Recurring / subscriptions | **Recurring/Subscriptions + Payment Tokens** (card & some e-wallet); Plans/Schedules | **Subscription API** + card tokenization (**one-click / recurring**) |
| Hosted checkout | **Invoices** (hosted pay page, all methods, expiry, email) | **Snap** (hosted popup/redirect, all methods) |
| Invoice-based billing | **First-class Invoices API** (great for per-cycle VA/QRIS billing) | Core API + Snap; invoice UX is more DIY |
| Developer experience | Generally regarded as cleaner API/docs for SaaS | Very widely used; solid, slightly more transaction-oriented |
| Webhooks | Yes (signed callbacks, per-event) | Yes (HTTP notification + signature key) |
| Fees *(verify — typical ranges)* | QRIS ~0.7%, VA ~Rp4–5k flat, e-wallet ~1.5–2%, card ~2.9%+ | similar order of magnitude |
| Payout | IDR to local bank, T+ settlement | IDR to local bank, T+ settlement |

**Decision (2026-07-11 — ADR-0039): adopt Xendit as the primary gateway**, because our model leans on
**invoice-based per-cycle billing** (VA/QRIS) *and* tokenized auto-charge, and Xendit's first-class
**Invoices** + **Recurring** APIs map most directly onto that (least custom glue). **Midtrans remains a
fully acceptable alternative** kept viable by the port (choose it if GoPay/Snap reach or an existing GoTo
relationship dominates). The Billing module is built behind a **provider-abstraction port**
(`IPaymentGateway`) so switching or running a second provider later is mechanical. Cost: **free to start,
pay-per-transaction only** (no setup/monthly fee).

> Action before first live charge: open a sandbox account, run a QRIS + VA + card + recurring test, and
> **re-verify current fees/settlement** and the auth detail in §3.4 against Xendit's live docs.

### 3.4 Xendit onboarding & integration mechanics (verified 2026-07-11 — re-verify at build)

Concrete facts that drive the Phase 0/1 plan. **Two decoupled gates — sandbox is instant; live needs
document activation:**

- **Sign-up → instant Test Mode, no approval, no documents.** Creating an account immediately grants a
  sandbox pre-funded with a **IDR 1,000,000,000** fake balance. The *entire* integration (hosted checkout,
  QRIS/VA/e-wallet/card, recurring, webhooks) is buildable and testable here. **Consequence: Phase 0 and
  most of Phase 1 can be built before the operating business is even registered.**
- **Live Mode → requires account activation** via legal-document review in the Dashboard; Xendit reviews
  and emails when activated. Only then do real charges/payouts (IDR → local bank, T+ settlement) work.
  - **PT / CV / PMA (company):** **NIB** (Nomor Induk Berusaha — replaces old TDP/SIUP), latest deed /
    director-appointment **Akta**, **SK Menkumham** (Ministry of Law & Human Rights decree), company
    **NPWP**, director **KTP**, settlement **bank account**; some industries need extra licenses. Docs
    must be complete, legible, uncensored, all pages.
  - **Individual / Perorangan:** lighter — KTP + NPWP + bank account (feature set may be narrower).
  - **PKP (VAT) status is a *separate* registration** — not required to onboard to Xendit (aligns with
    our "Not PKP yet" posture, §10).

**API keys & authentication.**
- Dashboard → **Settings → Developers → API Keys** → **Generate Secret Key**; set per-product
  permissions (None / Read / **Write** — we need **Write on Money-in**), confirm with password.
- **Separate key pairs for Test and Live** (dashboard mode toggle, top-right). Secret keys are prefixed
  **`xnd_development_…`** (test) / **`xnd_production_…`** (live). A **secret key is shown once** at
  creation — capture it straight into config/secrets (never source — CLAUDE.md Non-Negotiables).
- Auth is **HTTP Basic**: the **secret key as the username, blank password** *(verify)*. The **public
  key** only tokenizes cards client-side (keeps us at PCI SAQ-A, §8). Optional **IP whitelisting** for
  API calls.
- Keep both test and live keys behind the `IPaymentGateway` config; rotate periodically; restrict to
  least permission.

**Webhooks (source of truth for "paid", §5).**
- Configure the callback URL in **Dashboard → Settings → Developers → Callbacks/Webhooks** (per event
  type). Test and live have their own configuration.
- Every webhook carries an **`x-callback-token`** header — a **per-account shared secret** read from the
  dashboard. **Verify it on every request and reject mismatches** (this is the anti-spoofing mechanism;
  it is a shared token, not a per-body HMAC signature).
- **Retries: up to 6× with exponential backoff** on any non-2xx response → handlers **must be idempotent**
  (reuse `platform.InboxState`, §5). Respond **2xx fast**, do work async. Webhooks can be manually
  re-triggered from the dashboard.

---

## 4. Payment methods & recurring strategy (decided: both)

Truly *automatic* charging each cycle requires **tokenization** (works for **cards**, and linked
**e-wallets**). **QRIS / VA / retail are push payments** — "recurring" there means **issue an invoice each
period and the customer pays it**. We support **both**, per plan-holder preference:

1. **Auto-charge (tokenized)** — card or linked e-wallet saved at checkout; the scheduler charges the
   token each cycle. Lowest churn, best UX. Card data never touches our servers (§8).
2. **Invoice-based** — each cycle the scheduler creates a hosted **Invoice** (VA/QRIS/e-wallet/card
   selectable); the customer pays; the **webhook** activates the next period. Covers the majority of ID
   SMBs who don't want to save a card.
3. **Manual bank transfer (optional, later phase)** — offline transfer + admin-confirmed activation, for
   customers who insist. Lowest automation; gate behind an admin action.

Dunning differs by mode: auto-charge retries the token; invoice-based re-sends/extends the invoice (§6.4).

---

## 5. Architecture — a new **Billing** module (modular monolith)

A new bounded context `Modules/Billing/` (Domain/Application/Infrastructure/Api), same clean-architecture
rules as every other module. It **owns its schema** (`billing.` table prefix) and communicates only via
integration events / application contracts (ADR-0007). It must **never** write to a tenant's business
schema or GL.

**Platform vs tenant scope (important tenancy nuance):** most tables are tenant-owned and honor the global
query filters (MULTI_TENANCY.md). But billing is also read by a **platform/back-office** actor (us) across
all tenants for MRR/ops. Model subscription/billing rows as **tenant-scoped** (a tenant sees only its own
billing) **plus** a small set of `Platform.*` permissions for cross-tenant back-office views that use the
reviewed `IgnoreQueryFilters` admin path — do **not** widen the ambient tenant wall (Rule 33).

**Building blocks reused:**
- **Inbox de-dup** (`platform.InboxState`, from the outbox work, CHG-0083) for **idempotent webhook**
  processing — a gateway may deliver the same event more than once.
- **Transactional outbox** for emitting `SubscriptionActivated` / `SubscriptionPastDue` etc. to
  Notification (dunning emails) and Identity (entitlements).
- **QuestPDF** (already used for documents) to render billing invoices/receipts as PDF.
- **Hangfire** (future-infra, ROADMAP Phase 4) for the recurring scheduler + dunning retries. Until it
  lands, a hosted `BackgroundService` timer can drive the cycle (acceptable at low tenant counts).

**Webhook-first is the rule:** the **gateway webhook is the source of truth** for "paid", never the
browser redirect (users close tabs; redirects are spoofable). The redirect only updates UI optimistically;
the webhook flips subscription state. Verify the signature; process idempotently; ACK fast (200) and do
work async.

---

## 6. Subscription lifecycle

### 6.1 States
`trialing → active → past_due → canceled` (plus `unpaid`/`expired` terminal, and `paused` optional).
- **trialing** — full access, no charge, until `trialEnd`.
- **active** — paid; `currentPeriodEnd` in the future.
- **past_due** — a charge/invoice failed or lapsed; **grace period** (e.g. 7 days) with in-app banner +
  emails. **Decided (2026-07-11): access is READ-ONLY during grace** — the tenant may view/export its
  data but business *writes* are blocked (`SUBSCRIPTION_REQUIRED`), applying real payment pressure while
  avoiding data-loss complaints.
- **canceled** — user canceled; access until `currentPeriodEnd` (`cancelAtPeriodEnd = true`), then
  **expired** (locked out; data retained for a **90-day** retention window (decided 2026-07-11), then
  purge per policy).

### 6.2 Self-serve happy path
1. **Sign up** — reuse the existing `/register` (tenant + company + admin already provisioned, CHG-0078).
2. **Choose plan + cycle** (monthly/annual) on a pricing page.
3. **Start trial** (14 days). Card optional: "no card to start" maximizes conversion; "card required"
   reduces trial abuse — a launch decision (§12).
4. **Collect payment** at trial end (or immediately if annual/no-trial): hosted checkout →
   auto-charge token *or* first invoice.
5. **Webhook confirms** → subscription `active`, entitlements unlocked, receipt PDF emailed.
6. **Renewals** — scheduler auto-charges (tokenized) or issues the next invoice N days before period end.

### 6.3 Plan changes (proration)
- **Upgrade** — take effect immediately; **prorate** the remainder of the period (charge the difference
  now) or add to next invoice. Seat increases likewise.
- **Downgrade / seat decrease** — take effect **at period end** (credit-free) to avoid refunds and keep it
  simple; block downgrades that violate current usage (e.g. can't drop below active users/companies).
- **Cancel** — `cancelAtPeriodEnd`; offer a win-back/pause option.

### 6.4 Dunning (failed payment)
- Auto-charge: retry schedule (e.g. day 1, 3, 5, 7) with emails each attempt; on final failure → `past_due`
  then `unpaid`/lock.
- Invoice-based: reminders before due + after; extend/reissue the invoice; same grace → lock timeline.
- Always: clear in-app banner with a one-click "update payment / pay now".

---

## 7. Entitlement enforcement (gating access)

- A single **subscription-status guard** (middleware / authorization policy) resolves the caller's tenant
  subscription once per request (cached) and enforces:
  - **Hard lock** when `expired`/`unpaid`: allow only auth, billing, and read-only export/self-service;
    block business writes with a clear `SUBSCRIPTION_REQUIRED` error envelope.
  - **Soft state** when `past_due` (grace): full or read-only access + banner (a policy toggle).
  - **Plan limits**: creating the (N+1)th user/company beyond the plan fails with `PLAN_LIMIT_REACHED` and
    an upsell; feature-flagged endpoints check the plan's feature set.
- Entitlements are derived from the subscription + plan and exposed to the SPA (so it hides/disables gated
  UI) — but the **backend is the hard wall** (mirrors the RBAC approach, SECURITY.md §2).
- Keep this **orthogonal to RBAC**: RBAC = "may this user do X"; entitlements = "does this tenant's plan
  include X / is it paid". Both must pass.

---

## 8. Security & PCI

- **Never store card data.** Use **hosted checkout** (Xendit Invoice / Midtrans Snap) or client-side
  tokenization; store only the gateway **token/customer/subscription ids**. This keeps us at **PCI
  SAQ-A** (the lightest scope).
- **Verify webhook signatures** (provider callback token / HMAC) and restrict source IPs where possible;
  treat unsigned/failed-verification callbacks as hostile.
- **Idempotent** webhook handling via `platform.InboxState` (a replayed event must not double-activate or
  double-count revenue).
- Secrets (API keys, webhook tokens) via configuration/secrets, never in source (CLAUDE.md Non-Negotiables).
- Log billing events to the **audit trail** (immutable), including state transitions and who initiated
  plan changes.

---

## 9. Data model (sketch — `billing.` schema, GUID PKs, standard audit fields)

- **`Plan`** — `Code`, `Name`, `Interval` (Monthly|Annual), `BasePriceMinor` (IDR minor units),
  `IncludedSeats`, `PerSeatPriceMinor`, `MaxCompanies`, `Features` (jsonb), `IsActive`, `IsPublic`.
- **`Subscription`** — `TenantId`, `PlanId`, `Status`, `Interval`, `Quantity` (purchased seats),
  `TrialEndsAt`, `CurrentPeriodStart/End`, `CancelAtPeriodEnd`, `GatewayCustomerId`,
  `GatewaySubscriptionId`, `PaymentMode` (AutoCharge|Invoice|Manual).
- **`PaymentMethod`** — `TenantId`, `GatewayTokenId`, `Brand`/`Last4`/`Expiry` (display only), `IsDefault`.
  **No PAN/CVV — ever.**
- **`BillingInvoice`** — `TenantId`, `SubscriptionId`, `Number`, `PeriodStart/End`, `SubtotalMinor`,
  `TaxMinor` (PPN), `TotalMinor`, `Currency`, `Status` (Draft|Open|Paid|Void|Uncollectible), `DueDate`,
  `GatewayInvoiceId`, `PaidAt`, `PdfRef`.
- **`PaymentTransaction`** — `BillingInvoiceId`, `GatewayPaymentId`, `Method` (QRIS|VA|Ewallet|Card|
  Transfer), `AmountMinor`, `Status`, `RawPayloadRef`, timestamps.
- **`WebhookEvent`** — `Provider`, `EventId`, `Type`, `ReceivedAt`, `ProcessedAt`, `Signature`,
  de-duped via inbox.

**Money:** store integer **minor units** + `Currency`; IDR has effectively no sub-unit in practice —
standardize on "minor = rupiah" (scale 0) or 2-dp with `.00`, and document it once so display/rounding is
consistent. Reuse the SharedKernel `Money` where possible.

### API surface (sketch, `/api/v1`)
- Public: `GET /billing/plans`.
- Tenant (auth): `GET /billing/subscription`, `POST /billing/subscription` (subscribe/checkout),
  `POST /billing/subscription/change` (upgrade/downgrade/seats), `POST /billing/subscription/cancel`,
  `GET /billing/invoices`, `GET /billing/invoices/{id}/pdf`, `POST /billing/payment-method` (tokenize).
- Webhook (no auth, signature-verified): `POST /billing/webhooks/{provider}`.
- Platform back-office (`Platform.Billing`): cross-tenant subscription/MRR views.

---

## 10. Tax & compliance (Indonesia)

- **Decided (2026-07-11): the operating entity is NOT PKP yet** → **no PPN is charged on subscriptions
  for now**, and **no e-Faktur** is issued. Phase 1 checkout shows a flat price (simplifies the build —
  no tax computation). The data model still keeps `BillingInvoice.TaxMinor` and reserved tax fields so
  PPN can be switched on **without a migration** once/if the entity registers as PKP.
- **When the entity becomes PKP** (future): a SaaS subscription is generally subject to **PPN 11%** and
  you may be required to issue **e-Faktur**. At that point decide **tax-inclusive vs. price + PPN**
  (recommended: **price + PPN shown explicitly**, matching the ERP's own VAT model) and show it clearly
  at checkout and on the billing invoice. (It is worth Accountrack itself modelling this correctly.)
- **B2B withholding (PPh 23)** may apply when the customer is a company that withholds — the billing
  invoice/receipt should be structured to accommodate it.
- Keep **billing invoices sequential + immutable** with a proper numbering series (like the ERP's document
  numbering) for audit; store the buyer's tax id (NPWP) when provided.
- Refund policy, cancellation terms, and data-retention/deletion must be stated in **Terms** and a
  **Privacy Policy** on the site (also a Play/marketing prerequisite). Consumer-protection (UU PK) applies.
- Consult a local tax advisor before launch; treat the above as design intent, not tax advice.

---

## 11. Metrics & reporting (back-office)
Track from day one: **MRR/ARR**, active/trialing/past_due/churned counts, **trial→paid conversion**,
**churn** & **net revenue retention**, ARPA, failed-payment rate, and revenue by plan/method. These live in
the platform back-office (cross-tenant), not in any tenant's dashboard.

---

## 12. Phased rollout

- **Phase 0 — Foundations & decision:** sandbox both gateways; confirm fees/e-Faktur; ratify **ADR-0039**
  (provider + `IPaymentGateway` port). Finalize plans/prices + Terms/Privacy.
- **Phase 1 — MVP billing:** Plans, Subscription, hosted checkout (one gateway), **invoice-based** cycle,
  webhook activation (idempotent), entitlement guard (hard lock + plan limits), billing invoice PDF,
  Settings → Billing screen, 14-day trial. Manual bank-transfer fallback optional.
  Delivered in slices:
  - ✅ **Slice 1 — foundation (CHG-0138):** `billing.` schema, global `Plan` catalog, tenant-scoped
    `Subscription`/`BillingInvoice`, read-only API, `Billing.View`/`Billing.Manage`.
  - ✅ **Slice 2 — entitlement guard + trial (CHG-0140):** `ITenantEntitlements` contract +
    `EntitlementResolver` (status → access level, plan limits, feature flags);
    `POST /billing/subscription/trial` (14-day, no card) and `GET /billing/entitlements`; host
    middleware blocking business **writes** when past-due/unpaid/expired. **Dark-launched** —
    `Billing:Entitlements:Enforce` defaults to **false**, and a tenant with **no subscription** is
    always unrestricted, so enabling billing can never lock out existing customers.
  - 🔜 **Slice 3 — Xendit adapter:** `IPaymentGateway` + hosted checkout + idempotent webhooks
    (needs a free Xendit sandbox signup; no PT/bank account required to build).
  - 🔜 **Slice 4 — Billing UI + invoice PDF** (pause for the UI/UX discussion first).
- **Phase 2 — Auto-charge & dunning:** tokenized card/e-wallet auto-charge, proration on upgrade, dunning
  retries + emails, annual cycle + discount, seat add-ons.
- **Phase 3 — Growth:** coupons/promos, referrals, tax/e-Faktur automation, multi-gateway, back-office MRR
  analytics, PayLater, win-back/pause.

## 13. Decisions & open items

**Resolved (2026-07-11):**
1. ✅ **Gateway = Xendit** (primary, behind `IPaymentGateway`); Midtrans kept viable via the port, no
   second provider at launch — **ADR-0039**.
3. ✅ **Tax = not PKP yet** → no PPN / no e-Faktur on subscriptions for now; tax fields reserved so it
   switches on without migration (§10).
4. ✅ **Grace = read-only + banner** while `past_due`; hard lock at `expired`; **90-day** retention before
   purge (§6, §7).

**Still open (non-blocking for Phase 1 build):**
2. Trial: card-required vs. not (default assumed **no-card, 14 days** — a config toggle, §6.2).
5. Exact plan names, prices, seat inclusions, and feature-to-tier mapping (a pricing exercise; the schema
   treats plans as data, so this is seedable without code changes).
6. Legal: Terms of Service, Privacy Policy, refund/cancellation policy — **owner + local tax/legal
   advisor to be assigned** (a launch prerequisite, parallel track).

## 14. References
Provider docs to verify at build time: Xendit (Invoices, Recurring, Payment Tokens, Webhooks); Midtrans
(Snap, Core API, Subscription API, HTTP Notification). Indonesian tax: PPN/e-Faktur (DJP). PCI-DSS SAQ-A.
