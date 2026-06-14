using Accountrack.Application.Abstractions.Context;
using Accountrack.Application.Abstractions.Messaging;
using Accountrack.Approval.Application.Abstractions;
using Accountrack.Approval.Application.Contracts;
using Accountrack.Approval.Domain;
using Accountrack.SharedKernel.Results;

namespace Accountrack.Approval.Application.Features;

internal static class RequestMapping
{
    public static ApprovalRequestDto ToDto(this ApprovalRequest r) => new(
        r.Id, r.DocumentType, r.DocumentId, r.Status.ToString(), r.CurrentLevel, r.MaxLevel, r.SubmittedBy,
        r.Steps.OrderBy(s => s.Level).Select(s => new StepDto(s.Level, s.ApproverType.ToString(), s.ApproverRef)).ToList(),
        r.Actions.OrderBy(a => a.ActedAtUtc)
            .Select(a => new ApprovalActionDto(a.Level, a.ApproverId, a.Decision.ToString(), a.Comment, a.ActedAtUtc))
            .ToList());
}

/// <summary>Pending requests the current user can act on now (their level).</summary>
public sealed record GetMyPendingApprovalsQuery : IQuery<IReadOnlyList<ApprovalRequestDto>>;

public sealed class GetMyPendingApprovalsHandler
    : IQueryHandler<GetMyPendingApprovalsQuery, IReadOnlyList<ApprovalRequestDto>>
{
    private readonly IApprovalRequestRepository _requests;
    private readonly ICurrentUser _user;

    public GetMyPendingApprovalsHandler(IApprovalRequestRepository requests, ICurrentUser user)
    {
        _requests = requests;
        _user = user;
    }

    public async Task<Result<IReadOnlyList<ApprovalRequestDto>>> Handle(GetMyPendingApprovalsQuery request, CancellationToken ct)
    {
        var pending = await _requests.ListPendingAsync(ct);
        var mine = pending
            .Where(r => r.IsEligible(_user.UserId, _user.Roles) && !r.IsSubmitter(_user.UserId))
            .Select(r => r.ToDto())
            .ToList();
        return Result.Success<IReadOnlyList<ApprovalRequestDto>>(mine);
    }
}

public sealed record GetApprovalRequestQuery(Guid Id) : IQuery<ApprovalRequestDto>;

public sealed class GetApprovalRequestHandler : IQueryHandler<GetApprovalRequestQuery, ApprovalRequestDto>
{
    private readonly IApprovalRequestRepository _requests;
    public GetApprovalRequestHandler(IApprovalRequestRepository requests) => _requests = requests;

    public async Task<Result<ApprovalRequestDto>> Handle(GetApprovalRequestQuery request, CancellationToken ct)
    {
        var found = await _requests.GetAsync(request.Id, ct);
        return found is null ? ApprovalErrors.RequestNotFound : found.ToDto();
    }
}
