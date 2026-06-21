namespace Accountrack.Identity.Domain;

/// <summary>
/// The default permission matrix for the seeded system roles (SECURITY.md §2). Used both when seeding
/// a tenant and when a new organization registers, so every tenant starts with sensible, editable
/// roles. Sensitive verbs (Approve/Post/Pay) stay separate from Create (segregation of duties); the
/// Administrator always receives the full catalog.
/// </summary>
public static class StandardRoleDefinitions
{
    public sealed record RoleTemplate(string Name, string Description, IReadOnlyCollection<string> Permissions);

    private static readonly string[] AllViews =
    {
        PermissionCatalog.SalesView, PermissionCatalog.PurchasingView, PermissionCatalog.InventoryView,
        PermissionCatalog.AccountingView, PermissionCatalog.MasterDataView, PermissionCatalog.ExpensesView,
    };

    /// <summary>Non-admin role templates. The Administrator is seeded separately with every permission.</summary>
    public static IReadOnlyList<RoleTemplate> NonAdminTemplates { get; } = new[]
    {
        new RoleTemplate(SystemRoles.Accountant, "Accounting, AR/AP, reporting and expenses", new[]
        {
            PermissionCatalog.AccountingView, PermissionCatalog.AccountingPost, PermissionCatalog.AccountingApprove,
            PermissionCatalog.AccountingPeriodClose, PermissionCatalog.AccountingPeriodReopen,
            PermissionCatalog.ExpensesView, PermissionCatalog.ExpensesManage, PermissionCatalog.ExpensesPost,
            PermissionCatalog.SalesView, PermissionCatalog.PurchasingView, PermissionCatalog.InventoryView,
            PermissionCatalog.MasterDataView, PermissionCatalog.MasterDataExport, PermissionCatalog.AuditView,
        }),
        new RoleTemplate(SystemRoles.SalesUser, "Sales orders, deliveries, invoicing and customers", new[]
        {
            PermissionCatalog.SalesView, PermissionCatalog.SalesCreate, PermissionCatalog.SalesEdit,
            PermissionCatalog.SalesCancel, PermissionCatalog.SalesPost,
            PermissionCatalog.InventoryView, PermissionCatalog.MasterDataView, PermissionCatalog.MasterDataExport,
        }),
        new RoleTemplate(SystemRoles.PurchasingUser, "Purchase orders, receipts, bills and suppliers", new[]
        {
            PermissionCatalog.PurchasingView, PermissionCatalog.PurchasingCreate, PermissionCatalog.PurchasingEdit,
            PermissionCatalog.PurchasingCancel, PermissionCatalog.PurchasingPost,
            PermissionCatalog.InventoryView, PermissionCatalog.MasterDataView, PermissionCatalog.MasterDataExport,
        }),
        new RoleTemplate(SystemRoles.WarehouseUser, "Stock movements, adjustments and transfers", new[]
        {
            PermissionCatalog.InventoryView, PermissionCatalog.InventoryAdjust, PermissionCatalog.InventoryTransfer,
            PermissionCatalog.SalesView, PermissionCatalog.PurchasingView, PermissionCatalog.MasterDataView,
        }),
        new RoleTemplate(SystemRoles.Viewer, "Read-only access across all modules", AllViews
            .Append(PermissionCatalog.AuditView).Append(PermissionCatalog.MasterDataExport).ToArray()),
    };

    /// <summary>
    /// Builds the full set of system roles for a tenant: the Administrator (every permission) plus the
    /// non-admin templates. <paramref name="permissionIdByCode"/> maps each catalog code to its row id.
    /// </summary>
    public static IEnumerable<Role> BuildSystemRoles(Guid tenantId, IReadOnlyDictionary<string, Guid> permissionIdByCode)
    {
        var admin = new Role(tenantId, SystemRoles.Administrator, "Full access", isSystem: true);
        foreach (var (code, _) in PermissionCatalog.All)
        {
            if (permissionIdByCode.TryGetValue(code, out var pid))
            {
                admin.GrantPermission(pid);
            }
        }

        yield return admin;

        foreach (var template in NonAdminTemplates)
        {
            var role = new Role(tenantId, template.Name, template.Description, isSystem: true);
            foreach (var code in template.Permissions)
            {
                if (permissionIdByCode.TryGetValue(code, out var pid))
                {
                    role.GrantPermission(pid);
                }
            }

            yield return role;
        }
    }
}
