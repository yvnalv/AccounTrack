using System.Globalization;
using Accountrack.Application.Abstractions.Messaging;
using Accountrack.Expenses.Application.Abstractions;
using Accountrack.SharedKernel.Export;
using Accountrack.SharedKernel.Results;

namespace Accountrack.Expenses.Application.Features;

/// <summary>Exports the expense-voucher list as tabular data (ADR-0031); the API renders CSV/Excel.</summary>
public sealed record ExportExpenseVouchersQuery : IQuery<TabularData>;

public sealed class ExportExpenseVouchersHandler : IQueryHandler<ExportExpenseVouchersQuery, TabularData>
{
    private static readonly string[] Header = { "Number", "Date", "Payee", "SubTotal", "Tax", "GrandTotal", "Posted" };

    private readonly IExpenseVoucherRepository _vouchers;
    public ExportExpenseVouchersHandler(IExpenseVoucherRepository vouchers) => _vouchers = vouchers;

    public async Task<Result<TabularData>> Handle(ExportExpenseVouchersQuery request, CancellationToken ct)
    {
        var vouchers = await _vouchers.ListAsync(ct);
        var rows = vouchers.Select(v => (IReadOnlyList<string?>)new string?[]
        {
            v.Number,
            v.ExpenseDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            v.PayeeName,
            v.SubTotal.ToString(CultureInfo.InvariantCulture),
            v.TaxTotal.ToString(CultureInfo.InvariantCulture),
            v.GrandTotal.ToString(CultureInfo.InvariantCulture),
            v.JournalEntryId is null ? "false" : "true",
        });

        return TabularData.From(Header, rows);
    }
}
