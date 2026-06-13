using Accountrack.CompanyManagement.Domain;

namespace Accountrack.CompanyManagement.Application.Abstractions;

public interface ICompanyRepository
{
    Task<Company?> GetByIdAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<Company>> ListForCurrentTenantAsync(CancellationToken ct);

    Task<bool> CodeExistsAsync(string code, CancellationToken ct);

    Task<CompanySetting?> GetSettingAsync(Guid companyId, string key, CancellationToken ct);

    void Add(Company company);

    void AddSetting(CompanySetting setting);
}

public interface ICompanyUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct);
}
