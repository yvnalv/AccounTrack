using Accountrack.Accounting.Application.Abstractions;
using Accountrack.Accounting.Application.Contracts;
using Accountrack.Accounting.Domain;
using Accountrack.Application.Abstractions.Context;
using Accountrack.Application.Abstractions.Messaging;
using Accountrack.Modules.Contracts.Company;
using Accountrack.SharedKernel.Results;
using FluentValidation;

namespace Accountrack.Accounting.Application.Features;

public sealed record CreateFiscalYearCommand(int Year, int StartMonth = 1) : ICommand<Guid>;

public sealed class CreateFiscalYearCommandValidator : AbstractValidator<CreateFiscalYearCommand>
{
    public CreateFiscalYearCommandValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.StartMonth).InclusiveBetween(1, 12);
    }
}

public sealed class CreateFiscalYearCommandHandler : ICommandHandler<CreateFiscalYearCommand, Guid>
{
    private readonly IFiscalPeriodRepository _periods;
    private readonly IAccountingUnitOfWork _uow;

    public CreateFiscalYearCommandHandler(IFiscalPeriodRepository periods, IAccountingUnitOfWork uow)
    {
        _periods = periods;
        _uow = uow;
    }

    public async Task<Result<Guid>> Handle(CreateFiscalYearCommand request, CancellationToken cancellationToken)
    {
        if (await _periods.FiscalYearExistsAsync(request.Year, cancellationToken))
        {
            return AccountingErrors.FiscalYearExists;
        }

        var fy = FiscalYear.Create(request.Year, request.StartMonth);
        _periods.AddFiscalYear(fy);
        await _uow.SaveChangesAsync(cancellationToken);
        return fy.Id;
    }
}

public sealed record CloseFiscalPeriodCommand(Guid PeriodId) : ICommand;

public sealed class CloseFiscalPeriodCommandHandler : ICommandHandler<CloseFiscalPeriodCommand>
{
    private readonly IFiscalPeriodRepository _periods;
    private readonly IAccountingUnitOfWork _uow;

    public CloseFiscalPeriodCommandHandler(IFiscalPeriodRepository periods, IAccountingUnitOfWork uow)
    {
        _periods = periods;
        _uow = uow;
    }

