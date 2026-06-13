namespace Accountrack.Identity.Domain;

/// <summary>
/// The canonical RBAC permission catalog (SECURITY.md §2). Sensitive verbs (Approve, Post, Pay)
/// are separate permissions to enable segregation of duties (ADR-0019). Seeded at startup.
/// </summary>
public static class PermissionCatalog
{
    public const string SalesView = "Sales.View";
    public const string SalesCreate = "Sales.Create";
    public const string SalesEdit = "Sales.Edit";
    public const string SalesDelete = "Sales.Delete";
    public const string SalesApprove = "Sales.Approve";
    public const string SalesPost = "Sales.Post";

    public const string PurchasingView = "Purchasing.View";
    public const string PurchasingCreate = "Purchasing.Create";
    public const string PurchasingEdit = "Purchasing.Edit";
    public const string PurchasingApprove = "Purchasing.Approve";
    public const string PurchasingPost = "Purchasing.Post";

    public const string InventoryView = "Inventory.View";
    public const string InventoryAdjust = "Inventory.Adjust";
    public const string InventoryTransfer = "Inventory.Transfer";

    public const string AccountingView = "Accounting.View";
    public const string AccountingPost = "Accounting.Post";
    public const string AccountingApprove = "Accounting.Approve";
    public const string AccountingPeriodClose = "Accounting.PeriodClose";
    public const string AccountingPeriodReopen = "Accounting.PeriodReopen";

    public const string MasterDataView = "MasterData.View";
    public const string MasterDataManage = "MasterData.Manage";

    public const string AdminUsers = "Admin.Users";
    public const string AdminRoles = "Admin.Roles";
    public const string AdminCompanies = "Admin.Companies";

    /// <summary>All permission codes with display names, for seeding.</summary>
    public static IReadOnlyList<(string Code, string Name)> All { get; } = new[]
    {
        (SalesView, "View Sales"),
        (SalesCreate, "Create Sales"),
        (SalesEdit, "Edit Sales"),
        (SalesDelete, "Delete Sales"),
        (SalesApprove, "Approve Sales"),
        (SalesPost, "Post Sales"),
        (PurchasingView, "View Purchasing"),
        (PurchasingCreate, "Create Purchasing"),
        (PurchasingEdit, "Edit Purchasing"),
        (PurchasingApprove, "Approve Purchasing"),
        (PurchasingPost, "Post Purchasing"),
        (InventoryView, "View Inventory"),
        (InventoryAdjust, "Adjust Inventory"),
        (InventoryTransfer, "Transfer Inventory"),
        (AccountingView, "View Accounting"),
        (AccountingPost, "Post Journals"),
        (AccountingApprove, "Approve Journals"),
        (AccountingPeriodClose, "Close Fiscal Period"),
        (AccountingPeriodReopen, "Reopen Fiscal Period"),
        (MasterDataView, "View Master Data"),
        (MasterDataManage, "Manage Master Data"),
        (AdminUsers, "Manage Users"),
        (AdminRoles, "Manage Roles"),
        (AdminCompanies, "Manage Companies"),
    };
}

/// <summary>System role names seeded per tenant.</summary>
public static class SystemRoles
{
    public const string Administrator = "Administrator";
    public const string Accountant = "Accountant";
    public const string SalesUser = "Sales";
    public const string PurchasingUser = "Purchasing";
    public const string WarehouseUser = "Warehouse";
    public const string Viewer = "Viewer";
}
