using System.Globalization;
using Accountrack.Application.Abstractions.Messaging;
using Accountrack.Inventory.Application.Abstractions;
using Accountrack.Modules.Contracts.MasterData;
using Accountrack.SharedKernel.Export;
using Accountrack.SharedKernel.Results;

namespace Accountrack.Inventory.Application.Features;

/// <summary>Exports the on-hand stock list as tabular data (ADR-0031); the API renders CSV/Excel.</summary>
public sealed record ExportStockOnHandQuery : IQuery<TabularData>;

public sealed class ExportStockOnHandHandler : IQueryHandler<ExportStockOnHandQuery, TabularData>
{
    private static readonly string[] Header = { "Product", "Warehouse", "OnHandQty", "AvgUnitCost", "Value", "Currency" };

    private readonly IStockBucketRepository _buckets;
    private readonly IMasterDataLookup _lookup;

    public ExportStockOnHandHandler(IStockBucketRepository buckets, IMasterDataLookup lookup)
    {
        _buckets = buckets;
        _lookup = lookup;
    }

    public async Task<Result<TabularData>> Handle(ExportStockOnHandQuery request, CancellationToken ct)
    {
        var buckets = await _buckets.ListAsync(ct);
        var ids = buckets.Select(b => b.ProductId).Concat(buckets.Select(b => b.WarehouseId)).Distinct().ToArray();
        var names = await _lookup.ResolveNamesAsync(ids, ct);

        var rows = buckets.Select(b => (IReadOnlyList<string?>)new string?[]
        {
            names.GetValueOrDefault(b.ProductId, b.ProductId.ToString()),
            names.GetValueOrDefault(b.WarehouseId, b.WarehouseId.ToString()),
            b.OnHandQty.ToString(CultureInfo.InvariantCulture),
            b.AvgUnitCost.ToString(CultureInfo.InvariantCulture),
            Math.Round(b.OnHandQty * b.AvgUnitCost, 4).ToString(CultureInfo.InvariantCulture),
            b.Currency,
        });

        return TabularData.From(Header, rows);
    }
}
