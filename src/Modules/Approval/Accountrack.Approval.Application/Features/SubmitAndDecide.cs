using Accountrack.Application.Abstractions.Context;
using Accountrack.Application.Abstractions.Integration;
using Accountrack.Application.Abstractions.Messaging;
using Accountrack.Approval.Application.Abstractions;
using Accountrack.Approval.Application.Contracts;
using Accountrack.Approval.Domain;
using Accountrack.Modules.Contracts.Events;
using Accountrack.SharedKernel.Results;
using FluentValidation;

namespace Accountrack.Approval.Application.Features;

/// <summary>
/// Submits a document for approval: picks the first active matching definition (or auto-approves
/// when none matches) and creates the request. Used by transactional modules on Submit.
/// </summary>
public sealed record SubmitForApprovalCommand(
    string DocumentType, Guid DocumentId, IReadOnlyDictionary<string, decimal> Attributes) : ICommand<SubmitResult>;

public sealed class SubmitForApprovalValidator : AbstractValidator<SubmitForApprovalCommand>
{
    public SubmitForApprovalValidator()
    {
        RuleFor(x => x.DocumentType).NotEmpty();
        RuleFor(x => x.DocumentId).NotEmpty();
    }
}

public sealed class SubmitForApprovalHandler : ICommandHandler<SubmitForApprovalCommand, SubmitResult>
{
    private readonly IApprovalDefinitionRepository _definitions;
    private readonly IApprovalRequestRepository _requests;
    private readonly ICurrentUser _user;
    private readonly IApprovalUnitOfWork _uow;
    private readonly IIntegrationEventPublisher _events;

    public SubmitForApprovalHandler(
        IApprovalDefinitionRepository definitions, IApprovalRequestRepository requests,
        ICurrentUser user, IApprovalUnitOfWork uow, IIntegrationEventPublisher events)
    {
        _definitions = definitions;
        _requests = requests;
        _user = user;
        _uow = uow;
        _events = events;
    }

    public async Task<Result<SubmitResult>> Handle(SubmitForApprovalCommand request, CancellationToken ct)
    {
        if (await _requests.ExistsForDocumentAsync(request.DocumentType, request.DocumentId, ct))
        {
            return ApprovalErrors.AlreadySubmitted;
        }

        var definitions = await _definitions.GetActiveForDocumentTypeAsync(request.DocumentType, ct);
        var attributes = request.Attributes ?? new Dictionary<string, decimal>();
        var match = definitions.OrderBy(d => d.Priority).FirstOrDefault(d => d.Matches(attributes));

        var approvalRequest = match is null
            ? ApprovalRequest.CreateAutoApproved(request.DocumentType, request.DocumentId, _user.UserId)
            : ApprovalRequest.CreatePending(match, request.DocumentId, _user.UserId);

        _requests.Add(approvalRequest);
        await _uow.SaveChangesAsync(ct);

        await _events.PublishAsync(new ApprovalSubmitted(
            approvalRequest.DocumentType, approvalRequest.DocumentId, approvalRequest.Id,
            approvalRequest.Status.ToString(), approvalRequest.SubmittedBy), ct);

        return new SubmitResult(approvalRequest.Id, approvalRequest.Status.ToString());
    }
}

public sealed record DecideApprovalCommand(Guid RequestId, bool Approve, string? Comment) : ICommand<string>;

public sealed class DecideApprovalHandler : ICommandHandler<DecideApprovalCommand, string>
{
    private readonly IApprovalRequestRepository _requests;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IApprovalUnitOfWork _uow;
    private readonly IIntegrationEventPublisher _events;

    public DecideApprovalHandler(
        IApprovalRequestRepository requests, ICurrentUser user, IClock clock,
        IApprovalUnitOfWork uow, IIntegrationEventPublisher events)
    {
        _requests = requests;
        _user = user;
        _clock = clock;
        _uow = uow;
        _events = events;
    }

    public async Task<Result<string>> Handle(DecideApprovalCommand request, CancellationToken ct)
    {
        var approvalRequest = await _requests.GetAsync(request.RequestId, ct);
        if (approvalRequest is null)
        {
            return ApprovalErrors.RequestNotFound;
        }

        if (!approvalRequest.IsPending)
        {
            return ApprovalErrors.NotPending;
        }

        // Segregation of duties: the submitter cannot approve their own document (BR-APR-2).
        if (request.Approve && approvalRequest.IsSubmitter(_user.UserId))
        {
            return ApprovalErrors.SelfApproval;
        }

        if (!approvalRequest.IsEligible(_user.UserId, _user.Roles))
        {
            return ApprovalErrors.NotEligible;
        }

        if (request.Approve)
        {
            approvalRequest.Approve(_user.UserId, request.Comment, _clock.UtcNow);
        }
        else
        {
            approvalRequest.Reject(_user.UserId, request.Comment, _clock.UtcNow);
        }

        await _uow.SaveChangesAsync(ct);

        await _events.PublishAsync(new ApprovalDecided(
            approvalRequest.DocumentType, approvalRequest.DocumentId, approvalRequest.Id,
            approvalRequest.Status.ToString(), approvalRequest.SubmittedBy, _user.UserId, request.Approve), ct);

        return approvalRequest.Status.ToString();
    }
}
