using Accountrack.Approval.Application.Features;
using Accountrack.Modules.Contracts.Approval;
using MediatR;

namespace Accountrack.Approval.Infrastructure;

/// <summary>Implements the public <see cref="IApprovalService"/> contract over the submit use case.</summary>
public sealed class ApprovalService : IApprovalService
{
    private readonly ISender _sender;

    public ApprovalService(ISender sender) => _sender = sender;

    public async Task<ApprovalSubmissionResult> SubmitAsync(
        string documentType, Guid documentId, IReadOnlyDictionary<string, decimal> attributes, CancellationToken ct)
    {
        var result = await _sender.Send(new SubmitForApprovalCommand(documentType, documentId, attributes), ct);
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Approval submission failed: {result.Error.Code} — {result.Error.Message}");
        }

        return new ApprovalSubmissionResult(result.Value.RequestId, result.Value.Status);
    }
}
