using Accountrack.Application.Abstractions.Messaging;
using Accountrack.Approval.Application.Abstractions;
using Accountrack.Approval.Application.Contracts;
using Accountrack.Approval.Domain;
using Accountrack.SharedKernel.Results;
using FluentValidation;

namespace Accountrack.Approval.Application.Features;

internal static class ApprovalMapping
{
    public static ApprovalDefinitionDto ToDto(this ApprovalDefinition d) => new(
        d.Id, d.DocumentType, d.Name, d.Priority, d.IsActive,
        d.Conditions.Select(c => new ConditionDto(c.Attribute, c.Operator.ToString(), c.Value)).ToList(),
        d.Steps.OrderBy(s => s.Level).Select(s => new StepDto(s.Level, s.ApproverType.ToString(), s.ApproverRef)).ToList());
}

public sealed record DefinitionCondition(string Attribute, ConditionOperator Operator, decimal Value);
public sealed record DefinitionStep(int Level, ApproverType ApproverType, string ApproverRef);

public sealed record CreateApprovalDefinitionCommand(
    string DocumentType, string Name, int Priority,
    IReadOnlyList<DefinitionCondition> Conditions, IReadOnlyList<DefinitionStep> Steps) : ICommand<Guid>;

public sealed class CreateApprovalDefinitionValidator : AbstractValidator<CreateApprovalDefinitionCommand>
{
    public CreateApprovalDefinitionValidator()
    {
        RuleFor(x => x.DocumentType).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Steps).NotEmpty().WithMessage("At least one approval step is required.");
        RuleForEach(x => x.Steps).Must(s => !string.IsNullOrWhiteSpace(s.ApproverRef))
            .WithMessage("Each step needs an approver reference.");
    }
}

public sealed class CreateApprovalDefinitionHandler : ICommandHandler<CreateApprovalDefinitionCommand, Guid>
{
    private readonly IApprovalDefinitionRepository _definitions;
    private readonly IApprovalUnitOfWork _uow;

    public CreateApprovalDefinitionHandler(IApprovalDefinitionRepository definitions, IApprovalUnitOfWork uow)
    {
        _definitions = definitions;
        _uow = uow;
    }

    public async Task<Result<Guid>> Handle(CreateApprovalDefinitionCommand request, CancellationToken ct)
    {
        var definition = new ApprovalDefinition(request.DocumentType, request.Name, request.Priority);
        foreach (var c in request.Conditions)
        {
            definition.AddCondition(c.Attribute, c.Operator, c.Value);
        }

        foreach (var s in request.Steps)
        {
            definition.AddStep(s.Level, s.ApproverType, s.ApproverRef);
        }

        _definitions.Add(definition);
        await _uow.SaveChangesAsync(ct);
        return definition.Id;
    }
}

public sealed record GetApprovalDefinitionsQuery : IQuery<IReadOnlyList<ApprovalDefinitionDto>>;

public sealed class GetApprovalDefinitionsHandler
    : IQueryHandler<GetApprovalDefinitionsQuery, IReadOnlyList<ApprovalDefinitionDto>>
{
    private readonly IApprovalDefinitionRepository _definitions;
    public GetApprovalDefinitionsHandler(IApprovalDefinitionRepository definitions) => _definitions = definitions;

    public async Task<Result<IReadOnlyList<ApprovalDefinitionDto>>> Handle(GetApprovalDefinitionsQuery request, CancellationToken ct)
    {
        var defs = await _definitions.ListAsync(ct);
        return Result.Success<IReadOnlyList<ApprovalDefinitionDto>>(defs.Select(d => d.ToDto()).ToList());
    }
}
