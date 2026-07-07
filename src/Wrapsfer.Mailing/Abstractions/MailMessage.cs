namespace Wrapsfer.Mailing.Abstractions;

public sealed record MailMessage
{
    public required IReadOnlyList<string> To { get; init; }
    public IReadOnlyList<string> Cc { get; init; } = [];
    public IReadOnlyList<string> Bcc { get; init; } = [];

    public string? Subject { get; init; }
    public MailBody? Body { get; init; }

    public string? TemplateName { get; init; }
    public IReadOnlyDictionary<string, object?>? Tokens { get; init; }

    public IReadOnlyList<MailAttachment> Attachments { get; init; } = [];

    public bool UsesTemplate => !string.IsNullOrWhiteSpace(TemplateName);
}
