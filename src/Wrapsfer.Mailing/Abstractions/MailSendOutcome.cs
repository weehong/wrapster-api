namespace Wrapsfer.Mailing.Abstractions;

public sealed record MailSendOutcome(
    bool IsSuccess,
    string? ProviderMessageId,
    string? ErrorCode,
    string? ErrorDescription,
    bool IsTransient)
{
    public static MailSendOutcome Success(string? providerMessageId) =>
        new(true, providerMessageId, null, null, false);

    public static MailSendOutcome TransientFailure(string errorCode, string errorDescription) =>
        new(false, null, errorCode, errorDescription, true);

    public static MailSendOutcome PermanentFailure(string errorCode, string errorDescription) =>
        new(false, null, errorCode, errorDescription, false);
}
