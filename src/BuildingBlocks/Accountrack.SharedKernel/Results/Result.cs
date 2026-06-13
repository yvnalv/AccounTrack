namespace Accountrack.SharedKernel.Results;

/// <summary>
/// The outcome of an operation. Expected business failures are returned as a failed
/// <see cref="Result"/> rather than thrown (ERROR_HANDLING.md §3).
/// </summary>
public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None)
        {
            throw new InvalidOperationException("A successful result cannot carry an error.");
        }

        if (!isSuccess && error == Error.None)
        {
            throw new InvalidOperationException("A failed result must carry an error.");
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error Error { get; }

    public static Result Success() => new(true, Error.None);

    public static Result Failure(Error error) => new(false, error);

    public static Result<TValue> Success<TValue>(TValue value) => Result<TValue>.Of(value);

    public static Result<TValue> Failure<TValue>(Error error) => Result<TValue>.Fail(error);
}

/// <summary>The outcome of an operation that yields a value on success.</summary>
public sealed class Result<TValue> : Result
{
    private readonly TValue? _value;

    private Result(TValue? value, bool isSuccess, Error error) : base(isSuccess, error) => _value = value;

    /// <summary>The success value. Throws if accessed on a failed result.</summary>
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access the value of a failed result.");

    public static Result<TValue> Of(TValue value) => new(value, true, Error.None);

    public static Result<TValue> Fail(Error error) => new(default, false, error);

    public static implicit operator Result<TValue>(TValue value) => Of(value);

    public static implicit operator Result<TValue>(Error error) => Fail(error);
}
