namespace Accountrack.Accounting.Domain;

public enum AccountType
{
    Asset = 0,
    Liability = 1,
    Equity = 2,
    Revenue = 3,
    Expense = 4,
}

public enum NormalBalance
{
    Debit = 0,
    Credit = 1,
}

/// <summary>Marks an account as the GL control account for a subledger (ADR-0011).</summary>
public enum ControlType
{
    None = 0,
    AccountsReceivable = 1,
    AccountsPayable = 2,
    Inventory = 3,
}

public enum FiscalPeriodStatus
{
    Open = 0,
    Closed = 1,
    Locked = 2,
}

public enum JournalStatus
{
    Draft = 0,
    Posted = 1,
    Reversed = 2,
}

/// <summary>Which subledger an open item belongs to (ADR-0011).</summary>
public enum SubledgerType
{
    Receivable = 0,
    Payable = 1,
}

/// <summary>Settlement state of a subledger open item.</summary>
public enum OpenItemStatus
{
    Open = 0,
    PartiallyPaid = 1,
    Settled = 2,
}

/// <summary>What produced a journal (drill-down / idempotency source).</summary>
public enum JournalSource
{
    Manual = 0,
    SalesInvoice = 1,
    PurchaseInvoice = 2,
    Payment = 3,
    Shipment = 4,
    GoodsReceipt = 5,
    StockAdjustment = 6,
    PeriodClose = 7,
    SalesReturn = 8,
    PurchaseReturn = 9,
    Expense = 10,
}
