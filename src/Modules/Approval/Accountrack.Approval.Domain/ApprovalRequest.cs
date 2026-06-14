using Accountrack.SharedKernel.Domain;

namespace Accountrack.Approval.Domain;

/// <summary>
/// A live approval for a specific document. The steps are snapshotted from the definition at submit
/// time so later definition changes don't affect in-flight requests (WORKFLOW_APPROVAL.md §2).
/// </summary>
public sealed class ApprovalRequest : TenantOwnedEntity, IAggregateRoot
{
    private readonly List<ApprovalRequestStep> _steps = new();
    private readonly List<ApprovalAction> _actions = new();

    private ApprovalRequest() { }

    private ApprovalRequest(string documentType, Guid documentId, Guid? definitionId, Guid submittedBy)
    {
        DocumentType = documentType.Trim();
        DocumentId = documentId;
        ApprovalDefinitionId = definitionId;
        SubmittedBy = submittedBy;
    }

    public string DocumentType { get; private set; } = default!;
    public Guid DocumentId { get; private set; }
    public Guid? ApprovalDefinitionId { get; private set; }
    public Guid SubmittedBy { get; private set; }
    public int CurrentLevel { get; private set; }
    public int MaxLevel { get; private set; }
    public ApprovalRequestStatus Status { get; private set; }

    public IReadOnlyList<ApprovalRequestStep> Steps => _steps;
    public IReadOnlyList<ApprovalAction> Actions => _actions;

    public static ApprovalRequest CreateAutoApproved(string documentType, Guid documentId, Guid submittedBy)
    {
        var request = new ApprovalRequest(documentType, documentId, null, submittedBy)
        {
            Status = ApprovalRequestStatus.AutoApproved,
            CurrentLevel = 0,
            MaxLevel = 0,
        };
        return request;
    }

    public static ApprovalRequest CreatePending(ApprovalDefinition definition, Guid documentId, Guid submittedBy)
    {
        var request = new ApprovalRequest(definition.DocumentType, documentId, definition.Id, submittedBy)
        {
            Status = ApprovalRequestStatus.Pending,
            CurrentLevel = 1,
            MaxLevel = definition.MaxLevel,
        };

        foreach (var step in definition.Steps.OrderBy(s => s.Level))
        {
            request._steps.Add(new ApprovalRequestStep(request.Id, step.Level, step.ApproverType, step.ApproverRef));
        }

        return request;
    }

    public bool IsPending => Status == ApprovalRequestStatus.Pending;

    public bool IsSubmitter(Guid userId) => SubmittedBy == userId;

    /// <summary>Whether the user may act on the current level (a specific user, or a member of the role).</summary>
    public bool IsEligible(Guid userId, IReadOnlyCollection<string> roles)
    {
        var step = _steps.FirstOrDefault(s => s.Level == CurrentLevel);
        if (step is null)
        {
            return false;
        }

        return step.ApproverType switch
        {
            ApproverType.User => step.ApproverRef == userId.ToString(),
            ApproverType.Role => roles.Contains(step.ApproverRef, StringComparer.OrdinalIgnoreCase),
            _ => false,
        };
    }

    public void Approve(Guid approverId, string? comment, DateTime nowUtc)
    {
        EnsurePending();
        _actions.Add(new ApprovalAction(Id, CurrentLevel, approverId, ApprovalDecision.Approve, comment, nowUtc));

        if (CurrentLevel >= MaxLevel)
        {
            Status = ApprovalRequestStatus.Approved;
        }
        else
        {
            CurrentLevel++;
        }
    }

    public void Reject(Guid approverId, string? comment, DateTime nowUtc)
    {
        EnsurePending();
        _actions.Add(new ApprovalAction(Id, CurrentLevel, approverId, ApprovalDecision.Reject, comment, nowUtc));
        Status = ApprovalRequestStatus.Rejected;
    }

    private void EnsurePending()
    {
        if (Status != ApprovalRequestStatus.Pending)
        {
            throw new InvalidOperationException("Only a pending request can be acted on.");
        }
    }
}

/// <summary>A snapshot of a definition step, captured onto the request at submit time.</summary>
public sealed class ApprovalRequestStep : Entity
{
    private ApprovalRequestStep() { }

    public ApprovalRequestStep(Guid requestId, int level, ApproverType approverType, string approverRef)
    {
        ApprovalRequestId = requestId;
        Level = level;
        ApproverType = approverType;
        ApproverRef = approverRef;
    }

    public Guid ApprovalRequestId { get; private set; }
    public int Level { get; private set; }
    public ApproverType ApproverType { get; private set; }
    public string ApproverRef { get; private set; } = default!;
}

/// <summary>An approve/reject decision recorded against a request level.</summary>
public sealed class ApprovalAction : Entity
{
    private ApprovalAction() { }

    public ApprovalAction(Guid requestId, int level, Guid approverId, ApprovalDecision decision, string? comment, DateTime actedAtUtc)
    {
        ApprovalRequestId = requestId;
        Level = level;
        ApproverId = approverId;
        Decision = decision;
        Comment = comment;
        ActedAtUtc = actedAtUtc;
    }

    public Guid ApprovalRequestId { get; private set; }
    public int Level { get; private set; }
    public Guid ApproverId { get; private set; }
    public ApprovalDecision Decision { get; private set; }
    public string? Comment { get; private set; }
    public DateTime ActedAtUtc { get; private set; }
}
