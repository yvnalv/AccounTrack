# ADR-0035: Price lists (Sales/Purchase) with company default + party overrides

- **Status:** Superseded by [ADR-0036](0036-product-base-price-pricing.md)
- **Date:** 2026-07-07
- **Deciders:** Product owner, engineering
- **Tags:** master-data | sales | purchasing

> **Superseded (2026-07-07).** The "price lists only" model below forced a list-per-customer ×
> row-per-product maintenance burden. **ADR-0036** moves the base price onto the product and reframes
> price lists as optional shared discount rules. The rest of this record is kept for history.

## Context

Sales and Purchase order lines require a unit price, but the product master never modeled price, so
every line was typed from zero with no lookup. Trading/distribution businesses maintain sell and buy
prices per product, often with different prices for specific customers/suppliers, and expect an order
line to **auto-fill** the price on product selection (still editable).

Pricing must not change accounting: the entered `unitPrice` already flows to posting, so price lists
only **prefill** the form. Module boundaries stand (ADR-0007) and money stays in the single company
functional currency (ADR-0013).

## Decision

We will add **price lists** in the Master Data module: a named list is typed **Sales** or
**Purchase** and holds a per-product `unitPrice`.

- One list per type may be the company **default**. Customers carry an optional
  **SalesPriceListId**, suppliers a **PurchasePriceListId**, overriding the default for that party.
- **Resolution** for `(type, party)` = the company default list overlaid by the party's list →
  a `productId → price` map. The order forms fetch this map when the customer/supplier is chosen and
  prefill each line on product-select; the price stays editable. No match → the line stays manual (0).
- Because the price only prefills the existing `unitPrice`, there is **no posting impact and no
  cross-module service** — the frontend calls the resolve endpoint directly.
- **v1 scope:** default + per-party lists; **no** quantity breaks, date-effective versioning,
  discounts, or multi-currency lists (those are slice 2 / future). A list is soft-deactivated, never
  hard-deleted (ADR-0029); items are upserted/removed per product.

## Options Considered

1. **Price lists with default + party override (chosen)** — matches real mixed pricing; isolated to
   Master Data; zero accounting change. A little more model than a single price field.
2. **Single default price fields on Product** — smallest change, but can't express per-customer or
   per-supplier pricing that these businesses need.
3. **Full pricing engine now** (quantity breaks, date-effective, discounts, currencies) — closer to
   ERP pricing but a large, slow first drop; deferred to later slices on the same model.

## Consequences

- **Positive:** order lines auto-fill from real data; per-customer/supplier pricing supported; no
  accounting risk; the model extends to quantity breaks / dates / discounts without a rewrite.
- **Negative / trade-offs:** assignment is a plain FK per party (one list per type); resolution reads
  the default + party lists per order (small, cached client-side per party); prices are functional
  currency only.
- **Follow-ups:** quantity breaks + date-effective versioning; header/line discounts; multi-currency
  price lists (with the FX phase); optional price columns in product import.

## References

- [BUSINESS_RULES.md](../BUSINESS_RULES.md) BR-PRICE-* · [ADR-0013](../DECISIONS.md) single functional currency
- [ADR-0007](../DECISIONS.md) module boundaries · [ADR-0029](../DECISIONS.md) master-data soft delete
