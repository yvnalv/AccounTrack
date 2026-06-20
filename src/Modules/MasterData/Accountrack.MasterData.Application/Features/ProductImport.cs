using Accountrack.Application.Abstractions.Import;
using Accountrack.Application.Abstractions.Messaging;
using Accountrack.MasterData.Application.Abstractions;
using Accountrack.MasterData.Domain;
using Accountrack.SharedKernel.Csv;
using Accountrack.SharedKernel.Export;
using Accountrack.SharedKernel.Results;

namespace Accountrack.MasterData.Application.Features;

// CSV import/export for products (ADR-0031). UoM and category are given by *code* and resolved to
// ids; base UoM is immutable on update (it underpins inventory costing). Matches on Code.

public static class ProductImportColumns
{
    public static readonly string[] Header = { "Code", "Name", "BaseUom", "Category", "StockTracked", "Sold", "Purchased" };

    public static string Template() =>
        Csv.Write(Header, new[] { new string?[] { "SKU-001", "Sample Widget", "PCS", "GENERAL", "true", "true", "true" } });
}

internal sealed record ProductImportLine(
    ImportRowResult Result, string Code, string Name, Guid BaseUomId, Guid? CategoryId,
    bool StockTracked, bool Sold, bool Purchased);

internal static class ProductImportParser
{
    private static bool? ParseBool(string raw, bool fallback)
    {
        if (string.IsNullOrWhiteSpace(raw)) return fallback;
        return raw.Trim().ToLowerInvariant() switch
        {
            "true" or "yes" or "y" or "1" => true,
            "false" or "no" or "n" or "0" => false,
            _ => null,
        };
    }

    public static IReadOnlyList<ProductImportLine> Parse(
        string csv,
        IReadOnlyDictionary<string, Product> existingByCode,
        IReadOnlyDictionary<string, Guid> uomByCode,
        IReadOnlyDictionary<string, Guid> categoryByCode)
    {
        var rows = Csv.Parse(csv);
        var lines = new List<ProductImportLine>();
        if (rows.Count == 0)
        {
            return lines;
        }

        var header = rows[0].Select(h => h.Trim()).ToList();
        int Col(string name) => header.FindIndex(h => string.Equals(h, name, StringComparison.OrdinalIgnoreCase));
        var (iCode, iName, iUom, iCat, iStock, iSold, iPur) =
            (Col("Code"), Col("Name"), Col("BaseUom"), Col("Category"), Col("StockTracked"), Col("Sold"), Col("Purchased"));

        for (var r = 1; r < rows.Count; r++)
        {
            var row = rows[r];
            string Field(int i) => i >= 0 && i < row.Count ? row[i].Trim() : string.Empty;
            var errors = new List<string>();

            var code = Field(iCode).ToUpperInvariant();
            var name = Field(iName);
            var uomCode = Field(iUom).ToUpperInvariant();
            var catCode = Field(iCat).ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(code)) errors.Add("Code is required.");
            if (string.IsNullOrWhiteSpace(name)) errors.Add("Name is required.");

            var isUpdate = !string.IsNullOrWhiteSpace(code) && existingByCode.ContainsKey(code);

            // Base UoM is required to create; on update it is immutable and the column is ignored.
            Guid baseUomId = isUpdate ? existingByCode[code].BaseUomId : Guid.Empty;
            if (!isUpdate)
            {
                if (string.IsNullOrWhiteSpace(uomCode)) errors.Add("BaseUom is required.");
                else if (!uomByCode.TryGetValue(uomCode, out baseUomId)) errors.Add($"BaseUom '{uomCode}' does not exist.");
            }

            Guid? categoryId = null;
            if (!string.IsNullOrWhiteSpace(catCode))
            {
                if (categoryByCode.TryGetValue(catCode, out var cid)) categoryId = cid;
                else errors.Add($"Category '{catCode}' does not exist.");
            }

            var stock = ParseBool(Field(iStock), true);
            var sold = ParseBool(Field(iSold), true);
            var purchased = ParseBool(Field(iPur), true);
            if (stock is null || sold is null || purchased is null)
            {
                errors.Add("StockTracked/Sold/Purchased must be true or false.");
            }

            var action = errors.Count > 0
                ? ImportRowAction.Error
                : isUpdate ? ImportRowAction.Update : ImportRowAction.Create;

            lines.Add(new ProductImportLine(
                new ImportRowResult(r, action, code, name, errors), code, name, baseUomId, categoryId,
                stock ?? true, sold ?? true, purchased ?? true));
        }

        return lines;
    }

    public static ImportPreviewResult ToPreview(IReadOnlyList<ProductImportLine> lines) => new(
        lines.Count,
        lines.Count(l => l.Result.Action == ImportRowAction.Create),
        lines.Count(l => l.Result.Action == ImportRowAction.Update),
        lines.Count(l => l.Result.Action == ImportRowAction.Error),
        lines.Select(l => l.Result).ToList());
}

public sealed record PreviewProductImportQuery(string Csv) : IQuery<ImportPreviewResult>;

