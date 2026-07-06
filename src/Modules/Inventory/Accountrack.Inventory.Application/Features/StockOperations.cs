using Accountrack.Application.Abstractions.Context;
using Accountrack.Application.Abstractions.Messaging;
using Accountrack.Inventory.Application.Abstractions;
using Accountrack.Inventory.Application.Contracts;
using Accountrack.Inventory.Domain;
using Accountrack.Modules.Contracts.Accounting;
using Accountrack.Modules.Contracts.Company;
using Accountrack.Modules.Contracts.Transactions;
using Accountrack.SharedKernel.Results;
using FluentValidation;

namespace Accountrack.Inventory.Application.Features;

// NOTE: the negative-stock policy is a per-company setting resolved inside InventoryLedgerService
// (CompanySettingKeys.AllowNegativeStock, default false; BR-INV-3, ADR-0016). Adjustments and opname post
// a GL variance journal atomically (slice 2, CHG-0057); warehouse transfers are GL-neutral under a
// single Inventory control account (cost travels with the goods).

/// <summary>
/// Posts the GL side of an inventory value change (slice 2): Dr Inventory / Cr Inventory Variance for
/// an increase, the reverse for a decrease — accounts resolved from posting rules, never hardcoded.
/// Save-less: enlists in the caller's cross-module transaction. A zero-value change posts nothing.
/// </summary>
internal static class StockVariancePosting
{
    public static async Task<Result<Guid>> PostAsync(
        IGeneralLedgerPoster ledger, IPostingAccountResolver accounts, bool increase, decimal costApplied,
        Guid warehouseId, DateOnly date, Guid sourceTransactionId, string description, CancellationToken ct)
    {
        if (costApplied <= 0m)
        {
            return Guid.Empty; // no value change → no journal
        }

        var inventory = await accounts.ResolveAsync(
            "StockAdjustment", PostingKeys.Inventory, new PostingSelector(WarehouseId: warehouseId), ct);
        if (inventory.IsFailure) return inventory.Error;

        var variance = await accounts.ResolveAsync(
            "StockAdjustment", PostingKeys.InventoryVariance, PostingSelector.None, ct);
        if (variance.IsFailure) return variance.Error;

        var lines = increase
            ? new[]
            {
                new LedgerLine(inventory.Value, costApplied, 0m, "Inventory increase"),
                new LedgerLine(variance.Value, 0m, costApplied, "Inventory variance (gain)"),
            }
            : new[]
            {
                new LedgerLine(variance.Value, costApplied, 0m, "Inventory variance (loss)"),
                new LedgerLine(inventory.Value, 0m, costApplied, "Inventory decrease"),
            };

        return await ledger.PostAsync(
            new LedgerPostingRequest(date, LedgerSource.StockAdjustment, sourceTransactionId, description, lines), ct);
    }
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

