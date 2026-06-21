namespace Accountrack.MasterData.Application.Contracts;

public sealed record UnitOfMeasureDto(Guid Id, string Code, string Name, bool IsActive);
public sealed record ProductCategoryDto(Guid Id, string Code, string Name, bool IsActive);
public sealed record ProductDto(
    Guid Id, string Code, string Name, Guid BaseUomId, Guid? CategoryId,
    bool IsStockTracked, bool IsSold, bool IsPurchased, bool IsActive);
public sealed record CustomerDto(
    Guid Id, string Code, string Name, string? TaxId, int PaymentTermDays, decimal CreditLimit, bool IsActive);
public sealed record SupplierDto(
    Guid Id, string Code, string Name, string? TaxId, int PaymentTermDays, bool IsActive);
public sealed record WarehouseDto(Guid Id, string Code, string Name, string? Address, bool IsActive);
public sealed record TaxCodeDto(Guid Id, string Code, string Name, decimal Rate, bool IsActive);
