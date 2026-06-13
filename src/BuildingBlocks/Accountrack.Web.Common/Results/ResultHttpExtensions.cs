using Accountrack.SharedKernel.Results;
using Accountrack.Web.Common.Contracts;
using Microsoft.AspNetCore.Http;

namespace Accountrack.Web.Common.Results;

/// <summary>Maps an application <see cref="Result"/> to an HTTP response using the standard envelope.</summary>
public static class ResultHttpExtensions
{
    public static IResult ToHttpResult(this Result result) =>
        result.IsSuccess
            ? TypedResults.Ok(ApiResponse<object?>.Ok(null))
            : Problem(result.Error);

    public static IResult ToHttpResult<T>(this Result<T> result) =>
        result.IsSuccess
            ? TypedResults.Ok(ApiResponse<T>.Ok(result.Value))
            : Problem(result.Error);

    /// <summary>Same as <see cref="ToHttpResult{T}(Result{T})"/> but returns 201 Created on success.</summary>
    public static IResult ToCreatedResult<T>(this Result<T> result, string location) =>
        result.IsSuccess
            ? TypedResults.Created(location, ApiResponse<T>.Ok(result.Value))
            : Problem(result.Error);

    private static IResult Problem(Error error)
    {
        var status = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status422UnprocessableEntity,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.BusinessRule => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        };

        IReadOnlyCollection<ApiErrorDetail>? details = error.BusinessRuleId is null
            ? null
            : new[] { new ApiErrorDetail(error.BusinessRuleId, error.Message) };

        var body = ApiErrorResponse.From(error.Message, new ApiError(error.Code, details, null));
        return TypedResults.Json(body, statusCode: status);
    }
}
