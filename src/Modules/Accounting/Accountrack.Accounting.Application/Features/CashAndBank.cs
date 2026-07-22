using Accountrack.Accounting.Application.Abstractions;
using Accountrack.Accounting.Domain;
using Accountrack.Application.Abstractions.Context;
using Accountrack.Application.Abstractions.Idempotency;
using Accountrack.Application.Abstractions.Messaging;
using Accountrack.Modules.Contracts.Approval;
using Accountrack.Modules.Contracts.Company;
using Accountrack.SharedKernel.Results;
using FluentValidation;

namespace Accountrack.Accounting.Application.Features;

/// <summary>Approval document types owned by the Accounting module.</summary>
public static class AccountingDocumentTypes
{
    /// <summary>A manual journal or guided Cash & Bank flow submitted for approval (ADR-0040).</summary>
    public const string ManualJournal = "ManualJournal";
}

/// <summary>The outcome of a manual journal / guided flow: the entry id and whether it posted
/// immediately ("Posted") or is waiting for approval ("Pending").</summary>
public sealed record ManualJournalResult(Guid Id, string Status);

/// <summary>One assembled journal line for <see cref="IManualJournalService"/>.</summary>
public sealed record ManualJournalLine(Guid AccountId, decimal Debit, decimal Credit, string? Description);

/// <summary>
/// Shared engine behind the General Journal and every guided Cash & Bank flow (ADR-0040): validates a
/// balanced set of lines against postable accounts, then routes the journal through the Approval
/// Workflow exactly like an Expense voucher (ADR-0030) — auto-approve &amp; post now when no rule
/// matches, otherwise hold as PendingApproval for the approval consumer to post.
/// </summary>
public interface IManualJournalService
{
    Task<Result<ManualJournalResult>> SubmitAsync(
        DateOnly date, string description, JournalSource source,
        IReadOnlyList<ManualJournalLine> lines, CancellationToken ct);

    /// <summary>Resolves a company-wide default posting rule (AnyEvent, no selectors) to its account.</summary>
    Task<Result<Guid>> ResolveDefaultAsync(string ruleKey, CancellationToken ct);
}

public sealed class ManualJournalService : IManualJournalService
{
    private readonly ICompanyDirectory _companies;
    private readonly ITenantContext _tenant;
    private readonly IAccountRepository _accounts;
    private readonly IJournalRepository _journals;
    private readonly IJournalPoster _poster;
    private readonly IApprovalService _approval;
    private readonly IPostingRuleResolver _rules;
    private readonly IAccountingUnitOfWork _uow;

    public ManualJournalService(
        ICompanyDirectory companies, ITenantContext tenant, IAccountRepository accounts,
        IJournalRepository journals, IJournalPoster poster, IApprovalService approval,
        IPostingRuleResolver rules, IAccountingUnitOfWork uow)
    {
        _companies = companies;
        _tenant = tenant;
        _accounts = accounts;
        _journals = journals;
        _poster = poster;
        _approval = approval;
        _rules = rules;
        _uow = uow;
    }

    public Task<Result<Guid>> ResolveDefaultAsync(string ruleKey, CancellationToken ct) =>
        _rules.ResolveAsync(PostingRule.AnyEvent, ruleKey, PostingSelector.None, ct);