        // A manual receipt commits through the module unit of work, not the cross-module transaction, so
        // it cannot post the GL correction a back-dated recompute needs (ADR-0033). Reject it here.
        if (await _ledger.IsBackDatedAsync(request.ProductId, request.WarehouseId, request.Date, ct))
        {
            return InventoryErrors.BackDatingNotSupported;
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
    private readonly ICrossModuleUnitOfWork _uow;
    private readonly IGeneralLedgerPoster _gl;
    private readonly IPostingAccountResolver _accounts;

    public AdjustStockHandler(
        IInventoryLedger ledger, ICompanyDirectory companies, ITenantContext tenant,
        ICrossModuleUnitOfWork uow, IGeneralLedgerPoster gl, IPostingAccountResolver accounts)
    {
        _ledger = ledger;
        _companies = companies;
        _tenant = tenant;
        _uow = uow;
        _gl = gl;
        _accounts = accounts;
    }

    public Task<Result<StockMovementResult>> Handle(AdjustStockCommand request, CancellationToken ct) =>
        // Atomic: the inventory ledger entry and its GL variance journal commit together, or not at all.
        _uow.ExecuteAsync(token => AdjustAsync(request, token), ct);

    private async Task<Result<StockMovementResult>> AdjustAsync(AdjustStockCommand request, CancellationToken ct)
    {
        var company = await _companies.GetAsync(_tenant.CompanyId, ct);
        if (company is null)
        {
            return Error.NotFound("INVENTORY.COMPANY_NOT_FOUND", "Active company not found.");
        }

        var result = request.Increase
            ? await _ledger.ReceiveAsync(
                request.ProductId, request.WarehouseId, company.FunctionalCurrency, request.Quantity,
                request.UnitCost ?? 0m, request.Date, MovementType.AdjustmentIn, MovementSource.Adjustment, null, request.Reason, ct)
            : await _ledger.IssueAsync(
                request.ProductId, request.WarehouseId, request.Quantity, request.Date,
                MovementType.AdjustmentOut, MovementSource.Adjustment, null, request.Reason, ct);

        if (result.IsFailure)
        {
            return result.Error;
        }

        var posted = await StockVariancePosting.PostAsync(
            _gl, _accounts, request.Increase, result.Value.CostApplied, request.WarehouseId, request.Date,
            result.Value.TransactionId, $"Stock adjustment: {request.Reason}", ct);
        if (posted.IsFailure)
        {
            return posted.Error;
        }

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

        // A transfer commits through the module unit of work, not the cross-module transaction, so a
        // back-dated recompute could not post its GL correction (ADR-0033). Reject either side.
        if (await _ledger.IsBackDatedAsync(request.ProductId, request.FromWarehouseId, request.Date, ct)
            || await _ledger.IsBackDatedAsync(request.ProductId, request.ToWarehouseId, request.Date, ct))
        {
            return InventoryErrors.BackDatingNotSupported;
        }

        // Issue from source at its moving average; the cost travels to the destination.
        var outResult = await _ledger.IssueAsync(
            request.ProductId, request.FromWarehouseId, request.Quantity, request.Date,
            MovementType.TransferOut, MovementSource.Transfer, null, "Transfer out", ct);

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

// ---- Stock opname (physical count → reconcile to system on-hand, INVENTORY_DESIGN.md §5) ----
public sealed record StockOpnameCommand(
    Guid ProductId, Guid WarehouseId, decimal CountedQuantity, decimal? UnitCost, DateOnly Date, string? Notes)
    : ICommand<StockOpnameResult>;

public sealed class StockOpnameValidator : AbstractValidator<StockOpnameCommand>
{
    public StockOpnameValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.CountedQuantity).GreaterThanOrEqualTo(0);
        RuleFor(x => x.UnitCost).GreaterThanOrEqualTo(0).When(x => x.UnitCost.HasValue);
    }
}

/// <summary>
/// Records a physical stock count and reconciles it to the system on-hand: posts an adjustment-in
/// (counted &gt; system) or adjustment-out (counted &lt; system) for the difference, with its GL
/// variance journal, atomically. An exact match records nothing. An increase is valued at the
/// supplied unit cost or, by default, the current moving-average cost (so the average is unchanged).
/// </summary>
public sealed class StockOpnameHandler : ICommandHandler<StockOpnameCommand, StockOpnameResult>
{
    private readonly IInventoryLedger _ledger;
    private readonly IStockBucketRepository _buckets;
    private readonly ICompanyDirectory _companies;
    private readonly ITenantContext _tenant;
    private readonly ICrossModuleUnitOfWork _uow;
    private readonly IGeneralLedgerPoster _gl;
    private readonly IPostingAccountResolver _accounts;

    public StockOpnameHandler(
        IInventoryLedger ledger, IStockBucketRepository buckets, ICompanyDirectory companies,
        ITenantContext tenant, ICrossModuleUnitOfWork uow, IGeneralLedgerPoster gl, IPostingAccountResolver accounts)
    {
        _ledger = ledger;
        _buckets = buckets;
        _companies = companies;
        _tenant = tenant;
        _uow = uow;
        _gl = gl;
        _accounts = accounts;
    }

    public Task<Result<StockOpnameResult>> Handle(StockOpnameCommand request, CancellationToken ct) =>
        _uow.ExecuteAsync(token => OpnameAsync(request, token), ct);

    private async Task<Result<StockOpnameResult>> OpnameAsync(StockOpnameCommand request, CancellationToken ct)
    {
        var company = await _companies.GetAsync(_tenant.CompanyId, ct);
        if (company is null)
        {
            return Error.NotFound("INVENTORY.COMPANY_NOT_FOUND", "Active company not found.");
        }

        var systemQty = await _ledger.GetOnHandAsync(request.ProductId, request.WarehouseId, ct);
        var variance = request.CountedQuantity - systemQty;
        var reason = string.IsNullOrWhiteSpace(request.Notes) ? "Stock opname" : $"Stock opname: {request.Notes}";

        if (variance == 0m)
        {
            return new StockOpnameResult(systemQty, request.CountedQuantity, 0m, null, 0m);
        }

        var increase = variance > 0m;
        var qty = Math.Abs(variance);

        Result<StockMovementResult> result;
        if (increase)
        {
            var bucket = await _buckets.GetAsync(request.ProductId, request.WarehouseId, ct);
            var unitCost = request.UnitCost ?? bucket?.AvgUnitCost ?? 0m;
            result = await _ledger.ReceiveAsync(
                request.ProductId, request.WarehouseId, company.FunctionalCurrency, qty, unitCost,
                request.Date, MovementType.AdjustmentIn, MovementSource.Adjustment, null, reason, ct);
        }
        else
        {
            result = await _ledger.IssueAsync(
                request.ProductId, request.WarehouseId, qty, request.Date,
                MovementType.AdjustmentOut, MovementSource.Adjustment, null, reason, ct);
        }

        if (result.IsFailure)
        {
            return result.Error;
        }

        var posted = await StockVariancePosting.PostAsync(
            _gl, _accounts, increase, result.Value.CostApplied, request.WarehouseId, request.Date,
            result.Value.TransactionId, reason, ct);
        if (posted.IsFailure)
        {
            return posted.Error;
        }

        return new StockOpnameResult(
            systemQty, request.CountedQuantity, variance, result.Value.TransactionId, result.Value.CostApplied);
    }
}
