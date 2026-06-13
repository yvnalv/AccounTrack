namespace Accountrack.Web.Common.Contracts;

/// <summary>The standard success response envelope (CLAUDE.md API Response Standard, API_SPEC.md §2).</summary>
public sealed record ApiResponse<T>(bool Success, T Data)
{
    public static ApiResponse<T> Ok(T data) => new(true, data);
}

/// <summary>The standard failure envelope (ERROR_HANDLING.md §1).</summary>
public sealed record ApiErrorResponse(bool Success, string Message, ApiError Error)
{
    public static ApiErrorResponse From(string message, ApiError error) => new(false, message, error);
}

public sealed record ApiError(string Code, IReadOnlyCollection<ApiErrorDetail>? Details, string? TraceId);

public sealed record ApiErrorDetail(string Field, string Message);
