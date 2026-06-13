using System.Text.Json;
using Accountrack.Web.Common.Contracts;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Accountrack.Web.Common.Middleware;

/// <summary>
/// Translates unhandled exceptions into the standard failure envelope (ERROR_HANDLING.md).
/// Validation failures map to 422; everything unexpected maps to a generic 500 (no internals
/// leaked to the client — details go to the logs with the traceId).
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidationException ex)
        {
            var details = ex.Errors
                .Select(e => new ApiErrorDetail(e.PropertyName, e.ErrorMessage))
                .ToArray();

            await WriteAsync(
                context,
                StatusCodes.Status422UnprocessableEntity,
                "Validation failed",
                new ApiError("VALIDATION_ERROR", details, context.TraceIdentifier));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception (traceId: {TraceId})", context.TraceIdentifier);

            await WriteAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "An unexpected error occurred.",
                new ApiError("INTERNAL_ERROR", null, context.TraceIdentifier));
        }
    }

    private static async Task WriteAsync(HttpContext context, int statusCode, string message, ApiError error)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var body = ApiErrorResponse.From(message, error);
        await context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOptions));
    }
}
