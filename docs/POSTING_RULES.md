# POSTING_RULES.md

Account determination: how business events map to GL accounts. Rules are **configuration per
company** (ADR-0024), not hardcoded. This is the engine room of automatic posting — it must be
explicit, validated, and testable.

## 1. Model

```
PostingRule
├── CompanyId
├── EventType     (e.g. "SalesInvoice.Revenue", "GoodsReceipt.Clearing")
├── RuleKey       (the resolved purpose, e.g. "ARControl", "Revenue", "VATOutput", "COGS")
├── Selector      (optional dimension that refines the rule)
│      └── { ProductCategoryId? , WarehouseId? , TaxCodeId? , BankAccountId? , Default }
└── AccountId     (must be an active, postable account; control accounts where applicable)
```

Resolution: given `(CompanyId, EventType, RuleKey, selectors)`, pick the **most specific**
matching rule (e.g. a rule for `ProductCategory = Electronics` beats `Default`). If none matches,
fall back to the company `Default` rule for that key. If still unresolved → posting fails with a
clear configuration error (never a silent wrong account).

### Validation
- Every `RuleKey` required by an active event must resolve to an account.
- Control-account keys (ARControl, APControl, Inventory) must point to accounts with the matching
  `ControlType`.
- Account must be `IsActive` and postable (leaf).
- A "posting-rule health check" lists any unresolved/invalid rule before a company can transact.

## 2. Default Seed (Indonesian SMB template)

| RuleKey | Default account (seeded, `IsSystem`) |
|---|---|
| ARControl | 1100 Accounts Receivable |
| APControl | 2100 Accounts Payable |
| Inventory | 1200 Inventory |
| GRIRClearing | 2150 Goods Received / Invoice Received |
| Revenue | 4000 Sales Revenue |
| COGS | 5000 Cost of Goods Sold |
| VATOutput | 2300 VAT Output (PPN Keluaran) |
| VATInput | 1300 VAT Input (PPN Masukan) |
| CashBank (per bank/cash account) | 1000 Cash / 1010 Bank … |
| InventoryVariance | 5100 Inventory Adjustment / Variance |
| Rounding | 7900 Rounding Difference |
| CustomerAdvance | 2400 Customer Advances |
| SupplierAdvance | 1400 Supplier Advances |
| RetainedEarnings | 3900 Retained Earnings |

Selectors let advanced setups override, e.g. `Revenue` for `ProductCategory = Services` → a
separate service-revenue account; `CashBank` resolved from the payment's chosen bank account.

## 3. Determination Matrix per Event

Notation: **Dr** debit, **Cr** credit. Amounts: *gross* = net + tax.

### Sales Invoice (`SalesInvoicePosted`)
| Line | Account (RuleKey) | Dr | Cr |
|---|---|---|---|
| 1 | ARControl (+CustomerId) | gross | |
| 2 | Revenue (by product category) | | net |
| 3 | VATOutput (by tax code) | | tax |

Also creates an AR open item (CustomerId, gross, due date).

### Goods Shipment / Delivery (`GoodsShipped` → COGS)
| Line | Account | Dr | Cr |
|---|---|---|---|
| 1 | COGS (by product category) | cost | |
| 2 | Inventory (by warehouse) | | cost |

`cost` = moving-average cost at issue, returned by the Inventory ledger (INVENTORY_DESIGN.md).

### Customer Payment (`CustomerPaymentReceived`)
| Line | Account | Dr | Cr |
|---|---|---|---|
| 1 | CashBank (chosen account) | amount | |
| 2 | ARControl (+CustomerId) | | allocated |
| 3 | CustomerAdvance (if overpaid) | | unallocated |

Allocates to AR open items.

### Sales Return / Credit Note (`SalesReturnPosted`)
| Line | Account | Dr | Cr |
|---|---|---|---|
| 1 | Revenue | net | |
| 2 | VATOutput | tax | |
| 3 | ARControl (or CashBank if refunded) | | gross |
| 4 | Inventory (goods back) | cost | |
| 5 | COGS | | cost |

### Goods Receipt (`GoodsReceived`)
| Line | Account | Dr | Cr |
|---|---|---|---|
| 1 | Inventory (by warehouse) | cost | |
| 2 | GRIRClearing | | cost |

### Purchase Invoice / Bill (`PurchaseInvoicePosted`)
| Line | Account | Dr | Cr |
|---|---|---|---|
| 1 | GRIRClearing | net (received) | |
| 2 | VATInput (by tax code) | tax | |
| 3 | APControl (+SupplierId) | | gross |
| 4 | Rounding | residual | residual |

Price variance between PO/receipt cost and invoice cost posts to a Purchase Price Variance
account (configurable; defaults to InventoryVariance) — handling detailed when three-way match
is implemented. Creates an AP open item.

### Supplier Payment (`SupplierPaymentPosted`)
| Line | Account | Dr | Cr |
|---|---|---|---|
| 1 | APControl (+SupplierId) | allocated | |
| 2 | CashBank | | amount |
| 3 | SupplierAdvance (if prepaid) | unallocated | |

### Purchase Return / Debit Note (`PurchaseReturnPosted`)
| Line | Account | Dr | Cr |
|---|---|---|---|
| 1 | APControl (or CashBank if refunded) | gross | |
| 2 | Inventory or GRIRClearing | | cost |
| 3 | VATInput | | tax |

### Stock Adjustment (`StockAdjusted`)
| Direction | Dr | Cr |
|---|---|---|
| Increase | Inventory | InventoryVariance |
| Decrease | InventoryVariance | Inventory |

### Stock Transfer (`StockTransferred`)
No GL impact if both warehouses share the Inventory account. If warehouses map to different
Inventory accounts: Dr destination Inventory / Cr source Inventory at moving-average cost.

## 4. Idempotency & Reversal
- Each posting is keyed by (Source, SourceDocumentId, EventType) so retries never double-post.
- Reversing a source document posts the exact negation via the same rules (ACCOUNTING_DESIGN.md
  §9, ADR-0009).

## 5. Tests
For every event: rules resolve to valid accounts; generated lines balance; selector overrides
beat defaults; unresolved rule fails loudly; reversal mirrors the original.
