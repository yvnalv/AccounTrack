using Accountrack.SharedKernel.Inventory;

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

    /// <summary>The product's inventory costing method (ADR-0034), used by the Inventory ledger to
    /// value a bucket. Defaults to <see cref="CostingMethod.MovingAverage"/> for an unknown id.</summary>
    Task<CostingMethod> GetCostingMethodAsync(Guid productId, CancellationToken ct);

    /// <summary>Resolves master-data ids (customer/supplier/product/warehouse) to display names,
    /// e.g. for exports. Unknown ids are simply absent from the map.</summary>
    Task<IReadOnlyDictionary<Guid, string>> ResolveNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
}
