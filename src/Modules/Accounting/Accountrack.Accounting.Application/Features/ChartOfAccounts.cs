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

// ---- Edit an account (ADR-0029) — Code/Type are immutable; name + postability are editable ----
public sealed record UpdateAccountCommand(Guid Id, string Name, bool AllowPosting) : ICommand<Guid>;

public sealed class UpdateAccountCommandValidator : AbstractValidator<UpdateAccountCommand>
{
    public UpdateAccountCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

public sealed class UpdateAccountCommandHandler : ICommandHandler<UpdateAccountCommand, Guid>
{
    private readonly IAccountRepository _accounts;
    private readonly IAccountingUnitOfWork _uow;

    public UpdateAccountCommandHandler(IAccountRepository accounts, IAccountingUnitOfWork uow)
    {
        _accounts = accounts;
        _uow = uow;
    }

    public async Task<Result<Guid>> Handle(UpdateAccountCommand request, CancellationToken ct)
    {
        var account = await _accounts.GetByIdAsync(request.Id, ct);
        if (account is null)
        {
            return AccountingErrors.AccountNotFound;
        }

        account.Rename(request.Name);
        account.SetPostingAllowed(request.AllowPosting);
        await _uow.SaveChangesAsync(ct);
        return account.Id;
    }
}

public sealed record SetAccountActiveCommand(Guid Id, bool IsActive) : ICommand<Guid>;

public sealed class SetAccountActiveCommandHandler : ICommandHandler<SetAccountActiveCommand, Guid>
{
    private readonly IAccountRepository _accounts;
    private readonly IAccountingReadStore _readStore;
    private readonly IAccountingUnitOfWork _uow;

    public SetAccountActiveCommandHandler(
        IAccountRepository accounts, IAccountingReadStore readStore, IAccountingUnitOfWork uow)
    {
        _accounts = accounts;
        _readStore = readStore;
        _uow = uow;
    }

    public async Task<Result<Guid>> Handle(SetAccountActiveCommand request, CancellationToken ct)
    {
        var account = await _accounts.GetByIdAsync(request.Id, ct);
        if (account is null)
        {
            return AccountingErrors.AccountNotFound;
        }

        if (request.IsActive)
        {
            account.Activate();
        }
        else
        {
            // Deactivation guards (ADR-0029): system accounts stay; an account with GL activity stays.
            if (account.IsSystem)
            {
                return AccountingErrors.AccountIsSystem;
            }

            var movements = await _readStore.GetAccountMovementsAsync(new[] { account.Id }, null, null, ct);
            if (movements.Any(m => m.Debit != 0m || m.Credit != 0m))
            {
                return AccountingErrors.AccountInUse;
            }

            account.Deactivate();
        }

        await _uow.SaveChangesAsync(ct);
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
