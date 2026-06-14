using Accountrack.SharedKernel.Domain;

namespace Accountrack.Approval.Domain;

/// <summary>
/// A configured approval policy for a document type (WORKFLOW_APPROVAL.md §2). It applies when all
/// its conditions match; its ordered steps define who must approve at each level. Lower
/// <see cref="Priority"/> is evaluated first.
/// </summary>
public sealed class ApprovalDefinition : TenantOwnedEntity, IAggregateRoot
{
    private readonly List<ApprovalCondition> _conditions = new();
    private readonly List<ApprovalStep> _steps = new();

    private ApprovalDefinition() { }

    public ApprovalDefinition(string documentType, string name, int priority = 100)
    {
        DocumentType = documentType.Trim();
        Name = name.Trim();
        Priority = priority;
        IsActive = true;
    }

    public string DocumentType { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public int Priority { get; private set; }
    public bool IsActive { get; private set; }

    public IReadOnlyList<ApprovalCondition> Conditions => _conditions;
    public IReadOnlyList<ApprovalStep> Steps => _steps;

    public void AddCondition(string attribute, ConditionOperator op, decimal value) =>
        _conditions.Add(new ApprovalCondition(Id, attribute, op, value));

    public void AddStep(int level, ApproverType approverType, string approverRef) =>
        _steps.Add(new ApprovalStep(Id, level, approverType, approverRef));

    public int MaxLevel => _steps.Count == 0 ? 0 : _steps.Max(s => s.Level);

    public void Deactivate() => IsActive = false;

    /// <summary>True when every condition is satisfied by the supplied document attributes.</summary>
    public bool Matches(IReadOnlyDictionary<string, decimal> attributes) =>
        _conditions.All(c => attributes.TryGetValue(c.Attribute, out var value) && c.IsSatisfiedBy(value));
}

/// <summary>A numeric threshold condition on a document attribute (e.g. Total &gt; 50,000,000).</summary>
public sealed class ApprovalCondition : Entity
{
    private ApprovalCondition() { }

    public ApprovalCondition(Guid definitionId, string attribute, ConditionOperator op, decimal value)
    {
        ApprovalDefinitionId = definitionId;
        Attribute = attribute.Trim();
        Operator = op;
        Value = value;
    }

    public Guid ApprovalDefinitionId { get; private set; }
    public string Attribute { get; private set; } = default!;
    public ConditionOperator Operator { get; private set; }
    public decimal Value { get; private set; }

    public bool IsSatisfiedBy(decimal actual) => Operator switch
    {
        ConditionOperator.GreaterThan => actual > Value,
        ConditionOperator.GreaterThanOrEqual => actual >= Value,
        ConditionOperator.LessThan => actual < Value,
        ConditionOperator.LessThanOrEqual => actual <= Value,
        ConditionOperator.EqualTo => actual == Value,
        _ => false,
    };
}

/// <summary>One ordered approval level: who can approve (a specific user or any member of a role).</summary>
public sealed class ApprovalStep : Entity
{
    private ApprovalStep() { }

    public ApprovalStep(Guid definitionId, int level, ApproverType approverType, string approverRef)
    {
        ApprovalDefinitionId = definitionId;
        Level = level;
        ApproverType = approverType;
        ApproverRef = approverRef.Trim();
    }

    public Guid ApprovalDefinitionId { get; private set; }
    public int Level { get; private set; }
    public ApproverType ApproverType { get; private set; }

    /// <summary>A user id (when <see cref="ApproverType"/> is User) or a role name (when Role).</summary>
    public string ApproverRef { get; private set; } = default!;
}
