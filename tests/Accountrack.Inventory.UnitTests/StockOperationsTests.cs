using Accountrack.Application.Abstractions.Context;
using Accountrack.Inventory.Application.Abstractions;
using Accountrack.Inventory.Application.Features;
using Accountrack.Modules.Contracts.Accounting;
using Accountrack.Modules.Contracts.Company;
using Accountrack.Modules.Contracts.Transactions;
using Accountrack.SharedKernel.Results;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Accountrack.Inventory.UnitTests;

public class StockOperationsTests
{
    private static readonly DateOnly Date = new(2026, 6, 20);
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();
    private static readonly Guid WarehouseId = Guid.NewGuid();
    private static readonly Guid InventoryAccount = Guid.NewGuid();
    private static readonly Guid VarianceAccount = Guid.NewGuid();

    private sealed class DirectUnitOfWork : ICrossModuleUnitOfWork
    {
        public Task<Result<T>> ExecuteAsync<T>(Func<CancellationToken, Task<Result<T>>> work, CancellationToken ct) =>
            work(ct);
    }

    private readonly IInventoryLedger _ledger = Substitute.For<IInventoryLedger>();
    private readonly IStockBucketRepository _buckets = Substitute.For<IStockBucketRepository>();
    private readonly ICompanyDirectory _companies = Substitute.For<ICompanyDirectory>();
    private readonly ITenantContext _tenant = Substitute.For<ITenantContext>();
    private readonly IGeneralLedgerPoster _gl = Substitute.For<IGeneralLedgerPoster>();
    private readonly IPostingAccountResolver _accounts = Substitute.For<IPostingAccountResolver>();
    private LedgerPostingRequest? _posted;

    public StockOperationsTests()
    {
        _tenant.CompanyId.Returns(CompanyId);
        _companies.GetAsync(CompanyId, Arg.Any<CancellationToken>())
            .Returns(new CompanyInfo(CompanyId, "MAIN", "IDR", 1));
        _accounts.ResolveAsync("StockAdjustment", PostingKeys.Inventory, Arg.Any<PostingSelector>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(InventoryAccount));
        _accounts.ResolveAsync("StockAdjustment", PostingKeys.InventoryVariance, Arg.Any<PostingSelector>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(VarianceAccount));
        _gl.PostAsync(Arg.Any<LedgerPostingRequest>(), Arg.Any<CancellationToken>())
            .Returns(ci => { _posted = ci.Arg<LedgerPostingRequest>(); return Result.Success(Guid.NewGuid()); });
    }

    private AdjustStockHandler AdjustHandler() =>
        new(_ledger, _companies, _tenant, new DirectUnitOfWork(), _gl, _accounts);

    private StockOpnameHandler OpnameHandler() =>
        new(_ledger, _buckets, _companies, _tenant, new DirectUnitOfWork(), _gl, _accounts);

    [Fact]
    public async Task Adjustment_decrease_posts_Dr_Variance_Cr_Inventory_at_cost()
    {
        var txnId = Guid.NewGuid();
        _ledger.IssueAsync(ProductId, WarehouseId, 5m, Date, Arg.Any<Accountrack.Inventory.Domain.MovementType>(),
                Arg.Any<Accountrack.Inventory.Domain.MovementSource>(), Arg.Any<Guid?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new StockMovementResult(txnId, 500_000m, 3m, 100_000m)));

        var result = await AdjustHandler().Handle(
            new AdjustStockCommand(ProductId, WarehouseId, 5m, Increase: false, UnitCost: null, Date, "Damaged"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _posted.Should().NotBeNull();
        _posted!.Source.Should().Be(LedgerSource.StockAdjustment);
        _posted.SourceDocumentId.Should().Be(txnId);
        _posted.Lines.Should().SatisfyRespectively(
            dr => { dr.AccountId.Should().Be(VarianceAccount); dr.Debit.Should().Be(500_000m); dr.Credit.Should().Be(0m); },
            cr => { cr.AccountId.Should().Be(InventoryAccount); cr.Credit.Should().Be(500_000m); cr.Debit.Should().Be(0m); });
    }

    [Fact]
    public async Task Adjustment_increase_posts_Dr_Inventory_Cr_Variance_at_cost()
    {
        _ledger.ReceiveAsync(ProductId, WarehouseId, "IDR", 5m, 60_000m, Date,
                Arg.Any<Accountrack.Inventory.Domain.MovementType>(), Arg.Any<Accountrack.Inventory.Domain.MovementSource>(),
                Arg.Any<Guid?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new StockMovementResult(Guid.NewGuid(), 300_000m, 5m, 60_000m)));

        var result = await AdjustHandler().Handle(
            new AdjustStockCommand(ProductId, WarehouseId, 5m, Increase: true, UnitCost: 60_000m, Date, "Found in count"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _posted!.Lines.Should().SatisfyRespectively(
            dr => { dr.AccountId.Should().Be(InventoryAccount); dr.Debit.Should().Be(300_000m); },
            cr => { cr.AccountId.Should().Be(VarianceAccount); cr.Credit.Should().Be(300_000m); });
    }

    [Fact]
    public async Task Opname_with_shortfall_posts_a_reconciling_decrease()
    {
        _ledger.GetOnHandAsync(ProductId, WarehouseId, Arg.Any<CancellationToken>()).Returns(10m);
        _ledger.IssueAsync(ProductId, WarehouseId, 3m, Date, Arg.Any<Accountrack.Inventory.Domain.MovementType>(),
                Arg.Any<Accountrack.Inventory.Domain.MovementSource>(), Arg.Any<Guid?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new StockMovementResult(Guid.NewGuid(), 270_000m, 7m, 90_000m)));

        var result = await OpnameHandler().Handle(
            new StockOpnameCommand(ProductId, WarehouseId, CountedQuantity: 7m, UnitCost: null, Date, "Annual count"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.SystemQty.Should().Be(10m);
        result.Value.CountedQty.Should().Be(7m);
        result.Value.Variance.Should().Be(-3m);
        result.Value.TransactionId.Should().NotBeNull();
        _posted!.Lines[0].AccountId.Should().Be(VarianceAccount);   // Dr variance (loss)
        _posted.Lines[0].Debit.Should().Be(270_000m);
        _posted.Lines[1].AccountId.Should().Be(InventoryAccount);   // Cr inventory
    }

    [Fact]
    public async Task Opname_with_exact_match_records_nothing()
    {
        _ledger.GetOnHandAsync(ProductId, WarehouseId, Arg.Any<CancellationToken>()).Returns(12m);

        var result = await OpnameHandler().Handle(
            new StockOpnameCommand(ProductId, WarehouseId, CountedQuantity: 12m, UnitCost: null, Date, null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Variance.Should().Be(0m);
        result.Value.TransactionId.Should().BeNull();
        await _gl.DidNotReceive().PostAsync(Arg.Any<LedgerPostingRequest>(), Arg.Any<CancellationToken>());
        await _ledger.DidNotReceive().IssueAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<DateOnly>(),
            Arg.Any<Accountrack.Inventory.Domain.MovementType>(), Arg.Any<Accountrack.Inventory.Domain.MovementSource>(),
            Arg.Any<Guid?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }
}