    public async Task<Result<ManualJournalResult>> SubmitAsync(
        DateOnly date, string description, JournalSource source,
        IReadOnlyList<ManualJournalLine> lines, CancellationToken ct)
    {
        var company = await _companies.GetAsync(_tenant.CompanyId, ct);
        if (company is null)
        {
            return AccountingErrors.CompanyNotFound;
        }

        if (lines.Count < 2)
        {
            return AccountingErrors.TooFewLines;
        }

        // Fail fast on balance and account validity (both paths — the held path is not validated again
        // until the approval consumer posts it).
        var totalDebit = lines.Sum(l => l.Debit);
        var totalCredit = lines.Sum(l => l.Credit);
        if (decimal.Round(totalDebit, 4) != decimal.Round(totalCredit, 4))
        {
            return AccountingErrors.Unbalanced;
        }

        var accountIds = lines.Select(l => l.AccountId).Distinct().ToArray();
        var accounts = await _accounts.GetByIdsAsync(accountIds, ct);
        foreach (var id in accountIds)
        {
            if (!accounts.TryGetValue(id, out var account))
            {
                return AccountingErrors.AccountNotFound;
            }

            if (!account.IsActive || !account.AllowPosting)
            {
                return AccountingErrors.AccountNotPostable(account.Code);
            }
        }

        var draft = JournalEntry.CreateDraft(date, company.FunctionalCurrency, source, null, description.Trim());
        foreach (var line in lines)
        {
            draft.AddLine(line.AccountId, line.Debit, line.Credit, line.Description);
        }

        // Route through approval (ADR-0040): a matching definition holds it; otherwise it auto-posts now.
        var submission = await _approval.SubmitAsync(
            AccountingDocumentTypes.ManualJournal, draft.Id,
            new Dictionary<string, decimal> { ["Total"] = totalDebit }, ct);

        if (submission.Status != "AutoApproved")
        {
            draft.SubmitForApproval(submission.RequestId);
            _journals.Add(draft);
            await _uow.SaveChangesAsync(ct);
            return new ManualJournalResult(draft.Id, "Pending");
        }

        var posted = await _poster.PostAsync(draft, ct);
        if (posted.IsFailure)
        {
            return posted.Error;
        }

        await _uow.SaveChangesAsync(ct);
        return new ManualJournalResult(draft.Id, "Posted");
    }
}

// --- Guided Cash & Bank flows (ADR-0040) ------------------------------------------------------------
// Each is a thin, opinionated wrapper that assembles a balanced journal (accounts via the posting-rule
// engine where a default applies — never hardcoded, Rule 27) and submits it through the shared service.

/// <summary>Owner injects money: Dr Cash/Bank / Cr Owner's Capital (or a chosen equity account).</summary>
public sealed record RecordCapitalContributionCommand(
    DateOnly Date, decimal Amount, Guid CashAccountId, Guid? EquityAccountId, string? Memo)
    : ICommand<ManualJournalResult>, IIdempotentCommand;

public sealed class RecordCapitalContributionValidator : AbstractValidator<RecordCapitalContributionCommand>
{
    public RecordCapitalContributionValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.CashAccountId).NotEmpty();
    }
}

public sealed class RecordCapitalContributionHandler : ICommandHandler<RecordCapitalContributionCommand, ManualJournalResult>
{
    private readonly IManualJournalService _service;
    public RecordCapitalContributionHandler(IManualJournalService service) => _service = service;

    public async Task<Result<ManualJournalResult>> Handle(RecordCapitalContributionCommand r, CancellationToken ct)
    {
        var equity = r.EquityAccountId is { } id
            ? Result.Success(id)
            : await _service.ResolveDefaultAsync(PostingRuleKeys.OwnerCapital, ct);
        if (equity.IsFailure)
        {
            return equity.Error;
        }

        var description = string.IsNullOrWhiteSpace(r.Memo) ? "Capital contribution" : r.Memo!.Trim();
        return await _service.SubmitAsync(r.Date, description, JournalSource.CapitalContribution, new[]
        {
            new ManualJournalLine(r.CashAccountId, r.Amount, 0, description),
            new ManualJournalLine(equity.Value, 0, r.Amount, description),
        }, ct);
    }
}

/// <summary>Owner takes money out: Dr Owner's Drawings / Cr Cash/Bank.</summary>
public sealed record RecordOwnerDrawingCommand(
    DateOnly Date, decimal Amount, Guid CashAccountId, Guid? DrawingsAccountId, string? Memo)
    : ICommand<ManualJournalResult>, IIdempotentCommand;

public sealed class RecordOwnerDrawingValidator : AbstractValidator<RecordOwnerDrawingCommand>
{
    public RecordOwnerDrawingValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.CashAccountId).NotEmpty();
    }
}

public sealed class RecordOwnerDrawingHandler : ICommandHandler<RecordOwnerDrawingCommand, ManualJournalResult>
{
    private readonly IManualJournalService _service;
    public RecordOwnerDrawingHandler(IManualJournalService service) => _service = service;

