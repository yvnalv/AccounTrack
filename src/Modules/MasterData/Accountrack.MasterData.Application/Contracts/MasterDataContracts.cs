using Accountrack.SharedKernel.Inventory;

namespace Accountrack.MasterData.Application.Contracts;

public sealed record UnitOfMeasureDto(Guid Id, string Code, string Name, bool IsActive, byte[]? RowVersion);
public sealed record ProductCategoryDto(Guid Id, string Code, string Name, bool IsActive, byte[]? RowVersion);
public sealed record ProductDto(
    Guid Id, string Code, string Name, Guid BaseUomId, Guid? CategoryId,
    bool IsStockTracked, bool IsSold, bool IsPurchased, bool IsActive, byte[]? RowVersion,
    CostingMethod CostingMethod = CostingMethod.MovingAverage);
public sealed record CustomerDto(
    Guid Id, string Code, string Name, string? TaxId, int PaymentTermDays, decimal CreditLimit, bool IsActive,
    byte[]? RowVersion, Guid? SalesPriceListId = null);
public sealed record SupplierDto(
    Guid Id, string Code, string Name, string? TaxId, int PaymentTermDays, bool IsActive, byte[]? RowVersion,
    Guid? PurchasePriceListId = null);
public sealed record WarehouseDto(Guid Id, string Code, string Name, string? Address, bool IsActive, byte[]? RowVersion);
public sealed record TaxCodeDto(Guid Id, string Code, string Name, decimal Rate, bool IsActive, byte[]? RowVersion);
