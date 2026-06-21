using Accountrack.Application.Abstractions.Context;
using Accountrack.Application.Abstractions.Idempotency;
using Accountrack.Application.Abstractions.Messaging;
using Accountrack.Modules.Contracts.Approval;
using Accountrack.Modules.Contracts.Company;
using Accountrack.Modules.Contracts.MasterData;
using Accountrack.Purchasing.Application.Abstractions;
using Accountrack.Purchasing.Application.Contracts;
using Accountrack.Purchasing.Domain;
using Accountrack.SharedKernel.Results;
using FluentValidation;

namespace Accountrack.Purchasing.Application.Features;

public static class PurchasingDocumentTypes
{
    public const string PurchaseOrder = "PurchaseOrder";
}

public sealed record CreatePoLine(Guid ProductId, decimal Quantity, decimal UnitPrice, decimal TaxRate, string? Description);

public sealed record CreatePurchaseOrderCommand(
    Guid SupplierId, Guid WarehouseId, DateOnly OrderDate, string? Notes,
    IReadOnlyList<CreatePoLine> Lines) : ICommand<Guid>, IIdempotentCommand;

public sealed class CreatePurchaseOrderValidator : AbstractValidator<CreatePurchaseOrderCommand>
{
    public CreatePurchaseOrderValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty();
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.Lines).NotEmpty().WithMessage("A purchase order requires at least one line.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.ProductId).NotEmpty();
            l.RuleFor(x => x.Quantity).GreaterThan(0);
            l.RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0);
            l.RuleFor(x => x.TaxRate).InclusiveBetween(0m, 1m);
        });
    }
}

public sealed class CreatePurchaseOrderHandler : ICommandHandler<CreatePurchaseOrderCommand, Guid>
{
    private readonly IPurchaseOrderRepository _orders;
    private readonly IMasterDataLookup _masterData;
    private readonly ICompanyDirectory _companies;
    private readonly ITenantContext _tenant;
    private readonly IPurchasingUnitOfWork _uow;

    public CreatePurchaseOrderHandler(
        IPurchaseOrderRepository orders, IMasterDataLookup masterData, ICompanyDirectory companies,
        ITenantContext tenant, IPurchasingUnitOfWork uow)
    {
        _orders = orders;
        _masterData = masterData;
        _companies = companies;
        _tenant = tenant;
        _uow = uow;
    }

    public async Task<Result<Guid>> Handle(CreatePurchaseOrderCommand request, CancellationToken ct)
    {
        if (!await _masterData.SupplierExistsAsync(request.SupplierId, ct))
        {
            return PurchasingErrors.SupplierNotFound;
        }

        if (!await _masterData.WarehouseExistsAsync(request.WarehouseId, ct))
        {
            return PurchasingErrors.WarehouseNotFound;
        }

        foreach (var line in request.Lines)
        {
            if (!await _masterData.ProductExistsAsync(line.ProductId, ct))
            {
                return PurchasingErrors.ProductNotFound(line.ProductId);
            }
        }

        var company = await _companies.GetAsync(_tenant.CompanyId, ct);
        if (company is null)
        {
            return Error.NotFound("PURCHASING.COMPANY_NOT_FOUND", "Active company not found.");
        }

        var sequence = await _orders.GetSequenceAsync(ct);
        if (sequence is null)
        {
            sequence = new PurchaseOrderNumberSequence();
            _orders.AddSequence(sequence);
        }

        var number = sequence.Take(request.OrderDate);
        var order = PurchaseOrder.CreateDraft(
            number, request.SupplierId, request.WarehouseId, company.FunctionalCurrency, request.OrderDate, request.Notes);

        foreach (var line in request.Lines)
        {
            order.AddLine(line.ProductId, line.Quantity, line.UnitPrice, line.TaxRate, line.Description);
        }

        _orders.Add(order);
        await _uow.SaveChangesAsync(ct);
        return order.Id;
    }
}

public sealed record UpdatePurchaseOrderCommand(
    Guid Id, Guid SupplierId, Guid WarehouseId, DateOnly OrderDate, string? Notes,
    IReadOnlyList<CreatePoLine> Lines) : ICommand<Guid>;

public sealed class UpdatePurchaseOrderValidator : AbstractValidator<UpdatePurchaseOrderCommand>
{
    public UpdatePurchaseOrderValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.SupplierId).NotEmpty();
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.Lines).NotEmpty().WithMessage("A purchase order requires at least one line.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.ProductId).NotEmpty();
            l.RuleFor(x => x.Quantity).GreaterThan(0);
            l.RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0);
            l.RuleFor(x => x.TaxRate).InclusiveBetween(0m, 1m);
        });
    }
}

