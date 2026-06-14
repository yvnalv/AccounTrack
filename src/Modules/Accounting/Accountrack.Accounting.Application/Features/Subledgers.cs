using Accountrack.Accounting.Application.Abstractions;
using Accountrack.Accounting.Application.Contracts;
using Accountrack.Accounting.Domain;
using Accountrack.Application.Abstractions.Context;
using Accountrack.Application.Abstractions.Messaging;
using Accountrack.Modules.Contracts.Company;
using Accountrack.SharedKernel.Results;
using FluentValidation;

namespace Accountrack.Accounting.Application.Features;

// --- Record an open item (opening balance / manual; reused by invoice posting) ---

public sealed record RecordOpenItemCommand(
    SubledgerType Type, Guid PartyId, string DocumentNo, DateOnly DocumentDate, DateOnly DueDate, decimal Amount)
    : ICommand<Guid>;

public sealed class RecordOpenItemCommandValidator : AbstractValidator<RecordOpenItemCommand>
{
    public RecordOpenItemCommandValidator()
    {
        RuleFor(x => x.PartyId).NotEmpty();
        RuleFor(x => x.DocumentNo).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.DueDate).GreaterThanOrEqualTo(x => x.DocumentDate)
            .WithMessage("Due date cannot be before the document date.");
    }
}

public sealed class RecordOpenItemCommandHandler : ICommandHandler<RecordOpenItemCommand, Guid>
{
    private readonly ISubledgerService _subledger;
    private readonly ICompanyDirectory _companies;
    private readonly ITenantContext _tenant;
    private readonly IAccountingUnitOfWork _uow;

    public RecordOpenItemCommandHandler(
        ISubledgerService subledger, ICompanyDirectory companies, ITenantContext tenant, IAccountingUnitOfWork uow)
    {
        _subledger = subledger;
        _companies = companies;
        _tenant = tenant;
        _uow = uow;
    }

    public async Task<Result<Guid>> Handle(RecordOpenItemCommand request, CancellationToken cancellationToken)
    {
        var company = await _companies.GetAsync(_tenant.CompanyId, cancellationToken);
        if (company is null)
        {
            return Error.NotFound("ACCOUNTING.COMPANY_NOT_FOUND", "Active company not found.");
        }

        var result = await _subledger.OpenItemAsync(
            request.Type, request.PartyId, JournalSource.Manual, null,
            request.DocumentNo, request.DocumentDate, request.DueDate, request.Amount,
            company.FunctionalCurrency, cancellationToken);

        if (result.IsFailure)
        {
            return result.Error;
        }

        await _uow.SaveChangesAsync(cancellationToken);
        return result.Value;
    }
}

// --- Allocate a payment to an open item ---

public sealed record AllocatePaymentCommand(
    Guid OpenItemId, string PaymentReference, DateOnly Date, decimal Amount) : ICommand<Guid>;

public sealed class AllocatePaymentCommandValidator : AbstractValidator<AllocatePaymentCommand>
{
    public AllocatePaymentCommandValidator()
    {
        RuleFor(x => x.OpenItemId).NotEmpty();
        RuleFor(x => x.PaymentReference).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Amount).GreaterThan(0);
    }
}

public sealed class AllocatePaymentCommandHandler : ICommandHandler<AllocatePaymentCommand, Guid>
{
    private readonly ISubledgerService _subledger;
    private readonly IAccountingUnitOfWork _uow;

    public AllocatePaymentCommandHandler(ISubledgerService subledger, IAccountingUnitOfWork uow)
    {
        _subledger = subledger;
        _uow = uow;
    }

    public async Task<Result<Guid>> Handle(AllocatePaymentCommand request, CancellationToken cancellationToken)
    {
        var result = await _subledger.AllocateAsync(
            request.OpenItemId, request.PaymentReference, request.Date, request.Amount, null, cancellationToken);

        if (result.IsFailure)
        {
            return result.Error;
        }

        await _uow.SaveChangesAsync(cancellationToken);
        return result.Value;
    }
}

// --- List open items ---

public sealed record GetOpenItemsQuery(SubledgerType Type, Guid? PartyId, bool IncludeSettled)
    : IQuery<IReadOnlyList<SubledgerOpenItemDto>>;

public sealed class GetOpenItemsQueryHandler : IQueryHandler<GetOpenItemsQuery, IReadOnlyList<SubledgerOpenItemDto>>
{
    private readonly ISubledgerRepository _items;

    public GetOpenItemsQueryHandler(ISubledgerRepository items) => _items = items;

    public async Task<Result<IReadOnlyList<SubledgerOpenItemDto>>> Handle(GetOpenItemsQuery request, CancellationToken cancellationToken)
    {
        var items = await _items.ListAsync(request.Type, request.PartyId, request.IncludeSettled, cancellationToken);

        var dtos = items
            .OrderBy(i => i.DueDate)
            .Select(i => new SubledgerOpenItemDto(
                i.Id, i.Type.ToString(), i.PartyId, i.SourceType.ToString(), i.DocumentNo,
                i.DocumentDate, i.DueDate, i.Currency,
                i.OriginalAmount.Amount, i.SettledAmount.Amount, i.OutstandingAmount.Amount, i.Status.ToString()))
            .ToList();

        return dtos;
    }
}

// --- Aging report ---

public sealed record GetAgingQuery(SubledgerType Type, DateOnly AsOfDate) : IQuery<AgingReportDto>;

public sealed class GetAgingQueryHandler : IQueryHandler<GetAgingQuery, AgingReportDto>
{
    private readonly ISubledgerRepository _items;

    public GetAgingQueryHandler(ISubledgerRepository items) => _items = items;

    public async Task<Result<AgingReportDto>> Handle(GetAgingQuery request, CancellationToken cancellationToken)
    {
        var items = await _items.ListAsync(request.Type, partyId: null, includeSettled: false, cancellationToken);

        var rows = items
            .Where(i => i.OutstandingAmount.Amount > 0)
            .GroupBy(i => i.PartyId)
            .Select(g =>
            {
                decimal current = 0, b1 = 0, b2 = 0, b3 = 0, b4 = 0;
                foreach (var item in g)
                {
                    var outstanding = item.OutstandingAmount.Amount;
                    var daysPastDue = request.AsOfDate.DayNumber - item.DueDate.DayNumber;
                    switch (daysPastDue)
                    {
                        case <= 0: current += outstanding; break;
                        case <= 30: b1 += outstanding; break;
                        case <= 60: b2 += outstanding; break;
                        case <= 90: b3 += outstanding; break;
                        default: b4 += outstanding; break;
                    }
                }

                return new AgingRowDto(g.Key, current, b1, b2, b3, b4, current + b1 + b2 + b3 + b4);
            })
            .OrderByDescending(r => r.Total)
            .ToList();

        var report = new AgingReportDto(
            request.Type.ToString(), request.AsOfDate, rows,
            rows.Sum(r => r.Current), rows.Sum(r => r.Days1To30), rows.Sum(r => r.Days31To60),
            rows.Sum(r => r.Days61To90), rows.Sum(r => r.Days90Plus), rows.Sum(r => r.Total));

        return report;
    }
}
