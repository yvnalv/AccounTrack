using System.Globalization;
using Accountrack.Application.Abstractions.Import;
using Accountrack.Application.Abstractions.Messaging;
using Accountrack.MasterData.Application.Abstractions;
using Accountrack.MasterData.Domain;
using Accountrack.SharedKernel.Csv;
using Accountrack.SharedKernel.Export;
using Accountrack.SharedKernel.Results;

namespace Accountrack.MasterData.Application.Features;

// CSV import/export for customers (ADR-0031). The same parse+validate pass backs both the dry-run
// preview and the commit, so what the user previews is exactly what commits. Matching is by Code.

public static class CustomerImportColumns
{
    public static readonly string[] Header = { "Code", "Name", "TaxId", "PaymentTermDays", "CreditLimit" };

    public static string Template() =>
        Csv.Write(Header, new[] { new string?[] { "CUST-001", "Acme Corp", "01.234.567.8-901.000", "30", "10000000" } });
}

internal sealed record CustomerImportLine(
    ImportRowResult Result, string Code, string Name, string? TaxId, int Terms, decimal Credit);

internal static class CustomerImportParser
{
    public static IReadOnlyList<CustomerImportLine> Parse(string csv, IReadOnlyDictionary<string, Customer> existingByCode)
    {
        var rows = Csv.Parse(csv);
        var lines = new List<CustomerImportLine>();
        if (rows.Count == 0)
        {
            return lines;
        }

        var header = rows[0].Select(h => h.Trim()).ToList();
        int Col(string name) => header.FindIndex(h => string.Equals(h, name, StringComparison.OrdinalIgnoreCase));
        var (iCode, iName, iTax, iTerms, iCredit) =
            (Col("Code"), Col("Name"), Col("TaxId"), Col("PaymentTermDays"), Col("CreditLimit"));

        for (var r = 1; r < rows.Count; r++)
        {
            var row = rows[r];
            string Field(int i) => i >= 0 && i < row.Count ? row[i].Trim() : string.Empty;
            var errors = new List<string>();

            var code = Field(iCode).ToUpperInvariant();
            var name = Field(iName);
            var taxId = Field(iTax);
            var termsRaw = Field(iTerms);
            var creditRaw = Field(iCredit);

            if (string.IsNullOrWhiteSpace(code)) errors.Add("Code is required.");
            if (string.IsNullOrWhiteSpace(name)) errors.Add("Name is required.");

            var terms = 30;
            if (!string.IsNullOrWhiteSpace(termsRaw) &&
                (!int.TryParse(termsRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out terms) || terms < 0))
            {
                errors.Add("PaymentTermDays must be a non-negative whole number.");
            }

            var credit = 0m;
            if (!string.IsNullOrWhiteSpace(creditRaw) &&
                (!decimal.TryParse(creditRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out credit) || credit < 0))
            {
                errors.Add("CreditLimit must be a non-negative number.");
            }

            var action = errors.Count > 0
                ? ImportRowAction.Error
                : existingByCode.ContainsKey(code) ? ImportRowAction.Update : ImportRowAction.Create;

            lines.Add(new CustomerImportLine(
                new ImportRowResult(r, action, code, name, errors), code, name,
                string.IsNullOrWhiteSpace(taxId) ? null : taxId, terms, credit));
        }

        return lines;
    }

    public static ImportPreviewResult ToPreview(IReadOnlyList<CustomerImportLine> lines) => new(
        lines.Count,
        lines.Count(l => l.Result.Action == ImportRowAction.Create),
        lines.Count(l => l.Result.Action == ImportRowAction.Update),
        lines.Count(l => l.Result.Action == ImportRowAction.Error),
        lines.Select(l => l.Result).ToList());
}

// --- Preview (dry-run) ---

public sealed record PreviewCustomerImportQuery(string Csv) : IQuery<ImportPreviewResult>;

public sealed class PreviewCustomerImportHandler : IQueryHandler<PreviewCustomerImportQuery, ImportPreviewResult>
{
    private readonly ICodedRepository<Customer> _repo;
    public PreviewCustomerImportHandler(ICodedRepository<Customer> repo) => _repo = repo;

    public async Task<Result<ImportPreviewResult>> Handle(PreviewCustomerImportQuery request, CancellationToken ct)
    {
        var existing = (await _repo.ListAsync(ct)).ToDictionary(c => c.Code, c => c);
        var lines = CustomerImportParser.Parse(request.Csv, existing);
        return CustomerImportParser.ToPreview(lines);
    }
}

// --- Commit (all-or-nothing) ---

public sealed record CommitCustomerImportCommand(string Csv) : ICommand<ImportCommitResult>;

public sealed class CommitCustomerImportHandler : ICommandHandler<CommitCustomerImportCommand, ImportCommitResult>
{
    private readonly ICodedRepository<Customer> _repo;
    private readonly IMasterDataUnitOfWork _uow;
    public CommitCustomerImportHandler(ICodedRepository<Customer> repo, IMasterDataUnitOfWork uow) { _repo = repo; _uow = uow; }

    public async Task<Result<ImportCommitResult>> Handle(CommitCustomerImportCommand request, CancellationToken ct)
    {
        var existing = (await _repo.ListAsync(ct)).ToDictionary(c => c.Code, c => c);
        var lines = CustomerImportParser.Parse(request.Csv, existing);

        if (lines.Count == 0)
        {
            return new ImportCommitResult(false, 0, 0, 0, Array.Empty<ImportRowResult>());
        }

        // All-or-nothing (BR-IMP-3): any invalid row blocks the whole import.
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
            if (existing.TryGetValue(line.Code, out var customer))
            {
                customer.Update(line.Name, line.TaxId, line.Terms, line.Credit, customer.SalesPriceListId);
                updated++;
            }
            else
            {
                _repo.Add(Customer.Create(line.Code, line.Name, line.TaxId, line.Terms, line.Credit));
                created++;
            }
        }

        await _uow.SaveChangesAsync(ct);
        return new ImportCommitResult(true, created, updated, 0, lines.Select(l => l.Result).ToList());
    }
}

// --- Export ---

public sealed record ExportCustomersQuery : IQuery<TabularData>;

public sealed class ExportCustomersHandler : IQueryHandler<ExportCustomersQuery, TabularData>
{
    private readonly ICodedRepository<Customer> _repo;
    public ExportCustomersHandler(ICodedRepository<Customer> repo) => _repo = repo;

    public async Task<Result<TabularData>> Handle(ExportCustomersQuery request, CancellationToken ct)
    {
        var customers = await _repo.ListAsync(ct);
        var rows = customers.Select(c => (IReadOnlyList<string?>)new string?[]
        {
            c.Code, c.Name, c.TaxId,
            c.PaymentTermDays.ToString(CultureInfo.InvariantCulture),
            c.CreditLimit.ToString(CultureInfo.InvariantCulture),
        });

        return TabularData.From(CustomerImportColumns.Header, rows);
    }
}