    public async Task<Result<ManualJournalResult>> Handle(RecordOwnerDrawingCommand r, CancellationToken ct)
    {
        var drawings = r.DrawingsAccountId is { } id
            ? Result.Success(id)
            : await _service.ResolveDefaultAsync(PostingRuleKeys.OwnerDrawings, ct);
        if (drawings.IsFailure)
        {
            return drawings.Error;
        }

        var description = string.IsNullOrWhiteSpace(r.Memo) ? "Owner's drawing" : r.Memo!.Trim();
        return await _service.SubmitAsync(r.Date, description, JournalSource.OwnerDrawing, new[]
        {
            new ManualJournalLine(drawings.Value, r.Amount, 0, description),
            new ManualJournalLine(r.CashAccountId, 0, r.Amount, description),
        }, ct);
    }
}

/// <summary>Move money between two cash/bank accounts: Dr destination / Cr source (GL-neutral).</summary>
public sealed record RecordBankTransferCommand(
    DateOnly Date, decimal Amount, Guid FromAccountId, Guid ToAccountId, string? Memo)
    : ICommand<ManualJournalResult>, IIdempotentCommand;

public sealed class RecordBankTransferValidator : AbstractValidator<RecordBankTransferCommand>
{
    public RecordBankTransferValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.FromAccountId).NotEmpty();
        RuleFor(x => x.ToAccountId).NotEmpty();
    }
}

public sealed class RecordBankTransferHandler : ICommandHandler<RecordBankTransferCommand, ManualJournalResult>
{
    private readonly IManualJournalService _service;
    public RecordBankTransferHandler(IManualJournalService service) => _service = service;

    public async Task<Result<ManualJournalResult>> Handle(RecordBankTransferCommand r, CancellationToken ct)
    {
        if (r.FromAccountId == r.ToAccountId)
        {
            return AccountingErrors.TransferSameAccount;
        }

        var description = string.IsNullOrWhiteSpace(r.Memo) ? "Cash/bank transfer" : r.Memo!.Trim();
        return await _service.SubmitAsync(r.Date, description, JournalSource.BankTransfer, new[]
        {
            new ManualJournalLine(r.ToAccountId, r.Amount, 0, description),
            new ManualJournalLine(r.FromAccountId, 0, r.Amount, description),
        }, ct);
    }
}

/// <summary>Money in that is not a customer payment: Dr Cash/Bank / Cr a chosen income/source account.</summary>
public sealed record RecordMoneyReceiptCommand(
    DateOnly Date, decimal Amount, Guid CashAccountId, Guid CreditAccountId, string? Memo)
    : ICommand<ManualJournalResult>, IIdempotentCommand;

public sealed class RecordMoneyReceiptValidator : AbstractValidator<RecordMoneyReceiptCommand>
{
    public RecordMoneyReceiptValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.CashAccountId).NotEmpty();
        RuleFor(x => x.CreditAccountId).NotEmpty();
    }
}

public sealed class RecordMoneyReceiptHandler : ICommandHandler<RecordMoneyReceiptCommand, ManualJournalResult>
{
    private readonly IManualJournalService _service;
    public RecordMoneyReceiptHandler(IManualJournalService service) => _service = service;

    public async Task<Result<ManualJournalResult>> Handle(RecordMoneyReceiptCommand r, CancellationToken ct)
    {
        var description = string.IsNullOrWhiteSpace(r.Memo) ? "Money received" : r.Memo!.Trim();
        return await _service.SubmitAsync(r.Date, description, JournalSource.MoneyReceipt, new[]
        {
            new ManualJournalLine(r.CashAccountId, r.Amount, 0, description),
            new ManualJournalLine(r.CreditAccountId, 0, r.Amount, description),
        }, ct);
    }
}

/// <summary>Money out that is not a supplier payment: Dr a chosen expense/asset account / Cr Cash/Bank.</summary>
public sealed record RecordMoneySpentCommand(
    DateOnly Date, decimal Amount, Guid CashAccountId, Guid DebitAccountId, string? Memo)
    : ICommand<ManualJournalResult>, IIdempotentCommand;

public sealed class RecordMoneySpentValidator : AbstractValidator<RecordMoneySpentCommand>
{
    public RecordMoneySpentValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.CashAccountId).NotEmpty();
        RuleFor(x => x.DebitAccountId).NotEmpty();
    }
}

public sealed class RecordMoneySpentHandler : ICommandHandler<RecordMoneySpentCommand, ManualJournalResult>
{
    private readonly IManualJournalService _service;
    public RecordMoneySpentHandler(IManualJournalService service) => _service = service;

