namespace Accountrack.Modules.Contracts.Approval;

public sealed record ApprovalSubmissionResult(Guid RequestId, string Status);

/// <summary>
/// Public contract for submitting a document to the Approval Workflow engine, so transactional
/// modules (e.g. Purchasing) can request approval without depending on its internals (ADR-0007).
/// </summary>
public interface IApprovalService
{
    Task<ApprovalSubmissionResult> SubmitAsync(
        string documentType, Guid documentId, IReadOnlyDictionary<string, decimal> attributes, CancellationToken ct);
}
