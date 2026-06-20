using System.Globalization;
using Accountrack.Application.Abstractions.Messaging;
using Accountrack.Modules.Contracts.MasterData;
using Accountrack.Purchasing.Application.Abstractions;
using Accountrack.SharedKernel.Export;
using Accountrack.SharedKernel.Results;

namespace Accountrack.Purchasing.Application.Features;

/// <summary>Exports the purchase-order list as tabular data (ADR-0031); the API renders CSV/Excel.</summary>
public sealed record ExportPurchaseOrdersQuery : IQuery<TabularData>;

public sealed class ExportPurchaseOrdersHandler : IQueryHandler<ExportPurchaseOrdersQuery, TabularData>
{
    private static readonly string[] Header = { "Number", "Supplier", "Status", "OrderDate", "GrandTotal" };

    private readonly IPurchaseOrderRepository _orders;
    private readonly IMasterDataLookup _lookup;

    public ExportPurchaseOrdersHandler(IPurchaseOrderRepository orders, IMasterDataLookup lookup)
    {
        _orders = orders;
        _lookup = lookup;
    }

    public async Task<Result<TabularData>> Handle(ExportPurchaseOrdersQuery request, CancellationToken ct)
    {
        var orders = await _orders.ListAsync(ct);
        var names = await _lookup.ResolveNamesAsync(orders.Select(o => o.SupplierId).Distinct().ToArray(), ct);

        var rows = orders.Select(o => (IReadOnlyList<string?>)new string?[]
        {
            o.Number,
            names.GetValueOrDefault(o.SupplierId, o.SupplierId.ToString()),
            o.Status.ToString(),
            o.OrderDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            o.GrandTotal.ToString(CultureInfo.InvariantCulture),
        });

        return TabularData.From(Header, rows);
    }
}
