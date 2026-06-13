using Accountrack.Application.Abstractions.Context;
using Accountrack.Application.Abstractions.Messaging;
using Accountrack.Inventory.Application.Abstractions;
using Accountrack.Inventory.Application.Contracts;
using Accountrack.Inventory.Domain;
using Accountrack.Modules.Contracts.Company;
using Accountrack.SharedKernel.Results;
using FluentValidation;

namespace Accountrack.Inventory.Application.Features;

// NOTE (slice 1): negative stock is disallowed; reading the company's negative-stock setting and
// posting GL variance/COGS journals on stock moves are deferred (INVENTORY_DESIGN.md §4/§6).
internal static class InventoryPolicy
{
    public const bool AllowNegativeStock = false;
}

// ---- Receive stock (opening balances / manual goods-in) ----
public sealed record ReceiveStockCommand(
    Guid ProductId, Guid WarehouseId, decimal Quantity, decimal UnitCost, DateOnly Date, string? Description)
    : ICommand<StockMovementResult>;

public sealed class ReceiveStockValidator : AbstractValidator<ReceiveStockCommand>
{
    public ReceiveStockValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.UnitCost).GreaterThanOrEqualTo(0);
    }
}

public sealed class ReceiveStockHandler : ICommandHandler<ReceiveStockCommand, StockMovementResult>
{
    private readonly IInventoryLedger _ledger;
    private readonly ICompanyDirectory _companies;
    private readonly ITenantContext _tenant;
    private readonly IInventoryUnitOfWork _uow;

    public ReceiveStockHandler(IInventoryLedger ledger, ICompanyDirectory companies, ITenantContext tenant, IInventoryUnitOfWork uow)
    {
        _ledger = ledger;
        _companies = companies;
        _tenant = tenant;
        _uow = uow;
    }

    public async Task<Result<StockMovementResult>> Handle(ReceiveStockCommand request, CancellationToken ct)
    {
        var company = await _companies.GetAsync(_tenant.CompanyId, ct);
        if (company is null)
        {
            return Error.NotFound("INVENTORY.COMPANY_NOT_FOUND", "Active company not found.");
        }

        var result = await _ledger.ReceiveAsync(
            request.ProductId, request.WarehouseId, company.FunctionalCurrency, request.Quantity, request.UnitCost,
            request.Date, MovementType.Receipt, MovementSource.Manual, null, request.Description, ct);

        if (result.IsFailure)
        {
            return result.Error;
        }

        await _uow.SaveChangesAsync(ct);
        return result.Value;
    }
}

// ---- Stock adjustment (increase/decrease with reason) ----
public sealed record AdjustStockCommand(
    Guid ProductId, Guid WarehouseId, decimal Quantity, bool Increase, decimal? UnitCost, DateOnly Date, string Reason)
    : ICommand<StockMovementResult>;

public sealed class AdjustStockValidator : AbstractValidator<AdjustStockCommand>
{
    public AdjustStockValidator()
    {
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(256);
        RuleFor(x => x.UnitCost).GreaterThanOrEqualTo(0).When(x => x.Increase)
            .WithMessage("A unit cost is required for stock increases.");
    }
}

public sealed class AdjustStockHandler : ICommandHandler<AdjustStockCommand, StockMovementResult>
{
    private readonly IInventoryLedger _ledger;
    private readonly ICompanyDirectory _companies;
    private readonly ITenantContext _tenant;
    private readonly IInventoryUnitOfWork _uow;

    public AdjustStockHandler(IInventoryLedger ledger, ICompanyDirectory companies, ITenantContext tenant, IInventoryUnitOfWork uow)
    {
        _ledger = ledger;
        _companies = companies;
        _tenant = tenant;
        _uow = uow;
    }

    public async Task<Result<StockMovementResult>> Handle(AdjustStockCommand request, CancellationToken ct)
    {
        Result<StockMovementResult> result;
        if (request.Increase)
        {
            var company = await _companies.GetAsync(_tenant.CompanyId, ct);
            if (company is null)
            {
                return Error.NotFound("INVENTORY.COMPANY_NOT_FOUND", "Active company not found.");
            }

            result = await _ledger.ReceiveAsync(
                request.ProductId, request.WarehouseId, company.FunctionalCurrency, request.Quantity,
                request.UnitCost ?? 0m, request.Date, MovementType.AdjustmentIn, MovementSource.Adjustment, null, request.Reason, ct);
        }
        else
        {
            result = await _ledger.IssueAsync(
                request.ProductId, request.WarehouseId, request.Quantity, request.Date,
                MovementType.AdjustmentOut, MovementSource.Adjustment, null, request.Reason,
                InventoryPolicy.AllowNegativeStock, ct);
        }

        if (result.IsFailure)
        {
            return result.Error;
        }

        await _uow.SaveChangesAsync(ct);
        return result.Value;
    }
}

// ---- Stock transfer between warehouses (cost travels with the goods, ADR-0015) ----
public sealed record TransferStockCommand(
    Guid ProductId, Guid FromWarehouseId, Guid ToWarehouseId, decimal Quantity, DateOnly Date)
    : ICommand<TransferStockResult>;

public sealed class TransferStockValidator : AbstractValidator<TransferStockCommand>
{
    public TransferStockValidator()
    {
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.ToWarehouseId).NotEqual(x => x.FromWarehouseId)
            .WithMessage("Source and destination warehouses must differ.");
    }
}

public sealed class TransferStockHandler : ICommandHandler<TransferStockCommand, TransferStockResult>
{
    private readonly IInventoryLedger _ledger;
    private readonly ICompanyDirectory _companies;
    private readonly ITenantContext _tenant;
    private readonly IInventoryUnitOfWork _uow;

    public TransferStockHandler(IInventoryLedger ledger, ICompanyDirectory companies, ITenantContext tenant, IInventoryUnitOfWork uow)
    {
        _ledger = ledger;
        _companies = companies;
        _tenant = tenant;
        _uow = uow;
    }

    public async Task<Result<TransferStockResult>> Handle(TransferStockCommand request, CancellationToken ct)
    {
        if (request.FromWarehouseId == request.ToWarehouseId)
        {
            return InventoryErrors.SameWarehouse;
        }

        // Issue from source at its moving average; the cost travels to the destination.
        var outResult = await _ledger.IssueAsync(
            request.ProductId, request.FromWarehouseId, request.Quantity, request.Date,
            MovementType.TransferOut, MovementSource.Transfer, null, "Transfer out",
            InventoryPolicy.AllowNegativeStock, ct);

        if (outResult.IsFailure)
        {
            return outResult.Error;
        }

        var unitCost = request.Quantity == 0 ? 0m : outResult.Value.CostApplied / request.Quantity;

        var company = await _companies.GetAsync(_tenant.CompanyId, ct);
        var currency = company?.FunctionalCurrency ?? "XXX";

        var inResult = await _ledger.ReceiveAsync(
            request.ProductId, request.ToWarehouseId, currency, request.Quantity, unitCost, request.Date,
            MovementType.TransferIn, MovementSource.Transfer, null, "Transfer in", ct);

        if (inResult.IsFailure)
        {
            return inResult.Error;
        }

        await _uow.SaveChangesAsync(ct);
        return new TransferStockResult(outResult.Value.TransactionId, inResult.Value.TransactionId, unitCost);
    }
}
