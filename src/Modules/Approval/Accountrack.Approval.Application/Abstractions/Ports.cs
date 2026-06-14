using Accountrack.Approval.Domain;

namespace Accountrack.Approval.Application.Abstractions;

public interface IApprovalDefinitionRepository
{
    void Add(ApprovalDefinition definition);
    Task<IReadOnlyList<ApprovalDefinition>> GetActiveForDocumentTypeAsync(string documentType, CancellationToken ct);
    Task<IReadOnlyList<ApprovalDefinition>> ListAsync(CancellationToken ct);
}

public interface IApprovalRequestRepository
{
    void Add(ApprovalRequest request);
    Task<ApprovalRequest?> GetAsync(Guid id, CancellationToken ct);
    Task<bool> ExistsForDocumentAsync(string documentType, Guid documentId, CancellationToken ct);
    Task<IReadOnlyList<ApprovalRequest>> ListPendingAsync(CancellationToken ct);
}

public interface IApprovalUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct);
}
