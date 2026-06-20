using Accountrack.Application.Abstractions.Messaging;
using Accountrack.Expenses.Application.Abstractions;
using Accountrack.Expenses.Application.Contracts;
using Accountrack.Expenses.Domain;
using Accountrack.SharedKernel.Results;
using FluentValidation;

namespace Accountrack.Expenses.Application.Features;

public sealed record CreateExpenseCategoryCommand(string Code, string Name, string PostingRuleKey) : ICommand<Guid>;

public sealed class CreateExpenseCategoryValidator : AbstractValidator<CreateExpenseCategoryCommand>
{
    public CreateExpenseCategoryValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PostingRuleKey).NotEmpty().MaximumLength(64);
    }
}

public sealed class CreateExpenseCategoryHandler : ICommandHandler<CreateExpenseCategoryCommand, Guid>
{
    private readonly IExpenseCategoryRepository _repo;
    private readonly IExpensesUnitOfWork _uow;
    public CreateExpenseCategoryHandler(IExpenseCategoryRepository repo, IExpensesUnitOfWork uow) { _repo = repo; _uow = uow; }

    public async Task<Result<Guid>> Handle(CreateExpenseCategoryCommand request, CancellationToken ct)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        if (await _repo.CodeExistsAsync(code, ct))
        {
            return ExpenseErrors.CategoryCodeExists;
        }

        var category = ExpenseCategory.Create(code, request.Name, request.PostingRuleKey);
        _repo.Add(category);
        await _uow.SaveChangesAsync(ct);
        return category.Id;
    }
}

public sealed record GetExpenseCategoriesQuery : IQuery<IReadOnlyList<ExpenseCategoryDto>>;

public sealed class GetExpenseCategoriesHandler : IQueryHandler<GetExpenseCategoriesQuery, IReadOnlyList<ExpenseCategoryDto>>
{
    private readonly IExpenseCategoryRepository _repo;
    public GetExpenseCategoriesHandler(IExpenseCategoryRepository repo) => _repo = repo;

    public async Task<Result<IReadOnlyList<ExpenseCategoryDto>>> Handle(GetExpenseCategoriesQuery request, CancellationToken ct)
    {
        var items = await _repo.ListAsync(ct);
        return Result.Success<IReadOnlyList<ExpenseCategoryDto>>(items
            .Select(c => new ExpenseCategoryDto(c.Id, c.Code, c.Name, c.PostingRuleKey, c.IsActive))
            .ToList());
    }
}
