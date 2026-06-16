# UI/UX References

Inspiration sources and **specifically what we take from each** (so borrowing stays deliberate, not
wholesale copying). Save the actual image into [references/](references/) and link it here.

> Note: links to Dribbble/Behance can rot or be JS-gated (not machine-readable). Always save a
> screenshot alongside the link so the reference survives and can be analysed pixel-by-pixel.

---

## R1 — Decko CRM Dashboard

- **Source:** https://dribbble.com/shots/21901467-Decko-CRM-Dashboard
- **Screenshots:** `decko-crm-dashboard.webp` (full, angled), `-2.webp` (flat, head-on),
  `-3.webp` (full layout thumbnail), `sidebar-reference.webp` (nav close-up),
  `card-reference.webp` (cards + chart close-up).
- **Status:** ✅ analysed.

### Observed design language (approx values from the screenshots — verify before locking)

- **Overall:** dark left sidebar + light content canvas; airy, soft, rounded, generous whitespace.
- **Signature accent:** a vivid **lime / chartreuse green** (~`#A3E635`). Used for: active nav pill
  (lime fill, near-black text + filled icon), chart bars, progress fills, the calendar "today" dot,
  and the brand mark. Bold and energetic.
- **Sidebar:** near-black (~`#0F1012`); brand = lime circle lettermark "D" + wordmark; collapse
  chevron; **search field with a `⌘K` pill**; muted uppercase section labels ("MAIN MENU",
  "SETTINGS"); line-icon nav; a promo/event card pinned at the bottom.
- **Canvas / cards:** very light gray canvas (~`#F4F5F7`), **pure white cards**, large corner radius
  (~16–20px), hairline border / very soft shadow, ~20–24px padding, a `⋯` action at top-right.
- **KPI tiles:** small gray label → large bold number → a **delta chip** (green ▲ `+15%` / red ▼
  `-9%`) with a tiny boxed icon.
- **Charts:** rounded-top bars in lime over faint gray "track" bars; dotted gridlines; **black
  rounded tooltip** with white text; rounded lime progress bars.
- **Typography:** rounded geometric grotesque (looks like Plus Jakarta Sans / General Sans /
  Satoshi family); bold large headings & numbers; **numbers in ID/EU format** (`21.978`,
  `$64.981,97`) — conveniently matches our Indonesia-first locale.
- **Top bar:** greeting + subtitle on the left; **dark-mode (moon) toggle**, notifications bell,
  and user avatar+email on the right.

### What we take
- The **app shell** (dark sidebar + light canvas + slim top bar), the **KPI tile + delta chip**
  pattern, **soft white cards w/ large radius**, **rounded charts + black tooltip**, and the **⌘K
  search**. We keep Decko's *role usage* of a signature accent but swap **Decko's lime → our brand
  teal `#007E6E`** (better fit for finance; see [DESIGN_LANGUAGE.md](DESIGN_LANGUAGE.md) §1).

### What we adapt / do NOT take
- **CRM information architecture** (customers-centric home, "deals") → replaced with finance KPIs
  (cash, AR/AP, overdue, stock) + recent documents + pending approvals.
- **Spacious-everywhere density** → we keep this register for the **dashboard & detail headers**,
  but pair it with a **dense, tabular register** for lists/ledgers/document lines (an accountant's
  daily screens). See [DESIGN_LANGUAGE.md](DESIGN_LANGUAGE.md).
- **Lime used heavily** → in finance, green carries meaning (paid/credit/positive). We use lime as
  the **brand/primary** accent *sparingly* and define a **separate semantic palette** so a lime
  "Post" button never gets confused with a green "Paid" status (open decision — see design doc §1).

## R2 — Sidebar close-up (`sidebar-reference.webp`)
Confirms: lime active pill with near-black label + filled square-grid icon; inactive items =
light-gray label + thin line icon on near-black; rounded search field with `⌘K`; large touch-area
rows. We mirror this for the module nav.

## R3 — Cards & chart close-up (`card-reference.webp`)
Confirms card radius/padding, the dual progress-bar "Desktop/Mobile" tile, and the bar-chart
treatment (lime bars, gray tracks, dotted grid, black tooltip). Drives our `Card`, `StatTile`, and
chart styling.
