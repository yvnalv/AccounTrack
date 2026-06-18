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

    private readonly IIdempotencyContext _context = Substitute.For<IIdempotencyContext>();
    private readonly IIdempotencyStore _store = Substitute.For<IIdempotencyStore>();
    private readonly ITenantContext _tenant = Substitute.For<ITenantContext>();

    public IdempotencyBehaviorTests()
    {
        _tenant.TenantId.Returns(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    }

    private IdempotencyBehavior<TReq, Result<Guid>> Behavior<TReq>() where TReq : notnull =>
        new(_context, _store, _tenant);

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
}
