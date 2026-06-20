using System.Globalization;
using Accountrack.Application.Abstractions.Import;
using Accountrack.Application.Abstractions.Messaging;
using Accountrack.MasterData.Application.Abstractions;
using Accountrack.MasterData.Domain;
using Accountrack.SharedKernel.Csv;
using Accountrack.SharedKernel.Export;
using Accountrack.SharedKernel.Results;

namespace Accountrack.MasterData.Application.Features;

// CSV import/export for suppliers (ADR-0031). Matches on Code; same parse pass backs preview + commit.

public static class SupplierImportColumns
{
    public static readonly string[] Header = { "Code", "Name", "TaxId", "PaymentTermDays" };

    public static string Template() =>
        Csv.Write(Header, new[] { new string?[] { "SUP-001", "Globex Supplies", "02.345.678.9-012.000", "30" } });
}

internal sealed record SupplierImportLine(ImportRowResult Result, string Code, string Name, string? TaxId, int Terms);

internal static class SupplierImportParser
{
    public static IReadOnlyList<SupplierImportLine> Parse(string csv, IReadOnlyDictionary<string, Supplier> existingByCode)
    {
        var rows = Csv.Parse(csv);
        var lines = new List<SupplierImportLine>();
        if (rows.Count == 0)
        {
            return lines;
        }

        var header = rows[0].Select(h => h.Trim()).ToList();
        int Col(string name) => header.FindIndex(h => string.Equals(h, name, StringComparison.OrdinalIgnoreCase));
        var (iCode, iName, iTax, iTerms) = (Col("Code"), Col("Name"), Col("TaxId"), Col("PaymentTermDays"));

        for (var r = 1; r < rows.Count; r++)
        {
            var row = rows[r];
            string Field(int i) => i >= 0 && i < row.Count ? row[i].Trim() : string.Empty;
            var errors = new List<string>();

            var code = Field(iCode).ToUpperInvariant();
            var name = Field(iName);
            var taxId = Field(iTax);
            var termsRaw = Field(iTerms);

            if (string.IsNullOrWhiteSpace(code)) errors.Add("Code is required.");
            if (string.IsNullOrWhiteSpace(name)) errors.Add("Name is required.");

            var terms = 30;
            if (!string.IsNullOrWhiteSpace(termsRaw) &&
                (!int.TryParse(termsRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out terms) || terms < 0))
            {
                errors.Add("PaymentTermDays must be a non-negative whole number.");
            }

            var action = errors.Count > 0
                ? ImportRowAction.Error
                : existingByCode.ContainsKey(code) ? ImportRowAction.Update : ImportRowAction.Create;

            lines.Add(new SupplierImportLine(
                new ImportRowResult(r, action, code, name, errors), code, name,
                string.IsNullOrWhiteSpace(taxId) ? null : taxId, terms));
        }

        return lines;
    }

    public static ImportPreviewResult ToPreview(IReadOnlyList<SupplierImportLine> lines) => new(
        lines.Count,
        lines.Count(l => l.Result.Action == ImportRowAction.Create),
        lines.Count(l => l.Result.Action == ImportRowAction.Update),
        lines.Count(l => l.Result.Action == ImportRowAction.Error),
        lines.Select(l => l.Result).ToList());
}

public sealed record PreviewSupplierImportQuery(string Csv) : IQuery<ImportPreviewResult>;

public sealed class PreviewSupplierImportHandler : IQueryHandler<PreviewSupplierImportQuery, ImportPreviewResult>
{
    private readonly ICodedRepository<Supplier> _repo;
    public PreviewSupplierImportHandler(ICodedRepository<Supplier> repo) => _repo = repo;

    public async Task<Result<ImportPreviewResult>> Handle(PreviewSupplierImportQuery request, CancellationToken ct)
    {
        var existing = (await _repo.ListAsync(ct)).ToDictionary(c => c.Code, c => c);
        return SupplierImportParser.ToPreview(SupplierImportParser.Parse(request.Csv, existing));
    }
}

public sealed record CommitSupplierImportCommand(string Csv) : ICommand<ImportCommitResult>;

public sealed class CommitSupplierImportHandler : ICommandHandler<CommitSupplierImportCommand, ImportCommitResult>
{
    private readonly ICodedRepository<Supplier> _repo;
    private readonly IMasterDataUnitOfWork _uow;
    public CommitSupplierImportHandler(ICodedRepository<Supplier> repo, IMasterDataUnitOfWork uow) { _repo = repo; _uow = uow; }

    public async Task<Result<ImportCommitResult>> Handle(CommitSupplierImportCommand request, CancellationToken ct)
    {
        var existing = (await _repo.ListAsync(ct)).ToDictionary(c => c.Code, c => c);
        var lines = SupplierImportParser.Parse(request.Csv, existing);

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
            if (existing.TryGetValue(line.Code, out var supplier))
            {
                supplier.Update(line.Name, line.TaxId, line.Terms);
                updated++;
            }
            else
            {
                _repo.Add(Supplier.Create(line.Code, line.Name, line.TaxId, line.Terms));
                created++;
            }
        }

        await _uow.SaveChangesAsync(ct);
        return new ImportCommitResult(true, created, updated, 0, lines.Select(l => l.Result).ToList());
    }
}

public sealed record ExportSuppliersQuery : IQuery<TabularData>;

public sealed class ExportSuppliersHandler : IQueryHandler<ExportSuppliersQuery, TabularData>
{
    private readonly ICodedRepository<Supplier> _repo;
    public ExportSuppliersHandler(ICodedRepository<Supplier> repo) => _repo = repo;

    public async Task<Result<TabularData>> Handle(ExportSuppliersQuery request, CancellationToken ct)
    {
        var suppliers = await _repo.ListAsync(ct);
        var rows = suppliers.Select(s => (IReadOnlyList<string?>)new string?[]
        {
            s.Code, s.Name, s.TaxId, s.PaymentTermDays.ToString(CultureInfo.InvariantCulture),
        });

        return TabularData.From(SupplierImportColumns.Header, rows);
    }
}
