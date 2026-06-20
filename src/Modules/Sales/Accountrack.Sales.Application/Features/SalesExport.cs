using System.Globalization;
using Accountrack.Application.Abstractions.Messaging;
using Accountrack.Modules.Contracts.MasterData;
using Accountrack.Sales.Application.Abstractions;
using Accountrack.SharedKernel.Export;
using Accountrack.SharedKernel.Results;

namespace Accountrack.Sales.Application.Features;

/// <summary>Exports the sales-order list as tabular data (ADR-0031); the API renders CSV/Excel.</summary>
public sealed record ExportSalesOrdersQuery : IQuery<TabularData>;

public sealed class ExportSalesOrdersHandler : IQueryHandler<ExportSalesOrdersQuery, TabularData>
{
    private static readonly string[] Header = { "Number", "Customer", "Status", "OrderDate", "GrandTotal" };

    private readonly ISalesOrderRepository _orders;
    private readonly IMasterDataLookup _lookup;

    public ExportSalesOrdersHandler(ISalesOrderRepository orders, IMasterDataLookup lookup)
    {
        _orders = orders;
        _lookup = lookup;
    }

    public async Task<Result<TabularData>> Handle(ExportSalesOrdersQuery request, CancellationToken ct)
    {
        var orders = await _orders.ListAsync(ct);
        var names = await _lookup.ResolveNamesAsync(orders.Select(o => o.CustomerId).Distinct().ToArray(), ct);

        var rows = orders.Select(o => (IReadOnlyList<string?>)new string?[]
        {
            o.Number,
            names.GetValueOrDefault(o.CustomerId, o.CustomerId.ToString()),
            o.Status.ToString(),
            o.OrderDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            o.GrandTotal.ToString(CultureInfo.InvariantCulture),
        });

        return TabularData.From(Header, rows);
    }
}
