using Accountrack.Accounting.Application.Abstractions;
using Accountrack.Accounting.Application.Contracts;
using Accountrack.Accounting.Domain;
using Accountrack.Application.Abstractions.Messaging;
using Accountrack.SharedKernel.Results;
using FluentValidation;

namespace Accountrack.Accounting.Application.Features;

internal static class AccountMapping
{
    public static AccountDto ToDto(this Account a) => new(
        a.Id, a.Code, a.Name, a.Type.ToString(), a.NormalBalance.ToString(),
        a.IsControlAccount, a.ControlType.ToString(), a.AllowPosting, a.IsActive, a.IsSystem);
}

public sealed record CreateAccountCommand(
    string Code, string Name, AccountType Type, bool IsControlAccount, ControlType ControlType) : ICommand<Guid>;

public sealed class CreateAccountCommandValidator : AbstractValidator<CreateAccountCommand>
{
    public CreateAccountCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

public sealed class CreateAccountCommandHandler : ICommandHandler<CreateAccountCommand, Guid>
{
    private readonly IAccountRepository _accounts;
    private readonly IAccountingUnitOfWork _uow;

    public CreateAccountCommandHandler(IAccountRepository accounts, IAccountingUnitOfWork uow)
    {
        _accounts = accounts;
        _uow = uow;
    }

    public async Task<Result<Guid>> Handle(CreateAccountCommand request, CancellationToken cancellationToken)
    {
        if (await _accounts.CodeExistsAsync(request.Code.Trim(), cancellationToken))
        {
            return AccountingErrors.AccountCodeExists;
        }

        var account = Account.Create(
            request.Code, request.Name, request.Type,
            isControlAccount: request.IsControlAccount, controlType: request.ControlType);

        _accounts.Add(account);
        await _uow.SaveChangesAsync(cancellationToken);
        return account.Id;
    }
}

public sealed record GetAccountsQuery : IQuery<IReadOnlyList<AccountDto>>;

public sealed class GetAccountsQueryHandler : IQueryHandler<GetAccountsQuery, IReadOnlyList<AccountDto>>
{
    private readonly IAccountRepository _accounts;

    public GetAccountsQueryHandler(IAccountRepository accounts) => _accounts = accounts;

    public async Task<Result<IReadOnlyList<AccountDto>>> Handle(GetAccountsQuery request, CancellationToken cancellationToken)
    {
        var accounts = await _accounts.ListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<AccountDto>>(accounts.Select(a => a.ToDto()).ToList());
    }
}