    public async Task<Result> Handle(CloseFiscalPeriodCommand request, CancellationToken cancellationToken)
    {
        var period = await _periods.GetPeriodByIdAsync(request.PeriodId, cancellationToken);
        if (period is null)
        {
            return AccountingErrors.PeriodNotFound;
        }

        period.Close();
        await _uow.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record ReopenFiscalPeriodCommand(Guid PeriodId) : ICommand;

public sealed class ReopenFiscalPeriodCommandHandler : ICommandHandler<ReopenFiscalPeriodCommand>
{
    private readonly IFiscalPeriodRepository _periods;
    private readonly IAccountingUnitOfWork _uow;

    public ReopenFiscalPeriodCommandHandler(IFiscalPeriodRepository periods, IAccountingUnitOfWork uow)
    {
        _periods = periods;
        _uow = uow;
    }

    public async Task<Result> Handle(ReopenFiscalPeriodCommand request, CancellationToken cancellationToken)
    {
        var period = await _periods.GetPeriodByIdAsync(request.PeriodId, cancellationToken);
        if (period is null)
        {
            return AccountingErrors.PeriodNotFound;
        }

        period.Reopen();
        await _uow.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

// ---- Year-end close (closes P&L accounts to Retained Earnings) ----
public sealed record CloseFiscalYearCommand(Guid FiscalYearId) : ICommand<CloseFiscalYearResult>;

/// <summary>
/// Year-end close (ACCOUNTING_DESIGN.md §10): posts a closing journal that zeros every Revenue and
/// Expense account for the year and carries the net result to Retained Earnings, then marks the year
/// closed and locks all its periods. The closing entry is dated at the year-end, so the final period
/// must still be open. Idempotent guard: a year that is already closed is rejected.
/// </summary>
public sealed class CloseFiscalYearCommandHandler : ICommandHandler<CloseFiscalYearCommand, CloseFiscalYearResult>
{
    private readonly IFiscalPeriodRepository _periods;
    private readonly IAccountingReadStore _store;
    private readonly IAccountRepository _accounts;
    private readonly IPostingRuleResolver _resolver;
    private readonly IJournalPoster _poster;
    private readonly ICompanyDirectory _companies;
    private readonly ITenantContext _tenant;
    private readonly IAccountingUnitOfWork _uow;

    public CloseFiscalYearCommandHandler(
        IFiscalPeriodRepository periods, IAccountingReadStore store, IAccountRepository accounts,
        IPostingRuleResolver resolver, IJournalPoster poster, ICompanyDirectory companies,
        ITenantContext tenant, IAccountingUnitOfWork uow)
    {
        _periods = periods;
        _store = store;
        _accounts = accounts;
        _resolver = resolver;
        _poster = poster;
        _companies = companies;
        _tenant = tenant;
        _uow = uow;
    }

    public async Task<Result<CloseFiscalYearResult>> Handle(CloseFiscalYearCommand request, CancellationToken ct)
    {
        var year = await _periods.GetFiscalYearByIdAsync(request.FiscalYearId, ct);
        if (year is null)
        {
            return AccountingErrors.FiscalYearNotFound;
        }

        if (year.IsClosed)
        {
            return AccountingErrors.FiscalYearAlreadyClosed;
        }

        // The closing entry posts on the year-end date, so its period must still be open.
        var finalPeriod = year.PeriodFor(year.EndDate);
        if (finalPeriod is null || !finalPeriod.IsOpen)
        {
            return AccountingErrors.FinalPeriodNotOpen;
        }

        var company = await _companies.GetAsync(_tenant.CompanyId, ct);
        var currency = company?.FunctionalCurrency ?? "IDR";

        var rows = await _store.GetTrialBalanceAsync(year.StartDate, year.EndDate, ct);
        var byCode = (await _accounts.ListAsync(ct)).ToDictionary(a => a.Code);

        var draft = JournalEntry.CreateDraft(
            year.EndDate, currency, JournalSource.PeriodClose, year.Id, $"Year-end close {year.Year}");

        var netIncome = 0m;
        foreach (var row in rows)
        {
            if (!byCode.TryGetValue(row.AccountCode, out var account)) continue;
            if (account.Type is not (AccountType.Revenue or AccountType.Expense)) continue;

            var signed = row.Debit - row.Credit;     // net debit balance
            if (signed == 0m) continue;

            // Reverse the account onto itself to bring it to zero.
            draft.AddLine(account.Id, debit: Math.Max(0m, -signed), credit: Math.Max(0m, signed), $"Close {account.Code}");
            netIncome += row.Credit - row.Debit;      // revenue adds, expense subtracts
        }

        // Nothing to close (no P&L activity): just finalize the year.
        if (draft.Lines.Count == 0)
        {
            year.Close();
            await _uow.SaveChangesAsync(ct);
            return new CloseFiscalYearResult(null, 0m);
        }

        var retainedEarnings = await _resolver.ResolveAsync(
            PostingRule.AnyEvent, PostingRuleKeys.RetainedEarnings, PostingSelector.None, ct);
        if (retainedEarnings.IsFailure)
        {
            return retainedEarnings.Error;
        }

        // Balance the closing entry to Retained Earnings: a profit is a credit, a loss a debit.
        draft.AddLine(
            retainedEarnings.Value,
            debit: netIncome < 0m ? -netIncome : 0m,
            credit: netIncome > 0m ? netIncome : 0m,
            netIncome >= 0m ? "Net income to retained earnings" : "Net loss to retained earnings");

        var posted = await _poster.PostAsync(draft, ct);
        if (posted.IsFailure)
        {
            return posted.Error;
        }

        year.Close();
        await _uow.SaveChangesAsync(ct);
        return new CloseFiscalYearResult(posted.Value, netIncome);
    }
}

public sealed record GetFiscalYearsQuery : IQuery<IReadOnlyList<FiscalYearDto>>;

public sealed class GetFiscalYearsQueryHandler : IQueryHandler<GetFiscalYearsQuery, IReadOnlyList<FiscalYearDto>>
{
    private readonly IFiscalPeriodRepository _periods;

    public GetFiscalYearsQueryHandler(IFiscalPeriodRepository periods) => _periods = periods;

    public async Task<Result<IReadOnlyList<FiscalYearDto>>> Handle(GetFiscalYearsQuery request, CancellationToken cancellationToken)
    {
        var years = await _periods.ListYearsWithPeriodsAsync(cancellationToken);
        var dtos = years.Select(fy => new FiscalYearDto(
            fy.Id, fy.Year, fy.StartDate, fy.EndDate, fy.IsClosed,
            fy.Periods.OrderBy(p => p.PeriodNo)
                .Select(p => new FiscalPeriodDto(p.Id, p.PeriodNo, p.StartDate, p.EndDate, p.Status.ToString()))
                .ToList())).ToList();
        return Result.Success<IReadOnlyList<FiscalYearDto>>(dtos);
    }
}
