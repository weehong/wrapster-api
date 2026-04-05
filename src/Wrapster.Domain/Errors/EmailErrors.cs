using Wrapster.Domain.Common;

namespace Wrapster.Domain.Errors;

public static class EmailErrors
{
    public static readonly Error SendFailed = new(
        "Email.SendFailed",
        "Failed to send email",
        ErrorType.Failure);
}
