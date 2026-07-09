using Accountrack.Application.Abstractions.Idempotency;
using Accountrack.Inventory.Application.Abstractions;
using Accountrack.Inventory.Application.Contracts;
using Accountrack.Inventory.Application.Features;
using FluentAssertions;
using Xunit;

namespace Accountrack.Inventory.UnitTests;

/// <summary>
/// The manual stock commands that post to the GL through the cross-module coordinator are idempotent
/// (ADR-0021): a retried Adjust / Opname / Receive with the same Idempotency-Key must not double-post.
/// </summary>
public class InventoryIdempotencyTests
{
    [Theory]
    [InlineData(typeof(ReceiveStockCommand))]
    [InlineData(typeof(AdjustStockCommand))]
    [InlineData(typeof(StockOpnameCommand))]
    public void Gl_posting_manual_commands_are_marked_idempotent(Type command)
    {
        typeof(IIdempotentCommand).IsAssignableFrom(command)
            .Should().BeTrue($"{command.Name} posts through the coordinator and must be replay-safe");
    }

    [Fact]
    public void Adjustment_result_is_addressed_by_its_transaction_id()
    {
        var txn = Guid.NewGuid();
        var result = new StockMovementResult(txn, 500m, 10m, 50m);

        result.IdempotentId.Should().Be(txn);
        StockMovementResult.FromIdempotentId(txn).TransactionId.Should().Be(txn);
    }

    [Fact]
    public void Opname_result_is_addressed_by_its_reconciling_movement_id()
    {
        var txn = Guid.NewGuid();
        var result = new StockOpnameResult(100m, 90m, -10m, txn, 500m);

        result.IdempotentId.Should().Be(txn);
        StockOpnameResult.FromIdempotentId(txn).TransactionId.Should().Be(txn);
    }

    [Fact]
    public void Exact_match_opname_has_no_id_and_round_trips_to_a_null_movement()
    {
        // A count that matches the system posts nothing, so it has no reconciling movement.
        var noOp = new StockOpnameResult(100m, 100m, 0m, null, 0m);

        noOp.IdempotentId.Should().Be(Guid.Empty);
        StockOpnameResult.FromIdempotentId(Guid.Empty).TransactionId.Should().BeNull();
    }
}
