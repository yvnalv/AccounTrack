using System.Globalization;
using Accountrack.Application.Abstractions.Context;
using Accountrack.Application.Abstractions.Messaging;
using Accountrack.Inventory.Application.Abstractions;
using Accountrack.Inventory.Application.Contracts;
using Accountrack.Modules.Contracts.Accounting;
using Accountrack.Modules.Contracts.Company;
using Accountrack.Modules.Contracts.MasterData;
using Accountrack.SharedKernel.Inventory;
using Accountrack.SharedKernel.Pdf;
using Accountrack.SharedKernel.Results;
using MediatR;

namespace Accountrack.Inventory.Application.Features;

/// <summary>
/// Inventory valuation (INVENTORY_DESIGN.md §9): on-hand value by product at moving-average cost,
/// aggregated across warehouses, with the GL Inventory control-account balance it must reconcile to
/// (BR-INV-7). The ledger (stock buckets) is the source of truth; the GL figure comes from Accounting
/// via <see cref="IGeneralLedgerBalances"/>.
/// </summary>
public sealed record GetInventoryValuationQuery : IQuery<InventoryValuationDto>;

public sealed class GetInventoryValuationHandler : IQueryHandler<GetInventoryValuationQuery, InventoryValuationDto>
{
    // Moving-average rounding can leave a sub-unit residue between the ledger and the GL.
    private const decimal Tolerance = 1m;

    private readonly IStockBucketRepository _buckets;
    private readonly IStockCostLayerRepository _layers;
    private readonly IMasterDataLookup _lookup;
    private readonly IGeneralLedgerBalances _gl;
    private readonly ICompanyDirectory _companies;
    private readonly ITenantContext _tenant;

    public GetInventoryValuationHandler(
        IStockBucketRepository buckets, IStockCostLayerRepository layers, IMasterDataLookup lookup,
        IGeneralLedgerBalances gl, ICompanyDirectory companies, ITenantContext tenant)
    {
        _buckets = buckets;
        _layers = layers;
        _lookup = lookup;
        _gl = gl;
        _companies = companies;
        _tenant = tenant;
    }

    public async Task<Result<InventoryValuationDto>> Handle(GetInventoryValuationQuery request, CancellationToken ct)
    {
        var buckets = await _buckets.ListAsync(ct);
        var company = await _companies.GetAsync(_tenant.CompanyId, ct);
        var currency = company?.FunctionalCurrency ?? buckets.FirstOrDefault()?.Currency ?? "IDR";

        // FIFO buckets are valued at the sum of their open cost layers (the authoritative figure);
        // moving-average buckets at on-hand × average (ADR-0034).
        var layerValueByBucket = (await _layers.ListOpenAsync(ct))
            .GroupBy(l => (l.ProductId, l.WarehouseId))
            .ToDictionary(g => g.Key, g => g.Sum(l => Math.Round(l.RemainingQty * l.UnitCost, 4)));

        var byProduct = buckets
            .GroupBy(b => b.ProductId)
            .Select(g =>
            {
                var qty = g.Sum(b => b.OnHandQty);
                var value = g.Sum(b => b.CostingMethod == CostingMethod.Fifo
                    ? layerValueByBucket.GetValueOrDefault((b.ProductId, b.WarehouseId), 0m)
                    : Math.Round(b.OnHandQty * b.AvgUnitCost, 4));
                return (ProductId: g.Key, Qty: qty, Value: value);
            })
            .Where(x => x.Qty != 0 || x.Value != 0)
            .ToList();

        var names = await _lookup.ResolveNamesAsync(byProduct.Select(x => x.ProductId).ToArray(), ct);

        var rows = byProduct
            .Select(x => new InventoryValuationRowDto(
                x.ProductId,
                names.GetValueOrDefault(x.ProductId, x.ProductId.ToString()),
                x.Qty,
                x.Qty == 0 ? 0m : Math.Round(x.Value / x.Qty, 4),
                x.Value))
            .OrderByDescending(r => r.Value)
            .ToList();

        var total = rows.Sum(r => r.Value);
        var glBalance = await _gl.GetInventoryControlBalanceAsync(ct);
        var difference = total - glBalance;

        return new InventoryValuationDto(
            currency, rows, total, glBalance, difference, Math.Abs(difference) < Tolerance);
    }
}

// ---- PDF ----
public sealed record GetInventoryValuationPdfQuery : IQuery<PdfReport>;

public sealed class GetInventoryValuationPdfHandler : IQueryHandler<GetInventoryValuationPdfQuery, PdfReport>
{
    private static readonly CultureInfo Id = new("id-ID");

    private readonly ISender _sender;
    private readonly ICompanyDirectory _companies;
    private readonly ITenantContext _tenant;

    public GetInventoryValuationPdfHandler(ISender sender, ICompanyDirectory companies, ITenantContext tenant)
    {
        _sender = sender;
        _companies = companies;
        _tenant = tenant;
    }

    public async Task<Result<PdfReport>> Handle(GetInventoryValuationPdfQuery request, CancellationToken ct)
    {
        var result = await _sender.Send(new GetInventoryValuationQuery(), ct);
        if (result.IsFailure) return result.Error;
        var v = result.Value;

        var c = await _companies.GetAsync(_tenant.CompanyId, ct);
        var name = c is null || string.IsNullOrWhiteSpace(c.Name) ? "Accountrack" : c.Name;
        var companyLines = new List<string>();
        if (c is not null && !string.IsNullOrWhiteSpace(c.TaxId)) companyLines.Add($"NPWP: {c.TaxId}");
        var company = new PdfParty(name, companyLines);

        static string Amt(decimal x) => x.ToString("N0", Id);
        static string Qty(decimal x) => x.ToString("0.####", CultureInfo.InvariantCulture);

        var rows = v.Rows
            .Select(r => new PdfReportRow(new[] { r.ProductName, Qty(r.Quantity), Amt(r.AvgUnitCost), Amt(r.Value) }))
            .ToList();
        rows.Add(new PdfReportRow(new[] { "Total inventory value", null, null, Amt(v.TotalValue) }, PdfRowStyle.GrandTotal));
        rows.Add(new PdfReportRow(new[] { "GL Inventory account", null, null, Amt(v.GlInventoryBalance) }));
        rows.Add(new PdfReportRow(
            new[] { v.IsReconciled ? "Reconciled" : "Difference", null, null, Amt(v.Difference) }, PdfRowStyle.Subtotal));

        var footer = $"{name} · Generated by Accountrack · Amounts in {v.Currency}";
        return new PdfReport(
            "Inventory Valuation", $"As of {DateTime.UtcNow:d MMM yyyy}", company,
            new[] { "Product", "Qty", "Avg cost", "Value" }, rows, footer);
    }
}
