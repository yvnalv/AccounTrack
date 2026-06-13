using Accountrack.Identity.Application.Abstractions;
using Accountrack.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Accountrack.Identity.Infrastructure.Persistence.Repositories;

/// <summary>
/// Refresh-token persistence. Looked up by hash on the anonymous refresh/logout path, so it
/// bypasses the tenant filter (reviewed auth allow-list — MULTI_TENANCY.md §5).
/// </summary>
public sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly IdentityDbContext _db;

    public RefreshTokenRepository(IdentityDbContext db) => _db = db;

    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct) =>
        _db.RefreshTokens
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash && !t.IsDeleted, ct);

    public async Task<IReadOnlyList<RefreshToken>> GetActiveFamilyAsync(Guid familyId, CancellationToken ct) =>
        await _db.RefreshTokens
            .IgnoreQueryFilters()
            .Where(t => t.FamilyId == familyId && t.RevokedAtUtc == null && !t.IsDeleted)
            .ToListAsync(ct);

    public void Add(RefreshToken token) => _db.RefreshTokens.Add(token);
}