    public async Task<Result<ManualJournalResult>> Handle(RecordMoneySpentCommand r, CancellationToken ct)
    {
        var description = string.IsNullOrWhiteSpace(r.Memo) ? "Money spent" : r.Memo!.Trim();
        return await _service.SubmitAsync(r.Date, description, JournalSource.MoneyPayment, new[]
        {
            new ManualJournalLine(r.DebitAccountId, r.Amount, 0, description),
            new ManualJournalLine(r.CashAccountId, 0, r.Amount, description),
        }, ct);
    }
}

/// <summary>Loan drawn down: Dr Cash/Bank / Cr Loan Payable (or a chosen liability account).</summary>
public sealed record RecordLoanReceiptCommand(
    DateOnly Date, decimal Amount, Guid CashAccountId, Guid? LoanAccountId, string? Memo)
    : ICommand<ManualJournalResult>, IIdempotentCommand;

public sealed class RecordLoanReceiptValidator : AbstractValidator<RecordLoanReceiptCommand>
{
    public RecordLoanReceiptValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.CashAccountId).NotEmpty();
    }
}

public sealed class RecordLoanReceiptHandler : ICommandHandler<RecordLoanReceiptCommand, ManualJournalResult>
{
    private readonly IManualJournalService _service;
    public RecordLoanReceiptHandler(IManualJournalService service) => _service = service;

    public async Task<Result<ManualJournalResult>> Handle(RecordLoanReceiptCommand r, CancellationToken ct)
    {
        var loan = r.LoanAccountId is { } id
            ? Result.Success(id)
            : await _service.ResolveDefaultAsync(PostingRuleKeys.LoanPayable, ct);
        if (loan.IsFailure)
        {
            return loan.Error;
        }

        var description = string.IsNullOrWhiteSpace(r.Memo) ? "Loan received" : r.Memo!.Trim();
        return await _service.SubmitAsync(r.Date, description, JournalSource.LoanReceipt, new[]
        {
            new ManualJournalLine(r.CashAccountId, r.Amount, 0, description),
            new ManualJournalLine(loan.Value, 0, r.Amount, description),
        }, ct);
    }
}

/// <summary>Loan repayment: Dr Loan Payable (+ optional Dr Interest Expense) / Cr Cash/Bank.</summary>
public sealed record RecordLoanRepaymentCommand(
    DateOnly Date, decimal Principal, decimal Interest, Guid CashAccountId,
    Guid? LoanAccountId, Guid? InterestAccountId, string? Memo)
    : ICommand<ManualJournalResult>, IIdempotentCommand;

public sealed class RecordLoanRepaymentValidator : AbstractValidator<RecordLoanRepaymentCommand>
{
    public RecordLoanRepaymentValidator()
    {
        RuleFor(x => x.Principal).GreaterThan(0);
        RuleFor(x => x.Interest).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CashAccountId).NotEmpty();
        RuleFor(x => x.InterestAccountId).NotEmpty()
            .When(x => x.Interest > 0)
            .WithMessage("An interest account is required when interest is charged.");
    }
}

public sealed class RecordLoanRepaymentHandler : ICommandHandler<RecordLoanRepaymentCommand, ManualJournalResult>
{
    private readonly IManualJournalService _service;
    public RecordLoanRepaymentHandler(IManualJournalService service) => _service = service;

    public async Task<Result<ManualJournalResult>> Handle(RecordLoanRepaymentCommand r, CancellationToken ct)
    {
        var loan = r.LoanAccountId is { } id
            ? Result.Success(id)
            : await _service.ResolveDefaultAsync(PostingRuleKeys.LoanPayable, ct);
        if (loan.IsFailure)
        {
            return loan.Error;
        }

        var description = string.IsNullOrWhiteSpace(r.Memo) ? "Loan repayment" : r.Memo!.Trim();
        var lines = new List<ManualJournalLine>
        {
            new(loan.Value, r.Principal, 0, description),
        };
        if (r.Interest > 0)
        {
            lines.Add(new ManualJournalLine(r.InterestAccountId!.Value, r.Interest, 0, "Loan interest"));
        }

        lines.Add(new ManualJournalLine(r.CashAccountId, 0, r.Principal + r.Interest, description));
        return await _service.SubmitAsync(r.Date, description, JournalSource.LoanRepayment, lines, ct);
    }
}
