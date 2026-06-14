namespace Accountrack.Approval.Domain;

public enum ApproverType
{
    User = 0,
    Role = 1,
}

public enum ConditionOperator
{
    GreaterThan = 0,
    GreaterThanOrEqual = 1,
    LessThan = 2,
    LessThanOrEqual = 3,
    EqualTo = 4,
}

public enum ApprovalRequestStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    AutoApproved = 3,
}

public enum ApprovalDecision
{
    Approve = 0,
    Reject = 1,
}
