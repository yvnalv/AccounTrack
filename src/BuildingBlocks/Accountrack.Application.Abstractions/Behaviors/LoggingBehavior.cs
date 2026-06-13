using MediatR;
using Microsoft.Extensions.Logging;

namespace Accountrack.Application.Abstractions.Behaviors;

/// <summary>
/// Logs the start, completion, and duration of every request, plus failures.
/// First behavior in the pipeline (ARCHITECTURE.md §4).
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger) => _logger = logger;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var startTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();

        _logger.LogInformation("Handling {RequestName}", requestName);

        try
        {
            var response = await next();
            var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(startTimestamp);
            _logger.LogInformation("Handled {RequestName} in {ElapsedMs} ms", requestName, elapsed.TotalMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Request {RequestName} failed", requestName);
            throw;
        }
    }
}
