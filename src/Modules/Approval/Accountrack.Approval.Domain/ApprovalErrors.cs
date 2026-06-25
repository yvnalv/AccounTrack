using Accountrack.SharedKernel.Results;

namespace Accountrack.Approval.Domain;

public static class ApprovalErrors
{
    public static readonly Error RequestNotFound =
        Error.NotFound("APPROVAL.REQUEST_NOT_FOUND", "Approval request not found.");

    public static readonly Error NotPending =
        Error.Conflict("APPROVAL.NOT_PENDING", "This request has already been decided.");

    public static readonly Error NotEligible =
        Error.Forbidden("APPROVAL.NOT_ELIGIBLE", "You are not an approver for the current step.");

    public static readonly Error SelfApproval =
        Error.BusinessRule("BR-APR-2", "You cannot approve a document you submitted.", "APPROVAL.SELF_APPROVAL");

    public static readonly Error DefinitionNotFound =
        Error.NotFound("APPROVAL.DEFINITION_NOT_FOUND", "Approval definition not found.");

    public static readonly Error AlreadySubmitted =
        Error.Conflict("APPROVAL.ALREADY_SUBMITTED", "An approval request already exists for this document.");

    public static readonly Error OutboxMessageNotFound =
        Error.NotFound("APPROVAL.OUTBOX_MESSAGE_NOT_FOUND", "Dead-lettered event not found (or already redelivered).");
}
