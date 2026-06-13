using Accountrack.Accounting.Application.Abstractions;
using Accountrack.Accounting.Application.Contracts;
using Accountrack.Accounting.Domain;
using Accountrack.Application.Abstractions.Messaging;
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
