namespace Wrapster.Mailing.Resend;

public sealed class ResendOptions
{
    public const string SectionName = "Mailing:Resend";

    public string ApiKey { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
}
