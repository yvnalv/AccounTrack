using Accountrack.Application.Abstractions.Messaging;
using Accountrack.Inventory.Application.Abstractions;
using Accountrack.Inventory.Application.Contracts;
using Accountrack.SharedKernel.Results;

namespace Accountrack.Inventory.Application.Features;

/// <summary>Current on-hand quantity + moving-average value per (warehouse, product).</summary>
public sealed record GetStockOnHandQuery : IQuery<IReadOnlyList<StockOnHandDto>>;

public sealed class GetStockOnHandHandler : IQueryHandler<GetStockOnHandQuery, IReadOnlyList<StockOnHandDto>>
{
    private readonly IStockBucketRepository _buckets;

    public GetStockOnHandHandler(IStockBucketRepository buckets) => _buckets = buckets;

    public async Task<Result<IReadOnlyList<StockOnHandDto>>> Handle(GetStockOnHandQuery request, CancellationToken ct)
    {
        var buckets = await _buckets.ListAsync(ct);
        return Result.Success<IReadOnlyList<StockOnHandDto>>(buckets.Select(b => new StockOnHandDto(
            b.ProductId, b.WarehouseId, b.OnHandQty, b.AvgUnitCost,
            Math.Round(b.OnHandQty * b.AvgUnitCost, 4), b.Currency)).ToList());
    }
}

/// <summary>The stock card / ledger for a product (optionally one warehouse), newest first.</summary>
public sealed record GetStockCardQuery(Guid ProductId, Guid? WarehouseId) : IQuery<IReadOnlyList<StockCardEntryDto>>;

public sealed class GetStockCardHandler : IQueryHandler<GetStockCardQuery, IReadOnlyList<StockCardEntryDto>>
{
    private readonly IInventoryTransactionRepository _transactions;

    public GetStockCardHandler(IInventoryTransactionRepository transactions) => _transactions = transactions;

    public async Task<Result<IReadOnlyList<StockCardEntryDto>>> Handle(GetStockCardQuery request, CancellationToken ct)
    {
        var txns = await _transactions.ListAsync(request.ProductId, request.WarehouseId, ct);
        return Result.Success<IReadOnlyList<StockCardEntryDto>>(txns.Select(t => new StockCardEntryDto(
            t.Id, t.MovementDate, t.Type.ToString(), t.Quantity, t.UnitCost, t.TotalCost,
            t.RunningQtyAfter, t.RunningAvgCostAfter, t.Source.ToString(), t.SourceDocumentId, t.Description)).ToList());
    }
}
