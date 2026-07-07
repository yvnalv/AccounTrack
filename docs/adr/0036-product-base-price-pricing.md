# ADR-0036: Product base price + shared discount lists (supersedes ADR-0035)

- **Status:** Accepted
- **Date:** 2026-07-07
- **Supersedes:** [ADR-0035](0035-price-lists.md)
- **Deciders:** Product owner, engineering
- **Tags:** master-data | sales | purchasing

## Context

ADR-0035 modeled pricing as **price lists only**: to give a customer a price you built a list
containing that product, and there was no base price. In practice this forced a **list per customer ×
a row per product** (the classic N×M maintenance trap) and made the common case — "this product sells
for X" — needlessly heavy. Feedback after trying it: hard to add prices and hard to maintain.

Mainstream SMB ERPs (Odoo, NetSuite, QuickBooks, Xero) avoid this: the **base price lives on the
product**, and price lists are optional, **shared** rules (a discount, or a few overrides) layered on
top. We adopt that.

## Decision

- **Base price on the product.** `Product.SalePrice` and `Product.PurchasePrice` (nullable) are the
  default and auto-fill order lines directly — one number per product, no list needed for the 80% case.
- **Price lists become shared discount rules.** A list carries a **`DiscountPercent`** off the base
  price plus optional per-product **fixed overrides** (`PriceListItem`). One list (e.g. "Wholesale
  −10%") is shared by many parties. The **company-default-list** concept from ADR-0035 is removed — the
  product base price is the default.
- **Assignment unchanged:** a customer's `SalesPriceListId` / supplier's `PurchasePriceListId` points
  at a shared list.
- **Resolution** for `(type, party, product)`: per-product **override** → list **% off base** →
  **product base price** → manual. The order form fetches only the party's *adjustments* (a
  `productId → price` map of overrides + discounted bases) and overlays them on the product base price.
- Pricing still has **no posting impact** (the entered line price posts as before), and lists are
  soft-deactivated, never hard-deleted (ADR-0029).

## Options Considered

1. **Base price + shared discount lists (chosen)** — Odoo/NetSuite-style; lowest maintenance (set the
   product price once; a handful of shared discount lists), still supports per-product exceptions.
2. **Base price only** — simplest, but no per-customer/supplier pricing.
3. **Base price + customer groups + group discount** — low maintenance but coarser (no per-product
   overrides); the discount-list model subsumes it (a list *is* effectively a group rule).
4. **Keep ADR-0035 (full lists)** — rejected: the N×M maintenance burden is the problem being fixed.

## Consequences

- **Positive:** the common case is one price on the product; giving a segment a deal is one shared
  list with a percentage; per-product specials are a single override row. Maintenance drops from N×M to
  N + a few rules. No accounting change; the model still extends to quantity breaks / dates / discounts.
- **Negative / trade-offs:** a migration drops `PriceList.IsDefault` and adds `Product.SalePrice`,
  `Product.PurchasePrice`, `PriceList.DiscountPercent` (dev data only). Resolution reads all products
  to apply a percentage (fine at SMB scale; can be optimized later).
- **Follow-ups (unchanged from ADR-0035):** quantity breaks + date-effective versioning; header/line
  discounts; multi-currency price lists (with the FX phase); price columns in product import.

## References

- Supersedes [ADR-0035](0035-price-lists.md) · [BUSINESS_RULES.md](../BUSINESS_RULES.md) BR-PRICE-*
- [ADR-0013](../DECISIONS.md) single functional currency · [ADR-0029](../DECISIONS.md) master-data soft delete
