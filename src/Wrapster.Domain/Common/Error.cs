namespace Wrapster.Domain.Common;

public enum ErrorType
{
    None = 0,
    Failure = 1,
    Validation = 2,
    NotFound = 3,
    Conflict = 4
}

public sealed record Error(string Code, string Description, ErrorType Type)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.None);

    public static readonly Error Failure = new("Error.Failure", "A general failure occurred", ErrorType.Failure);

    public static readonly Error NullValue = new("Error.NullValue", "A null value was provided", ErrorType.Failure);

    public static readonly Error Validation = new("Error.Validation", "A validation error occurred",
        ErrorType.Validation);

    public static readonly Error NotFound = new("Error.NotFound", "A requested resource was not found",
        ErrorType.NotFound);

    public static readonly Error Conflict = new("Error.Conflict", "A conflict occurred with the current state",
        ErrorType.Conflict);
}
