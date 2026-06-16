namespace Accountrack.Modules.Contracts.MasterData;

/// <summary>
/// Public contract exposed by Master Data for other modules to validate references (suppliers,
/// products, warehouses) without depending on its internals (ADR-0007).
/// </summary>
public interface IMasterDataLookup
{
    Task<bool> SupplierExistsAsync(Guid supplierId, CancellationToken ct);
    Task<bool> CustomerExistsAsync(Guid customerId, CancellationToken ct);
    Task<bool> ProductExistsAsync(Guid productId, CancellationToken ct);
    Task<bool> WarehouseExistsAsync(Guid warehouseId, CancellationToken ct);
}
