using Wrapsfer.Domain.Common;

namespace Wrapsfer.Domain.Errors;

public static class AuthErrors
{
    public static readonly Error InvalidCredentials = new(
        "Auth.InvalidCredentials",
        "Invalid username or password",
        ErrorType.Validation);

    public static readonly Error PasswordChangeFailed = new(
        "Auth.PasswordChangeFailed",
        "Failed to change the password",
        ErrorType.Failure);

    public static readonly Error NewPasswordSameAsCurrent = new(
        "Auth.NewPasswordSameAsCurrent",
        "The new password must be different from the current password",
        ErrorType.Validation);
}
