using Accountrack.Approval.Application.Abstractions;
using Accountrack.Approval.Domain;
using Microsoft.EntityFrameworkCore;

namespace Accountrack.Approval.Infrastructure.Persistence;

public sealed class ApprovalDefinitionRepository : IApprovalDefinitionRepository
{
    private readonly ApprovalDbContext _db;
    public ApprovalDefinitionRepository(ApprovalDbContext db) => _db = db;

    public void Add(ApprovalDefinition definition) => _db.Definitions.Add(definition);

    public async Task<IReadOnlyList<ApprovalDefinition>> GetActiveForDocumentTypeAsync(string documentType, CancellationToken ct) =>
        await _db.Definitions
            .Include(d => d.Conditions)
            .Include(d => d.Steps)
            .Where(d => d.DocumentType == documentType && d.IsActive)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ApprovalDefinition>> ListAsync(CancellationToken ct) =>
        await _db.Definitions
            .Include(d => d.Conditions)
            .Include(d => d.Steps)
            .OrderBy(d => d.DocumentType).ThenBy(d => d.Priority)
            .ToListAsync(ct);
}

public sealed class ApprovalRequestRepository : IApprovalRequestRepository
{
    private readonly ApprovalDbContext _db;
    public ApprovalRequestRepository(ApprovalDbContext db) => _db = db;

    public void Add(ApprovalRequest request) => _db.Requests.Add(request);

    public Task<ApprovalRequest?> GetAsync(Guid id, CancellationToken ct) =>
        _db.Requests.Include(r => r.Steps).Include(r => r.Actions).FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<bool> ExistsForDocumentAsync(string documentType, Guid documentId, CancellationToken ct) =>
        _db.Requests.AnyAsync(r => r.DocumentType == documentType && r.DocumentId == documentId, ct);

    public async Task<IReadOnlyList<ApprovalRequest>> ListPendingAsync(CancellationToken ct) =>
        await _db.Requests
            .Include(r => r.Steps).Include(r => r.Actions)
            .Where(r => r.Status == ApprovalRequestStatus.Pending)
            .ToListAsync(ct);
}
