using Accountrack.Application.Abstractions.Context;
using Accountrack.Application.Abstractions.Idempotency;
using Accountrack.Application.Abstractions.Messaging;
using Accountrack.Modules.Contracts.Approval;
using Accountrack.Modules.Contracts.Company;
using Accountrack.Modules.Contracts.MasterData;
using Accountrack.Sales.Application.Abstractions;
using Accountrack.Sales.Domain;
using Accountrack.SharedKernel.Results;
using FluentValidation;

namespace Accountrack.Sales.Application.Features;

public static class SalesDocumentTypes
{
    public const string SalesOrder = "SalesOrder";
}

public sealed record CreateSoLine(Guid ProductId, decimal Quantity, decimal UnitPrice, decimal TaxRate, string? Description);

public sealed record CreateSalesOrderCommand(
    Guid CustomerId, Guid WarehouseId, DateOnly OrderDate, string? Notes,
    IReadOnlyList<CreateSoLine> Lines) : ICommand<Guid>, IIdempotentCommand;

public sealed class CreateSalesOrderValidator : AbstractValidator<CreateSalesOrderCommand>
{
    public CreateSalesOrderValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.Lines).NotEmpty().WithMessage("A sales order requires at least one line.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.ProductId).NotEmpty();
            l.RuleFor(x => x.Quantity).GreaterThan(0);
            l.RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0);
            l.RuleFor(x => x.TaxRate).InclusiveBetween(0m, 1m);
        });
    }
}

public sealed class CreateSalesOrderHandler : ICommandHandler<CreateSalesOrderCommand, Guid>
{
    private readonly ISalesOrderRepository _orders;
    private readonly IMasterDataLookup _masterData;
    private readonly ICompanyDirectory _companies;
    private readonly ITenantContext _tenant;
    private readonly ISalesUnitOfWork _uow;

    public CreateSalesOrderHandler(
        ISalesOrderRepository orders, IMasterDataLookup masterData, ICompanyDirectory companies,
        ITenantContext tenant, ISalesUnitOfWork uow)
    {
        _orders = orders;
        _masterData = masterData;
        _companies = companies;
        _tenant = tenant;
        _uow = uow;
    }

    public async Task<Result<Guid>> Handle(CreateSalesOrderCommand request, CancellationToken ct)
    {
        if (!await _masterData.CustomerExistsAsync(request.CustomerId, ct))
        {
            return SalesErrors.CustomerNotFound;
        }

        if (!await _masterData.WarehouseExistsAsync(request.WarehouseId, ct))
        {
            return SalesErrors.WarehouseNotFound;
        }

        foreach (var line in request.Lines)
        {
            if (!await _masterData.ProductExistsAsync(line.ProductId, ct))
            {
                return SalesErrors.ProductNotFound(line.ProductId);
            }
        }

        var company = await _companies.GetAsync(_tenant.CompanyId, ct);
        if (company is null)
        {
            return Error.NotFound("SALES.COMPANY_NOT_FOUND", "Active company not found.");
        }

        var sequence = await _orders.GetSequenceAsync(ct);
        if (sequence is null)
        {
            sequence = new SalesOrderNumberSequence();
            _orders.AddSequence(sequence);
        }

        var number = sequence.Take(request.OrderDate);
        var order = SalesOrder.CreateDraft(
            number, request.CustomerId, request.WarehouseId, company.FunctionalCurrency, request.OrderDate, request.Notes);

        foreach (var line in request.Lines)
        {
            order.AddLine(line.ProductId, line.Quantity, line.UnitPrice, line.TaxRate, line.Description);
        }

        _orders.Add(order);
        await _uow.SaveChangesAsync(ct);
        return order.Id;
    }
}

public sealed record SubmitSalesOrderCommand(Guid Id) : ICommand<string>;

public sealed class SubmitSalesOrderHandler : ICommandHandler<SubmitSalesOrderCommand, string>
{
    private readonly ISalesOrderRepository _orders;
    private readonly IApprovalService _approval;
    private readonly ISalesUnitOfWork _uow;

    public SubmitSalesOrderHandler(ISalesOrderRepository orders, IApprovalService approval, ISalesUnitOfWork uow)
    {
        _orders = orders;
        _approval = approval;
        _uow = uow;
    }

    public async Task<Result<string>> Handle(SubmitSalesOrderCommand request, CancellationToken ct)
    {
        var order = await _orders.GetByIdAsync(request.Id, ct);
        if (order is null)
        {
            return SalesErrors.NotFound;
        }

        if (order.Status != SalesOrderStatus.Draft)
        {
            return SalesErrors.NotDraft;
        }

        if (order.Lines.Count == 0)
        {
            return SalesErrors.NoLines;
        }

        var submission = await _approval.SubmitAsync(
            SalesDocumentTypes.SalesOrder, order.Id,
            new Dictionary<string, decimal> { ["Total"] = order.GrandTotal }, ct);

        if (submission.Status == "AutoApproved")
        {
            order.MarkAutoApproved(submission.RequestId);
        }
        else
        {
            order.MarkPendingApproval(submission.RequestId);
        }

        await _uow.SaveChangesAsync(ct);
        return order.Status.ToString();
    }
}
