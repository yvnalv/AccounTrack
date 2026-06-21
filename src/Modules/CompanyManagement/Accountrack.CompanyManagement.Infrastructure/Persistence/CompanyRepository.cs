using Accountrack.CompanyManagement.Application.Abstractions;
using Accountrack.CompanyManagement.Domain;
using Microsoft.EntityFrameworkCore;

namespace Accountrack.CompanyManagement.Infrastructure.Persistence;

/// <summary>
/// Company persistence. Reads are tenant-scoped automatically by the global query filter
/// (the company-code uniqueness check therefore only sees the current tenant's companies).
/// </summary>
public sealed class CompanyRepository : ICompanyRepository
{
    private readonly CompanyDbContext _db;

    public CompanyRepository(CompanyDbContext db) => _db = db;

    public Task<Company?> GetByIdAsync(Guid id, CancellationToken ct) =>
        _db.Companies.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<Company>> ListForCurrentTenantAsync(CancellationToken ct) =>
        await _db.Companies.OrderBy(c => c.Code).ToListAsync(ct);

    public Task<bool> CodeExistsAsync(string code, CancellationToken ct) =>
        _db.Companies.AnyAsync(c => c.Code == code, ct);

    public Task<CompanySetting?> GetSettingAsync(Guid companyId, string key, CancellationToken ct) =>
        _db.CompanySettings.FirstOrDefaultAsync(s => s.CompanyId == companyId && s.Key == key, ct);

    public async Task<IReadOnlyDictionary<Guid, bool>> GetBoolSettingsAsync(string key, CancellationToken ct)
    {
        // Tenant-scoped by the global query filter.
        var rows = await _db.CompanySettings
            .Where(s => s.Key == key)
            .Select(s => new { s.CompanyId, s.Value })
            .ToListAsync(ct);

        return rows.ToDictionary(r => r.CompanyId, r => bool.TryParse(r.Value, out var b) && b);
    }

    public void Add(Company company) => _db.Companies.Add(company);

    public void AddSetting(CompanySetting setting) => _db.CompanySettings.Add(setting);
}
