namespace ECommerceApp.Domain.Common;

/// <summary>
/// Represents the outcome of an application/domain operation without using
/// exceptions for expected failure paths.
/// </summary>
public class Result
{
    protected Result(bool isSuccess, IReadOnlyList<Error> errors)
    {
        if (isSuccess && errors.Count > 0)
        {
            throw new InvalidOperationException("A successful result cannot carry errors.");
        }

        if (!isSuccess && errors.Count == 0)
        {
            throw new InvalidOperationException("A failed result must carry at least one error.");
        }

        IsSuccess = isSuccess;
        Errors = errors;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public IReadOnlyList<Error> Errors { get; }
    public Error FirstError => Errors.Count > 0 ? Errors[0] : Error.None;

    public static Result Success() => new(true, Array.Empty<Error>());
    public static Result Failure(Error error) => new(false, new[] { error });
    public static Result Failure(IReadOnlyList<Error> errors) => new(false, errors);

    public static Result<T> Success<T>(T value) => new(value, true, Array.Empty<Error>());
    public static Result<T> Failure<T>(Error error) => new(default, false, new[] { error });
    public static Result<T> Failure<T>(IReadOnlyList<Error> errors) => new(default, false, errors);
}

/// <summary>
/// A <see cref="Result"/> that carries a value on success.
/// </summary>
public sealed class Result<T> : Result
{
    internal Result(T? value, bool isSuccess, IReadOnlyList<Error> errors)
        : base(isSuccess, errors)
    {
        _value = value;
    }

    private readonly T? _value;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access the value of a failed result.");

    public static implicit operator Result<T>(T value) => Success(value);
}