/// <summary>Edits a still-draft purchase order's header + lines before submission (ADR-0029, BR-X-8).</summary>
public sealed class UpdatePurchaseOrderHandler : ICommandHandler<UpdatePurchaseOrderCommand, Guid>
{
    private readonly IPurchaseOrderRepository _orders;
    private readonly IMasterDataLookup _masterData;
    private readonly IPurchasingUnitOfWork _uow;

    public UpdatePurchaseOrderHandler(IPurchaseOrderRepository orders, IMasterDataLookup masterData, IPurchasingUnitOfWork uow)
    {
        _orders = orders;
        _masterData = masterData;
        _uow = uow;
    }

    public async Task<Result<Guid>> Handle(UpdatePurchaseOrderCommand request, CancellationToken ct)
    {
        var order = await _orders.GetByIdAsync(request.Id, ct);
        if (order is null)
        {
            return PurchasingErrors.NotFound;
        }

        if (order.Status != PurchaseOrderStatus.Draft)
        {
            return PurchasingErrors.NotDraft;
        }

        if (!await _masterData.SupplierExistsAsync(request.SupplierId, ct))
        {
            return PurchasingErrors.SupplierNotFound;
        }

        if (!await _masterData.WarehouseExistsAsync(request.WarehouseId, ct))
        {
            return PurchasingErrors.WarehouseNotFound;
        }

        foreach (var line in request.Lines)
        {
            if (!await _masterData.ProductExistsAsync(line.ProductId, ct))
            {
                return PurchasingErrors.ProductNotFound(line.ProductId);
            }
        }

        order.EditDraft(
            request.SupplierId, request.WarehouseId, request.OrderDate, request.Notes,
            request.Lines.Select(l => (l.ProductId, l.Quantity, l.UnitPrice, l.TaxRate, l.Description)));

        await _uow.SaveChangesAsync(ct);
        return order.Id;
    }
}

public sealed record SubmitPurchaseOrderCommand(Guid Id) : ICommand<string>;

public sealed class SubmitPurchaseOrderHandler : ICommandHandler<SubmitPurchaseOrderCommand, string>
{
    private readonly IPurchaseOrderRepository _orders;
    private readonly IApprovalService _approval;
    private readonly IPurchasingUnitOfWork _uow;

    public SubmitPurchaseOrderHandler(IPurchaseOrderRepository orders, IApprovalService approval, IPurchasingUnitOfWork uow)
    {
        _orders = orders;
        _approval = approval;
        _uow = uow;
    }

    public async Task<Result<string>> Handle(SubmitPurchaseOrderCommand request, CancellationToken ct)
    {
        var order = await _orders.GetByIdAsync(request.Id, ct);
        if (order is null)
        {
            return PurchasingErrors.NotFound;
        }

        if (order.Status != PurchaseOrderStatus.Draft)
        {
            return PurchasingErrors.NotDraft;
        }

        if (order.Lines.Count == 0)
        {
            return PurchasingErrors.NoLines;
        }

        var submission = await _approval.SubmitAsync(
            PurchasingDocumentTypes.PurchaseOrder, order.Id,
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

/// <summary>Cancels a draft (or pending-approval) purchase order (ADR-0029). Approved/decided or
/// received orders are immutable and must be reversed via returns, not cancelled.</summary>
public sealed record CancelPurchaseOrderCommand(Guid Id) : ICommand<Guid>;

public sealed class CancelPurchaseOrderHandler : ICommandHandler<CancelPurchaseOrderCommand, Guid>
{
    private readonly IPurchaseOrderRepository _orders;
    private readonly IPurchasingUnitOfWork _uow;

    public CancelPurchaseOrderHandler(IPurchaseOrderRepository orders, IPurchasingUnitOfWork uow)
    {
        _orders = orders;
        _uow = uow;
    }

    public async Task<Result<Guid>> Handle(CancelPurchaseOrderCommand request, CancellationToken ct)
    {
        var order = await _orders.GetByIdAsync(request.Id, ct);
        if (order is null)
        {
            return PurchasingErrors.NotFound;
        }

        if (!order.CanCancel)
        {
            return PurchasingErrors.NotCancellable;
        }

        order.Cancel();
        await _uow.SaveChangesAsync(ct);
        return order.Id;
    }
}
