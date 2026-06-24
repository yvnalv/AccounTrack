using Accountrack.Application.Abstractions.Integration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Accountrack.Infrastructure.Common.Outbox;

/// <summary>
/// Polls the transactional outbox and delivers pending events to their handlers (ADR-0007). Reads a
/// batch, then processes each message in its own DI scope so a message's tenant + module contexts are
/// isolated. Failures are recorded and retried on the next pass (up to a cap).
/// </summary>
public sealed class OutboxDispatcherService : BackgroundService
{
    private const int BatchSize = 50;
    private const int MaxAttempts = 10;
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(2);

    private readonly IServiceProvider _services;
    private readonly ILogger<OutboxDispatcherService> _logger;

    public OutboxDispatcherService(IServiceProvider services, ILogger<OutboxDispatcherService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PumpAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox dispatcher pump failed; retrying after the interval.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task PumpAsync(CancellationToken ct)
    {
        IReadOnlyList<OutboxRecord> batch;
        using (var scope = _services.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IOutboxStore>();
            batch = await store.GetPendingAsync(BatchSize, MaxAttempts, ct);
        }

        foreach (var record in batch)
        {
            using var scope = _services.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<IOutboxProcessor>();
            await processor.ProcessAsync(record, ct);
        }
    }
}
