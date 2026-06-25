namespace Accountrack.Approval.Application.Contracts;

public sealed record ConditionDto(string Attribute, string Operator, decimal Value);
public sealed record StepDto(int Level, string ApproverType, string ApproverRef);

public sealed record ApprovalDefinitionDto(
    Guid Id, string DocumentType, string Name, int Priority, bool IsActive,
    IReadOnlyList<ConditionDto> Conditions, IReadOnlyList<StepDto> Steps);

public sealed record ApprovalActionDto(int Level, Guid ApproverId, string Decision, string? Comment, DateTime ActedAtUtc);

public sealed record ApprovalRequestDto(
    Guid Id, string DocumentType, Guid DocumentId, string Status, int CurrentLevel, int MaxLevel,
    Guid SubmittedBy, IReadOnlyList<StepDto> Steps, IReadOnlyList<ApprovalActionDto> Actions);

public sealed record SubmitResult(Guid RequestId, string Status);

/// <summary>A failed (dead-lettered) integration event surfaced for operator triage/retry.</summary>
public sealed record DeadLetterEventDto(
    Guid Id, string EventType, DateTime OccurredOnUtc, int Attempts, string? Error);
