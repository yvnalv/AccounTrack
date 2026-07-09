using Accountrack.Application.Abstractions.Behaviors;
using Accountrack.Application.Abstractions.Context;
using Accountrack.Application.Abstractions.Idempotency;
using Accountrack.Application.Abstractions.Messaging;
using Accountrack.SharedKernel.Results;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Accountrack.BuildingBlocks.UnitTests;

public class IdempotencyBehaviorTests
{
    private sealed record IdempotentDoc(string Name) : ICommand<Guid>, IIdempotentCommand;

    private sealed record PlainDoc(string Name) : ICommand<Guid>;

    // A richer result addressed by a single Guid — mirrors StockMovementResult (ReceiveStock).
    private sealed record Receipt(Guid TransactionId, decimal Cost) : IIdempotentResult<Receipt>
    {
        public Guid IdempotentId => TransactionId;
        public static Receipt FromIdempotentId(Guid id) => new(id, 0m);
    }

    private sealed record IdempotentRichDoc(string Name) : ICommand<Receipt>, IIdempotentCommand;

    private readonly IIdempotencyContext _context = Substitute.For<IIdempotencyContext>();
    private readonly IIdempotencyStore _store = Substitute.For<IIdempotencyStore>();
    private readonly ITenantContext _tenant = Substitute.For<ITenantContext>();
    private readonly IIdempotencyScope _scope = Substitute.For<IIdempotencyScope>();

    public IdempotencyBehaviorTests()
    {
        _tenant.TenantId.Returns(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    }

    private IdempotencyBehavior<TReq, Result<Guid>> Behavior<TReq>() where TReq : notnull =>
        new(_context, _store, _tenant, _scope);

    private IdempotencyBehavior<TReq, Result<TResp>> Behavior<TReq, TResp>() where TReq : notnull =>
        new(_context, _store, _tenant, _scope);

    [Fact]
    public async Task First_call_executes_handler_and_records_the_result()
    {
        _context.Key.Returns("key-1");
        _store.TryGetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((Guid?)null);
        var produced = Guid.NewGuid();

        var result = await Behavior<IdempotentDoc>().Handle(
            new IdempotentDoc("A"), () => Task.FromResult(Result.Success(produced)), default);

        result.Value.Should().Be(produced);
        await _store.Received(1).SaveAsync(
            Arg.Is<string>(k => k.Contains("IdempotentDoc") && k.Contains("key-1")), produced, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Replay_with_same_key_returns_stored_id_without_running_handler()
    {
        var prior = Guid.NewGuid();
        _context.Key.Returns("key-1");
        _store.TryGetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(prior);
        var handlerRan = false;

        var result = await Behavior<IdempotentDoc>().Handle(
            new IdempotentDoc("A"),
            () => { handlerRan = true; return Task.FromResult(Result.Success(Guid.NewGuid())); },
            default);

        handlerRan.Should().BeFalse();
        result.Value.Should().Be(prior);
        await _store.DidNotReceive().SaveAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task No_key_bypasses_the_store_entirely()
    {
        _context.Key.Returns((string?)null);
        var produced = Guid.NewGuid();

        var result = await Behavior<IdempotentDoc>().Handle(
            new IdempotentDoc("A"), () => Task.FromResult(Result.Success(produced)), default);

        result.Value.Should().Be(produced);
        await _store.DidNotReceive().TryGetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _store.DidNotReceive().SaveAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Non_idempotent_command_is_not_deduplicated_even_with_a_key()
    {
        _context.Key.Returns("key-1");
        var produced = Guid.NewGuid();

        var result = await Behavior<PlainDoc>().Handle(
            new PlainDoc("A"), () => Task.FromResult(Result.Success(produced)), default);

        result.Value.Should().Be(produced);
        await _store.DidNotReceive().TryGetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Failed_result_is_not_recorded_so_a_retry_can_succeed()
    {
        _context.Key.Returns("key-1");
        _store.TryGetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((Guid?)null);

        var result = await Behavior<IdempotentDoc>().Handle(
            new IdempotentDoc("A"),
            () => Task.FromResult(Result.Failure<Guid>(Error.Validation("x", "bad"))),
            default);

        result.IsSuccess.Should().BeFalse();
        await _store.DidNotReceive().SaveAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Scoped_key_is_published_for_the_coordinator_before_the_handler_runs()
    {
        _context.Key.Returns("key-1");
        _store.TryGetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((Guid?)null);

        await Behavior<IdempotentDoc>().Handle(
            new IdempotentDoc("A"), () => Task.FromResult(Result.Success(Guid.NewGuid())), default);

        _scope.Received(1).Begin(Arg.Is<string>(k => k.Contains("IdempotentDoc") && k.Contains("key-1")));
        _scope.Received(1).Clear();
    }

    [Fact]
    public async Task Rich_idempotent_result_records_its_id_on_first_call()
    {
        _context.Key.Returns("key-1");
        _store.TryGetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((Guid?)null);
        var txnId = Guid.NewGuid();

        var result = await Behavior<IdempotentRichDoc, Receipt>().Handle(
            new IdempotentRichDoc("A"), () => Task.FromResult(Result.Success(new Receipt(txnId, 42m))), default);

        result.Value.Cost.Should().Be(42m);
        await _store.Received(1).SaveAsync(
            Arg.Is<string>(k => k.Contains("IdempotentRichDoc") && k.Contains("key-1")), txnId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rich_idempotent_result_replay_returns_the_stored_id_with_other_fields_defaulted()
    {
        var prior = Guid.NewGuid();
        _context.Key.Returns("key-1");
        _store.TryGetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(prior);
        var handlerRan = false;

        var result = await Behavior<IdempotentRichDoc, Receipt>().Handle(
            new IdempotentRichDoc("A"),
            () => { handlerRan = true; return Task.FromResult(Result.Success(new Receipt(Guid.NewGuid(), 99m))); },
            default);

        handlerRan.Should().BeFalse();
        result.Value.TransactionId.Should().Be(prior); // id survives the replay
        result.Value.Cost.Should().Be(0m);             // other fields default (Option A)
    }

    [Fact]
    public async Task Key_written_in_transaction_is_not_recorded_again_on_a_separate_connection()
    {
        // The coordinator persisted the key atomically with the effects (exactly-once) and marked the
        // scope written — the behavior must not double-write via the legacy separate-connection path.
        _context.Key.Returns("key-1");
        _store.TryGetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((Guid?)null);
        _scope.Written.Returns(true);

        var result = await Behavior<IdempotentDoc>().Handle(
            new IdempotentDoc("A"), () => Task.FromResult(Result.Success(Guid.NewGuid())), default);

        result.IsSuccess.Should().BeTrue();
        await _store.DidNotReceive().SaveAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
