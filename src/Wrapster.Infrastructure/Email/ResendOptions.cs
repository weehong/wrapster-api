namespace Wrapster.Infrastructure.Email;

public sealed class ResendOptions
{
    public const string SectionName = "Resend";

    public string ApiKey { get; init; } = string.Empty;
    public string FromAddress { get; init; } = string.Empty;
    public string FromName { get; init; } = string.Empty;
}