public sealed class PreviewProductImportHandler : IQueryHandler<PreviewProductImportQuery, ImportPreviewResult>
{
    private readonly ICodedRepository<Product> _products;
    private readonly ICodedRepository<UnitOfMeasure> _uoms;
    private readonly ICodedRepository<ProductCategory> _categories;

    public PreviewProductImportHandler(
        ICodedRepository<Product> products, ICodedRepository<UnitOfMeasure> uoms, ICodedRepository<ProductCategory> categories)
    {
        _products = products;
        _uoms = uoms;
        _categories = categories;
    }

    public async Task<Result<ImportPreviewResult>> Handle(PreviewProductImportQuery request, CancellationToken ct)
    {
        var existing = (await _products.ListAsync(ct)).ToDictionary(p => p.Code, p => p);
        var uoms = (await _uoms.ListAsync(ct)).ToDictionary(u => u.Code, u => u.Id);
        var cats = (await _categories.ListAsync(ct)).ToDictionary(c => c.Code, c => c.Id);
        return ProductImportParser.ToPreview(ProductImportParser.Parse(request.Csv, existing, uoms, cats));
    }
}

public sealed record CommitProductImportCommand(string Csv) : ICommand<ImportCommitResult>;

public sealed class CommitProductImportHandler : ICommandHandler<CommitProductImportCommand, ImportCommitResult>
{
    private readonly ICodedRepository<Product> _products;
    private readonly ICodedRepository<UnitOfMeasure> _uoms;
    private readonly ICodedRepository<ProductCategory> _categories;
    private readonly IMasterDataUnitOfWork _uow;

    public CommitProductImportHandler(
        ICodedRepository<Product> products, ICodedRepository<UnitOfMeasure> uoms,
        ICodedRepository<ProductCategory> categories, IMasterDataUnitOfWork uow)
    {
        _products = products;
        _uoms = uoms;
        _categories = categories;
        _uow = uow;
    }

    public async Task<Result<ImportCommitResult>> Handle(CommitProductImportCommand request, CancellationToken ct)
    {
        var existing = (await _products.ListAsync(ct)).ToDictionary(p => p.Code, p => p);
        var uoms = (await _uoms.ListAsync(ct)).ToDictionary(u => u.Code, u => u.Id);
        var cats = (await _categories.ListAsync(ct)).ToDictionary(c => c.Code, c => c.Id);
        var lines = ProductImportParser.Parse(request.Csv, existing, uoms, cats);

        if (lines.Count == 0)
        {
            return new ImportCommitResult(false, 0, 0, 0, Array.Empty<ImportRowResult>());
        }

        if (lines.Any(l => l.Result.Action == ImportRowAction.Error))
        {
            return new ImportCommitResult(
                false, 0, 0, lines.Count(l => l.Result.Action == ImportRowAction.Error),
                lines.Select(l => l.Result).ToList());
        }

        var created = 0;
        var updated = 0;
        foreach (var line in lines)
        {
            if (existing.TryGetValue(line.Code, out var product))
            {
                product.Update(line.Name, line.CategoryId, line.StockTracked, line.Sold, line.Purchased);
                updated++;
            }
            else
            {
                _products.Add(Product.Create(
                    line.Code, line.Name, line.BaseUomId, line.CategoryId, line.StockTracked, line.Sold, line.Purchased));
                created++;
            }
        }

        await _uow.SaveChangesAsync(ct);
        return new ImportCommitResult(true, created, updated, 0, lines.Select(l => l.Result).ToList());
    }
}

public sealed record ExportProductsQuery : IQuery<TabularData>;

public sealed class ExportProductsHandler : IQueryHandler<ExportProductsQuery, TabularData>
{
    private readonly ICodedRepository<Product> _products;
    private readonly ICodedRepository<UnitOfMeasure> _uoms;
    private readonly ICodedRepository<ProductCategory> _categories;

    public ExportProductsHandler(
        ICodedRepository<Product> products, ICodedRepository<UnitOfMeasure> uoms, ICodedRepository<ProductCategory> categories)
    {
        _products = products;
        _uoms = uoms;
        _categories = categories;
    }

    public async Task<Result<TabularData>> Handle(ExportProductsQuery request, CancellationToken ct)
    {
        var products = await _products.ListAsync(ct);
        var uomCodeById = (await _uoms.ListAsync(ct)).ToDictionary(u => u.Id, u => u.Code);
        var catCodeById = (await _categories.ListAsync(ct)).ToDictionary(c => c.Id, c => c.Code);

        var rows = products.Select(p => (IReadOnlyList<string?>)new string?[]
        {
            p.Code, p.Name,
            uomCodeById.GetValueOrDefault(p.BaseUomId, ""),
            p.CategoryId is { } cid ? catCodeById.GetValueOrDefault(cid, "") : "",
            p.IsStockTracked ? "true" : "false",
            p.IsSold ? "true" : "false",
            p.IsPurchased ? "true" : "false",
        });

        return TabularData.From(ProductImportColumns.Header, rows);
    }
}
