namespace Accountrack.SharedKernel.Results;

/// <summary>The category of a failure — maps to an HTTP status in the API layer (ERROR_HANDLING.md).</summary>
public enum ErrorType
{
    Failure = 0,
    Validation = 1,
    NotFound = 2,
    Conflict = 3,
    Forbidden = 4,
    Unauthorized = 5,
    BusinessRule = 6
}

/// <summary>
/// A structured, machine-readable error. <see cref="Code"/> is a stable string the frontend can
/// switch on / localize; <see cref="BusinessRuleId"/> optionally references docs/BUSINESS_RULES.md.
/// </summary>
public sealed record Error(string Code, string Message, ErrorType Type = ErrorType.Failure, string? BusinessRuleId = null)
{
    public static readonly Error None = new(string.Empty, string.Empty);

    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);

    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);

    public static Error BusinessRule(string businessRuleId, string message, string? code = null) =>
        new(code ?? "BUSINESS_RULE_VIOLATED", message, ErrorType.BusinessRule, businessRuleId);
}
