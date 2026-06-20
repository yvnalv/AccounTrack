using Accountrack.Application.Abstractions.Import;
using Accountrack.Application.Abstractions.Messaging;
using Accountrack.MasterData.Application.Abstractions;
using Accountrack.MasterData.Domain;
using Accountrack.SharedKernel.Csv;
using Accountrack.SharedKernel.Export;
using Accountrack.SharedKernel.Results;

namespace Accountrack.MasterData.Application.Features;

// CSV import/export for warehouses (ADR-0031). Matches on Code; same parse pass backs preview + commit.

public static class WarehouseImportColumns
{
    public static readonly string[] Header = { "Code", "Name", "Address" };

    public static string Template() =>
        Csv.Write(Header, new[] { new string?[] { "WH-001", "Main Warehouse", "Jl. Industri No. 1, Jakarta" } });
}

internal sealed record WarehouseImportLine(ImportRowResult Result, string Code, string Name, string? Address);

internal static class WarehouseImportParser
{
    public static IReadOnlyList<WarehouseImportLine> Parse(string csv, IReadOnlyDictionary<string, Warehouse> existingByCode)
    {
        var rows = Csv.Parse(csv);
        var lines = new List<WarehouseImportLine>();
        if (rows.Count == 0)
        {
            return lines;
        }

        var header = rows[0].Select(h => h.Trim()).ToList();
        int Col(string name) => header.FindIndex(h => string.Equals(h, name, StringComparison.OrdinalIgnoreCase));
        var (iCode, iName, iAddr) = (Col("Code"), Col("Name"), Col("Address"));

        for (var r = 1; r < rows.Count; r++)
        {
            var row = rows[r];
            string Field(int i) => i >= 0 && i < row.Count ? row[i].Trim() : string.Empty;
            var errors = new List<string>();

            var code = Field(iCode).ToUpperInvariant();
            var name = Field(iName);
            var address = Field(iAddr);

            if (string.IsNullOrWhiteSpace(code)) errors.Add("Code is required.");
            if (string.IsNullOrWhiteSpace(name)) errors.Add("Name is required.");

            var action = errors.Count > 0
                ? ImportRowAction.Error
                : existingByCode.ContainsKey(code) ? ImportRowAction.Update : ImportRowAction.Create;

            lines.Add(new WarehouseImportLine(
                new ImportRowResult(r, action, code, name, errors), code, name,
                string.IsNullOrWhiteSpace(address) ? null : address));
        }

        return lines;
    }

    public static ImportPreviewResult ToPreview(IReadOnlyList<WarehouseImportLine> lines) => new(
        lines.Count,
        lines.Count(l => l.Result.Action == ImportRowAction.Create),
        lines.Count(l => l.Result.Action == ImportRowAction.Update),
        lines.Count(l => l.Result.Action == ImportRowAction.Error),
        lines.Select(l => l.Result).ToList());
}

public sealed record PreviewWarehouseImportQuery(string Csv) : IQuery<ImportPreviewResult>;

public sealed class PreviewWarehouseImportHandler : IQueryHandler<PreviewWarehouseImportQuery, ImportPreviewResult>
{
    private readonly ICodedRepository<Warehouse> _repo;
    public PreviewWarehouseImportHandler(ICodedRepository<Warehouse> repo) => _repo = repo;

    public async Task<Result<ImportPreviewResult>> Handle(PreviewWarehouseImportQuery request, CancellationToken ct)
    {
        var existing = (await _repo.ListAsync(ct)).ToDictionary(c => c.Code, c => c);
        return WarehouseImportParser.ToPreview(WarehouseImportParser.Parse(request.Csv, existing));
    }
}

public sealed record CommitWarehouseImportCommand(string Csv) : ICommand<ImportCommitResult>;

public sealed class CommitWarehouseImportHandler : ICommandHandler<CommitWarehouseImportCommand, ImportCommitResult>
{
    private readonly ICodedRepository<Warehouse> _repo;
    private readonly IMasterDataUnitOfWork _uow;
    public CommitWarehouseImportHandler(ICodedRepository<Warehouse> repo, IMasterDataUnitOfWork uow) { _repo = repo; _uow = uow; }

    public async Task<Result<ImportCommitResult>> Handle(CommitWarehouseImportCommand request, CancellationToken ct)
    {
        var existing = (await _repo.ListAsync(ct)).ToDictionary(c => c.Code, c => c);
        var lines = WarehouseImportParser.Parse(request.Csv, existing);

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
            if (existing.TryGetValue(line.Code, out var warehouse))
            {
                warehouse.Update(line.Name, line.Address);
                updated++;
            }
            else
            {
                _repo.Add(Warehouse.Create(line.Code, line.Name, line.Address));
                created++;
            }
        }

        await _uow.SaveChangesAsync(ct);
        return new ImportCommitResult(true, created, updated, 0, lines.Select(l => l.Result).ToList());
    }
}

public sealed record ExportWarehousesQuery : IQuery<TabularData>;

public sealed class ExportWarehousesHandler : IQueryHandler<ExportWarehousesQuery, TabularData>
{
    private readonly ICodedRepository<Warehouse> _repo;
    public ExportWarehousesHandler(ICodedRepository<Warehouse> repo) => _repo = repo;

    public async Task<Result<TabularData>> Handle(ExportWarehousesQuery request, CancellationToken ct)
    {
        var warehouses = await _repo.ListAsync(ct);
        var rows = warehouses.Select(w => (IReadOnlyList<string?>)new string?[] { w.Code, w.Name, w.Address });
        return TabularData.From(WarehouseImportColumns.Header, rows);
    }
}
