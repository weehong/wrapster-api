namespace Wrapster.Domain.Common;

public interface IValidationResult<out TSelf> where TSelf : IValidationResult<TSelf>
{
    static abstract TSelf Failure(Error error);
}

public class Result : IValidationResult<Result>
{
    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None)
        {
            throw new InvalidOperationException(
                "A successful result cannot have an error"
            );
        }

        if (!isSuccess && error == Error.None)
        {
            throw new InvalidOperationException(
                "An error result must have an error"
            );
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    public static Result Failure(Error error) => new(false, error);

    public static Result Success() => new(true, Error.None);
    public static Result<T> Success<T>(T value) => Result<T>.Success(value);
}

public class Result<T> : Result, IValidationResult<Result<T>>
{
    private Result(T? value, bool isSuccess, Error error) : base(isSuccess, error) => Value = value!;

    public T Value
    {
        get => IsSuccess
            ? field!
            : throw new InvalidOperationException("Cannot access value of a failed result");
        init;
    }

    public static new Result<T> Failure(Error error) => new(default, false,
        error);

    public static Result<T> Success(T value) => new(value, true, Error.None);

    public static implicit operator Result<T>(T value) =>
        value is null ? Failure(Error.NullValue) : Success(value);
}
