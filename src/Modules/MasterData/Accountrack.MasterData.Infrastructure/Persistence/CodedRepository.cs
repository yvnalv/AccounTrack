using Accountrack.MasterData.Application.Abstractions;
using Accountrack.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace Accountrack.MasterData.Infrastructure.Persistence;

/// <summary>Generic repository for coded, company-scoped master-data aggregates.</summary>
public sealed class CodedRepository<T> : ICodedRepository<T>
    where T : Entity, IHasCode
{
    private readonly MasterDataDbContext _db;

    public CodedRepository(MasterDataDbContext db) => _db = db;

    public void Add(T entity) => _db.Set<T>().Add(entity);

    public Task<bool> CodeExistsAsync(string code, CancellationToken ct) =>
        _db.Set<T>().AnyAsync(e => EF.Property<string>(e, "Code") == code, ct);

    public Task<T?> GetByIdAsync(Guid id, CancellationToken ct) =>
        _db.Set<T>().FirstOrDefaultAsync(e => e.Id == id, ct);

    public Task<bool> ExistsAsync(Guid id, CancellationToken ct) =>
        _db.Set<T>().AnyAsync(e => e.Id == id, ct);

    public async Task<IReadOnlyList<T>> ListAsync(CancellationToken ct) =>
        await _db.Set<T>().OrderBy(e => EF.Property<string>(e, "Code")).ToListAsync(ct);
}
